using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;

namespace SharkTools
{
    /// <summary>
    /// Electron 应用通信客户端
    /// 负责启动 Electron 应用并与其通信
    /// </summary>
    public class ElectronBridge
    {
        private static ElectronBridge _instance;
        private static readonly object _lock = new object();
        
        private HttpClient _httpClient;
        private Process _electronProcess;
        private const int HTTP_PORT = 52789;
        private const string BASE_URL = "http://127.0.0.1:52789";
        
        private bool _isConnected = false;
        private string _electronPath;

        public static ElectronBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ElectronBridge();
                        }
                    }
                }
                return _instance;
            }
        }

        private ElectronBridge()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(5);
            
            // 查找 Electron 应用路径
            FindElectronPath();
        }

        /// <summary>
        /// 查找 Electron 应用路径
        /// </summary>
        private void FindElectronPath()
        {
            // 可能的路径
            string[] possiblePaths = new string[]
            {
                // 开发环境 - electron-app 目录
                Path.Combine(GetSharkToolsDirectory(), "electron-app"),
                // 打包后的路径
                Path.Combine(GetSharkToolsDirectory(), "SharkTools-win32-x64"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SharkTools"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SharkTools")
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    // 检查是否有 electron.exe 或 SharkTools.exe
                    string electronExe = Path.Combine(path, "SharkTools.exe");
                    string nodeModules = Path.Combine(path, "node_modules");
                    
                    if (File.Exists(electronExe))
                    {
                        _electronPath = electronExe;
                        Log($"找到 Electron 应用: {_electronPath}");
                        return;
                    }
                    else if (Directory.Exists(nodeModules))
                    {
                        // 开发模式，使用 npm start
                        _electronPath = path;
                        Log($"找到 Electron 开发目录: {_electronPath}");
                        return;
                    }
                }
            }

            Log("未找到 Electron 应用");
        }

        /// <summary>
        /// 获取 SharkTools 目录
        /// </summary>
        private string GetSharkToolsDirectory()
        {
            // 从当前 DLL 位置向上查找
            string dllPath = typeof(ElectronBridge).Assembly.Location;
            string dir = Path.GetDirectoryName(dllPath);
            
            // 向上查找直到找到包含 electron-app 的目录
            while (!string.IsNullOrEmpty(dir))
            {
                string electronAppPath = Path.Combine(dir, "electron-app");
                if (Directory.Exists(electronAppPath))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            
            // 默认返回桌面路径
            return @"c:\Users\Administrator\Desktop\SharkToolForSW";
        }

        /// <summary>
        /// 启动 Electron 应用
        /// </summary>
        public async Task<bool> StartElectronAppAsync()
        {
            try
            {
                // 先检查是否已经在运行
                if (await IsElectronRunningAsync())
                {
                    Log("Electron 应用已在运行");
                    _isConnected = true;
                    return true;
                }

                if (string.IsNullOrEmpty(_electronPath))
                {
                    Log("Electron 路径未设置");
                    return false;
                }

                // 启动 Electron 应用
                ProcessStartInfo startInfo;
                
                if (File.Exists(_electronPath))
                {
                    // 打包后的 exe
                    startInfo = new ProcessStartInfo
                    {
                        FileName = _electronPath,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal
                    };
                }
                else if (Directory.Exists(_electronPath))
                {
                    // 开发模式，使用 npm start
                    startInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c cd /d \"{_electronPath}\" && npm start",
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                }
                else
                {
                    Log($"Electron 路径无效: {_electronPath}");
                    return false;
                }

                _electronProcess = Process.Start(startInfo);
                Log($"已启动 Electron 应用");

                // 等待应用启动
                for (int i = 0; i < 30; i++) // 最多等待 15 秒
                {
                    await Task.Delay(500);
                    if (await IsElectronRunningAsync())
                    {
                        _isConnected = true;
                        Log("Electron 应用已就绪");
                        return true;
                    }
                }

                Log("等待 Electron 应用超时");
                return false;
            }
            catch (Exception ex)
            {
                Log($"启动 Electron 应用失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查 Electron 应用是否在运行
        /// </summary>
        public async Task<bool> IsElectronRunningAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{BASE_URL}/ping");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 发送消息到 Electron 应用
        /// </summary>
        public async Task<bool> SendMessageAsync(object message)
        {
            try
            {
                string json = JsonConvert.SerializeObject(message);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{BASE_URL}/", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Log($"发送消息失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 显示 Electron 窗口
        /// </summary>
        public async Task ShowWindowAsync()
        {
            await SendMessageAsync(new { type = "show" });
        }

        /// <summary>
        /// 隐藏 Electron 窗口
        /// </summary>
        public async Task HideWindowAsync()
        {
            await SendMessageAsync(new { type = "hide" });
        }

        /// <summary>
        /// 通知文档打开
        /// </summary>
        public async Task NotifyDocumentOpenedAsync(string name, string path)
        {
            await SendMessageAsync(new
            {
                type = "document-opened",
                name = name,
                path = path
            });
        }

        /// <summary>
        /// 通知文档关闭
        /// </summary>
        public async Task NotifyDocumentClosedAsync()
        {
            await SendMessageAsync(new { type = "document-closed" });
        }

        /// <summary>
        /// 发送历史记录更新
        /// </summary>
        public async Task SendHistoryUpdateAsync(object records)
        {
            await SendMessageAsync(new
            {
                type = "history-update",
                records = records
            });
        }

        /// <summary>
        /// 发送分支更新
        /// </summary>
        public async Task SendBranchesUpdateAsync(object branches, string currentBranch)
        {
            await SendMessageAsync(new
            {
                type = "branches-update",
                branches = branches,
                currentBranch = currentBranch
            });
        }

        /// <summary>
        /// 发送新记录添加通知
        /// </summary>
        public async Task SendRecordAddedAsync(object record)
        {
            await SendMessageAsync(new
            {
                type = "record-added",
                record = record
            });
        }

        /// <summary>
        /// 停止 Electron 应用
        /// </summary>
        public void StopElectronApp()
        {
            try
            {
                if (_electronProcess != null && !_electronProcess.HasExited)
                {
                    _electronProcess.Kill();
                    _electronProcess = null;
                }
                _isConnected = false;
            }
            catch (Exception ex)
            {
                Log($"停止 Electron 应用失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 是否已连接
        /// </summary>
        public bool IsConnected => _isConnected;

        private void Log(string message)
        {
            try
            {
                string logFile = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
                File.AppendAllText(logFile, $"{DateTime.Now}: ElectronBridge - {message}\r\n");
            }
            catch { }
        }
    }
}
