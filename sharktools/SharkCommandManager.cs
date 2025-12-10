using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;

namespace SharkTools
{
    public class SharkCommandManager
    {
        private ISldWorks _swApp;
        private ICommandManager _cmdMgr;
        private CommandGroup _cmdGroup;
        private int _cookie;
        private TaskpaneView _taskPaneView;
        private SharkTaskPaneControl _taskPaneControl;
        
        private const int MainCommandGroupId = 2001;
        private const int HelloCommandIndex = 0;
        private const int GitHubLoginCommandIndex = 1;  // 新增：GitHub 登录命令索引
        private const string TabName = "SharkTools";

        public SharkCommandManager(ISldWorks app, int cookie)
        {
            _swApp = app;
            _cookie = cookie;
            _cmdMgr = _swApp.GetCommandManager(cookie);
        }

        public void Initialize()
        {
            Log("Initialize started");
            try 
            {
                // 0. 创建 TaskPane（最可靠的方式）
                CreateTaskPane();

                // 1. Create icon files
                string assemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string bmpPath = System.IO.Path.Combine(assemblyDir, "toolbar.bmp");
                string[] iconPaths = CreateIconFiles(assemblyDir);
                CreateDummyBitmap(bmpPath);

                // 2. 创建 CommandGroup
                int errors = 0;
                bool ignorePrev = false;
                
                _cmdGroup = _cmdMgr.CreateCommandGroup2(
                    MainCommandGroupId,      // 唯一标识
                    "SharkTools",            // 标题
                    "SharkTools 插件工具",   // 工具提示
                    "SharkTools 命令组",     // 提示
                    -1,                      // 位置
                    ignorePrev,              // 忽略旧版
                    ref errors               // 错误码
                );
                Log($"CreateCommandGroup2 result: {_cmdGroup != null}, errors: {errors}");

                if (_cmdGroup != null)
                {
                    // 设置图标（使用多尺寸图标列表）
                    _cmdGroup.IconList = iconPaths;
                    _cmdGroup.MainIconList = iconPaths;
                    
                    // 添加命令 1：打招呼
                    int cmdIndex = _cmdGroup.AddCommandItem2(
                        "打招呼",                         // Name（中文）
                        -1,                              // Position
                        "显示一个问候信息",               // Hint
                        "打招呼",                         // Tooltip
                        0,                               // Image index
                        "SharkHello",                    // Callback
                        "",                              // Enable method (empty = always enabled)
                        HelloCommandIndex,               // User command ID
                        (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem
                    );
                    Log($"AddCommandItem2 (打招呼) result: {cmdIndex}");

                    // 添加命令 2：登录 GitHub
                    int cmdIndex2 = _cmdGroup.AddCommandItem2(
                        "登录 GitHub",                    // Name（中文）
                        -1,                              // Position
                        "使用 GitHub 账号登录",           // Hint
                        "登录 GitHub",                    // Tooltip
                        0,                               // Image index（使用同一图标）
                        "GitHubLogin",                   // Callback
                        "",                              // Enable method
                        GitHubLoginCommandIndex,         // User command ID
                        (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem
                    );
                    Log($"AddCommandItem2 (登录 GitHub) result: {cmdIndex2}");

                    // 激活 CommandGroup（必须在创建 Tab 之前）
                    // 注意：Activate() 在某些版本可能崩溃，但需要它才能获取 CommandID
                    try 
                    {
                        bool activateResult = _cmdGroup.Activate();
                        Log($"CommandGroup.Activate result: {activateResult}");
                        
                        // 3. 创建 CommandTab（标签页）- 在激活后创建
                        CreateCommandTabs(iconPaths);
                    }
                    catch (Exception activateEx)
                    {
                        Log($"CommandGroup.Activate exception: {activateEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Error in Initialize: {ex.Message}");
            }
        }

        /// <summary>
        /// 为各种文档类型创建 CommandTab（标签页）
        /// </summary>
        private void CreateCommandTabs(string[] iconPaths)
        {
            // 为零件、装配体、工程图创建标签页
            int[] docTypes = new int[] {
                (int)swDocumentTypes_e.swDocPART,
                (int)swDocumentTypes_e.swDocASSEMBLY,
                (int)swDocumentTypes_e.swDocDRAWING
            };

            foreach (int docType in docTypes)
            {
                try
                {
                    // 获取或创建 CommandTab
                    CommandTab cmdTab = _cmdMgr.GetCommandTab(docType, "SharkTools");
                    
                    if (cmdTab == null)
                    {
                        // 创建新标签页
                        cmdTab = _cmdMgr.AddCommandTab(docType, "SharkTools");
                        Log($"AddCommandTab for docType {docType} result: {cmdTab != null}");
                    }
                    else
                    {
                        Log($"GetCommandTab for docType {docType}: already exists");
                    }

                    if (cmdTab != null)
                    {
                        // 获取标签页的工具箱
                        CommandTabBox cmdBox = cmdTab.AddCommandTabBox();
                        Log($"AddCommandTabBox for docType {docType} result: {cmdBox != null}");

                        if (cmdBox != null)
                        {
                            // 获取两个命令的 ID
                            int cmdId1 = _cmdGroup.CommandID[HelloCommandIndex];
                            int cmdId2 = _cmdGroup.CommandID[GitHubLoginCommandIndex];
                            Log($"命令 ID: 打招呼={cmdId1}, 登录GitHub={cmdId2}");

                            // 添加两个命令到工具箱
                            // 参数: CommandIDs, TextTypes
                            int[] cmdIds = new int[] { cmdId1, cmdId2 };
                            int[] textTypes = new int[] { 
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow
                            };
                            
                            bool addResult = cmdBox.AddCommands(cmdIds, textTypes);
                            Log($"添加命令到标签页 docType {docType}: {addResult}");
                        }
                    }
                }
                catch (Exception tabEx)
                {
                    Log($"CreateCommandTab error for docType {docType}: {tabEx.Message}");
                }
            }
        }

        private string[] CreateIconFiles(string dir)
        {
            // 使用已有的图标文件：20x20, 32x32, 40x40, 64x64, 96x96, 128x128
            int[] sizes = new int[] { 20, 32, 40, 64, 96, 128 };
            string[] paths = new string[sizes.Length];
            
            for (int i = 0; i < sizes.Length; i++)
            {
                string path = System.IO.Path.Combine(dir, $"icon_{sizes[i]}.bmp");
                paths[i] = path;
                // 不再自动创建，使用已有的 logo 图标
            }
            
            return paths;
        }

        private void CreateColorBitmap(string path, int width, int height, byte r, byte g, byte b)
        {
            try
            {
                if (!System.IO.File.Exists(path))
                {
                    int rowSize = ((width * 3 + 3) / 4) * 4;
                    int dataSize = rowSize * height;
                    int fileSize = 54 + dataSize;
                    byte[] bmp = new byte[fileSize];

                    // BMP Header
                    bmp[0] = 0x42; bmp[1] = 0x4D;
                    BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
                    bmp[10] = 54;

                    // DIB Header
                    bmp[14] = 40;
                    BitConverter.GetBytes(width).CopyTo(bmp, 18);
                    BitConverter.GetBytes(height).CopyTo(bmp, 22);
                    bmp[26] = 1;
                    bmp[28] = 24;

                    // Pixel data
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int offset = 54 + y * rowSize + x * 3;
                            bmp[offset] = b;
                            bmp[offset + 1] = g;
                            bmp[offset + 2] = r;
                        }
                    }

                    System.IO.File.WriteAllBytes(path, bmp);
                }
            }
            catch { }
        }

        private void CreateDummyBitmap(string path)
        {
            try {
                if (!System.IO.File.Exists(path))
                {
                    // Create a simple 16x16 bitmap (Red color)
                    // BMP Header (14 bytes) + DIB Header (40 bytes) + Pixel Data
                    int width = 16;
                    int height = 16;
                    int rowSize = ((width * 3 + 3) / 4) * 4; // Row size padded to 4 bytes
                    int dataSize = rowSize * height;
                    int fileSize = 54 + dataSize;

                    byte[] bmp = new byte[fileSize];

                    // BMP Header
                    bmp[0] = 0x42; bmp[1] = 0x4D; // "BM"
                    BitConverter.GetBytes(fileSize).CopyTo(bmp, 2);
                    bmp[10] = 54; // Offset to data

                    // DIB Header
                    bmp[14] = 40; // Header size
                    BitConverter.GetBytes(width).CopyTo(bmp, 18);
                    BitConverter.GetBytes(height).CopyTo(bmp, 22);
                    bmp[26] = 1; // Planes
                    bmp[28] = 24; // Bit count (24bpp)

                    // Data (Red pixels: B=0, G=0, R=255)
                    for (int i = 54; i < fileSize; i += 3)
                    {
                        if (i + 2 < fileSize)
                        {
                            bmp[i] = 0;     // B
                            bmp[i+1] = 0;   // G
                            bmp[i+2] = 255; // R
                        }
                    }

                    System.IO.File.WriteAllBytes(path, bmp);
                }
            } catch {}
        }

        private void Log(string message)
        {
            try
            {
                System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: {message}\r\n"
                );
            }
            catch {}
        }

        private void CreateTaskPane()
        {
            try
            {
                // 设置 Provider
                SharkTaskPaneControl.SetProvider(new TaskPaneProvider(_swApp));
                
                // 获取图标路径 - 使用多尺寸图标
                string assemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string[] iconPaths = new string[] {
                    System.IO.Path.Combine(assemblyDir, "icon_20.bmp"),
                    System.IO.Path.Combine(assemblyDir, "icon_32.bmp"),
                    System.IO.Path.Combine(assemblyDir, "icon_40.bmp"),
                    System.IO.Path.Combine(assemblyDir, "icon_64.bmp"),
                    System.IO.Path.Combine(assemblyDir, "icon_96.bmp"),
                    System.IO.Path.Combine(assemblyDir, "icon_128.bmp")
                };
                
                // 使用 CreateTaskpaneView3 支持多尺寸图标
                _taskPaneView = _swApp.CreateTaskpaneView3(
                    iconPaths,
                    "SharkTools"
                );
                
                if (_taskPaneView != null)
                {
                    Log("TaskPane created successfully with custom icon");
                    
                    // 使用 ProgID 添加控件
                    object ctrl = _taskPaneView.AddControl(
                        "SharkTools.TaskPaneControl",
                        ""
                    );
                    Log($"TaskPane AddControl result: {ctrl != null}");
                    
                    if (ctrl != null)
                    {
                        _taskPaneControl = ctrl as SharkTaskPaneControl;
                    }
                }
                else
                {
                    Log("TaskPane creation failed - returned null");
                }
            }
            catch (Exception ex)
            {
                Log($"TaskPane error: {ex.Message}");
            }
        }
        
        private class TaskPaneProvider : SharkTaskPaneControl.ISldWorksProvider
        {
            private ISldWorks _swApp;
            public TaskPaneProvider(ISldWorks app) { _swApp = app; }
            public void ShowHello() { ExampleCommand.ShowHello(_swApp); }
            public void ShowMessage(string msg) 
            { 
                _swApp.SendMsgToUser2(msg, (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk); 
            }
        }

        public void Teardown()
        {
            try
            {
                if (_taskPaneView != null)
                {
                    _taskPaneView.DeleteView();
                    _taskPaneView = null;
                }
                if (_taskPaneControl != null)
                {
                    _taskPaneControl.Dispose();
                    _taskPaneControl = null;
                }
            }
            catch { }

            if (_cmdMgr != null)
            {
                _cmdMgr.RemoveCommandGroup(MainCommandGroupId);
            }
            _cmdGroup = null;
        }
    }
}
