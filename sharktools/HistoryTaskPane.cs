using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using Newtonsoft.Json;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using SolidWorks.Interop.sldworks;

namespace SharkTools
{
    /// <summary>
    /// 历史记录 WebView2 任务窗格控件
    /// </summary>
    [ComVisible(true)]
    [Guid("F1E2D3C4-B5A6-4C7D-8E9F-0A1B2C3D4E5F")]
    [ProgId("SharkTools.HistoryTaskPane")]
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    public class HistoryTaskPane : UserControl
    {
        private WebView2 _webView;
        private ISldWorks _swApp;
        private IModelDoc2 _activeDoc;
        private HistoryTracker _tracker;
        private bool _isInitialized = false;

        public HistoryTaskPane(ISldWorks swApp, IModelDoc2 modelDoc, HistoryTracker tracker)
        {
            _swApp = swApp;
            _activeDoc = modelDoc;
            _tracker = tracker;

            InitializeComponent();
            InitializeWebView2Async();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };

            this.Controls.Add(_webView);
            this.Size = new System.Drawing.Size(300, 600);
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.ResumeLayout(false);
        }

        private async void InitializeWebView2Async()
        {
            try
            {
                Log("初始化历史记录 WebView2...");

                var env = await CoreWebView2Environment.CreateAsync(null, GetUserDataFolder(), null);
                await _webView.EnsureCoreWebView2Async(env);

                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true;

                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
                _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                _isInitialized = true;
                Log("历史记录 WebView2 初始化完成");

                LoadHistoryUI();
            }
            catch (Exception ex)
            {
                Log($"初始化失败: {ex.Message}");
            }
        }

