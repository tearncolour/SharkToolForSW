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

                // Initialize Command Manager
                _sharkCmdMgr = new SharkCommandManager(_swApp, Cookie);
                
                // Try to initialize UI immediately
                _sharkCmdMgr.Initialize();

                // 弹窗确认插件已加载
                _swApp.SendMsgToUser2("SharkTools 插件已成功加载！\n\n提示：\n1) 在顶部工具栏空白处右键，勾选 'SharkTools' 以显示工具栏。\n2) 或者在右侧任务窗格图标栏中打开 'SharkTools' 面板以查看更多功能。", 
                    (int)swMessageBoxIcon_e.swMbInformation, 
                    (int)swMessageBoxBtn_e.swMbOk);

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
            // 1 = Deselect and Enable, 2 = Deselect and Disable, 3 = Select and Enable, 4 = Select and Disable
            return 1; 
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
                    regKey.SetValue("Description", "SharkTools: A simple SolidWorks Add-in");
                    regKey.SetValue("Title", "SharkTools");
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
