using System;
using System.Runtime.InteropServices;
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

        // 注册到 SolidWorks 的注册表键
        [ComRegisterFunction]
        public static void Register(Type t)
        {
            try
            {
                // 使用 Addins 注册表键
                string key = $"SOFTWARE\\SolidWorks\\Addins\\{t.GUID.ToString("B").ToUpper()}";

                using (RegistryKey regKey = Registry.LocalMachine.CreateSubKey(key))
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
                Registry.LocalMachine.DeleteSubKey(key, false);
            }
            catch { }
        }
    }
}
