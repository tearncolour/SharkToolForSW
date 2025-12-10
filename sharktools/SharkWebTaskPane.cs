using System;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace SharkTools
{
    /// <summary>
    /// SharkTools WebBrowser 任务窗格控件
    /// 使用 HTML + Vue 界面替代传统 WinForms
    /// </summary>
    [ComVisible(true)]
    [Guid("9B6F6E3E-5C2D-4E4F-AF9B-8D7C6E5F4E3B")]
    [ProgId("SharkTools.WebTaskPane")]
    [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
    public class SharkWebTaskPane : UserControl
    {
        private WebBrowser _browser;
        private static ISldWorksProvider _swProvider;

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

        public SharkWebTaskPane()
        {
            InitializeComponent();
            LoadHtmlUI();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // 创建 WebBrowser 控件
            _browser = new WebBrowser
            {
                Dock = DockStyle.Fill,
                ScriptErrorsSuppressed = true,
                IsWebBrowserContextMenuEnabled = false,
                WebBrowserShortcutsEnabled = true,
                AllowWebBrowserDrop = false
            };

            // 设置为可由脚本访问
            _browser.ObjectForScripting = new ScriptInterface(this);
            
            this.Controls.Add(_browser);
            this.Size = new System.Drawing.Size(300, 600);
            this.ResumeLayout(false);
        }

        /// <summary>
        /// 加载 HTML 界面
        /// </summary>
        private void LoadHtmlUI()
        {
            try
            {
                // 获取 HTML 文件路径
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string assemblyDir = Path.GetDirectoryName(assemblyPath);
                string htmlPath = Path.Combine(assemblyDir, "ui", "index.html");

                Log($"尝试加载 HTML: {htmlPath}");

                if (File.Exists(htmlPath))
                {
                    _browser.Navigate(htmlPath);
                    Log("HTML 界面加载成功");
                }
                else
                {
                    // 如果文件不存在，使用内嵌的简单 HTML
                    string fallbackHtml = @"
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <meta charset='UTF-8'>
                        <style>
                            body { 
                                font-family: 'Microsoft YaHei UI'; 
                                padding: 20px; 
                                background: #f5f5f5; 
                            }
                            .error {
                                background: #fff3cd;
                                border: 1px solid #ffc107;
                                padding: 15px;
                                border-radius: 8px;
                                color: #856404;
                            }
                        </style>
                    </head>
                    <body>
                        <div class='error'>
                            <h3>⚠️ 界面文件未找到</h3>
                            <p>HTML 文件路径: " + htmlPath + @"</p>
                            <p>请确保 ui/index.html 文件存在</p>
                        </div>
                    </body>
                    </html>";
                    
                    _browser.DocumentText = fallbackHtml;
                    Log($"HTML 文件不存在，使用备用界面");
                }
            }
            catch (Exception ex)
            {
                Log($"加载 HTML 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// JavaScript 可调用的脚本接口
        /// </summary>
        [ComVisible(true)]
        public class ScriptInterface
        {
            private SharkWebTaskPane _parent;

            public ScriptInterface(SharkWebTaskPane parent)
            {
                _parent = parent;
            }

            /// <summary>
            /// JavaScript 调用：显示 Hello 消息
            /// </summary>
            public void ShowHello()
            {
                try
                {
                    _swProvider?.ShowHello();
                }
                catch (Exception ex)
                {
                    Log($"ShowHello 错误: {ex.Message}");
                }
            }

            /// <summary>
            /// JavaScript 调用：打开 GitHub Token 创建页面
            /// </summary>
            public void OpenGitHubTokenPage()
            {
                try
                {
                    GitHubAuth.StartLogin((success, msg) =>
                    {
                        // 回调由登录流程处理
                    });
                }
                catch (Exception ex)
                {
                    Log($"OpenGitHubTokenPage 错误: {ex.Message}");
                }
            }

            /// <summary>
            /// JavaScript 调用：使用 Token 登录
            /// </summary>
            public void LoginWithToken(string token)
            {
                try
                {
                    Log($"收到登录请求，Token 长度: {token?.Length ?? 0}");
                    
                    GitHubAuth.LoginWithToken(token, (success, msg) =>
                    {
                        // 在 UI 线程更新界面
                        _parent.BeginInvoke(new Action(() =>
                        {
                            if (success)
                            {
                                // 通知 JavaScript 更新登录状态
                                string userName = GitHubAuth.GetDisplayName();
                                _parent.InvokeScript("updateLoginStatus", true, userName);
                                _parent.InvokeScript("showMessage", $"登录成功！欢迎 {userName}");
                            }
                            else
                            {
                                _parent.InvokeScript("updateLoginStatus", false, "");
                                _parent.InvokeScript("showMessage", msg);
                            }
                        }));
                    });
                }
                catch (Exception ex)
                {
                    Log($"LoginWithToken 错误: {ex.Message}");
                    _parent.InvokeScript("showMessage", $"登录失败: {ex.Message}");
                }
            }

            /// <summary>
            /// JavaScript 调用：退出登录
            /// </summary>
            public void Logout()
            {
                try
                {
                    GitHubAuth.Logout();
                    _parent.InvokeScript("updateLoginStatus", false, "");
                    _parent.InvokeScript("showMessage", "已退出登录");
                }
                catch (Exception ex)
                {
                    Log($"Logout 错误: {ex.Message}");
                }
            }

            /// <summary>
            /// JavaScript 调用：检查登录状态
            /// </summary>
            public void CheckLoginStatus()
            {
                try
                {
                    if (GitHubAuth.TryLoadSavedLogin())
                    {
                        string userName = GitHubAuth.GetDisplayName();
                        _parent.InvokeScript("updateLoginStatus", true, userName);
                    }
                    else
                    {
                        _parent.InvokeScript("updateLoginStatus", false, "");
                    }
                }
                catch (Exception ex)
                {
                    Log($"CheckLoginStatus 错误: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 调用 JavaScript 函数
        /// </summary>
        private void InvokeScript(string functionName, params object[] args)
        {
            try
            {
                if (_browser.Document != null)
                {
                    _browser.Document.InvokeScript(functionName, args);
                }
            }
            catch (Exception ex)
            {
                Log($"InvokeScript({functionName}) 错误: {ex.Message}");
            }
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt",
                    $"{DateTime.Now}: [WebTaskPane] {message}\r\n"
                );
            }
            catch { }
        }
    }
}
