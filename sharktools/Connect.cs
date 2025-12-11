using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace SharkTools
{
    [ComVisible(true)]
    [Guid("D7F5A4A3-9F38-4367-849A-5A7F6C26DFB1")]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class Connect : ISwAddin
    {
        private ISldWorks _swApp;
        private SharkCommandManager _sharkCmdMgr;
        private ElectronServer _electronServer;
        public int AddinCookie { get; set; }

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                _swApp = (ISldWorks)ThisSW;
                AddinCookie = Cookie;
                
                // CRITICAL FIX: Use SetAddinCallbackInfo2 for better compatibility
                bool callbackRes = _swApp.SetAddinCallbackInfo2(0, this, AddinCookie);
                try {
                     System.IO.File.AppendAllText(
                        @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                        $"{DateTime.Now}: SetAddinCallbackInfo2 result: {callbackRes}\r\n"
                    );
                } catch {}

                // 初始化数据库
                HistoryDatabase.Initialize();
                
                // 异步执行数据迁移
                Task.Run(() => HistoryDatabase.MigrateFromJson());

                // Initialize Command Manager
                _sharkCmdMgr = new SharkCommandManager(_swApp, Cookie);
                
                // 获取当前 UI 线程上下文
                var uiContext = SynchronizationContext.Current;

                // 初始化并启动 Electron 通信服务 (优先启动服务，确保通信正常)
                try
                {
                    _electronServer = new ElectronServer(_swApp, _sharkCmdMgr, uiContext);
                    _electronServer.Start();
                }
                catch (Exception ex)
                {
                    try {
                        System.IO.File.AppendAllText(
                            @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                            $"{DateTime.Now}: ElectronServer init error: {ex.Message}\r\n"
                        );
                    } catch {}
                }

                // Try to initialize UI immediately
                _sharkCmdMgr.Initialize();

                return true;
            }
            catch (Exception ex)
            {
                try {
                    System.IO.File.AppendAllText(
                        @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                        $"{DateTime.Now}: Connect Error: {ex.Message}\r\n"
                    );
                } catch {}
                return false;
            }
        }

        public bool DisconnectFromSW()
        {
            try
            {
                if (_electronServer != null)
                {
                    _electronServer.Stop();
                    _electronServer = null;
                }

                if (_sharkCmdMgr != null)
                {
                    _sharkCmdMgr.Teardown();
                    _sharkCmdMgr = null;
                }

                return true;
            }
            finally
            {
                _swApp = null;
            }
        }

        [ComVisible(true)]
        public void SharkHello()
        {
            try {
                 System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: SharkHello called!\r\n"
                );
            } catch {}
            ExampleCommand.ShowHello(_swApp);
        }

        [ComVisible(true)]
        public int SharkEnable()
        {
            // 1 = 取消选中并启用, 2 = 取消选中并禁用, 3 = 选中并启用, 4 = 选中并禁用
            return 1; 
        }

        /// <summary>
        /// 启动独立的 Electron 应用
        /// </summary>
        [ComVisible(true)]
        public void LaunchElectronApp()
        {
            try {
                System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: LaunchElectronApp called!\r\n"
                );
            } catch {}

            // 异步启动 Electron 应用
            Task.Run(async () =>
            {
                try
                {
                    bool started = await ElectronBridge.Instance.StartElectronAppAsync();
                    if (started)
                    {
                        // 等待连接建立
                        await Task.Delay(1000);

                        // 显示窗口
                        _electronServer?.ShowWindow();
                        
                        // 发送当前文档信息
                        var doc = _swApp?.ActiveDoc as IModelDoc2;
                        if (doc != null)
                        {
                            string docName = System.IO.Path.GetFileName(doc.GetPathName());
                            string docPath = doc.GetPathName();
                            _electronServer?.NotifyDocumentOpened(docName, docPath);
                            
                            // 获取并发送历史记录
                            if (_sharkCmdMgr?.HistoryTracker != null)
                            {
                                var records = _sharkCmdMgr.HistoryTracker.GetAllRecords();
                                _electronServer?.SendHistoryUpdate(records);
                                
                                System.IO.File.AppendAllText(
                                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                                    $"{DateTime.Now}: Sent {records?.Count ?? 0} history records to Electron\r\n"
                                );
                            }
                        }
                    }
                    else
                    {
                        _swApp?.SendMsgToUser2(
                            "无法启动 SharkTools 应用。\n\n请确保 Electron 应用已正确安装。",
                            (int)swMessageBoxIcon_e.swMbWarning,
                            (int)swMessageBoxBtn_e.swMbOk
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.IO.File.AppendAllText(
                        @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                        $"{DateTime.Now}: LaunchElectronApp Error: {ex.Message}\r\n"
                    );
                }
            });
        }

        /// <summary>
        /// 启动 Electron 应用按钮状态
        /// </summary>
        [ComVisible(true)]
        public int LaunchElectronAppEnable()
        {
            return 1; // 始终启用
        }

        /// <summary>
        /// GitHub 登录回调 - 点击"登录 GitHub"按钮时调用
        /// </summary>
        [ComVisible(true)]
        public void GitHubLogin()
        {
            try {
                System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: GitHubLogin called!\r\n"
                );
            } catch {}

            // 如果已登录，显示用户信息
            if (GitHubAuth.IsLoggedIn)
            {
                _swApp.SendMsgToUser2(
                    $"已登录 GitHub\n\n用户名: {GitHubAuth.GetDisplayName()}\n\n如需重新登录，请先退出当前账号。",
                    (int)swMessageBoxIcon_e.swMbInformation,
                    (int)swMessageBoxBtn_e.swMbOk
                );
                return;
            }

            // 开始登录流程
            GitHubAuth.StartLogin((success, message) =>
            {
                if (success)
                {
                    // 显示输入 Token 的对话框提示
                    _swApp.SendMsgToUser2(
                        "浏览器已打开 GitHub Token 创建页面。\n\n" +
                        "请按以下步骤操作：\n" +
                        "1. 在浏览器中登录 GitHub\n" +
                        "2. 点击 \"Generate token\" 按钮\n" +
                        "3. 复制生成的 Token\n" +
                        "4. 在 SharkTools 任务窗格中粘贴 Token 并点击确认",
                        (int)swMessageBoxIcon_e.swMbInformation,
                        (int)swMessageBoxBtn_e.swMbOk
                    );
                }
                else
                {
                    _swApp.SendMsgToUser2(
                        $"登录失败: {message}",
                        (int)swMessageBoxIcon_e.swMbWarning,
                        (int)swMessageBoxBtn_e.swMbOk
                    );
                }
            });
        }

        /// <summary>
        /// GitHub 登录按钮启用状态回调
        /// </summary>
        [ComVisible(true)]
        public int GitHubLoginEnable()
        {
            return 1; // 始终启用
        }

        /// <summary>
        /// 性能优化回调 - 点击"优化性能"按钮时调用
        /// </summary>
        [ComVisible(true)]
        public void OptimizePerformance()
        {
            try {
                System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: OptimizePerformance called!\r\n"
                );
            } catch {}

            try
            {
                // 获取优化前的状态
                string beforeStatus = PerformanceOptimizer.GetResourceStatus();
                
                // 执行优化
                PerformanceOptimizer.Optimize();
                
                // 获取优化后的状态
                string afterStatus = PerformanceOptimizer.GetResourceStatus();
                
                _swApp.SendMsgToUser2(
                    $"性能优化完成！\n\n优化前:\n{beforeStatus}\n\n优化后:\n{afterStatus}",
                    (int)swMessageBoxIcon_e.swMbInformation,
                    (int)swMessageBoxBtn_e.swMbOk
                );

                // 检查 GDI 限制
                int currentLimit = PerformanceOptimizer.GetGDIProcessHandleQuota();
                if (currentLimit < 65536)
                {
                    int result = _swApp.SendMsgToUser2(
                        $"检测到系统 GDI 对象限制为 {currentLimit} (默认值通常为 10000)。\n" +
                        $"SolidWorks 在处理大型装配体时容易达到此限制导致崩溃。\n\n" +
                        $"是否将限制增加到最大值 65536？\n" +
                        $"(注意：此操作需要管理员权限，且修改后需重启电脑生效)",
                        (int)swMessageBoxIcon_e.swMbQuestion,
                        (int)swMessageBoxBtn_e.swMbYesNo
                    );
                    
                    if (result == (int)swMessageBoxResult_e.swMbHitYes)
                    {
                        string error;
                        if (PerformanceOptimizer.SetGDIProcessHandleQuota(65536, out error))
                        {
                            _swApp.SendMsgToUser2(
                                "GDI 限制已成功修改！\n请重启电脑以使更改生效。", 
                                (int)swMessageBoxIcon_e.swMbInformation, 
                                (int)swMessageBoxBtn_e.swMbOk
                            );
                        }
                        else
                        {
                            _swApp.SendMsgToUser2(
                                $"修改失败: {error}\n\n请尝试以管理员身份运行 SolidWorks 后重试。", 
                                (int)swMessageBoxIcon_e.swMbWarning, 
                                (int)swMessageBoxBtn_e.swMbOk
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _swApp.SendMsgToUser2(
                    $"优化过程中发生错误: {ex.Message}",
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
        }

        /// <summary>
        /// 性能优化按钮启用状态回调
        /// </summary>
        [ComVisible(true)]
        public int OptimizePerformanceEnable()
        {
            return 1; // 始终启用
        }

        // 注册到 SolidWorks 的注册表键
        [ComRegisterFunction]
        public static void Register(Type t)
        {
            try
            {
                // 使用 Addins 注册表键 (尝试写入 HKCU 以避免权限问题)
                string key = $"SOFTWARE\\SolidWorks\\Addins\\{t.GUID.ToString("B").ToUpper()}";

                using (RegistryKey regKey = Registry.CurrentUser.CreateSubKey(key))
                {
                    regKey.SetValue("Description", "SharkTools: SOLIDWORKS 智能工具插件");
                    regKey.SetValue("Title", "SharkTools 工具箱");
                    regKey.SetValue("LoadAtStartup", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Register error: " + ex.Message);
            }
        }

        [ComUnregisterFunction]
        public static void Unregister(Type t)
        {
            try
            {
                string key = $"SOFTWARE\\SolidWorks\\Addins\\{t.GUID.ToString("B").ToUpper()}";
                Registry.CurrentUser.DeleteSubKey(key, false);
            }
            catch { }
        }
    }
}
