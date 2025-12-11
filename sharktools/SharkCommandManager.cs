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
        private HistoryTracker _historyTracker;
        private string _currentDocPath; // 当前追踪的文档路径
        
        private const int MainCommandGroupId = 2001;
        private const int HelloCommandIndex = 0;
        private const int GitHubLoginCommandIndex = 1;  // GitHub 登录命令索引
        private const int LaunchElectronCommandIndex = 2;  // 启动 Electron 应用命令索引
        private const int DiagnoseCommandIndex = 3;        // 资源诊断命令索引
        private const int CleanupCommandIndex = 4;         // 清理优化命令索引
        private const int MemoryToolCommandIndex = 5;      // 妙妙内存清理工具命令索引
        private const string TabName = "SharkTools";

        /// <summary>
        /// 公开历史追踪器以便其他类访问
        /// </summary>
        public HistoryTracker HistoryTracker => _historyTracker;

        /// <summary>
        /// 公开 TaskPane 控件以便进行 UI 线程调用
        /// </summary>
        public SharkTaskPaneControl TaskPaneControl => _taskPaneControl;

        public SharkCommandManager(ISldWorks app, int cookie)
        {
            _swApp = app;
            _cookie = cookie;
            _cmdMgr = _swApp.GetCommandManager(cookie);
        }

        public void Initialize()
        {
            Log("Initialize started");
            
            // 0. 创建 TaskPane（最可靠的方式）
            try 
            {
                Log("准备创建 TaskPane");
                CreateTaskPane();
                Log("TaskPane 创建完成");
            }
            catch (Exception ex)
            {
                Log($"CreateTaskPane error: {ex.Message}");
            }

            // 0.5 初始化历史追踪器（独立 try-catch，确保一定执行）
            Log("准备初始化历史追踪器");
            InitializeHistoryTracker();
            Log("历史追踪器初始化完成");

            try 
            {
                Log("准备创建图标文件和命令组");
                // 1. Create icon files
                string assemblyDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string bmpPath = System.IO.Path.Combine(assemblyDir, "toolbar.bmp");
                string[] iconPaths = CreateIconFiles(assemblyDir);
                CreateDummyBitmap(bmpPath);

                // 2. 创建 CommandGroup
                // 先尝试删除旧的命令组（如果存在）
                // 【激进清理】先删除所有 CommandTab（SolidWorks可能缓存了5个按钮的布局）
                try
                {
                    int[] docTypes = new int[] {
                        (int)swDocumentTypes_e.swDocPART,
                        (int)swDocumentTypes_e.swDocASSEMBLY,
                        (int)swDocumentTypes_e.swDocDRAWING
                    };
                    
                    foreach (int docType in docTypes)
                    {
                        CommandTab existingTab = _cmdMgr.GetCommandTab(docType, "SharkTools");
                        if (existingTab != null)
                        {
                            bool removed = _cmdMgr.RemoveCommandTab(existingTab);
                            Log($"【预清理】删除标签页 docType {docType}: {removed}");
                        }
                    }
                }
                catch (Exception tabEx)
                {
                    Log($"预清理标签页失败: {tabEx.Message}");
                }
                
                // 然后删除旧的命令组
                try
                {
                    ICommandGroup oldGroup = _cmdMgr.GetCommandGroup(MainCommandGroupId);
                    if (oldGroup != null)
                    {
                        bool removed = _cmdMgr.RemoveCommandGroup(MainCommandGroupId);
                        Log($"删除旧命令组: {removed}");
                        
                        // 等待确保删除生效
                        System.Threading.Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    Log($"删除旧命令组失败: {ex.Message}");
                }
                
                int errors = 0;
                // ignorePrev = true 强制重新创建命令组（当命令数量改变时必须）
                bool ignorePrev = true;
                
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

                    // 添加命令 3：启动 SharkTools 应用
                    int cmdIndex3 = _cmdGroup.AddCommandItem2(
                        "启动工具箱",                     // Name（中文）
                        -1,                              // Position
                        "启动 SharkTools 独立应用",       // Hint
                        "启动工具箱",                     // Tooltip
                        0,                               // Image index
                        "LaunchElectronApp",             // Callback
                        "",                              // Enable method
                        LaunchElectronCommandIndex,      // User command ID
                        (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem
                    );
                    Log($"AddCommandItem2 (启动工具箱) result: {cmdIndex3}");

                    // 添加命令 4：资源诊断
                    int cmdIndex4 = _cmdGroup.AddCommandItem2(
                        "资源诊断",                       // Name（中文）
                        -1,                              // Position
                        "分析资源使用情况和问题",         // Hint
                        "资源诊断",                       // Tooltip
                        0,                               // Image index
                        "DiagnoseResources",             // Callback
                        "",                              // Enable method
                        DiagnoseCommandIndex,            // User command ID
                        (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem
                    );
                    Log($"AddCommandItem2 (资源诊断) result: {cmdIndex4}");

                    // 添加命令 5：清理优化
                    int cmdIndex5 = _cmdGroup.AddCommandItem2(
                        "清理优化",                       // Name（中文）
                        -1,                              // Position
                        "选择清理级别优化资源",           // Hint
                        "清理优化",                       // Tooltip
                        0,                               // Image index
                        "CleanupOptimize",               // Callback
                        "",                              // Enable method
                        CleanupCommandIndex,             // User command ID
                        (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem
                    );
                    Log($"AddCommandItem2 (清理优化) result: {cmdIndex5}");

                    // 添加命令 6：妙妙内存清理工具
                    int cmdIndex6 = _cmdGroup.AddCommandItem2(
                        "妙妙内存清理",                   // Name（中文）
                        -1,                              // Position
                        "强制GC和工作集压缩",            // Hint
                        "妙妙内存清理工具",               // Tooltip
                        0,                               // Image index
                        "MemoryCleanupTool",             // Callback
                        "",                              // Enable method
                        MemoryToolCommandIndex,          // User command ID
                        (int)swCommandItemType_e.swMenuItem | (int)swCommandItemType_e.swToolbarItem
                    );
                    Log($"AddCommandItem2 (妙妙内存清理) result: {cmdIndex6}");

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
                    // 创建全新的标签页（已在Initialize开始时预清理）
                    CommandTab cmdTab = _cmdMgr.AddCommandTab(docType, "SharkTools");
                    Log($"AddCommandTab for docType {docType} result: {cmdTab != null}");

                    if (cmdTab != null)
                    {
                        // 获取标签页的工具箱
                        CommandTabBox cmdBox = cmdTab.AddCommandTabBox();
                        Log($"AddCommandTabBox for docType {docType} result: {cmdBox != null}");

                        if (cmdBox != null)
                        {
                            // 获取六个命令的 ID
                            int cmdId1 = _cmdGroup.CommandID[HelloCommandIndex];
                            int cmdId2 = _cmdGroup.CommandID[GitHubLoginCommandIndex];
                            int cmdId3 = _cmdGroup.CommandID[LaunchElectronCommandIndex];
                            int cmdId4 = _cmdGroup.CommandID[DiagnoseCommandIndex];
                            int cmdId5 = _cmdGroup.CommandID[CleanupCommandIndex];
                            int cmdId6 = _cmdGroup.CommandID[MemoryToolCommandIndex];
                            
                            // 【诊断日志】验证所有命令ID是否有效
                            Log($"=== 命令 ID 诊断 ===");
                            Log($"[0] 打招呼 ID={cmdId1}");
                            Log($"[1] 登录GitHub ID={cmdId2}");
                            Log($"[2] 启动工具箱 ID={cmdId3}");
                            Log($"[3] 资源诊断 ID={cmdId4}");
                            Log($"[4] 清理优化 ID={cmdId5}");
                            Log($"[5] 妙妙内存清理 ID={cmdId6}");

                            // 添加六个命令到工具箱
                            int[] cmdIds = new int[] { cmdId1, cmdId2, cmdId3, cmdId4, cmdId5, cmdId6 };
                            int[] textTypes = new int[] { 
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow,
                                (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow
                            };
                            
                            bool addResult = cmdBox.AddCommands(cmdIds, textTypes);
                            Log($"添加 6 个命令到标签页 docType {docType}: {addResult}");
                            
                            if (!addResult)
                            {
                                Log($"【警告】命令添加失败！docType={docType}");
                            }
                        }
                        else
                        {
                            Log($"【错误】无法创建 CommandTabBox，docType={docType}");
                        }
                    }
                    else
                    {
                        Log($"【错误】无法创建 CommandTab，docType={docType}");
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
                // 设置 Provider - 使用新的 WebView2 控件
                SharkWebView2TaskPane.SetProvider(new WebView2TaskPaneProvider(_swApp));
                
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
                    
                    // 使用新的 WebView2 控件 ProgID
                    object ctrl = _taskPaneView.AddControl(
                        "SharkTools.WebView2TaskPane",
                        ""
                    );
                    Log($"TaskPane AddControl result: {ctrl != null}");
                    
                    if (ctrl != null)
                    {
                        Log("WebView2TaskPane 控件加载成功");
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
        
        // WebView2 Provider
        private class WebView2TaskPaneProvider : SharkWebView2TaskPane.ISldWorksProvider
        {
            private ISldWorks _swApp;
            public WebView2TaskPaneProvider(ISldWorks app) { _swApp = app; }
            public void ShowHello() { ExampleCommand.ShowHello(_swApp); }
            public void ShowMessage(string msg) 
            { 
                _swApp.SendMsgToUser2(msg, (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk); 
            }
            public IModelDoc2 GetActiveDocument()
            {
                return (IModelDoc2)_swApp.ActiveDoc;
            }
        }

        public void Teardown()
        {
            try
            {
                Log("Teardown started");
                
                // 停止历史追踪
                if (_historyTracker != null)
                {
                    _historyTracker.StopTracking();
                    _historyTracker = null;
                }
                
                // 清理 CommandTabs
                RemoveCommandTabs();
                
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

        /// <summary>
        /// 清理所有 CommandTabs
        /// </summary>
        private void RemoveCommandTabs()
        {
            try
            {
                int[] docTypes = new int[] {
                    (int)swDocumentTypes_e.swDocPART,
                    (int)swDocumentTypes_e.swDocASSEMBLY,
                    (int)swDocumentTypes_e.swDocDRAWING
                };

                foreach (int docType in docTypes)
                {
                    CommandTab cmdTab = _cmdMgr.GetCommandTab(docType, "SharkTools");
                    if (cmdTab != null)
                    {
                        bool removed = _cmdMgr.RemoveCommandTab(cmdTab);
                        Log($"RemoveCommandTab for docType {docType}: {removed}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"RemoveCommandTabs error: {ex.Message}");
            }
        }

        /// <summary>
        /// 初始化历史记录追踪器
        /// </summary>
        private void InitializeHistoryTracker()
        {
            try
            {
                Log("初始化历史追踪器");
                
                // 创建追踪器实例
                _historyTracker = new HistoryTracker(_swApp);
                
                // 创建定时器监控文档切换
                var docSwitchTimer = new System.Windows.Forms.Timer();
                docSwitchTimer.Interval = 1000; // 每秒检查一次
                docSwitchTimer.Tick += (sender, e) => CheckAndSwitchDocument();
                docSwitchTimer.Start();
                
                Log("文档切换监控已启动");
                
                // 如果当前已有活动文档，立即开始追踪
                CheckAndSwitchDocument();
            }
            catch (Exception ex)
            {
                Log($"初始化历史追踪器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查并切换文档追踪
        /// </summary>
        private void CheckAndSwitchDocument()
        {
            try
            {
                IModelDoc2 activeDoc = (IModelDoc2)_swApp.ActiveDoc;
                
                if (activeDoc == null)
                {
                    // 没有活动文档，停止追踪
                    if (!string.IsNullOrEmpty(_currentDocPath))
                    {
                        Log("没有活动文档，停止追踪");
                        if (_historyTracker != null)
                        {
                            _historyTracker.StopTracking();
                        }
                        _currentDocPath = null;
                    }
                    return;
                }

                string docPath = activeDoc.GetPathName();
                
                // 文档未保存
                if (string.IsNullOrEmpty(docPath))
                {
                    if (!string.IsNullOrEmpty(_currentDocPath))
                    {
                        Log("文档未保存，停止追踪");
                        if (_historyTracker != null)
                        {
                            _historyTracker.StopTracking();
                        }
                        _currentDocPath = null;
                    }
                    return;
                }

                // 检查是否切换了文档
                if (docPath != _currentDocPath)
                {
                    Log($"检测到文档切换: {_currentDocPath} -> {docPath}");
                    
                    // 停止旧文档的追踪
                    if (_historyTracker != null && !string.IsNullOrEmpty(_currentDocPath))
                    {
                        _historyTracker.StopTracking();
                    }
                    
                    // 开始新文档的追踪
                    if (_historyTracker != null)
                    {
                        _historyTracker.StartTracking(activeDoc);
                        _currentDocPath = docPath;
                        Log($"已切换到新文档追踪: {docPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"CheckAndSwitchDocument 错误: {ex.Message}");
            }
        }
    }
}
