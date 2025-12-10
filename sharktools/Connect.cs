using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace SharkTools
{
    [ComVisible(true)]
    [Guid("D7F5A4A3-9F38-4367-849A-5A7F6C26DFB1")]
    public class Connect : ISwAddin
    {
        private ISldWorks _swApp;
        private ICommandManager _cmdMgr;
        public int AddinCookie { get; set; }

        private const int MainCommandGroupId = 1;
        private const int HelloCommandIndex = 0; // first and only command
        private const string TabName = "SharkTools";
            private static bool EnableUI = true; // enable command group/menu/toolbar
            private static bool EnableTabs = false; // set true to create command tabs per doc type

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            _swApp = (ISldWorks)ThisSW;
            AddinCookie = Cookie;
            _cmdMgr = _swApp.GetCommandManager(Cookie);

            if (EnableUI)
            {
                try
                {
                    SetupCommandUI();
                }
                catch (Exception ex)
                {
                    try { _swApp.SendMsgToUser2($"SharkTools UI init failed: {ex.Message}", 1, 0); } catch { }
                }
            }
            else
            {
                try { _swApp.SendMsgToUser2("SharkTools Add-in 已连接（UI 暂未创建）", 0, 0); } catch { }
            }

            // TODO: 初始化命令、线程、监听事件等
            // 示例：显示消息确认已连接
            try
            {
                _swApp.SendMsgToUser2("SharkTools Add-in 已连接到 SOLIDWORKS", 0, 0);
            }
            catch { }

            return true;
        }

        public bool DisconnectFromSW()
        {
            if (EnableUI)
            {
                TeardownCommandUI();
            }

            // TODO: 取消注册命令、事件回调等，释放资源
            try
            {
                _swApp.SendMsgToUser2("SharkTools Add-in 已从 SOLIDWORKS 断开", 0, 0);
            }
            catch { }

            _swApp = null;
            _cmdMgr = null;
            return true;
        }

        public void OnHello()
        {
            ExampleCommand.ShowHello(_swApp);
        }

        private void SetupCommandUI()
        {
            if (_cmdMgr == null)
            {
                return;
            }

            CommandGroup cmdGroup = _cmdMgr.GetCommandGroup(MainCommandGroupId);
            if (cmdGroup == null)
            {
                int err = 0;
                bool ignorePrev = false;
                cmdGroup = _cmdMgr.CreateCommandGroup2(
                    MainCommandGroupId,
                    "SharkTools",
                    "SharkTools commands",
                    "SharkTools",
                    -1,
                    ignorePrev,
                    ref err);
            }

            cmdGroup.HasMenu = true;
            cmdGroup.HasToolbar = true;

            cmdGroup.AddCommandItem2(
                "Hello",
                -1,
                "Show hello message",
                "Hello",
                0,
                nameof(OnHello),
                "",
                HelloCommandIndex,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem));

            cmdGroup.Activate();

            AddCommandTab(cmdGroup, (int)swDocumentTypes_e.swDocPART);
            AddCommandTab(cmdGroup, (int)swDocumentTypes_e.swDocASSEMBLY);
            AddCommandTab(cmdGroup, (int)swDocumentTypes_e.swDocDRAWING);
        }

        private void AddCommandTab(CommandGroup cmdGroup, int docType)
        {
                if (cmdGroup == null || _cmdMgr == null || !EnableTabs)
            {
                return;
            }

            CommandTab existingTab = _cmdMgr.GetCommandTab(docType, TabName);
            if (existingTab != null)
            {
                _cmdMgr.RemoveCommandTab(existingTab);
            }

            CommandTab cmdTab = _cmdMgr.AddCommandTab(docType, TabName);
            if (cmdTab == null)
            {
                return;
            }

            CommandTabBox cmdBox = cmdTab.AddCommandTabBox();
            int[] cmdIDs = { cmdGroup.CommandID[HelloCommandIndex] };
            int[] textTypes = { (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow };
            cmdBox.AddCommands(cmdIDs, textTypes);
        }

        private void TeardownCommandUI()
        {
            if (_cmdMgr == null)
            {
                return;
            }

            CommandTab tab;

            tab = _cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocPART, TabName);
            if (tab != null)
            {
                _cmdMgr.RemoveCommandTab(tab);
            }

            tab = _cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocASSEMBLY, TabName);
            if (tab != null)
            {
                _cmdMgr.RemoveCommandTab(tab);
            }

            tab = _cmdMgr.GetCommandTab((int)swDocumentTypes_e.swDocDRAWING, TabName);
            if (tab != null)
            {
                _cmdMgr.RemoveCommandTab(tab);
            }

            _cmdMgr.RemoveCommandGroup(MainCommandGroupId);
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
