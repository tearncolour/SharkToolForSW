using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace SharkTools
{
    /// <summary>
    /// GitHub OAuth 登录管理器
    /// 使用 Device Flow 方式进行桌面应用授权
    /// </summary>
    public class GitHubAuth
    {
        // GitHub OAuth App 配置（需要在 GitHub 创建 OAuth App）
        // 由于这是开源项目，建议用户自行创建 OAuth App
        // 临时使用 Device Flow 不需要 Client Secret
        private const string ClientId = "Ov23liYourClientId"; // 需要替换为实际的 Client ID
        
        // 保存登录状态的文件路径
        private static readonly string TokenFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SharkTools",
            "github_token.json"
        );

        // 当前登录的用户信息
        public static GitHubUser CurrentUser { get; private set; }

        /// <summary>
        /// GitHub 用户信息
        /// </summary>
        public class GitHubUser
        {
            public string Login { get; set; }      // 用户名
            public string Name { get; set; }       // 显示名称
            public string AvatarUrl { get; set; } // 头像 URL
            public string AccessToken { get; set; } // 访问令牌（不显示）
        }

        /// <summary>
        /// 保存的令牌数据
        /// </summary>
        private class SavedToken
        {
            public string access_token { get; set; }
            public string login { get; set; }
            public string name { get; set; }
            public string avatar_url { get; set; }
        }

        private static readonly JavaScriptSerializer _jsonSerializer = new JavaScriptSerializer();

        /// <summary>
        /// 检查是否已登录（加载保存的令牌）
        /// </summary>
        public static bool TryLoadSavedLogin()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                {
                    string json = File.ReadAllText(TokenFilePath, Encoding.UTF8);
                    var saved = _jsonSerializer.Deserialize<SavedToken>(json);
                    
                    if (!string.IsNullOrEmpty(saved?.access_token))
                    {
                        CurrentUser = new GitHubUser
                        {
                            Login = saved.login,
                            Name = saved.name,
                            AvatarUrl = saved.avatar_url,
                            AccessToken = saved.access_token
                        };
                        Log($"已加载保存的登录状态: {saved.login}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"加载登录状态失败: {ex.Message}");
            }
            return false;
        }

        /// <summary>
        /// 保存登录状态到本地文件
        /// </summary>
        private static void SaveLogin(GitHubUser user)
        {
            try
            {
                // 确保目录存在
                string dir = Path.GetDirectoryName(TokenFilePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var saved = new SavedToken
                {
                    access_token = user.AccessToken,
                    login = user.Login,
                    name = user.Name,
                    avatar_url = user.AvatarUrl
                };

                string json = _jsonSerializer.Serialize(saved);
                File.WriteAllText(TokenFilePath, json, Encoding.UTF8);
                Log($"登录状态已保存: {user.Login}");
            }
            catch (Exception ex)
            {
                Log($"保存登录状态失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 开始 GitHub 登录流程
        /// 打开浏览器让用户授权
        /// </summary>
        public static void StartLogin(Action<bool, string> onComplete)
        {
            // 使用简单的方式：打开 GitHub 授权页面
            // 用户手动复制 token 或使用 Personal Access Token
            
            // 方案1：使用 Personal Access Token（最简单）
            // 打开 GitHub 设置页面让用户创建 token
            string tokenUrl = "https://github.com/settings/tokens/new?description=SharkTools&scopes=read:user";
            
            try
            {
                // 打开浏览器
                Process.Start(new ProcessStartInfo
                {
                    FileName = tokenUrl,
                    UseShellExecute = true
                });
                
                Log("已打开浏览器，等待用户创建 Personal Access Token");
                onComplete?.Invoke(true, "浏览器已打开，请创建 Personal Access Token 后输入");
            }
            catch (Exception ex)
            {
                Log($"打开浏览器失败: {ex.Message}");
                onComplete?.Invoke(false, $"打开浏览器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 使用 Personal Access Token 登录
        /// </summary>
        public static async Task<bool> LoginWithToken(string token, Action<bool, string> onComplete)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                onComplete?.Invoke(false, "Token 不能为空");
                return false;
            }

            try
            {
                // 启用 TLS 1.2（GitHub API 需要）
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                
                using (var handler = new HttpClientHandler())
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                    
                    using (var client = new HttpClient(handler))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", $"token {token}");
                        client.DefaultRequestHeaders.Add("User-Agent", "SharkTools-SolidWorks-Addin");
                        client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
                        client.Timeout = TimeSpan.FromSeconds(30);

                        // 获取用户信息
                        Log($"正在请求 GitHub API...");
                        var response = await client.GetAsync("https://api.github.com/user");
                        
                        Log($"响应状态: {response.StatusCode}");
                        
                        if (response.IsSuccessStatusCode)
                        {
                            string json = await response.Content.ReadAsStringAsync();
                            Log($"响应内容: {json.Substring(0, Math.Min(200, json.Length))}...");
                            
                            var userInfo = _jsonSerializer.Deserialize<Dictionary<string, object>>(json);
                            
                            CurrentUser = new GitHubUser
                            {
                                Login = userInfo.ContainsKey("login") ? userInfo["login"]?.ToString() : null,
                                Name = userInfo.ContainsKey("name") ? userInfo["name"]?.ToString() : null,
                                AvatarUrl = userInfo.ContainsKey("avatar_url") ? userInfo["avatar_url"]?.ToString() : null,
                                AccessToken = token
                            };

                            // 保存登录状态
                            SaveLogin(CurrentUser);

                            Log($"登录成功: {CurrentUser.Login}");
                            onComplete?.Invoke(true, $"登录成功！欢迎 {CurrentUser.Name ?? CurrentUser.Login}");
                            return true;
                        }
                        else
                        {
                            string error = await response.Content.ReadAsStringAsync();
                            Log($"登录失败: {response.StatusCode} - {error}");
                            onComplete?.Invoke(false, $"登录失败: Token 无效或已过期 ({response.StatusCode})");
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"登录异常: {ex.GetType().Name} - {ex.Message}");
                if (ex.InnerException != null)
                {
                    Log($"内部异常: {ex.InnerException.Message}");
                }
                onComplete?.Invoke(false, $"登录失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 退出登录
        /// </summary>
        public static void Logout()
        {
            try
            {
                if (File.Exists(TokenFilePath))
                {
                    File.Delete(TokenFilePath);
                }
                CurrentUser = null;
                Log("已退出登录");
            }
            catch (Exception ex)
            {
                Log($"退出登录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否已登录
        /// </summary>
        public static bool IsLoggedIn => CurrentUser != null && !string.IsNullOrEmpty(CurrentUser.AccessToken);

        /// <summary>
        /// 获取当前用户显示名称
        /// </summary>
        public static string GetDisplayName()
        {
            if (CurrentUser == null) return "未登录";
            return CurrentUser.Name ?? CurrentUser.Login ?? "未知用户";
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt",
                    $"{DateTime.Now}: [GitHubAuth] {message}\r\n"
                );
            }
            catch { }
        }
    }
}
