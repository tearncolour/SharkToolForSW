using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace SharkTools
{
    /// <summary>
    /// Electron 通信服务端
    /// 通过 WebSocket 与 Electron 应用通信
    /// </summary>
    public class ElectronServer
    {
        private readonly ISldWorks _swApp;
        private readonly SharkCommandManager _cmdMgr;
        private readonly SynchronizationContext _uiContext;
        private WebSocketServer _wsServer;
        private const int PORT = 52789;
        private bool _isRunning = false;

        public ElectronServer(ISldWorks swApp, SharkCommandManager cmdMgr, SynchronizationContext uiContext)
        {
            _swApp = swApp;
            _cmdMgr = cmdMgr;
            _uiContext = uiContext;
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _wsServer = new WebSocketServer($"ws://127.0.0.1:{PORT}");
                _wsServer.AddWebSocketService<SharkToolsService>("/", () => new SharkToolsService(_swApp, _cmdMgr, _uiContext));
                _wsServer.Start();
                _isRunning = true;
                
                Log($"WebSocket 服务已启动: ws://127.0.0.1:{PORT}");
            }
            catch (Exception ex)
            {
                Log($"启动 WebSocket 服务失败: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                if (_wsServer != null && _wsServer.IsListening)
                {
                    _wsServer.Stop();
                }
            }
            catch { }
        }

        private void Log(string message)
        {
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: ElectronServer - {message}\r\n");
            }
            catch { }
        }
    }

    /// <summary>
    /// WebSocket 服务行为类
    /// </summary>
    public class SharkToolsService : WebSocketBehavior
    {
        private ISldWorks _swApp;
        private SharkCommandManager _cmdMgr;
        private SynchronizationContext _uiContext;

        public SharkToolsService() { }

        public SharkToolsService(ISldWorks swApp, SharkCommandManager cmdMgr, SynchronizationContext uiContext)
        {
            _swApp = swApp;
            _cmdMgr = cmdMgr;
            _uiContext = uiContext;
        }

        protected override void OnOpen()
        {
            WriteLog("Electron 客户端已连接");
        }

        protected override void OnMessage(MessageEventArgs e)
        {
            try
            {
                string message = e.Data;
                WriteLog($"收到消息: {message}");
                
                // 异步处理命令
                Task.Run(async () =>
                {
                    string response = await HandleCommandAsync(message);
                    Send(response);
                });
            }
            catch (Exception ex)
            {
                WriteLog($"处理消息错误: {ex.Message}");
                SendError(ex.Message);
            }
        }

        protected override void OnClose(CloseEventArgs e)
        {
            WriteLog($"Electron 客户端已断开: {e.Reason}");
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            WriteLog($"WebSocket 错误: {e.Message}");
        }

        private void SendError(string message)
        {
            var response = new { success = false, message = message };
            Send(JsonConvert.SerializeObject(response));
        }

        private async Task<string> HandleCommandAsync(string jsonBody)
        {
            string messageId = "";
            try
            {
                var data = JObject.Parse(jsonBody);
                messageId = data["id"]?.ToString();
                string command = data["command"]?.ToString();
                var payload = data["payload"];

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
                    control.Invoke(new Action(() =>
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
                    return new { success = true, image = "data:image/png;base64," + base64 };
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
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: SharkToolsService - {message}\r\n");
            }
            catch { }
        }
    }
}
