using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

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

        /// <summary>
        /// C# 与 JavaScript 交互接口
        /// </summary>
        public interface ISldWorksProvider
        {
            void ShowHello();
            void ShowMessage(string msg);
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
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
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
                string htmlPath = Path.Combine(assemblyDir, "ui", "app.html");

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
                string message = e.TryGetWebMessageAsString();
                Log($"收到 JS 消息: {message}");

                // 解析 JSON 消息
                var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
                var data = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(message);

                if (data.ContainsKey("method"))
                {
                    string method = data["method"].ToString();
                    object[] args = data.ContainsKey("args") 
                        ? ((System.Collections.ArrayList)data["args"]).ToArray() 
                        : new object[0];

                    HandleJavaScriptCall(method, args);
                }
            }
            catch (Exception ex)
            {
                Log($"处理 JS 消息失败: {ex.Message}");
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
                            CallJavaScript("updateLoginStatus", true, userName);
                            CallJavaScript("showMessage", $"登录成功！欢迎 {userName}");
                        }
                        else
                        {
                            CallJavaScript("updateLoginStatus", false, "");
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
                CallJavaScript("updateLoginStatus", false, "");
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
                    CallJavaScript("updateLoginStatus", true, userName);
                }
                else
                {
                    CallJavaScript("updateLoginStatus", false, "");
                }
            }
            catch (Exception ex)
            {
                Log($"检查登录状态失败: {ex.Message}");
            }
        }

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
