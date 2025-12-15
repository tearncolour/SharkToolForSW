using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.fworks;
using WebSocketSharp;

namespace SharkTools
{
    /// <summary>
    /// Electron 通信客户端
    /// 通过 WebSocket 连接到 Electron 应用
    /// </summary>
    public class ElectronServer
    {
        private readonly ISldWorks _swApp;
        private readonly SharkCommandManager _cmdMgr;
        private readonly SynchronizationContext _uiContext;
        private WebSocket _wsClient;
        private readonly string[] _wsTargets = new[]
        {
            "ws://127.0.0.1:52789", // Electron 桌面客户端
            "ws://127.0.0.1:52790"  // VS Code 插件端
        };
        private int _targetIndex = 0;
        private string CurrentWsUrl => _wsTargets[_targetIndex];
        private bool _isRunning = false;
        private System.Threading.Timer _reconnectTimer;
        private volatile bool _isConnecting = false;
        
        // 请求去重：防止同一文件的重复请求堵塞队列
        private readonly ConcurrentDictionary<string, DateTime> _recentRequests = new ConcurrentDictionary<string, DateTime>();
        private readonly TimeSpan _dedupeWindow = TimeSpan.FromSeconds(2);
        
        // 消息处理队列，确保串行处理，限制队列大小防止堆积
        private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>(100);
        private readonly BlockingCollection<string> _priorityQueue = new BlockingCollection<string>(50); // 高优先级队列
        private Task _messageProcessorTask;
        private CancellationTokenSource _messageProcessorCts;
        private volatile int _activeRequestCount = 0; // 活跃请求计数

        public ElectronServer(ISldWorks swApp, SharkCommandManager cmdMgr, SynchronizationContext uiContext)
        {
            _swApp = swApp;
            _cmdMgr = cmdMgr;
            _uiContext = uiContext;
            
            // 启动消息处理器
            StartMessageProcessor();
        }

        private void SwitchToNextTarget(string reason)
        {
            _targetIndex = (_targetIndex + 1) % _wsTargets.Length;
            Log($"切换到下一个 WebSocket 目标({reason}): {CurrentWsUrl}");
        }
        
        private void StartMessageProcessor()
        {
            _messageProcessorCts = new CancellationTokenSource();
            _messageProcessorTask = Task.Run(async () =>
            {
                while (!_messageProcessorCts.Token.IsCancellationRequested)
                {
                    string message = null;
                    
                    // 优先处理高优先级队列
                    if (_priorityQueue.TryTake(out message, 10))
                    {
                        await ProcessMessage(message, "优先");
                    }
                    else if (_messageQueue.TryTake(out message, 100, _messageProcessorCts.Token))
                    {
                        await ProcessMessage(message, "普通");
                    }
                }
            }, _messageProcessorCts.Token);
        }
        
        private async Task ProcessMessage(string message, string queueType)
        {
            var startTime = DateTime.Now;
            System.Threading.Interlocked.Increment(ref _activeRequestCount);
            try
            {
                Log($"[{queueType}队列 活跃:{_activeRequestCount}] 开始处理消息...");
                string response = await HandleCommandAsync(message);
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Log($"[耗时:{elapsed:F0}ms] 处理完成");
                if (!string.IsNullOrEmpty(response))
                {
                    Send(response);
                }
            }
            catch (TimeoutException tex)
            {
                Log($"⚠️ 消息处理超时: {tex.Message}");
                SendError($"操作超时: {tex.Message}");
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Log($"❌ [耗时:{elapsed:F0}ms] 错误: {ex.Message}");
                SendError($"处理失败: {ex.Message}");
            }
            finally
            {
                System.Threading.Interlocked.Decrement(ref _activeRequestCount);
            }
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            Connect();

            // 启动重连定时器，每5秒检查一次连接
            _reconnectTimer = new System.Threading.Timer(CheckConnection, null, 5000, 5000);
            
            Log($"正在连接 WebSocket 服务，首选: {CurrentWsUrl}，候选: {string.Join(", ", _wsTargets)}");
        }

        private void Connect()
        {
            if (_isConnecting) return;
            _isConnecting = true;

            Task.Run(() =>
            {
                var url = CurrentWsUrl;
                try
                {
                    if (!_isRunning) return;

                    if (_wsClient != null)
                    {
                        _wsClient.OnOpen -= OnOpen;
                        _wsClient.OnMessage -= OnMessage;
                        _wsClient.OnClose -= OnClose;
                        _wsClient.OnError -= OnError;
                        try { _wsClient.Close(); } catch { }
                        _wsClient = null;
                    }

                    var client = new WebSocket(url);
                    // 先设置 _wsClient，避免 OnOpen 触发时 _wsClient 为 null
                    _wsClient = client;
                    
                    client.OnOpen += OnOpen;
                    client.OnMessage += OnMessage;
                    client.OnClose += OnClose;
                    client.OnError += OnError;
                    client.Connect();

                    if (!_isRunning)
                    {
                        _wsClient = null;
                        client.Close();
                        return;
                    }

                    if (!client.IsAlive)
                    {
                        Log($"连接 {url} 未建立，准备切换目标");
                        _wsClient = null;
                        SwitchToNextTarget("连接未建立");
                    }
                }
                catch (Exception ex)
                {
                    Log($"连接 {url} 失败: {ex.Message}");
                    _wsClient = null;
                    SwitchToNextTarget("异常");
                }
                finally
                {
                    _isConnecting = false;
                }
            });
        }

        private void CheckConnection(object state)
        {
            if (!_isRunning) return;
            if (_isConnecting) return;

            if (_wsClient == null || !_wsClient.IsAlive)
            {
                Log("连接断开，尝试重连...");
                Connect();
            }
        }

        public void Stop()
        {
            _isRunning = false;
            
            // 停止消息处理器
            try
            {
                _messageProcessorCts?.Cancel();
                _messageQueue.CompleteAdding();
            }
            catch { }
            
            if (_reconnectTimer != null)
            {
                _reconnectTimer.Dispose();
                _reconnectTimer = null;
            }

            try
            {
                if (_wsClient != null)
                {
                    _wsClient.Close();
                    _wsClient = null;
                }
            }
            catch { }
        }

        private void OnOpen(object sender, EventArgs e)
        {
            Log($"已连接到 WebSocket: {CurrentWsUrl}");
            // 发送身份标识 - 直接使用 _wsClient.Send 因为此时 IsAlive 可能还没更新
            try
            {
                var identifyMessage = JsonConvert.SerializeObject(new { type = "identify", client = "solidworks" });
                _wsClient.Send(identifyMessage);
                Log($"已发送身份标识: {identifyMessage}");
            }
            catch (Exception ex)
            {
                Log($"发送身份标识失败: {ex.Message}");
            }
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                string message = e.Data;
                Log($"收到消息: {message}");
                
                // 请求去重检查
                if (ShouldSkipDuplicateRequest(message))
                {
                    Log($"跳过重复请求 (2秒内已处理相同请求)");
                    return;
                }
                
                // 判断是否为高优先级命令
                bool isHighPriority = IsHighPriorityCommand(message);
                var targetQueue = isHighPriority ? _priorityQueue : _messageQueue;
                var queueName = isHighPriority ? "优先" : "普通";
                
                // 将消息加入队列，确保串行处理
                if (!targetQueue.TryAdd(message, 100))
                {
                    Log($"警告: {queueName}队列已满, 消息被丢弃");
                    SendError("服务器繁忙，请稍后重试");
                }
                else
                {
                    Log($"消息已加入{queueName}队列 (大小: {targetQueue.Count})");
                }
            }
            catch (Exception ex)
            {
                Log($"处理消息错误: {ex.Message}");
                SendError(ex.Message);
            }
        }
        
        private bool IsHighPriorityCommand(string message)
        {
            try
            {
                var data = JObject.Parse(message);
                string command = data["command"]?.ToString() ?? data["type"]?.ToString();
                
                // 用户直接操作的命令使用高优先级
                var highPriority = new HashSet<string> { "open", "open-file", "ping", "create-file", 
                    "new-part", "new-assembly", "new-drawing", "save-document" };
                return highPriority.Contains(command);
            }
            catch { return false; }
        }
        