        private string GetUserDataFolder()
        {
            string folder = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "SharkTools",
                "WebView2Cache_History"
            );

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            return folder;
        }

        private void LoadHistoryUI()
        {
            try
            {
                string uiPath = Path.Combine(GetUIFolder(), "history.html");

                if (!File.Exists(uiPath))
                {
                    Log($"UI 文件不存在: {uiPath}");
                    return;
                }

                _webView.CoreWebView2.Navigate($"file:///{uiPath.Replace("\\", "/")}");
                Log($"加载 UI: {uiPath}");
            }
            catch (Exception ex)
            {
                Log($"加载 UI 失败: {ex.Message}");
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (e.IsSuccess)
                {
                    Log("导航完成，发送历史数据");
                    SendHistoryData();
                }
                else
                {
                    Log($"导航失败: {e.WebErrorStatus}");
                }
            }
            catch (Exception ex)
            {
                Log($"导航完成处理失败: {ex.Message}");
            }
        }

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var message = Newtonsoft.Json.Linq.JObject.Parse(e.WebMessageAsJson);
                string method = message["method"].ToString();

                Log($"收到消息: {method}");

                switch (method)
                {
                    case "loadHistory":
                        SendHistoryData();
                        break;

                    case "rollbackTo":
                        string recordId = message["args"][0].ToString();
                        RollbackToRecord(recordId);
                        break;

                    case "toggleImportant":
                        string toggleId = message["args"][0].ToString();
                        ToggleImportant(toggleId);
                        break;

                    case "deleteRecord":
                        string deleteId = message["args"][0].ToString();
                        DeleteRecord(deleteId);
                        break;

                    case "exportHistory":
                        ExportHistory();
                        break;

                    case "restoreAll":
                        RestoreAllFeatures();
                        break;

                    case "goBack":
                        GoBackToMain();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"处理消息失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送历史数据到前端
        /// </summary>
        private void SendHistoryData()
        {
            try
            {
                if (_activeDoc == null)
                {
                    Log("没有活动文档");
                    return;
                }

                string docPath = _activeDoc.GetPathName();
                if (string.IsNullOrEmpty(docPath))
                {
                    ShowMessage("请先保存文档才能查看历史记录");
                    return;
                }

                var history = HistoryManager.LoadHistory(docPath);
                string json = JsonConvert.SerializeObject(history);

                CallJavaScript("updateHistory", json);
            }
            catch (Exception ex)
            {
                Log($"发送历史数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 回溯到指定记录
        /// </summary>
        private void RollbackToRecord(string recordId)
        {
            try
            {
                if (_tracker == null)
                {
                    ShowMessage("历史追踪器未初始化");
                    return;
                }

                bool success = _tracker.RollbackToRecord(recordId);

                if (success)
                {
                    ShowMessage("已回溯到指定时间点");
                }
                else
                {
                    ShowMessage("回溯失败，请查看日志");
                }
            }
            catch (Exception ex)
            {
                Log($"回溯失败: {ex.Message}");
                ShowMessage($"回溯失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换重要标记
        /// </summary>
        private void ToggleImportant(string recordId)
        {
            try
            {
                string docPath = _activeDoc?.GetPathName();
                if (string.IsNullOrEmpty(docPath)) return;

                HistoryManager.ToggleImportant(docPath, recordId);
                Log($"已切换重要标记: {recordId}");
            }
            catch (Exception ex)
            {
                Log($"切换重要标记失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除记录
        /// </summary>
        private void DeleteRecord(string recordId)
        {
            try
            {
                string docPath = _activeDoc?.GetPathName();
                if (string.IsNullOrEmpty(docPath)) return;

                HistoryManager.DeleteRecord(docPath, recordId);
                Log($"已删除记录: {recordId}");
            }
            catch (Exception ex)
            {
                Log($"删除记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出历史记录
        /// </summary>
        private void ExportHistory()
        {
            try
            {
                string docPath = _activeDoc?.GetPathName();
                if (string.IsNullOrEmpty(docPath))
                {
                    ShowMessage("请先保存文档");
                    return;
                }

                string textContent = HistoryManager.ExportToText(docPath);
                string exportPath = Path.Combine(
                    Path.GetDirectoryName(docPath),
                    $"{Path.GetFileNameWithoutExtension(docPath)}_历史记录.txt"
                );

                File.WriteAllText(exportPath, textContent, System.Text.Encoding.UTF8);

                ShowMessage($"导出成功！\n\n保存位置：\n{exportPath}");

                // 打开文件所在文件夹
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{exportPath}\"");
            }
            catch (Exception ex)
            {
                Log($"导出失败: {ex.Message}");
                ShowMessage($"导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复所有特征
        /// </summary>
        private void RestoreAllFeatures()
        {
            try
            {
                if (_tracker == null)
                {
                    ShowMessage("历史追踪器未初始化");
                    return;
                }

                bool success = _tracker.RestoreAllFeatures();

                if (success)
                {
                    ShowMessage("已恢复所有特征");
                }
                else
                {
                    ShowMessage("恢复失败");
                }
            }
            catch (Exception ex)
            {
                Log($"恢复失败: {ex.Message}");
                ShowMessage($"恢复失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 返回主界面
        /// </summary>
        private void GoBackToMain()
        {
            try
            {
                // 关闭历史记录面板，显示主面板
                // 这个需要在 SharkCommandManager 中实现面板切换逻辑
                Log("请求返回主界面");
                
                // 触发自定义事件通知父级
                this.Tag = "CLOSE_HISTORY";
            }
            catch (Exception ex)
            {
                Log($"返回主界面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 调用 JavaScript 方法
        /// </summary>
        private async void CallJavaScript(string method, params object[] args)
        {
            try
            {
                if (!_isInitialized) return;

                string argsJson = JsonConvert.SerializeObject(args);
                string script = $"window.dispatchEvent(new MessageEvent('message', {{ data: {{ method: '{method}', args: {argsJson} }} }}))";

                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Log($"调用 JS 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message)
        {
            try
            {
                _swApp?.SendMsgToUser(message);
            }
            catch { }
        }

        private string GetUIFolder()
        {
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string assemblyDir = Path.GetDirectoryName(assemblyPath);
            return Path.Combine(assemblyDir, "ui");
        }

        private void Log(string message)
        {
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: HistoryTaskPane - {message}\r\n");
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _webView?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
