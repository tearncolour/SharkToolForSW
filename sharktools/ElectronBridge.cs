using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;

namespace SharkTools
{
    /// <summary>
    /// Electron 应用启动器
    /// 负责启动 Electron 应用
    /// </summary>
    public class ElectronBridge
    {
        private static ElectronBridge _instance;
        private static readonly object _lock = new object();
        
        private Process _electronProcess;
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
                // 简单检查进程是否存在
                if (_electronProcess != null && !_electronProcess.HasExited)
                {
                    Log("Electron 应用已在运行");
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

                return true;
            }
            catch (Exception ex)
            {
                Log($"启动 Electron 应用失败: {ex.Message}");
                return false;
            }
        }

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