        private bool ShouldSkipDuplicateRequest(string message)
        {
            try
            {
                var data = JObject.Parse(message);
                string command = data["command"]?.ToString() ?? data["type"]?.ToString();
                string path = data["payload"]?["path"]?.ToString();
                
                // 只对耗时的文件操作去重
                if (string.IsNullOrEmpty(command) || string.IsNullOrEmpty(path)) return false;
                if (command != "get-thumbnail" && command != "get-properties" && command != "get-custom-properties") return false;
                
                string key = $"{command}:{path}";
                var now = DateTime.Now;
                
                // 清理过期记录
                var expired = _recentRequests.Where(kv => now - kv.Value > _dedupeWindow).Select(kv => kv.Key).ToList();
                foreach (var k in expired) _recentRequests.TryRemove(k, out _);
                
                // 检查是否重复
                if (_recentRequests.TryGetValue(key, out var lastTime))
                {
                    if (now - lastTime < _dedupeWindow) return true;
                }
                
                _recentRequests[key] = now;
                return false;
            }
            catch { return false; }
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            Log($"连接断开: {e.Reason}, 取消 {_messageQueue.Count + _priorityQueue.Count} 个待处理请求");
            
            // 清空队列，丢弃未处理的请求
            while (_messageQueue.TryTake(out _)) { }
            while (_priorityQueue.TryTake(out _)) { }
            
            SwitchToNextTarget("连接关闭");
        }

