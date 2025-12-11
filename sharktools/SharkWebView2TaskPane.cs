using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SharkTools
{
    /// <summary>
    /// SharkTools WebView2 任务窗格控件
    /// 使用 Chromium Edge 内核支持现代 Web 技术
    /// 支持 CSS backdrop-filter、Grid、Flexbox 等现代特性
    /// </summary>
    [ComVisible(true)]
    [Guid("A1B2C3D4-E5F6-4A5B-8C9D-0E1F2A3B4C5D")]
    [ProgId("SharkTools.WebView2TaskPane")]
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    public class SharkWebView2TaskPane : UserControl
    {
        private WebView2 _webView;
        private static ISldWorksProvider _swProvider;
        private bool _isInitialized = false;
        private string _uiFolder;  // UI 文件夹路径

        /// <summary>
        /// C# 与 JavaScript 交互接口
        /// </summary>
        public interface ISldWorksProvider
        {
            void ShowHello();
            void ShowMessage(string msg);
            IModelDoc2 GetActiveDocument();
        }

        public static void SetProvider(ISldWorksProvider provider)
        {
            _swProvider = provider;
        }

        public SharkWebView2TaskPane()
        {
            InitializeComponent();
            InitializeWebView2Async();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 创建 WebView2 控件
            _webView = new WebView2
            {
                Dock = DockStyle.Fill
            };

            this.Controls.Add(_webView);
            this.Size = new System.Drawing.Size(300, 600);
            this.BackColor = System.Drawing.Color.FromArgb(102, 126, 234);
            this.ResumeLayout(false);
        }

        /// <summary>
        /// 异步初始化 WebView2
        /// </summary>
        private async void InitializeWebView2Async()
        {
            try
            {
                Log("开始初始化 WebView2...");

                // 设置 WebView2 运行时环境
                var env = await CoreWebView2Environment.CreateAsync(null, GetUserDataFolder(), null);
                await _webView.EnsureCoreWebView2Async(env);

                Log("WebView2 核心初始化完成");

                // 配置 WebView2 设置
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;
                _webView.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true;
                _webView.CoreWebView2.Settings.IsWebMessageEnabled = true;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true; // 启用开发者工具

                // 注册 JavaScript 消息接收器
                _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

                // 注册导航完成事件
                _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

                _isInitialized = true;
                Log("WebView2 配置完成");

                // 加载 HTML 内容
                LoadHtmlUI();
            }
            catch (Exception ex)
            {
                Log($"WebView2 初始化失败: {ex.Message}");
                ShowErrorPage($"WebView2 初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取用户数据文件夹路径
        /// </summary>
        private string GetUserDataFolder()
        {
            string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "SharkTools", "WebView2");
            
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            
            return folder;
        }

        /// <summary>
        /// 加载 HTML 界面
        /// </summary>
        private void LoadHtmlUI()
        {
            try
            {
                if (!_isInitialized)
                {
                    Log("WebView2 尚未初始化，延迟加载");
                    return;
                }

                // 获取 HTML 文件路径
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyPath);
                _uiFolder = Path.Combine(assemblyDir, "ui");
                string htmlPath = Path.Combine(_uiFolder, "app.html");

                Log($"尝试加载 HTML: {htmlPath}");

                if (File.Exists(htmlPath))
                {
                    // WebView2 需要使用 file:/// 协议
                    string fileUri = new Uri(htmlPath).AbsoluteUri;
                    _webView.CoreWebView2.Navigate(fileUri);
                    Log($"导航到: {fileUri}");
                }
                else
                {
                    Log($"HTML 文件不存在: {htmlPath}");
                    ShowErrorPage($"界面文件未找到: {htmlPath}");
                }
            }
            catch (Exception ex)
            {
                Log($"加载 HTML 失败: {ex.Message}");
                ShowErrorPage($"加载失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示错误页面
        /// </summary>
        private void ShowErrorPage(string error)
        {
            string errorHtml = $@"
<!DOCTYPE html>
<html lang='zh-CN'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        body {{
            font-family: 'Microsoft YaHei UI', sans-serif;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 20px;
            margin: 0;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
        }}
        .error {{
            background: rgba(255, 255, 255, 0.15);
            backdrop-filter: blur(20px);
            border: 1px solid rgba(255, 255, 255, 0.3);
            padding: 30px;
            border-radius: 20px;
            text-align: center;
        }}
        h3 {{
            margin-top: 0;
            font-size: 20px;
        }}
        p {{
            font-size: 14px;
            line-height: 1.6;
            opacity: 0.9;
        }}
    </style>
</head>
<body>
    <div class='error'>
        <h3>⚠️ 加载错误</h3>
        <p>{error.Replace("<", "&lt;").Replace(">", "&gt;")}</p>
        <p style='margin-top: 20px; font-size: 12px;'>请检查文件是否存在</p>
    </div>
</body>
</html>";

            _webView.CoreWebView2.NavigateToString(errorHtml);
        }

        /// <summary>
        /// 导航完成事件处理
        /// </summary>
        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                Log("页面加载成功");
                
                // 注入 C# 桥接对象到 JavaScript
                InjectCSharpBridge();
            }
            else
            {
                Log($"页面加载失败: {e.WebErrorStatus}");
            }
        }

        /// <summary>
        /// 注入 C# 桥接对象到 JavaScript 环境
        /// </summary>
        private async void InjectCSharpBridge()
        {
            try
            {
                // 注入 window.chrome.webview 对象已经自动可用
                // 只需要通知 JavaScript 可以开始通信
                await _webView.CoreWebView2.ExecuteScriptAsync(@"
                    console.log('WebView2 桥接已就绪');
                    if (window.chrome && window.chrome.webview) {
                        console.log('chrome.webview 可用');
                    }
                ");
                
                Log("C# 桥接对象注入成功");
            }
            catch (Exception ex)
            {
                Log($"注入桥接对象失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 接收来自 JavaScript 的消息
        /// </summary>
        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                // 获取消息字符串
                string message = e.TryGetWebMessageAsString();
                Log($"收到 JS 消息: {message}");

                // 解析 JSON 消消息
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var data = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(message);

                if (data != null && data.ContainsKey("method"))
                {
                    string method = data["method"].ToString();
                    object[] args = new object[0];
                    
                    if (data.ContainsKey("args") && data["args"] != null)
                    {
                        if (data["args"] is System.Collections.ArrayList arrayList)
                        {
                            args = arrayList.ToArray();
                        }
                        else if (data["args"] is object[] objArray)
                        {
                            args = objArray;
                        }
                    }

                    HandleJavaScriptCall(method, args);
                }
            }
            catch (Exception ex)
            {
                Log($"处理 JS 消息失败: {ex.Message}\n堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 处理来自 JavaScript 的方法调用
        /// </summary>
        private void HandleJavaScriptCall(string method, object[] args)
        {
            Log($"处理 JS 调用: {method}");

            switch (method)
            {
                case "ShowHello":
                    _swProvider?.ShowHello();
                    break;

                case "OpenGitHubTokenPage":
                    GitHubAuth.StartLogin((success, msg) => { });
                    break;

                case "LoginWithToken":
                    if (args.Length > 0)
                    {
                        string token = args[0]?.ToString();
                        LoginWithToken(token);
                    }
                    break;

                case "Logout":
                    Logout();
                    break;

                case "CheckLoginStatus":
                    CheckLoginStatus();
                    break;

                // ===== 历史记录功能 =====
                case "openHistory":
                case "OpenHistory":
                    OpenHistory();
                    break;

                case "loadHistory":
                case "LoadHistory":
                    LoadHistoryRecords();
                    break;

                case "rollbackTo":
                    if (args.Length > 0)
                    {
                        string recordId = args[0]?.ToString();
                        RollbackToRecord(recordId);
                    }
                    break;

                case "restoreAll":
                    RestoreAllFeatures();
                    break;

                case "exportHistory":
                    ExportHistoryToFile();
                    break;

                case "toggleImportant":
                    if (args.Length > 0)
                    {
                        string recordId = args[0]?.ToString();
                        ToggleImportant(recordId);
                    }
                    break;

                case "deleteRecord":
                    if (args.Length > 0)
                    {
                        string recordId = args[0]?.ToString();
                        DeleteRecord(recordId);
                    }
                    break;

                case "goBack":
                    GoBackToMain();
                    break;

                case "LaunchClient":
                    LaunchElectronClient();
                    break;

                // 新增：更新标签
                case "updateTags":
                    if (args.Length >= 2)
                    {
                        string recordId = args[0]?.ToString();
                        string tagsJson = args[1]?.ToString();
                        UpdateRecordTags(recordId, tagsJson);
                    }
                    break;

                // 新增：更新用户注释
                case "updateUserNote":
                    if (args.Length >= 2)
                    {
                        string recordId = args[0]?.ToString();
                        string userNote = args[1]?.ToString();
                        UpdateRecordUserNote(recordId, userNote);
                    }
                    break;

                // 新增：创建手动保存点
                case "createSavePoint":
                    if (args.Length > 0)
                    {
                        string savePointName = args[0]?.ToString();
                        CreateManualSavePoint(savePointName);
                    }
                    break;

                // 新增：导入历史记录
                case "importHistory":
                    if (args.Length > 0)
                    {
                        string jsonContent = args[0]?.ToString();
                        ImportHistoryFromJson(jsonContent);
                    }
                    break;

                default:
                    Log($"未知方法: {method}");
                    break;
            }
        }

        /// <summary>
        /// 使用 Token 登录
        /// </summary>
        private async void LoginWithToken(string token)
        {
            try
            {
                Log($"开始登录，Token 长度: {token?.Length ?? 0}");

                await GitHubAuth.LoginWithToken(token, (success, msg) =>
                {
                    // 在 UI 线程更新
                    this.Invoke(new Action(() =>
                    {
                        if (success)
                        {
                            string userName = GitHubAuth.GetDisplayName();
                            string avatarUrl = GitHubAuth.CurrentUser?.AvatarUrl ?? "";
                            CallJavaScript("updateLoginStatus", true, userName, avatarUrl);
                            CallJavaScript("showMessage", $"登录成功！欢迎 {userName}");
                        }
                        else
                        {
                            CallJavaScript("updateLoginStatus", false, "", "");
                            CallJavaScript("showMessage", msg);
                        }
                    }));
                });
            }
            catch (Exception ex)
            {
                Log($"登录异常: {ex.Message}");
                CallJavaScript("showMessage", $"登录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 退出登录
        /// </summary>
        private void Logout()
        {
            try
            {
                GitHubAuth.Logout();
                CallJavaScript("updateLoginStatus", false, "", "");
                CallJavaScript("showMessage", "已退出登录");
            }
            catch (Exception ex)
            {
                Log($"退出登录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查登录状态
        /// </summary>
        private void CheckLoginStatus()
        {
            try
            {
                if (GitHubAuth.TryLoadSavedLogin())
                {
                    string userName = GitHubAuth.GetDisplayName();
                    string avatarUrl = GitHubAuth.CurrentUser?.AvatarUrl ?? "";
                    CallJavaScript("updateLoginStatus", true, userName, avatarUrl);
                }
                else
                {
                    CallJavaScript("updateLoginStatus", false, "", "");
                }
            }
            catch (Exception ex)
            {
                Log($"检查登录状态失败: {ex.Message}");
            }
        }

        #region 历史记录功能

        /// <summary>
        /// 打开历史记录页面
        /// </summary>
        private void OpenHistory()
        {
            try
            {
                if (string.IsNullOrEmpty(_uiFolder))
                {
                    string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    string assemblyDir = Path.GetDirectoryName(assemblyPath);
                    _uiFolder = Path.Combine(assemblyDir, "ui");
                }

                string historyPath = Path.Combine(_uiFolder, "history.html");
                if (File.Exists(historyPath))
                {
                    string fileUri = new Uri(historyPath).AbsoluteUri;
                    _webView.CoreWebView2.Navigate(fileUri);
                    Log($"导航到历史页面: {fileUri}");
                }
                else
                {
                    Log($"历史页面不存在: {historyPath}");
                    CallJavaScript("showMessage", "历史记录页面不存在");
                }
            }
            catch (Exception ex)
            {
                Log($"打开历史页面失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载历史记录
        /// </summary>
        private void LoadHistoryRecords()
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null)
                {
                    CallJavaScript("onHistoryLoaded", new object[0], "main", false);
                    return;
                }

                string docPath = doc.GetPathName();
                if (string.IsNullOrEmpty(docPath))
                {
                    CallJavaScript("onHistoryLoaded", new object[0], "main", false);
                    return;
                }

                // 从数据库加载记录
                var records = HistoryDatabase.GetRecords(docPath);
                var docMeta = HistoryDatabase.GetDocumentMeta(docPath);
                
                // 转换为前端格式，同时检查每个特征的压制状态
                var frontendRecords = records.Select((r, idx) => new
                {
                    id = r.Id,
                    timestamp = r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    type = r.Type.ToString(),           // 前端用 type 不是 operationType
                    name = r.FeatureName,               // 前端用 name 不是 featureName
                    featureType = r.FeatureType,
                    featureName = r.FeatureName,        // 也传递原始特征名
                    featureIndex = idx,                 // 用于判断回溯位置
                    isImportant = r.IsImportant,
                    isSuppressed = IsFeatureSuppressed(doc, r.FeatureName),  // 检查压制状态
                    description = r.Description ?? r.FeatureName,
                    tags = r.Tags ?? new List<string>(),
                    userNote = r.UserNote ?? "",
                    recordType = r.RecordType ?? "auto"  // 记录类型
                }).ToList();

                var historyData = new 
                {
                    records = frontendRecords
                };

                CallJavaScript("onHistoryLoaded", historyData);
                Log($"加载了 {frontendRecords.Count} 条历史记录");
            }
            catch (Exception ex)
            {
                Log($"加载历史记录失败: {ex.Message}");
                CallJavaScript("onHistoryLoaded", new object[0], "main", false);
            }
        }

        /// <summary>
        /// 检查特征是否被压制
        /// </summary>
        private bool IsFeatureSuppressed(IModelDoc2 doc, string featureName)
        {
            try
            {
                var feat = GetFeatureByName(doc, featureName);
                if (feat == null) return false;
                return feat.IsSuppressed();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 回滚到指定记录 - 通过压制后续特征实现
        /// </summary>
        private void RollbackToRecord(string recordId)
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null)
                {
                    CallJavaScript("showMessage", "请先打开文档");
                    return;
                }

                string docPath = doc.GetPathName();
                var records = HistoryDatabase.GetRecords(docPath);
                
                // 找到目标记录在列表中的位置
                int targetListIndex = records.FindIndex(r => r.Id == recordId);
                if (targetListIndex < 0)
                {
                    CallJavaScript("showMessage", "未找到指定记录");
                    return;
                }

                var targetRecord = records[targetListIndex];
                
                // 需要压制的是目标之前的记录（列表是倒序的，索引小的是后创建的特征）
                // 例如：列表 [最新, ..., 目标, ..., 最旧]
                // 压制 targetListIndex 之前的所有记录
                var recordsToSuppress = records.Take(targetListIndex).ToList();

                Log($"回滚到记录 {recordId} (列表位置={targetListIndex})，需要压制 {recordsToSuppress.Count} 个特征");

                int suppressedCount = 0;
                // 压制这些特征
                foreach (var record in recordsToSuppress)
                {
                    if (SuppressFeature(doc, record.FeatureName))
                    {
                        suppressedCount++;
                    }
                }

                // 保存回滚状态
                // HistoryDatabase.SetBranchRollbackState(docPath, "main", recordId);

                doc.ForceRebuild3(false);
                
                // 重新加载历史记录
                LoadHistoryRecords();
                CallJavaScript("showMessage", $"已回滚到: {targetRecord.FeatureName}，压制了 {suppressedCount} 个特征");
            }
            catch (Exception ex)
            {
                Log($"回滚失败: {ex.Message}");
                CallJavaScript("showMessage", $"回滚失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 压制指定特征
        /// </summary>
        private bool SuppressFeature(IModelDoc2 doc, string featureName)
        {
            try
            {
                var feat = GetFeatureByName(doc, featureName);
                if (feat == null)
                {
                    Log($"未找到特征: {featureName}");
                    return false;
                }

                // 检查是否已经被压制
                if (feat.IsSuppressed())
                {
                    Log($"特征已压制: {featureName}");
                    return true;
                }

                bool result = feat.SetSuppression2(
                    (int)swFeatureSuppressionAction_e.swSuppressFeature,
                    (int)swInConfigurationOpts_e.swThisConfiguration, 
                    null);
                
                Log($"压制特征 {featureName}: {(result ? "成功" : "失败")}");
                return result;
            }
            catch (Exception ex)
            {
                Log($"压制特征失败 [{featureName}]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 解压指定特征
        /// </summary>
        private bool UnsuppressFeature(IModelDoc2 doc, string featureName)
        {
            try
            {
                var feat = GetFeatureByName(doc, featureName);
                if (feat == null)
                {
                    Log($"未找到特征: {featureName}");
                    return false;
                }

                bool result = feat.SetSuppression2(
                    (int)swFeatureSuppressionAction_e.swUnSuppressFeature,
                    (int)swInConfigurationOpts_e.swThisConfiguration, 
                    null);
                
                Log($"解压特征 {featureName}: {(result ? "成功" : "失败")}");
                return result;
            }
            catch (Exception ex)
            {
                Log($"解压特征失败 [{featureName}]: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 根据名称获取特征
        /// </summary>
        private IFeature GetFeatureByName(IModelDoc2 doc, string featureName)
        {
            try
            {
                IFeature feat = doc.FirstFeature() as IFeature;
                while (feat != null)
                {
                    if (feat.Name == featureName)
                    {
                        return feat;
                    }
                    feat = feat.GetNextFeature() as IFeature;
                }
            }
            catch (Exception ex)
            {
                Log($"查找特征失败: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 恢复所有特征到最新状态 - 解压所有被压制的特征
        /// </summary>
        private void RestoreAllFeatures()
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null)
                {
                    CallJavaScript("showMessage", "请先打开文档");
                    return;
                }

                string docPath = doc.GetPathName();
                var records = HistoryDatabase.GetRecords(docPath);

                Log($"恢复所有特征，共 {records.Count} 条记录");

                int unsuppressedCount = 0;
                // 解压所有记录对应的特征
                foreach (var record in records)
                {
                    if (UnsuppressFeature(doc, record.FeatureName))
                    {
                        unsuppressedCount++;
                    }
                }

                // 清除回滚状态
                // HistoryDatabase.SetBranchRollbackState(docPath, "main", null);

                doc.ForceRebuild3(false);
                
                // 重新加载历史记录
                LoadHistoryRecords();
                CallJavaScript("showMessage", $"已恢复所有特征，解压了 {unsuppressedCount} 个");
            }
            catch (Exception ex)
            {
                Log($"恢复失败: {ex.Message}");
                CallJavaScript("showMessage", $"恢复失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 导出历史记录到文件
        /// </summary>
        private void ExportHistoryToFile()
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null)
                {
                    CallJavaScript("showMessage", "请先打开文档");
                    return;
                }

                string docPath = doc.GetPathName();
                var records = HistoryDatabase.GetRecords(docPath);

                if (records.Count == 0)
                {
                    CallJavaScript("showMessage", "没有历史记录可导出");
                    return;
                }

                // 导出到桌面
                string desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                string fileName = $"History_{Path.GetFileNameWithoutExtension(docPath)}_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string exportPath = Path.Combine(desktop, fileName);

                var exportData = new
                {
                    documentPath = docPath,
                    exportTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    recordCount = records.Count,
                    records = records
                };

                string json = Newtonsoft.Json.JsonConvert.SerializeObject(exportData, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(exportPath, json, System.Text.Encoding.UTF8);

                CallJavaScript("showMessage", $"已导出到: {exportPath}");
                Log($"历史记录已导出: {exportPath}");
            }
            catch (Exception ex)
            {
                Log($"导出失败: {ex.Message}");
                CallJavaScript("showMessage", $"导出失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从JSON导入历史记录
        /// </summary>
        private void ImportHistoryFromJson(string jsonContent)
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null)
                {
                    CallJavaScript("showMessage", "请先打开文档");
                    return;
                }

                string docPath = doc.GetPathName();
                
                // 解析导入数据
                var importData = Newtonsoft.Json.Linq.JObject.Parse(jsonContent);
                var importedRecords = importData["records"] as Newtonsoft.Json.Linq.JArray;
                
                if (importedRecords == null)
                {
                    CallJavaScript("showMessage", "无效的导入文件格式");
                    return;
                }
                
                int importedCount = 0;
                int skippedCount = 0;
                
                // 获取现有记录ID列表
                var existingRecords = HistoryDatabase.GetRecords(docPath);
                var existingFeatureNames = new HashSet<string>(existingRecords.Select(r => r.FeatureName));
                
                foreach (var record in importedRecords)
                {
                    try
                    {
                        string featureName = record["FeatureName"]?.ToString() ?? record["featureName"]?.ToString() ?? "";
                        
                        // 跳过已存在的记录（根据特征名判断）
                        if (existingFeatureNames.Contains(featureName))
                        {
                            skippedCount++;
                            continue;
                        }
                        
                        // 解析数值类型
                        int featureIndex = 0;
                        var indexToken = record["FeatureIndex"] ?? record["featureIndex"];
                        if (indexToken != null) int.TryParse(indexToken.ToString(), out featureIndex);
                        
                        bool isImportant = false;
                        var importantToken = record["IsImportant"] ?? record["isImportant"];
                        if (importantToken != null) bool.TryParse(importantToken.ToString(), out isImportant);
                        
                        var newRecord = new HistoryRecord
                        {
                            Id = Guid.NewGuid().ToString(), // 生成新ID避免冲突
                            Name = record["Name"]?.ToString() ?? record["name"]?.ToString() ?? "",
                            FeatureName = featureName,
                            FeatureType = record["FeatureType"]?.ToString() ?? record["featureType"]?.ToString() ?? "",
                            FeatureIndex = featureIndex,
                            Description = record["Description"]?.ToString() ?? record["description"]?.ToString() ?? "",
                            IsImportant = isImportant,
                            RecordType = record["RecordType"]?.ToString() ?? record["recordType"]?.ToString() ?? "auto",
                            UserNote = record["UserNote"]?.ToString() ?? record["userNote"]?.ToString() ?? "",
                            Timestamp = DateTime.Now // 使用当前时间
                        };
                        
                        // 导入标签
                        var tagsToken = record["Tags"] ?? record["tags"];
                        if (tagsToken != null && tagsToken is Newtonsoft.Json.Linq.JArray tagsArray)
                        {
                            newRecord.Tags = new List<string>();
                            foreach (var tag in tagsArray)
                            {
                                newRecord.Tags.Add(tag.ToString());
                            }
                        }
                        
                        HistoryDatabase.AddRecord(docPath, newRecord);
                        importedCount++;
                        existingFeatureNames.Add(featureName);
                    }
                    catch (Exception ex)
                    {
                        Log($"导入单条记录失败: {ex.Message}");
                    }
                }
                
                LoadHistoryRecords();
                CallJavaScript("showMessage", $"导入完成：{importedCount} 条新记录，{skippedCount} 条已跳过");
                Log($"历史记录导入完成: {importedCount} 新增, {skippedCount} 跳过");
            }
            catch (Exception ex)
            {
                Log($"导入失败: {ex.Message}");
                CallJavaScript("showMessage", $"导入失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 切换记录的重要标记
        /// </summary>
        private void ToggleImportant(string recordId)
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null) return;

                string docPath = doc.GetPathName();
                var records = HistoryDatabase.GetRecords(docPath);
                var record = records.FirstOrDefault(r => r.Id == recordId);

                if (record != null)
                {
                    record.IsImportant = !record.IsImportant;
                    // 同步更新记录类型
                    if (record.IsImportant)
                    {
                        record.RecordType = "important";
                    }
                    else if (record.RecordType == "important")
                    {
                        record.RecordType = "auto";
                    }
                    HistoryDatabase.UpdateRecord(docPath, record);
                    LoadHistoryRecords();
                    Log($"切换重要标记: {recordId} -> {record.IsImportant}");
                }
            }
            catch (Exception ex)
            {
                Log($"切换重要标记失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新记录的标签
        /// </summary>
        private void UpdateRecordTags(string recordId, string tagsJson)
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null) return;

                string docPath = doc.GetPathName();
                var records = HistoryDatabase.GetRecords(docPath);
                var record = records.FirstOrDefault(r => r.Id == recordId);

                if (record != null)
                {
                    var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                    record.Tags = serializer.Deserialize<List<string>>(tagsJson) ?? new List<string>();
                    HistoryDatabase.UpdateRecord(docPath, record);
                    Log($"更新标签: {recordId}, 标签数: {record.Tags.Count}");
                }
            }
            catch (Exception ex)
            {
                Log($"更新标签失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新记录的用户注释
        /// </summary>
        private void UpdateRecordUserNote(string recordId, string userNote)
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null) return;

                string docPath = doc.GetPathName();
                var records = HistoryDatabase.GetRecords(docPath);
                var record = records.FirstOrDefault(r => r.Id == recordId);

                if (record != null)
                {
                    record.UserNote = userNote;
                    HistoryDatabase.UpdateRecord(docPath, record);
                    Log($"更新注释: {recordId}");
                }
            }
            catch (Exception ex)
            {
                Log($"更新注释失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建手动保存点
        /// </summary>
        private void CreateManualSavePoint(string name)
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null)
                {
                    CallJavaScript("showMessage", "请先打开文档");
                    return;
                }

                string docPath = doc.GetPathName();
                var docMeta = HistoryDatabase.GetDocumentMeta(docPath);

                // 获取当前特征数量作为索引
                var records = HistoryDatabase.GetRecords(docPath);
                int featureIndex = records.Count > 0 ? records.Max(r => r.FeatureIndex) + 1 : 0;

                // 创建手动保存点记录
                var savePointRecord = new HistoryRecord
                {
                    Name = name,
                    FeatureName = $"SavePoint_{DateTime.Now:yyyyMMddHHmmss}",
                    Type = OperationType.Unknown,
                    FeatureType = "SavePoint",
                    Description = $"手动保存点: {name}",
                    FeatureIndex = featureIndex,
                    RecordType = "manual",  // 标记为手动保存点
                    IsImportant = true
                };

                HistoryDatabase.AddRecord(docPath, savePointRecord);
                LoadHistoryRecords();
                CallJavaScript("showMessage", $"已创建保存点: {name}");
                Log($"创建手动保存点: {name}");
            }
            catch (Exception ex)
            {
                Log($"创建保存点失败: {ex.Message}");
                CallJavaScript("showMessage", $"创建保存点失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除历史记录
        /// </summary>
        private void DeleteRecord(string recordId)
        {
            try
            {
                var doc = _swProvider?.GetActiveDocument();
                if (doc == null) return;

                string docPath = doc.GetPathName();
                HistoryDatabase.DeleteRecord(docPath, recordId);
                LoadHistoryRecords();
                Log($"删除记录: {recordId}");
            }
            catch (Exception ex)
            {
                Log($"删除记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 创建新分支
        /// </summary>
        private void GoBackToMain()
        {
            try
            {
                if (string.IsNullOrEmpty(_uiFolder))
                {
                    string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    string assemblyDir = Path.GetDirectoryName(assemblyPath);
                    _uiFolder = Path.Combine(assemblyDir, "ui");
                }

                string mainPath = Path.Combine(_uiFolder, "app.html");
                if (File.Exists(mainPath))
                {
                    string fileUri = new Uri(mainPath).AbsoluteUri;
                    _webView.CoreWebView2.Navigate(fileUri);
                    Log($"返回主界面: {fileUri}");
                }
                else
                {
                    Log($"主界面文件不存在: {mainPath}");
                }
            }
            catch (Exception ex)
            {
                Log($"返回主界面失败: {ex.Message}");
            }
        }

        private void LaunchElectronClient()
        {
            try
            {
                string desktopPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
                string clientPath = Path.Combine(desktopPath, "SharkToolForSW", "electron-app", "release", "win-unpacked", "SharkTools.exe");

                if (File.Exists(clientPath))
                {
                    System.Diagnostics.Process.Start(clientPath);
                    Log("已启动客户端");
                }
                else
                {
                    CallJavaScript("showMessage", $"未找到客户端程序: {clientPath}");
                }
            }
            catch (Exception ex)
            {
                Log($"启动客户端失败: {ex.Message}");
                CallJavaScript("showMessage", $"启动客户端失败: {ex.Message}");
            }
        }

        #endregion

        /// <summary>
        /// 调用 JavaScript 函数
        /// </summary>
        private async void CallJavaScript(string functionName, params object[] args)
        {
            try
            {
                if (!_isInitialized || _webView.CoreWebView2 == null)
                {
                    Log("WebView2 未初始化，无法调用 JS");
                    return;
                }

                // 构建参数字符串
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                string argsJson = string.Join(", ", Array.ConvertAll(args, arg => 
                {
                    if (arg is string)
                        return "\"" + arg.ToString().Replace("\"", "\\\"") + "\"";
                    else if (arg is bool)
                        return arg.ToString().ToLower();
                    else
                        return serializer.Serialize(arg);
                }));

                string script = $"{functionName}({argsJson})";
                Log($"执行 JS: {script}");

                await _webView.CoreWebView2.ExecuteScriptAsync(script);
            }
            catch (Exception ex)
            {
                Log($"调用 JS 函数失败: {ex.Message}");
            }
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt",
                    $"{DateTime.Now}: [WebView2TaskPane] {message}\r\n"
                );
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
