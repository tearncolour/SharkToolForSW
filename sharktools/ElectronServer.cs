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
        private const string WS_URL = "ws://127.0.0.1:52789";
        private bool _isRunning = false;
        private System.Threading.Timer _reconnectTimer;
        private volatile bool _isConnecting = false;
        
        // 消息处理队列，确保串行处理
        private readonly BlockingCollection<string> _messageQueue = new BlockingCollection<string>();
        private Task _messageProcessorTask;
        private CancellationTokenSource _messageProcessorCts;

        public ElectronServer(ISldWorks swApp, SharkCommandManager cmdMgr, SynchronizationContext uiContext)
        {
            _swApp = swApp;
            _cmdMgr = cmdMgr;
            _uiContext = uiContext;
            
            // 启动消息处理器
            StartMessageProcessor();
        }
        
        private void StartMessageProcessor()
        {
            _messageProcessorCts = new CancellationTokenSource();
            _messageProcessorTask = Task.Run(async () =>
            {
                foreach (var message in _messageQueue.GetConsumingEnumerable(_messageProcessorCts.Token))
                {
                    try
                    {
                        string response = await HandleCommandAsync(message);
                        if (!string.IsNullOrEmpty(response))
                        {
                            Send(response);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"消息处理错误: {ex.Message}");
                    }
                }
            }, _messageProcessorCts.Token);
        }

        public void Start()
        {
            if (_isRunning) return;
            _isRunning = true;

            Connect();

            // 启动重连定时器，每5秒检查一次连接
            _reconnectTimer = new System.Threading.Timer(CheckConnection, null, 5000, 5000);
            
            Log($"正在连接 WebSocket 服务: {WS_URL}");
        }

        private void Connect()
        {
            if (_isConnecting) return;
            _isConnecting = true;

            Task.Run(() =>
            {
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

                    var client = new WebSocket(WS_URL);
                    client.OnOpen += OnOpen;
                    client.OnMessage += OnMessage;
                    client.OnClose += OnClose;
                    client.OnError += OnError;
                    client.Connect();

                    if (_isRunning)
                    {
                        _wsClient = client;
                    }
                    else
                    {
                        client.Close();
                    }
                }
                catch (Exception ex)
                {
                    Log($"连接失败: {ex.Message}");
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
            Log("已连接到 Electron 应用");
            // 发送身份标识
            Send(JsonConvert.SerializeObject(new { type = "identify", client = "solidworks" }));
        }

        private void OnMessage(object sender, MessageEventArgs e)
        {
            try
            {
                string message = e.Data;
                Log($"收到消息: {message}");
                
                // 将消息加入队列，确保串行处理
                _messageQueue.Add(message);
            }
            catch (Exception ex)
            {
                Log($"处理消息错误: {ex.Message}");
                SendError(ex.Message);
            }
        }

        private void OnClose(object sender, CloseEventArgs e)
        {
            Log($"连接断开: {e.Reason}");
        }

        private void OnError(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            Log($"WebSocket 错误: {e.Message}");
        }

        private void Send(string data)
        {
            if (_wsClient != null && _wsClient.IsAlive)
            {
                _wsClient.Send(data);
            }
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
                string command = data["command"]?.ToString();
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

                // 在 UI 线程执行 SolidWorks 操作
                await RunOnUIThread(() =>
                {
                    switch (command)
                    {
                        case "ping":
                            // 心跳检查
                            result = new { pong = true, timestamp = DateTime.Now };
                            break;

                        case "open":
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
                            string newPath = payload?["path"]?.ToString();
                            string fileType = payload?["docType"]?.ToString();
                            if (!string.IsNullOrEmpty(newPath) && !string.IsNullOrEmpty(fileType))
                            {
                                result = CreateDocument(newPath, fileType);
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
                                result = GetDocumentProperties(propPath);
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
                                    var customPropMgr = new SwCustomPropertyManager(_swApp, RunOnUIThread);
                                    result = customPropMgr.GetCustomProperties(customPropPath, configName).Result;
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

                        default:
                            throw new Exception("未知命令");
                    }
                });

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

        private Task RunOnUIThread(Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            if (_uiContext != null)
            {
                _uiContext.Post(state =>
                {
                    try
                    {
                        action();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
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
                            tcs.SetResult(true);
                        }
                        catch (Exception ex)
                        {
                            tcs.SetException(ex);
                        }
                    }));
                }
                else
                {
                    try
                    {
                        action();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }
            }
            return tcs.Task;
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
                
                _swApp.CloseDoc(model.GetTitle());

                if (saved)
                {
                    return new { success = true };
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
    }
}
