using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Concurrent;
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

                object result = null;

                // 在 UI 线程执行 SolidWorks 操作
                await RunOnUIThread(() =>
                {
                    switch (command)
                    {
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
                var properties = new
                {
                    fileName = fileInfo.Name,
                    fileSize = fileInfo.Length,
                    createdDate = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    modifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    filePath = filePath
                };

                return new { success = true, properties = properties };
            }
            catch (Exception ex)
            {
                return new { success = false, message = ex.Message };
            }
        }

        private void WriteLog(string message)
        {
            Log(message);
        }
    }
}