        private void OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            Log($"WebSocket 错误: {e.Message}");
            SwitchToNextTarget("错误");
        }

        private void Send(string data)
        {
            if (_wsClient != null && _wsClient.IsAlive)
            {
                _wsClient.Send(data);
                Log($"发送数据成功，长度: {data?.Length ?? 0}");
            }
            else
            {
                Log($"发送失败: WebSocket 未连接 (client={_wsClient != null}, alive={_wsClient?.IsAlive})");
            }
        }
        
        /// <summary>
        /// 发送消息到 Electron 应用
        /// </summary>
        /// <param name="type">消息类型</param>
        /// <param name="payload">消息内容</param>
        public void Send(string type, object payload)
        {
            var message = new 
            {
                type = type,
                payload = payload
            };
            Send(Newtonsoft.Json.JsonConvert.SerializeObject(message));
        }

        public void ShowWindow()
        {
            Send(JsonConvert.SerializeObject(new { type = "show" }));
        }

        public void HideWindow()
        {
            Send(JsonConvert.SerializeObject(new { type = "hide" }));
        }

        public void NotifyDocumentOpened(string name, string path)
        {
            var message = new 
            {
                type = "document-opened",
                payload = new { name = name, path = path }
            };
            Send(JsonConvert.SerializeObject(message));
        }

        public void SendHistoryUpdate(object records)
        {
            var message = new 
            {
                type = "history-update",
                payload = new { records = records }
            };
            Send(JsonConvert.SerializeObject(message));
        }

        private void SendError(string message)
        {
            var response = new { success = false, message = message };
            Send(JsonConvert.SerializeObject(response));
        }

        private void Log(string message)
        {
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: ElectronClient - {message}\r\n");
            }
            catch { }
        }

        private async Task<string> HandleCommandAsync(string jsonBody)
        {
            string messageId = "";
            try
            {
                var data = JObject.Parse(jsonBody);
                // 忽略非命令消息（如连接确认）
                if (data["type"]?.ToString() == "connected") return null;

                messageId = data["id"]?.ToString();
                // 支持 "command" 和 "type" 两种字段名（兼容不同客户端）
                string command = data["command"]?.ToString() ?? data["type"]?.ToString();
                var payload = data["payload"];

                Log($"Parsed Command: '{command}'"); // Debug log

                if (string.IsNullOrEmpty(command)) return null;

                if (command == "convert-and-recognize")
                {
                    Log($"Command: {command}, Payload: {payload}");
                    string convertPath = payload?["path"]?.ToString();
                    var options = payload?["options"];
                    
                    if (string.IsNullOrEmpty(convertPath))
                    {
                        return JsonConvert.SerializeObject(new 
                        { 
                            id = messageId,
                            success = false, 
                            message = "Path is required for conversion" 
                        });
                    }

                    var converter = new ModelConverter(_swApp, RunOnUIThread);
                    var convertResult = await converter.ConvertAsync(convertPath, options);
                    return JsonConvert.SerializeObject(new 
                    { 
                        id = messageId,
                        success = true, 
                        data = convertResult 
                    });
                }

                // 文件对比命令 - 单独处理，因为内部使用 async
                if (command == "compare-files")
                {
                    string filePath1 = payload?["filePath1"]?.ToString();
                    string filePath2 = payload?["filePath2"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(filePath1) && !string.IsNullOrEmpty(filePath2))
                    {
                        var compareMgr = new FileCompareManager(_swApp, RunOnUIThread);
                        var compareResult = await compareMgr.CompareFiles(filePath1, filePath2);
                        return JsonConvert.SerializeObject(new 
                        { 
                            id = messageId,
                            success = true, 
                            data = compareResult 
                        });
                    }
                    else
                    {
                        return JsonConvert.SerializeObject(new 
                        { 
                            id = messageId,
                            success = false, 
                            message = "请提供两个文件路径" 
                        });
                    }
                }

                object result = null;

                // 验证操作前提条件，避免非法操作导致 SolidWorks 卡死
                var validationError = ValidateOperation(command, payload);
                if (validationError != null)
                {
                    return JsonConvert.SerializeObject(new 
                    { 
                        id = messageId,
                        success = false, 
                        message = validationError 
                    });
                }

                // 确定超时时间：需要用户交互的命令使用更长超时
                int timeout = GetCommandTimeout(command);

                // 在 UI 线程执行 SolidWorks 操作
                await RunOnUIThreadWithTimeout(() =>
                {
                    switch (command)
                    {
                        case "ping":
                            // 心跳检查
                            result = new { pong = true, timestamp = DateTime.Now };
                            break;

                        case "open":
                        case "open-file":
                            string path = payload?["path"]?.ToString();
                            if (!string.IsNullOrEmpty(path))
                            {
                                result = OpenDocument(path);
                            }
                            break;
                        
                        case "get_active":
                            result = GetActiveDocumentInfo();
                            break;

                        case "load-history":
                            var activeDoc = _swApp.ActiveDoc as ModelDoc2;
                            if (activeDoc != null)
                            {
                                string docPath = activeDoc.GetPathName();
                                if (!string.IsNullOrEmpty(docPath))
                                {
                                    var records = HistoryDatabase.GetRecords(docPath);
                                    result = new { records = records };
                                }
                            }
                            break;

                        case "create-file":
                        case "new-part":
                        case "new-assembly":
                        case "new-drawing":
                            string newPath = payload?["path"]?.ToString();
                            string fileType = payload?["docType"]?.ToString() ?? payload?["type"]?.ToString();
                            
                            // 根据命令推断文件类型
                            if (string.IsNullOrEmpty(fileType))
                            {
                                if (command == "new-part") fileType = "sldprt";
                                else if (command == "new-assembly") fileType = "sldasm";
                                else if (command == "new-drawing") fileType = "slddrw";
                            }
                            
                            if (!string.IsNullOrEmpty(newPath) && !string.IsNullOrEmpty(fileType))
                            {
                                result = CreateDocument(newPath, fileType);
                            }
                            else
                            {
                                result = new { success = false, message = "路径和文件类型不能为空" };
                            }
                            break;

                        case "get-feature-tree":
                            string featurePath = payload?["path"]?.ToString();
                            if (!string.IsNullOrEmpty(featurePath))
                            {
                                result = GetFeatureTree(featurePath);
                            }
                            else
                            {
                                result = new { success = false, message = "文件路径不能为空" };
                            }
                            break;

                        case "get-thumbnail":
                            string thumbPath = payload?["path"]?.ToString();
                            if (!string.IsNullOrEmpty(thumbPath))
                            {
                                result = GetThumbnail(thumbPath);
                            }
                            break;

                        case "get-properties":
                            string propPath = payload?["path"]?.ToString();
                            if (!string.IsNullOrEmpty(propPath))
                            {
                                // 快速返回基本信息，不等待 UI 线程
                                if (!File.Exists(propPath))
                                {
                                    result = new { success = false, message = "File not found" };
                                }
                                else
                                {
                                    var fileInfo = new FileInfo(propPath);
                                    result = new {
                                        success = true,
                                        fileName = fileInfo.Name,
                                        fileSize = fileInfo.Length,
                                        extension = fileInfo.Extension,
                                        lastModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
                                    };
                                }
                            }
                            break;

                        case "save-settings":
                            var settings = payload?["settings"];
                            if (settings != null)
                            {
                                int interval = settings["autoSaveInterval"]?.ToObject<int>() ?? 30;
                                if (_cmdMgr?.HistoryTracker != null)
                                {
                                    _cmdMgr.HistoryTracker.SetInterval(interval * 1000);
                                }
                                result = new { success = true };
                            }
                            break;

                        // ============ 自定义属性管理命令 ============
                        case "get-custom-properties":
                            {
                                string customPropPath = payload?["path"]?.ToString();
                                string configName = payload?["configName"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(customPropPath))
                                {
                                    // 支持读取任意文件的属性（会自动打开/激活）
                                    var customPropMgr = new SwCustomPropertyManager(_swApp, RunOnUIThread);
                                    result = customPropMgr.GetCustomPropertiesWithAutoOpen(customPropPath, configName).Result;
                                }
                                else
                                {
                                    result = new CustomPropertyResult { Success = false, Message = "路径不能为空" };
                                }
                            }
                            break;

                        case "set-custom-property":
                            {
                                string customPropPath = payload?["path"]?.ToString();
                                string propName = payload?["propertyName"]?.ToString();
                                string propValue = payload?["propertyValue"]?.ToString() ?? "";
                                string configName = payload?["configName"]?.ToString() ?? "";
                                
                                if (!string.IsNullOrEmpty(customPropPath) && !string.IsNullOrEmpty(propName))
                                {
                                    var customPropMgr = new SwCustomPropertyManager(_swApp, RunOnUIThread);
                                    result = customPropMgr.SetCustomProperty(customPropPath, propName, propValue, configName).Result;
                                }
                                else
                                {
                                    result = new OperationResult { Success = false, Message = "路径和属性名不能为空" };
                                }
                            }
                            break;

                        case "set-custom-properties-batch":
                            {
                                string batchPropPath = payload?["path"]?.ToString();
                                var properties = payload?["properties"]?.ToObject<Dictionary<string, string>>();
                                string configName = payload?["configName"]?.ToString() ?? "";
                                
                                if (!string.IsNullOrEmpty(batchPropPath) && properties != null)
                                {
                                    var customPropMgr = new SwCustomPropertyManager(_swApp, RunOnUIThread);
                                    result = customPropMgr.SetCustomPropertiesBatch(batchPropPath, properties, configName).Result;
                                }
                                else
                                {
                                    result = new BatchOperationResult { Success = false, Message = "参数错误" };
                                }
                            }
                            break;

                        case "set-custom-properties-multiple-files":
                            {
                                var filePaths = payload?["paths"]?.ToObject<List<string>>();
                                var properties = payload?["properties"]?.ToObject<Dictionary<string, string>>();
                                string configName = payload?["configName"]?.ToString() ?? "";
                                
                                if (filePaths != null && filePaths.Count > 0 && properties != null)
                                {
                                    var customPropMgr = new SwCustomPropertyManager(_swApp, RunOnUIThread);
                                    result = customPropMgr.SetCustomPropertiesMultipleFiles(filePaths, properties, configName).Result;
                                }
                                else
                                {
                                    result = new { success = false, message = "参数错误" };
                                }
                            }
                            break;

                        case "delete-custom-property":
                            {
                                string deletePropPath = payload?["path"]?.ToString();
                                string deletePropName = payload?["propertyName"]?.ToString();
                                string configName = payload?["configName"]?.ToString() ?? "";
                                
                                if (!string.IsNullOrEmpty(deletePropPath) && !string.IsNullOrEmpty(deletePropName))
                                {
                                    var customPropMgr = new SwCustomPropertyManager(_swApp, RunOnUIThread);
                                    result = customPropMgr.DeleteCustomProperty(deletePropPath, deletePropName, configName).Result;
                                }
                                else
                                {
                                    result = new OperationResult { Success = false, Message = "路径和属性名不能为空" };
                                }
                            }
                            break;

                        case "get-property-templates":
                            {
                                var customPropMgr = new SwCustomPropertyManager(_swApp, RunOnUIThread);
                                result = new { success = true, templates = customPropMgr.GetPropertyTemplates() };
                            }
                            break;

                        // ============ 批量重命名命令 ============
                        case "preview-rename":
                            {
                                var renamePaths = payload?["paths"]?.ToObject<List<string>>();
                                var renameOptions = payload?["options"]?.ToObject<RenameOptions>();
                                
                                if (renamePaths != null && renamePaths.Count > 0 && renameOptions != null)
                                {
                                    var renameMgr = new BatchRenameManager(_swApp, RunOnUIThread);
                                    result = renameMgr.PreviewRename(renamePaths, renameOptions);
                                }
                                else
                                {
                                    result = new RenamePreviewResult { Success = false, Message = "参数错误" };
                                }
                            }
                            break;

                        case "execute-rename":
                            {
                                var renamePaths = payload?["paths"]?.ToObject<List<string>>();
                                var renameOptions = payload?["options"]?.ToObject<RenameOptions>();
                                
                                if (renamePaths != null && renamePaths.Count > 0 && renameOptions != null)
                                {
                                    var renameMgr = new BatchRenameManager(_swApp, RunOnUIThread);
                                    result = renameMgr.ExecuteRename(renamePaths, renameOptions).Result;
                                }
                                else
                                {
                                    result = new BatchRenameResult { Success = false, Message = "参数错误" };
                                }
                            }
                            break;

                        case "get-rename-templates":
                            {
                                var renameMgr = new BatchRenameManager(_swApp, RunOnUIThread);
                                result = new { success = true, templates = renameMgr.GetRenameTemplates() };
                            }
                            break;

                        // ============ 项目管理命令 ============
                        case "create-project":
                            {
                                string projectPath = payload?["path"]?.ToString();
                                string projectName = payload?["name"]?.ToString();
                                var template = payload?["template"]?.ToObject<ProjectTemplate>();
                                
                                if (!string.IsNullOrEmpty(projectPath) && !string.IsNullOrEmpty(projectName))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.CreateProject(projectPath, projectName, template);
                                }
                                else
                                {
                                    result = new ProjectResult { Success = false, Message = "路径和项目名不能为空" };
                                }
                            }
                            break;

                        case "delete-project":
                            {
                                string projectPath = payload?["path"]?.ToString();
                                bool deleteFiles = payload?["deleteFiles"]?.ToObject<bool>() ?? false;
                                
                                if (!string.IsNullOrEmpty(projectPath))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.DeleteProject(projectPath, deleteFiles);
                                }
                                else
                                {
                                    result = new OperationResult { Success = false, Message = "项目路径不能为空" };
                                }
                            }
                            break;

                        case "rename-project":
                            {
                                string projectPath = payload?["path"]?.ToString();
                                string newProjectName = payload?["newName"]?.ToString();
                                
                                if (!string.IsNullOrEmpty(projectPath) && !string.IsNullOrEmpty(newProjectName))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.RenameProject(projectPath, newProjectName);
                                }
                                else
                                {
                                    result = new ProjectResult { Success = false, Message = "项目路径和新名称不能为空" };
                                }
                            }
                            break;

                        case "get-all-projects":
                            {
                                var projectMgr = new ProjectManager();
                                result = projectMgr.GetAllProjects();
                            }
                            break;

                        case "get-project-info":
                            {
                                string projectPath = payload?["path"]?.ToString();
                                if (!string.IsNullOrEmpty(projectPath))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.GetProjectInfo(projectPath);
                                }
                            }
                            break;

                        case "get-project-statistics":
                            {
                                string projectPath = payload?["path"]?.ToString();
                                if (!string.IsNullOrEmpty(projectPath))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.GetProjectStatistics(projectPath);
                                }
                            }
                            break;

                        case "move-files-to-project":
                            {
                                var moveFilePaths = payload?["filePaths"]?.ToObject<List<string>>();
                                string targetFolder = payload?["targetFolder"]?.ToString();
                                
                                if (moveFilePaths != null && moveFilePaths.Count > 0 && !string.IsNullOrEmpty(targetFolder))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.MoveFilesToProject(moveFilePaths, targetFolder);
                                }
                                else
                                {
                                    result = new OperationResult { Success = false, Message = "参数错误" };
                                }
                            }
                            break;

                        case "copy-files-to-project":
                            {
                                var copyFilePaths = payload?["filePaths"]?.ToObject<List<string>>();
                                string targetFolder = payload?["targetFolder"]?.ToString();
                                
                                if (copyFilePaths != null && copyFilePaths.Count > 0 && !string.IsNullOrEmpty(targetFolder))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.CopyFilesToProject(copyFilePaths, targetFolder);
                                }
                                else
                                {
                                    result = new OperationResult { Success = false, Message = "参数错误" };
                                }
                            }
                            break;

                        case "import-as-project":
                            {
                                string folderPath = payload?["folderPath"]?.ToString();
                                string projectName = payload?["projectName"]?.ToString();
                                
                                if (!string.IsNullOrEmpty(folderPath))
                                {
                                    var projectMgr = new ProjectManager();
                                    result = projectMgr.ImportAsProject(folderPath, projectName);
                                }
                                else
                                {
                                    result = new ProjectResult { Success = false, Message = "文件夹路径不能为空" };
                                }
                            }
                            break;

                        case "get-project-templates":
                            {
                                var projectMgr = new ProjectManager();
                                result = new { success = true, templates = projectMgr.GetProjectTemplates() };
                            }
                            break;

                        // ============ 草图绘制命令 ============
                        case "sketch-create":
                            {
                                string planeName = payload?["planeName"]?.ToString() ?? "Front Plane";
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.CreateSketch(planeName).Result;
                            }
                            break;

                        case "sketch-draw-line":
                            {
                                double x1 = payload?["x1"]?.ToObject<double>() ?? 0;
                                double y1 = payload?["y1"]?.ToObject<double>() ?? 0;
                                double x2 = payload?["x2"]?.ToObject<double>() ?? 0;
                                double y2 = payload?["y2"]?.ToObject<double>() ?? 0;
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.DrawLine(x1, y1, x2, y2).Result;
                            }
                            break;

                        case "sketch-draw-rectangle":
                            {
                                double x1 = payload?["x1"]?.ToObject<double>() ?? 0;
                                double y1 = payload?["y1"]?.ToObject<double>() ?? 0;
                                double x2 = payload?["x2"]?.ToObject<double>() ?? 0;
                                double y2 = payload?["y2"]?.ToObject<double>() ?? 0;
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.DrawRectangle(x1, y1, x2, y2).Result;
                            }
                            break;

                        case "sketch-draw-circle":
                            {
                                double centerX = payload?["centerX"]?.ToObject<double>() ?? 0;
                                double centerY = payload?["centerY"]?.ToObject<double>() ?? 0;
                                double radius = payload?["radius"]?.ToObject<double>() ?? 0.01;
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.DrawCircle(centerX, centerY, radius).Result;
                            }
                            break;

                        case "sketch-draw-arc":
                            {
                                double centerX = payload?["centerX"]?.ToObject<double>() ?? 0;
                                double centerY = payload?["centerY"]?.ToObject<double>() ?? 0;
                                double startX = payload?["startX"]?.ToObject<double>() ?? 0;
                                double startY = payload?["startY"]?.ToObject<double>() ?? 0;
                                double endX = payload?["endX"]?.ToObject<double>() ?? 0;
                                double endY = payload?["endY"]?.ToObject<double>() ?? 0;
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.DrawArc(centerX, centerY, startX, startY, endX, endY).Result;
                            }
                            break;

                        case "sketch-draw-polygon":
                            {
                                double centerX = payload?["centerX"]?.ToObject<double>() ?? 0;
                                double centerY = payload?["centerY"]?.ToObject<double>() ?? 0;
                                double radius = payload?["radius"]?.ToObject<double>() ?? 0.01;
                                int sides = payload?["sides"]?.ToObject<int>() ?? 6;
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.DrawPolygon(centerX, centerY, radius, sides).Result;
                            }
                            break;

                        case "sketch-draw-ellipse":
                            {
                                double centerX = payload?["centerX"]?.ToObject<double>() ?? 0;
                                double centerY = payload?["centerY"]?.ToObject<double>() ?? 0;
                                double majorRadius = payload?["majorRadius"]?.ToObject<double>() ?? 0.02;
                                double minorRadius = payload?["minorRadius"]?.ToObject<double>() ?? 0.01;
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.DrawEllipse(centerX, centerY, majorRadius, minorRadius).Result;
                            }
                            break;

                        case "sketch-draw-slot":
                            {
                                double x1 = payload?["x1"]?.ToObject<double>() ?? 0;
                                double y1 = payload?["y1"]?.ToObject<double>() ?? 0;
                                double x2 = payload?["x2"]?.ToObject<double>() ?? 0;
                                double y2 = payload?["y2"]?.ToObject<double>() ?? 0;
                                double width = payload?["width"]?.ToObject<double>() ?? 0.01;
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.DrawSlot(x1, y1, x2, y2, width).Result;
                            }
                            break;

                        case "sketch-add-dimension":
                            {
                                double x = payload?["x"]?.ToObject<double>() ?? 0;
                                double y = payload?["y"]?.ToObject<double>() ?? 0;
                                double? value = payload?["value"]?.ToObject<double?>();
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.AddDimension(x, y, value).Result;
                            }
                            break;

                        case "sketch-add-constraint":
                            {
                                string constraintType = payload?["constraintType"]?.ToString() ?? "horizontal";
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.AddConstraint(constraintType).Result;
                            }
                            break;

                        case "sketch-exit":
                            {
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.ExitSketch().Result;
                            }
                            break;

                        case "sketch-get-info":
                            {
                                var sketchMgr = new SharkSketchManager(_swApp, RunOnUIThread);
                                result = sketchMgr.GetSketchInfo().Result;
                            }
                            break;

                        // ============ 特征创建命令 ============
                        case "feature-extrude-boss":
                            {
                                double depth = payload?["depth"]?.ToObject<double>() ?? 0.01;
                                bool direction = payload?["direction"]?.ToObject<bool>() ?? true;
                                double draftAngle = payload?["draftAngle"]?.ToObject<double>() ?? 0;
                                bool draftOutward = payload?["draftOutward"]?.ToObject<bool>() ?? false;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.ExtrudeBoss(depth, direction, draftAngle, draftOutward).Result;
                            }
                            break;

                        case "feature-extrude-cut":
                            {
                                double depth = payload?["depth"]?.ToObject<double>() ?? 0.01;
                                bool direction = payload?["direction"]?.ToObject<bool>() ?? true;
                                bool throughAll = payload?["throughAll"]?.ToObject<bool>() ?? false;
                                double draftAngle = payload?["draftAngle"]?.ToObject<double>() ?? 0;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.ExtrudeCut(depth, direction, throughAll, draftAngle).Result;
                            }
                            break;

                        case "feature-revolve-boss":
                            {
                                double angle = payload?["angle"]?.ToObject<double>() ?? 360;
                                bool direction = payload?["direction"]?.ToObject<bool>() ?? true;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.RevolveBoss(angle, direction).Result;
                            }
                            break;

                        case "feature-revolve-cut":
                            {
                                double angle = payload?["angle"]?.ToObject<double>() ?? 360;
                                bool direction = payload?["direction"]?.ToObject<bool>() ?? true;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.RevolveCut(angle, direction).Result;
                            }
                            break;

                        case "feature-sweep-boss":
                            {
                                bool thinWall = payload?["thinWall"]?.ToObject<bool>() ?? false;
                                double thickness = payload?["thickness"]?.ToObject<double>() ?? 0;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.SweepBoss(thinWall, thickness).Result;
                            }
                            break;

                        case "feature-sweep-cut":
                            {
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.SweepCut().Result;
                            }
                            break;

                        case "feature-loft-boss":
                            {
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.LoftBoss().Result;
                            }
                            break;

                        case "feature-loft-cut":
                            {
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.LoftCut().Result;
                            }
                            break;

                        case "feature-fillet":
                            {
                                double radius = payload?["radius"]?.ToObject<double>() ?? 0.001;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.Fillet(radius).Result;
                            }
                            break;

                        case "feature-chamfer":
                            {
                                double distance = payload?["distance"]?.ToObject<double>() ?? 0.001;
                                double angle = payload?["angle"]?.ToObject<double>() ?? 45;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.Chamfer(distance, angle).Result;
                            }
                            break;

                        case "feature-shell":
                            {
                                double thickness = payload?["thickness"]?.ToObject<double>() ?? 0.001;
                                bool outward = payload?["outward"]?.ToObject<bool>() ?? false;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.Shell(thickness, outward).Result;
                            }
                            break;

                        case "feature-hole-wizard":
                            {
                                string holeType = payload?["holeType"]?.ToString() ?? "simple";
                                double diameter = payload?["diameter"]?.ToObject<double>() ?? 0.006;
                                double depth = payload?["depth"]?.ToObject<double>() ?? 0.01;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.HoleWizard(holeType, diameter, depth).Result;
                            }
                            break;

                        case "feature-linear-pattern":
                            {
                                int direction1Count = payload?["direction1Count"]?.ToObject<int>() ?? 2;
                                double direction1Spacing = payload?["direction1Spacing"]?.ToObject<double>() ?? 0.01;
                                int direction2Count = payload?["direction2Count"]?.ToObject<int>() ?? 1;
                                double direction2Spacing = payload?["direction2Spacing"]?.ToObject<double>() ?? 0;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.LinearPattern(direction1Count, direction1Spacing, direction2Count, direction2Spacing).Result;
                            }
                            break;

                        case "feature-circular-pattern":
                            {
                                int count = payload?["count"]?.ToObject<int>() ?? 4;
                                double angle = payload?["angle"]?.ToObject<double>() ?? 360;
                                bool equalSpacing = payload?["equalSpacing"]?.ToObject<bool>() ?? true;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.CircularPattern(count, angle, equalSpacing).Result;
                            }
                            break;

                        case "feature-mirror":
                            {
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.Mirror().Result;
                            }
                            break;

                        case "feature-reference-plane":
                            {
                                double offsetDistance = payload?["offsetDistance"]?.ToObject<double>() ?? 0.01;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.CreateReferencePlane(offsetDistance).Result;
                            }
                            break;

                        case "select-face":
                            {
                                string faceName = payload?["faceName"]?.ToString() ?? "";
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.SelectFace(faceName).Result;
                            }
                            break;

                        case "select-edge":
                            {
                                string edgeName = payload?["edgeName"]?.ToString() ?? "";
                                bool append = payload?["append"]?.ToObject<bool>() ?? false;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.SelectEdge(edgeName, append).Result;
                            }
                            break;

                        case "select-feature":
                            {
                                string featureName = payload?["featureName"]?.ToString() ?? "";
                                bool append = payload?["append"]?.ToObject<bool>() ?? false;
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.SelectFeature(featureName, append).Result;
                            }
                            break;

                        case "clear-selection":
                            {
                                var featCreator = new SharkFeatureCreator(_swApp, RunOnUIThread);
                                result = featCreator.ClearSelection().Result;
                            }
                            break;

                        // ============ 装配体命令 ============
                        case "assembly-add-component":
                            {
                                string componentPath = payload?["componentPath"]?.ToString() ?? "";
                                double x = payload?["x"]?.ToObject<double>() ?? 0;
                                double y = payload?["y"]?.ToObject<double>() ?? 0;
                                double z = payload?["z"]?.ToObject<double>() ?? 0;
                                string configName = payload?["configName"]?.ToString() ?? "";
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddComponent(componentPath, x, y, z, configName).Result;
                            }
                            break;

                        case "assembly-add-coincident-mate":
                            {
                                bool align = payload?["align"]?.ToObject<bool>() ?? true;
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddCoincidentMate(align).Result;
                            }
                            break;

                        case "assembly-add-concentric-mate":
                            {
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddConcentricMate().Result;
                            }
                            break;

                        case "assembly-add-distance-mate":
                            {
                                double distance = payload?["distance"]?.ToObject<double>() ?? 0;
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddDistanceMate(distance).Result;
                            }
                            break;

                        case "assembly-add-angle-mate":
                            {
                                double angle = payload?["angle"]?.ToObject<double>() ?? 0;
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddAngleMate(angle).Result;
                            }
                            break;

                        case "assembly-add-parallel-mate":
                            {
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddParallelMate().Result;
                            }
                            break;

                        case "assembly-add-perpendicular-mate":
                            {
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddPerpendicularMate().Result;
                            }
                            break;

                        case "assembly-add-tangent-mate":
                            {
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.AddTangentMate().Result;
                            }
                            break;

                        case "assembly-fix-component":
                            {
                                string componentName = payload?["componentName"]?.ToString() ?? "";
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.FixComponent(componentName).Result;
                            }
                            break;

                        case "assembly-float-component":
                            {
                                string componentName = payload?["componentName"]?.ToString() ?? "";
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.FloatComponent(componentName).Result;
                            }
                            break;

                        case "assembly-hide-component":
                            {
                                string componentName = payload?["componentName"]?.ToString() ?? "";
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.HideComponent(componentName).Result;
                            }
                            break;

                        case "assembly-show-component":
                            {
                                string componentName = payload?["componentName"]?.ToString() ?? "";
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.ShowComponent(componentName).Result;
                            }
                            break;

                        case "assembly-get-components":
                            {
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.GetComponentList().Result;
                            }
                            break;

                        case "assembly-detect-interference":
                            {
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.DetectInterference().Result;
                            }
                            break;

                        case "assembly-select-component-face":
                            {
                                string componentName = payload?["componentName"]?.ToString() ?? "";
                                string faceName = payload?["faceName"]?.ToString() ?? "";
                                bool append = payload?["append"]?.ToObject<bool>() ?? false;
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.SelectComponentFace(componentName, faceName, append).Result;
                            }
                            break;

                        case "assembly-select-component-edge":
                            {
                                string componentName = payload?["componentName"]?.ToString() ?? "";
                                string edgeName = payload?["edgeName"]?.ToString() ?? "";
                                bool append = payload?["append"]?.ToObject<bool>() ?? false;
                                var assemblyMgr = new SharkAssemblyManager(_swApp, RunOnUIThread);
                                result = assemblyMgr.SelectComponentEdge(componentName, edgeName, append).Result;
                            }
                            break;

                        // ============ 工程图命令 ============
                        case "drawing-create-model-view":
                            {
                                string modelPath = payload?["modelPath"]?.ToString() ?? "";
                                string viewName = payload?["viewName"]?.ToString() ?? "*Front";
                                double x = payload?["x"]?.ToObject<double>() ?? 0.15;
                                double y = payload?["y"]?.ToObject<double>() ?? 0.15;
                                double scale = payload?["scale"]?.ToObject<double>() ?? 1;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.CreateModelView(modelPath, viewName, x, y, scale).Result;
                            }
                            break;

                        case "drawing-create-projected-view":
                            {
                                string parentViewName = payload?["parentViewName"]?.ToString() ?? "";
                                double x = payload?["x"]?.ToObject<double>() ?? 0.25;
                                double y = payload?["y"]?.ToObject<double>() ?? 0.15;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.CreateProjectedView(parentViewName, x, y).Result;
                            }
                            break;

                        case "drawing-create-section-view":
                            {
                                string viewName = payload?["viewName"]?.ToString() ?? "";
                                double x = payload?["x"]?.ToObject<double>() ?? 0.35;
                                double y = payload?["y"]?.ToObject<double>() ?? 0.15;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.CreateSectionView(viewName, x, y).Result;
                            }
                            break;

                        case "drawing-create-detail-view":
                            {
                                string viewName = payload?["viewName"]?.ToString() ?? "";
                                double x = payload?["x"]?.ToObject<double>() ?? 0.35;
                                double y = payload?["y"]?.ToObject<double>() ?? 0.25;
                                double scale = payload?["scale"]?.ToObject<double>() ?? 2;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.CreateDetailView(viewName, x, y, scale).Result;
                            }
                            break;

                        case "drawing-add-dimension":
                            {
                                double x = payload?["x"]?.ToObject<double>() ?? 0;
                                double y = payload?["y"]?.ToObject<double>() ?? 0;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddLinearDimension(x, y).Result;
                            }
                            break;

                        case "drawing-add-diameter-dimension":
                            {
                                double x = payload?["x"]?.ToObject<double>() ?? 0;
                                double y = payload?["y"]?.ToObject<double>() ?? 0;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddDiameterDimension(x, y).Result;
                            }
                            break;

                        case "drawing-add-radius-dimension":
                            {
                                double x = payload?["x"]?.ToObject<double>() ?? 0;
                                double y = payload?["y"]?.ToObject<double>() ?? 0;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddRadiusDimension(x, y).Result;
                            }
                            break;

                        case "drawing-add-angular-dimension":
                            {
                                double x = payload?["x"]?.ToObject<double>() ?? 0;
                                double y = payload?["y"]?.ToObject<double>() ?? 0;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddAngularDimension(x, y).Result;
                            }
                            break;

                        case "drawing-add-note":
                            {
                                string text = payload?["text"]?.ToString() ?? "";
                                double x = payload?["x"]?.ToObject<double>() ?? 0;
                                double y = payload?["y"]?.ToObject<double>() ?? 0;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddNote(text, x, y).Result;
                            }
                            break;

                        case "drawing-add-centerline":
                            {
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddCenterline().Result;
                            }
                            break;

                        case "drawing-add-centermark":
                            {
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddCenterMark().Result;
                            }
                            break;

                        case "drawing-auto-dimension":
                            {
                                string viewName = payload?["viewName"]?.ToString() ?? "";
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AutoDimension(viewName).Result;
                            }
                            break;

                        case "drawing-get-sheet-info":
                            {
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.GetSheetInfo().Result;
                            }
                            break;

                        case "drawing-set-sheet-scale":
                            {
                                double numerator = payload?["numerator"]?.ToObject<double>() ?? 1;
                                double denominator = payload?["denominator"]?.ToObject<double>() ?? 1;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.SetSheetScale(numerator, denominator).Result;
                            }
                            break;

                        case "drawing-activate-sheet":
                            {
                                string sheetName = payload?["sheetName"]?.ToString() ?? "";
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.ActivateSheet(sheetName).Result;
                            }
                            break;

                        case "drawing-add-new-sheet":
                            {
                                string sheetName = payload?["sheetName"]?.ToString() ?? "";
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.AddNewSheet(sheetName).Result;
                            }
                            break;

                        case "drawing-insert-bom":
                            {
                                string viewName = payload?["viewName"]?.ToString() ?? "";
                                double x = payload?["x"]?.ToObject<double>() ?? 0.4;
                                double y = payload?["y"]?.ToObject<double>() ?? 0.25;
                                var drawingMgr = new SharkDrawingManager(_swApp, RunOnUIThread);
                                result = drawingMgr.InsertBOM(viewName, x, y).Result;
                            }
                            break;

                        default:
                            throw new Exception("未知命令");
                    }
                }, timeout);

                return JsonConvert.SerializeObject(new 
                { 
                    id = messageId,
                    success = true, 
                    data = result 
                });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new 
                { 
                    id = messageId,
                    success = false, 
                    message = ex.Message 
                });
            }
        }

        // Removed ConvertAndRecognize as it is now in ModelConverter.cs

        /// <summary>
        /// 验证操作的前提条件，避免非法操作导致 SolidWorks 卡死
        /// </summary>
        private string ValidateOperation(string command, JToken payload)
        {
            try
            {
                var model = _swApp.ActiveDoc as ModelDoc2;
                
                // 需要活动文档的命令
                var requiresActiveDoc = new HashSet<string>
                {
                    "sketch-create", "sketch-draw-line", "sketch-draw-rectangle", 
                    "sketch-draw-circle", "sketch-draw-arc", "sketch-draw-slot",
                    "sketch-draw-polygon", "sketch-exit", "sketch-get-info",
                    "feature-extrude-boss", "feature-extrude-cut", "feature-revolve-boss",
                    "feature-sweep-boss", "feature-sweep-cut", "feature-loft-boss", "feature-loft-cut",
                    "feature-hole-wizard", "feature-shell", "feature-fillet", "feature-chamfer",
                    "select-face", "select-edge", "select-feature", "select-by-ray", "clear-selection",
                    "get-active-doc"
                };
                
                if (requiresActiveDoc.Contains(command) && model == null)
                {
                    return "请先打开或创建一个文档";
                }
                
                if (model != null)
                {
                    int docType = model.GetType();
                    
                    // 只能在零件中执行的命令
                    var partOnlyCommands = new HashSet<string>
                    {
                        "sketch-create", "sketch-draw-line", "sketch-draw-rectangle", 
                        "sketch-draw-circle", "sketch-draw-arc", "sketch-draw-slot",
                        "sketch-draw-polygon", "sketch-exit",
                        "feature-extrude-boss", "feature-extrude-cut", "feature-revolve-boss",
                        "feature-sweep-boss", "feature-sweep-cut", "feature-loft-boss", "feature-loft-cut",
                        "feature-hole-wizard", "feature-shell", "feature-fillet", "feature-chamfer"
                    };
                    
                    if (partOnlyCommands.Contains(command) && docType != (int)swDocumentTypes_e.swDocPART)
                    {
                        string docTypeName = docType == (int)swDocumentTypes_e.swDocASSEMBLY ? "装配体" : 
                                           docType == (int)swDocumentTypes_e.swDocDRAWING ? "工程图" : "未知类型";
                        return $"此操作只能在零件中执行，当前文档是{docTypeName}";
                    }
                    
                    // 只能在装配体中执行的命令
                    var assemblyOnlyCommands = new HashSet<string>
                    {
                        "assembly-add-mate", "assembly-add-concentric-mate", 
                        "assembly-detect-interference", "assembly-add-component"
                    };
                    
                    if (assemblyOnlyCommands.Contains(command) && docType != (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        return "此操作只能在装配体中执行";
                    }
                    
                    // 需要选择的命令
                    var requiresSelectionCommands = new HashSet<string>
                    {
                        "feature-fillet", "feature-chamfer", "feature-extrude-boss", 
                        "feature-extrude-cut", "feature-revolve-boss"
                    };
                    
                    if (requiresSelectionCommands.Contains(command))
                    {
                        var selMgr = model.SelectionManager as SelectionMgr;
                        if (selMgr != null && selMgr.GetSelectedObjectCount2(-1) == 0)
                        {
                            return "请先选择要操作的面、边或草图";
                        }
                    }
                    
                    // 检查是否有特征（避免在空零件上操作）
                    var requiresFeatureCommands = new HashSet<string>
                    {
                        "select-face", "select-edge", "select-feature"
                    };
                    
                    if (requiresFeatureCommands.Contains(command) && docType == (int)swDocumentTypes_e.swDocPART)
                    {
                        Feature feat = model.FirstFeature() as Feature;
                        int featureCount = 0;
                        while (feat != null && featureCount < 100)
                        {
                            string typeName = feat.GetTypeName2();
                            if (!typeName.Contains("Folder") && !typeName.Contains("Reference"))
                            {
                                featureCount++;
                                if (featureCount > 5) break; // 有实际特征
                            }
                            feat = feat.GetNextFeature() as Feature;
                        }
                        
                        if (featureCount <= 5)
                        {
                            return "零件没有可选择的特征，请先创建特征";
                        }
                    }
                }
                
                return null; // 验证通过
            }
            catch (Exception ex)
            {
                Log($"验证操作时出错: {ex.Message}");
                return null; // 出错时允许继续，由后续处理
            }
        }

        /// <summary>
        /// 根据命令类型确定超时时间
        /// </summary>
        private int GetCommandTimeout(string command)
        {
            // 需要长时间的命令（UI/模型加载/属性读取）使用更长超时
            var longRunning = new HashSet<string>
            {
                // 用户交互/特征创建
                "sketch-create", "sketch-draw-line", "sketch-draw-rectangle",
                "sketch-draw-circle", "sketch-draw-arc", "sketch-draw-slot",
                "sketch-draw-polygon", "sketch-exit",
                "feature-extrude-boss", "feature-extrude-cut", "feature-revolve-boss",
                "feature-sweep-boss", "feature-sweep-cut", "feature-loft-boss", "feature-loft-cut",
                "feature-hole-wizard", "feature-shell",
                "select-by-ray", "select-face", "select-edge", "select-feature",
                // 文档加载/属性/缩略图（大模型耗时）
                "open", "open-file", "get-thumbnail", "get-properties", "get-custom-properties"
            };

            // 长任务 60s，其余默认 10s
            return longRunning.Contains(command) ? 60000 : 10000;
        }
        
        private Task RunOnUIThread(Action action)
        {
            return RunOnUIThreadWithTimeout(action, 10000);
        }
        
        private async Task RunOnUIThreadWithTimeout(Action action, int timeoutMs)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (_uiContext != null)
            {
                _uiContext.Post(state =>
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }, null);
            }
            else
            {
                var control = _cmdMgr?.TaskPaneControl;

                if (control != null && control.InvokeRequired)
                {
                    // 使用 BeginInvoke 避免死锁
                    control.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            action();
                            tcs.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.TrySetException(ex);
                        }
                    }));
                }
                else
                {
                    try
                    {
                        action();
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                }
            }

            // 添加超时机制，避免无限等待
            var timeoutTask = Task.Delay(timeoutMs);
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"UI 线程操作超时 ({timeoutMs}ms)");
            }
            
            await tcs.Task; // 如果有异常会在这里抛出
        }

        private string FindTemplate(int docType)
        {
            string extension = "";
            switch(docType)
            {
                case (int)swDocumentTypes_e.swDocPART: extension = "*.prtdot"; break;
                case (int)swDocumentTypes_e.swDocASSEMBLY: extension = "*.asmdot"; break;
                case (int)swDocumentTypes_e.swDocDRAWING: extension = "*.drwdot"; break;
            }

            string templateFolders = _swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swFileLocationsDocumentTemplates);
            if (!string.IsNullOrEmpty(templateFolders))
            {
                string[] folders = templateFolders.Split(';');
                foreach (string folder in folders)
                {
                    if (Directory.Exists(folder))
                    {
                        string[] files = Directory.GetFiles(folder, extension);
                        if (files.Length > 0)
                        {
                            return files[0];
                        }
                    }
                }
            }
            return "";
        }

        private object CreateDocument(string path, string type)
        {
            WriteLog($"Creating document. Path: {path}, Type: {type}");
            try 
            {
                string defaultTemplate = "";
                int docType = 0;
                
                switch(type.ToLower())
                {
                    case "sldprt":
                        defaultTemplate = _swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
                        docType = (int)swDocumentTypes_e.swDocPART;
                        break;
                    case "sldasm":
                        defaultTemplate = _swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
                        docType = (int)swDocumentTypes_e.swDocASSEMBLY;
                        break;
                    case "slddrw":
                        defaultTemplate = _swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
                        docType = (int)swDocumentTypes_e.swDocDRAWING;
                        break;
                    default:
                        return new { success = false, message = "Invalid file type" };
                }

                if (string.IsNullOrEmpty(defaultTemplate) || !File.Exists(defaultTemplate))
                {
                     defaultTemplate = _swApp.GetDocumentTemplate(docType, "", 0, 0, 0);
                }

                if (string.IsNullOrEmpty(defaultTemplate) || !File.Exists(defaultTemplate))
                {
                     defaultTemplate = FindTemplate(docType);
                }

                if (string.IsNullOrEmpty(defaultTemplate))
                {
                     return new { success = false, message = "No valid template found" };
                }

                var model = _swApp.NewDocument(defaultTemplate, docType, 0, 0) as ModelDoc2;
                if (model == null)
                {
                    return new { success = false, message = "Failed to create new document" };
                }

                int errors = 0;
                int warnings = 0;
                bool saved = model.Extension.SaveAs(path, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);
                
                // 不关闭文档，保持打开状态
                // _swApp.CloseDoc(model.GetTitle());

                if (saved)
                {
                    // 激活新创建的文档
                    _swApp.ActivateDoc3(model.GetTitle(), false, (int)swRebuildOnActivation_e.swUserDecision, ref errors);
                    return new { success = true, title = model.GetTitle(), path = path };
                }
                else
                {
                    return new { success = false, message = $"Save failed. Errors: {errors}" };
                }
            }
            catch (Exception ex)
            {
                return new { success = false, message = ex.Message };
            }
        }

        private object OpenDocument(string path)
        {
            if (!File.Exists(path))
            {
                return new { success = false, message = "文件不存在" };
            }

            int errors = 0;
            int warnings = 0;
            
            string ext = Path.GetExtension(path).ToLower();
            int docType = (int)swDocumentTypes_e.swDocPART;
            
            if (ext == ".sldasm") docType = (int)swDocumentTypes_e.swDocASSEMBLY;
            else if (ext == ".slddrw") docType = (int)swDocumentTypes_e.swDocDRAWING;

            var model = _swApp.OpenDoc6(path, docType, 
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);

            if (model != null)
            {
                _swApp.ActivateDoc3(model.GetTitle(), false, (int)swRebuildOnActivation_e.swUserDecision, ref errors);
                return new { success = true, title = model.GetTitle() };
            }
            else
            {
                return new { success = false, error = errors };
            }
        }

        private object GetActiveDocumentInfo()
        {
            var model = _swApp.ActiveDoc as ModelDoc2;
            if (model != null)
            {
                return new 
                { 
                    title = model.GetTitle(),
                    path = model.GetPathName(),
                    type = model.GetType()
                };
            }
            return null;
        }

        private object GetThumbnail(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new { success = false, message = "File not found" };
                }

                string base64 = ThumbnailHelper.GetThumbnailBase64(_swApp, filePath);
                
                if (!string.IsNullOrEmpty(base64))
                {
                    return new { success = true, image = base64 };
                }
                else
                {
                    return new { success = false, message = "No preview available" };
                }
            }
            catch (Exception ex)
            {
                return new { success = false, message = ex.Message };
            }
        }

        private object GetDocumentProperties(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new { success = false, message = "File not found" };
                }

                var fileInfo = new FileInfo(filePath);
                string ext = Path.GetExtension(filePath).ToLower();
                
                // 检查是否是 SolidWorks 文件
                bool isSWFile = ext == ".sldprt" || ext == ".sldasm" || ext == ".slddrw";
                
                if (!isSWFile)
                {
                    // 非 SW 文件，返回基本属性
                    var basicProps = new
                    {
                        fileName = fileInfo.Name,
                        fileSize = fileInfo.Length,
                        createdDate = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        modifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        filePath = filePath
                    };
                    return new { success = true, properties = basicProps };
                }

                // SolidWorks 文件 - 获取详细属性和自定义属性
                IModelDoc2 doc = null;
                bool needClose = false;
                int errors = 0, warnings = 0;
                
                try
                {
                    // 尝试获取已打开的文档
                    doc = FindOpenDocument(filePath);
                    
                    if (doc == null)
                    {
                        // 打开文档
                        doc = _swApp.OpenDoc6(filePath,
                            GetDocumentType(filePath),
                            (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                            "", ref errors, ref warnings) as IModelDoc2;
                        needClose = true;
                    }

                    if (doc == null)
                    {
                        // 无法打开文档，返回基本信息
                        var basicProps = new
                        {
                            fileName = fileInfo.Name,
                            fileSize = fileInfo.Length,
                            createdDate = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            modifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                            filePath = filePath,
                            docType = GetFileTypeLabel(filePath)
                        };
                        return new { success = true, properties = basicProps };
                    }

                    // 获取文档属性
                    var docType = GetFileTypeLabel(filePath);
                    var author = doc.Extension.CustomPropertyManager[""].Get("Author") ?? "-";
                    
                    // 获取材料和质量信息（仅零件）
                    string material = "-";
                    string mass = "-";
                    string volume = "-";
                    string surfaceArea = "-";
                    
                    if (doc.GetType() == (int)swDocumentTypes_e.swDocPART)
                    {
                        IPartDoc part = doc as IPartDoc;
                        if (part != null)
                        {
                            material = part.GetMaterialPropertyName2("", out _) ?? "-";
                            
                            // 获取质量属性
                            var massProps = doc.Extension.CreateMassProperty();
                            if (massProps != null)
                            {
                                mass = (massProps.Mass * 1000).ToString("F2") + " g";
                                volume = (massProps.Volume * 1000000).ToString("F2") + " cm³";
                                surfaceArea = (massProps.SurfaceArea * 10000).ToString("F2") + " cm²";
                            }
                        }
                    }

                    var properties = new
                    {
                        fileName = fileInfo.Name,
                        fileSize = fileInfo.Length,
                        createdDate = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        modifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                        filePath = filePath,
                        docType = docType,
                        material = material,
                        mass = mass,
                        volume = volume,
                        surfaceArea = surfaceArea,
                        author = author
                    };

                    // 获取自定义属性
                    var customProps = new List<object>();
                    var propMgr = doc.Extension.get_CustomPropertyManager("");
                    if (propMgr != null)
                    {
                        object propNames = null;
                        object propTypes = null;
                        object propValues = null;
                        object propResolved = null;
                        object propLinked = null;

                        int count = propMgr.GetAll3(ref propNames, ref propTypes, ref propValues, ref propResolved, ref propLinked);
                        
                        if (count > 0 && propNames is string[] names && propValues is string[] values)
                        {
                            for (int i = 0; i < names.Length; i++)
                            {
                                customProps.Add(new { name = names[i], value = values[i] });
                            }
                        }
                    }

                    return new { success = true, properties = properties, customProperties = customProps };
                }
                finally
                {
                    if (needClose && doc != null)
                    {
                        _swApp.CloseDoc(doc.GetTitle());
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"GetDocumentProperties error: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        private string GetFileTypeLabel(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            switch (ext)
            {
                case ".sldprt": return "SolidWorks 零件";
                case ".sldasm": return "SolidWorks 装配体";
                case ".slddrw": return "SolidWorks 工程图";
                case ".step":
                case ".stp": return "STEP 文件";
                case ".iges":
                case ".igs": return "IGES 文件";
                case ".stl": return "STL 文件";
                default: return "未知类型";
            }
        }

        private int GetDocumentType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLower();
            switch (ext)
            {
                case ".sldprt":
                    return (int)swDocumentTypes_e.swDocPART;
                case ".sldasm":
                    return (int)swDocumentTypes_e.swDocASSEMBLY;
                case ".slddrw":
                    return (int)swDocumentTypes_e.swDocDRAWING;
                default:
                    return (int)swDocumentTypes_e.swDocPART;
            }
        }

        private IModelDoc2 FindOpenDocument(string filePath)
        {
            object[] docs = _swApp.GetDocuments() as object[];
            if (docs != null)
            {
                foreach (object docObj in docs)
                {
                    IModelDoc2 doc = docObj as IModelDoc2;
                    if (doc != null)
                    {
                        string docPath = doc.GetPathName();
                        if (!string.IsNullOrEmpty(docPath) && 
                            docPath.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return doc;
                        }
                    }
                }
            }
            return null;
        }

        private void WriteLog(string message)
        {
            Log(message);
        }

        /// <summary>
        /// 获取零件或装配体的特征树
        /// </summary>
        private object GetFeatureTree(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    return new { success = false, message = "文件不存在" };
                }

                // 检查文件类型
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".sldprt" && ext != ".sldasm")
                {
                    return new { success = false, message = "只支持零件(.sldprt)和装配体(.sldasm)文件" };
                }

                // 检查文档是否已打开
                var doc = FindOpenDocument(filePath);
                bool needClose = false;

                if (doc == null)
                {
                    // 打开文档
                    int errors = 0, warnings = 0;
                    int docType = (ext == ".sldasm") ? 
                        (int)swDocumentTypes_e.swDocASSEMBLY : 
                        (int)swDocumentTypes_e.swDocPART;
                    
                    doc = _swApp.OpenDoc6(filePath, docType, 
                        (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errors, ref warnings);
                    
                    if (doc == null)
                    {
                        return new { success = false, message = $"无法打开文档。错误代码: {errors}" };
                    }
                    needClose = true;
                }

                try
                {
                    var featureManager = doc.FeatureManager;
                    if (featureManager == null)
                    {
                        return new { success = false, message = "无法访问特征管理器" };
                    }

                    var features = new List<object>();
                    IFeature feature = doc.FirstFeature() as IFeature;

                    while (feature != null)
                    {
                        var featureInfo = BuildFeatureInfo(feature);
                        if (featureInfo != null)
                        {
                            features.Add(featureInfo);
                        }
                        feature = feature.GetNextFeature() as IFeature;
                    }

                    return new 
                    { 
                        success = true, 
                        fileName = Path.GetFileName(filePath),
                        docType = ext == ".sldprt" ? "零件" : "装配体",
                        featureCount = features.Count,
                        features = features 
                    };
                }
                finally
                {
                    if (needClose)
                    {
                        _swApp.CloseDoc(doc.GetTitle());
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"GetFeatureTree error: {ex.Message}");
                return new { success = false, message = ex.Message };
            }
        }

        /// <summary>
        /// 构建单个特征的信息
        /// </summary>
        private object BuildFeatureInfo(IFeature feature)
        {
            try
            {
                if (feature == null) return null;

                string typeName = feature.GetTypeName2();
                string name = feature.Name;
                bool isSuppressed = feature.IsSuppressed();
                
                var featureInfo = new
                {
                    name = name,
                    type = typeName,
                    typeName = GetFeatureTypeName(typeName),
                    isSuppressed = isSuppressed,
                    id = feature.GetID()
                };

                return featureInfo;
            }
            catch (Exception ex)
            {
                Log($"BuildFeatureInfo error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 获取特征类型的中文名称
        /// </summary>
        private string GetFeatureTypeName(string typeName)
        {
            switch (typeName)
            {
                case "ProfileFeature": return "草图";
                case "Boss": return "拉伸凸台/基体";
                case "Cut": return "拉伸切除";
                case "Fillet": return "圆角";
                case "Chamfer": return "倒角";
                case "Shell": return "抽壳";
                case "Hole": return "孔";
                case "Pattern": return "阵列";
                case "Mirror": return "镜像";
                case "Reference": return "基准";
                case "Material": return "材料";
                case "Appearance": return "外观";
                case "AssemblyFeature": return "装配体特征";
                case "Component": return "零部件";
                case "Mate": return "配合";
                default: return typeName;
            }
        }
    }
}
