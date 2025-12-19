using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using SharkTools.Cache;

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
        private AssemblyCacheManager _cacheManager;
        public int AddinCookie { get; set; }

        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            // 最早期日志，确认插件被调用
            ErrorHandler.LogInfo("========== ConnectToSW 开始 ==========");
            
            try
            {
                _swApp = (ISldWorks)ThisSW;
                AddinCookie = Cookie;
                
                ErrorHandler.LogInfo($"_swApp 获取成功, Cookie={Cookie}");
                
                // CRITICAL FIX: Use SetAddinCallbackInfo2 for better compatibility
                bool callbackRes = _swApp.SetAddinCallbackInfo2(0, this, AddinCookie);
                ErrorHandler.LogInfo($"SetAddinCallbackInfo2 result: {callbackRes} (Version: 2025-12-15)");

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
                    
                    // 订阅日志事件，转发到前端
                    ErrorHandler.OnLogMessage += (msg, type) => {
                        if (_electronServer != null)
                        {
                            string level = type == ErrorHandler.MessageType.Error ? "error" : 
                                          (type == ErrorHandler.MessageType.Warning ? "warning" : "info");
                            _electronServer.SendLog(msg, level);
                        }
                    };

                    _electronServer.Start();
                }
                catch (Exception ex)
                {
                    ErrorHandler.LogError("Connect", "ElectronServer init error", ex);
                }

                // Try to initialize UI immediately
                _sharkCmdMgr.Initialize();

                // 启动插件资源监控和弹窗拦截器
                try
                {
                    PerformanceOptimizer.Initialize(_swApp);
                    var optimizer = PerformanceOptimizer.Instance;
                    
                    // 启动弹窗拦截器（每500ms检查一次，拦截SW原生资源监控弹窗）
                    optimizer.StartPopupBlocker(500);
                    
                    // 启动插件资源监控（更智能的通知，每2分钟检查一次）
                    optimizer.StartResourceMonitor(2);
                    
                    ErrorHandler.LogInfo("已启动弹窗拦截器和资源监控");
                }
                catch (Exception ex)
                {
                    ErrorHandler.LogError("Connect", "资源监控初始化失败", ex);
                }

                // 初始化缓存管理器
                try
                {
                    string cachePath = System.IO.Path.Combine(
                        System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                        "SharkTools",
                        "Cache"
                    );
                    _cacheManager = new AssemblyCacheManager(cachePath);
                    
                    // 订阅文件打开事件
                    if (_swApp is SldWorks swAppConcrete)
                    {
                        swAppConcrete.FileOpenPostNotify += OnFileOpenPostNotify;
                        swAppConcrete.FileOpenPreNotify += OnFileOpenPreNotify;
                    }
                }
                catch (Exception ex)
                {
                    ErrorHandler.LogError("Connect", "Cache Manager init error", ex);
                }

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
                // 取消订阅事件
                if (_swApp is SldWorks swAppConcrete)
                {
                    swAppConcrete.FileOpenPostNotify -= OnFileOpenPostNotify;
                    swAppConcrete.FileOpenPreNotify -= OnFileOpenPreNotify;
                }

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
        /// 资源诊断回调 - 点击"资源诊断"按钮时调用
        /// 显示资源状态分析和弹窗拦截器控制
        /// </summary>
        [ComVisible(true)]
        public void DiagnoseResources()
        {
            try {
                System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: DiagnoseResources called!\r\n"
                );
            } catch {}

            try
            {
                // 初始化优化器
                PerformanceOptimizer.Initialize(_swApp);
                var optimizer = PerformanceOptimizer.Instance;
                
                // 获取当前状态
                var snapshot = optimizer.GetResourceSnapshot();
                int gdiLimit = PerformanceOptimizer.GetGDIProcessHandleQuota();
                var (vmTotal, vmAvailable, vmUsage) = PerformanceOptimizer.GetSystemVirtualMemory();
                
                // 获取弹窗拦截器状态
                string blockerStatus = optimizer.IsPopupBlockerEnabled 
                    ? $"✅ 已启用 (已拦截 {optimizer.PopupBlockedCount} 个)" 
                    : "❌ 未启用";
                
                // 资源状态评估
                double gdiPercent = (double)snapshot.GDIObjects / gdiLimit * 100;
                string gdiStatus = gdiPercent > 85 ? "⚠️ 危险" : gdiPercent > 70 ? "⚡ 注意" : "✓ 正常";
                string vmStatus = vmAvailable < 2048 ? "⚠️ 不足" : vmAvailable < 4096 ? "⚡ 偏低" : "✓ 充足";
                
                // 显示诊断对话框
                int choice = _swApp.SendMsgToUser2(
                    $"=== 资源诊断报告 ===\n\n" +
                    $"【内存】\n" +
                    $"  工作集: {snapshot.MemoryMB} MB\n" +
                    $"  提交内存: {snapshot.CommitMB} MB\n\n" +
                    $"【GDI对象】{gdiStatus}\n" +
                    $"  当前: {snapshot.GDIObjects} / {gdiLimit} ({gdiPercent:F0}%)\n\n" +
                    $"【虚拟内存】{vmStatus}\n" +
                    $"  可用: {vmAvailable} MB\n\n" +
                    $"【弹窗拦截器】{blockerStatus}\n\n" +
                    "请选择操作:\n" +
                    "【是】查看详细分析\n" +
                    "【否】切换弹窗拦截器\n" +
                    "【取消】关闭",
                    (int)swMessageBoxIcon_e.swMbInformation,
                    (int)swMessageBoxBtn_e.swMbYesNoCancel
                );
                
                if (choice == (int)swMessageBoxResult_e.swMbHitYes)
                {
                    // 显示详细分析报告
                    string analysis = optimizer.AnalyzeSWResourceIssues();
                    _swApp.SendMsgToUser2(
                        analysis,
                        (int)swMessageBoxIcon_e.swMbInformation,
                        (int)swMessageBoxBtn_e.swMbOk
                    );
                }
                else if (choice == (int)swMessageBoxResult_e.swMbHitNo)
                {
                    // 切换弹窗拦截器状态
                    bool newState = optimizer.TogglePopupBlocker();
                    _swApp.SendMsgToUser2(
                        newState 
                            ? "✅ 弹窗拦截器已启用！\n\n" +
                              "SharkTools 将自动拦截 SolidWorks 资源监控弹窗，\n" +
                              "并使用智能通知替代（只在资源真正严重不足时提醒）。"
                            : "❌ 弹窗拦截器已禁用\n\n" +
                              "SolidWorks 资源监控弹窗将正常显示。",
                        (int)swMessageBoxIcon_e.swMbInformation,
                        (int)swMessageBoxBtn_e.swMbOk
                    );
                }
            }
            catch (Exception ex)
            {
                _swApp.SendMsgToUser2(
                    $"诊断过程中发生错误: {ex.Message}",
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
        }

        /// <summary>
        /// 资源诊断按钮启用状态回调
        /// </summary>
        [ComVisible(true)]
        public int DiagnoseResourcesEnable()
        {
            return 1; // 始终启用
        }

        /// <summary>
        /// 清理优化回调 - 点击"清理优化"按钮时调用
        /// 提供多档位清理选择
        /// </summary>
        [ComVisible(true)]
        public void CleanupOptimize()
        {
            try {
                System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: CleanupOptimize called!\r\n"
                );
            } catch {}

            try
            {
                // 初始化优化器
                PerformanceOptimizer.Initialize(_swApp);
                var optimizer = PerformanceOptimizer.Instance;
                
                // 获取优化前的状态
                var beforeSnapshot = optimizer.GetResourceSnapshot();
                
                // 显示清理级别选择对话框
                int choice = _swApp.SendMsgToUser2(
                    $"当前资源状态:\n{beforeSnapshot.ToShortString()}\n\n" +
                    "请选择清理级别:\n\n" +
                    "【是】轻度清理\n" +
                    "    - 仅清理插件托管内存\n" +
                    "    - 不影响SW操作性能\n\n" +
                    "【否】中度清理 (推荐)\n" +
                    "    - 释放COM引用\n" +
                    "    - 不压缩工作集\n\n" +
                    "【取消】重度/极限清理...",
                    (int)swMessageBoxIcon_e.swMbQuestion,
                    (int)swMessageBoxBtn_e.swMbYesNoCancel
                );
                
                OptimizationLevel level;
                
                if (choice == (int)swMessageBoxResult_e.swMbHitYes)
                {
                    // 轻度清理
                    level = OptimizationLevel.Light;
                }
                else if (choice == (int)swMessageBoxResult_e.swMbHitNo)
                {
                    // 中度清理
                    level = OptimizationLevel.Medium;
                }
                else
                {
                    // 显示重度/极限选项
                    int advChoice = _swApp.SendMsgToUser2(
                        "请选择高级清理级别:\n\n" +
                        "【是】重度清理\n" +
                        "    - 释放COM引用\n" +
                        "    - 非活动装配体轻量化\n\n" +
                        "【否】极限清理 ⚠️\n" +
                        "    - 关闭所有隐藏文档\n" +
                        "    - 轻量化当前装配体\n" +
                        "    - 唯一真正释放SW内存的方式\n\n" +
                        "【取消】返回",
                        (int)swMessageBoxIcon_e.swMbQuestion,
                        (int)swMessageBoxBtn_e.swMbYesNoCancel
                    );
                    
                    if (advChoice == (int)swMessageBoxResult_e.swMbHitYes)
                    {
                        level = OptimizationLevel.Heavy;
                    }
                    else if (advChoice == (int)swMessageBoxResult_e.swMbHitNo)
                    {
                        // 极限清理需要确认
                        int confirmChoice = _swApp.SendMsgToUser2(
                            "⚠️ 极限清理警告\n\n" +
                            "此操作将：\n" +
                            "• 关闭所有隐藏/不可见的文档\n" +
                            "• 轻量化当前装配体的所有组件\n\n" +
                            "请确保已保存所有工作！\n\n" +
                            "确定要继续吗？",
                            (int)swMessageBoxIcon_e.swMbWarning,
                            (int)swMessageBoxBtn_e.swMbYesNo
                        );
                        
                        if (confirmChoice != (int)swMessageBoxResult_e.swMbHitYes)
                        {
                            return;
                        }
                        level = OptimizationLevel.Extreme;
                    }
                    else
                    {
                        return; // 取消
                    }
                }
                
                // 执行优化
                var afterSnapshot = optimizer.ExecuteOptimization(level, silent: false);
                
                // 显示优化结果
                long memoryFreed = beforeSnapshot.MemoryMB - afterSnapshot.MemoryMB;
                int gdiFreed = (int)beforeSnapshot.GDIObjects - (int)afterSnapshot.GDIObjects;
                
                // 询问是否需要额外清理（深度清理选项）
                int extraChoice = _swApp.SendMsgToUser2(
                    $"清理完成！ [{PerformanceOptimizer.GetLevelDescription(level)}]\n\n" +
                    $"清理前: {beforeSnapshot.ToShortString()}\n" +
                    $"清理后: {afterSnapshot.ToShortString()}\n" +
                    $"释放: 内存 {memoryFreed} MB | GDI {gdiFreed}\n\n" +
                    "是否需要额外深度清理？\n\n" +
                    "【是】强制GC回收\n" +
                    "    - 完整垃圾回收（可能卡顿1-2秒）\n\n" +
                    "【否】工作集压缩 ⚠️\n" +
                    "    - 释放物理内存到页面文件\n" +
                    "    - 会导致后续操作变慢\n\n" +
                    "【取消】不需要额外清理",
                    (int)swMessageBoxIcon_e.swMbQuestion,
                    (int)swMessageBoxBtn_e.swMbYesNoCancel
                );
                
                if (extraChoice == (int)swMessageBoxResult_e.swMbHitYes)
                {
                    // 强制GC回收
                    var beforeGC = optimizer.GetResourceSnapshot();
                    PerformanceOptimizer.ForceGarbageCollection();
                    System.Threading.Thread.Sleep(500); // 等待GC完成
                    var afterGC = optimizer.GetResourceSnapshot();
                    
                    _swApp.SendMsgToUser2(
                        $"强制GC回收完成！\n\n" +
                        $"回收前: {beforeGC.MemoryMB} MB\n" +
                        $"回收后: {afterGC.MemoryMB} MB\n" +
                        $"释放: {beforeGC.MemoryMB - afterGC.MemoryMB} MB",
                        (int)swMessageBoxIcon_e.swMbInformation,
                        (int)swMessageBoxBtn_e.swMbOk
                    );
                }
                else if (extraChoice == (int)swMessageBoxResult_e.swMbHitNo)
                {
                    // 工作集压缩（需要确认）
                    int confirmCompress = _swApp.SendMsgToUser2(
                        "⚠️ 工作集压缩警告\n\n" +
                        "此操作会将工作集中的数据移到页面文件，\n" +
                        "可以释放大量物理内存，但会导致：\n\n" +
                        "• 后续操作从磁盘读取数据，变慢\n" +
                        "• 装配体操作、特征编辑会卡顿\n" +
                        "• 只在内存真正不足时使用\n\n" +
                        "确定要压缩工作集吗？",
                        (int)swMessageBoxIcon_e.swMbWarning,
                        (int)swMessageBoxBtn_e.swMbYesNo
                    );
                    
                    if (confirmCompress == (int)swMessageBoxResult_e.swMbHitYes)
                    {
                        var beforeCompress = optimizer.GetResourceSnapshot();
                        PerformanceOptimizer.CompressWorkingSet();
                        System.Threading.Thread.Sleep(500);
                        var afterCompress = optimizer.GetResourceSnapshot();
                        
                        _swApp.SendMsgToUser2(
                            $"工作集压缩完成！\n\n" +
                            $"压缩前: {beforeCompress.MemoryMB} MB\n" +
                            $"压缩后: {afterCompress.MemoryMB} MB\n" +
                            $"释放: {beforeCompress.MemoryMB - afterCompress.MemoryMB} MB\n\n" +
                            "⚠️ 后续操作可能会变慢",
                            (int)swMessageBoxIcon_e.swMbInformation,
                            (int)swMessageBoxBtn_e.swMbOk
                        );
                    }
                }

                // 询问是否开启自动清理
                if (!optimizer.AutoOptimizeEnabled)
                {
                    int autoChoice = _swApp.SendMsgToUser2(
                        "是否开启自动清理？\n\n" +
                        "开启后将每5分钟自动执行轻度清理，\n" +
                        "当资源紧张时自动提升到重度清理。\n" +
                        "(自动清理不会弹窗打扰您)",
                        (int)swMessageBoxIcon_e.swMbQuestion,
                        (int)swMessageBoxBtn_e.swMbYesNo
                    );
                    
                    if (autoChoice == (int)swMessageBoxResult_e.swMbHitYes)
                    {
                        optimizer.StartAutoOptimize(5, OptimizationLevel.Light);
                        _swApp.SendMsgToUser2(
                            "自动清理已开启！\n每5分钟执行一次轻度清理。",
                            (int)swMessageBoxIcon_e.swMbInformation,
                            (int)swMessageBoxBtn_e.swMbOk
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _swApp.SendMsgToUser2(
                    $"清理过程中发生错误: {ex.Message}",
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
        }

        /// <summary>
        /// 清理优化按钮启用状态回调
        /// </summary>
        [ComVisible(true)]
        public int CleanupOptimizeEnable()
        {
            return 1; // 始终启用
        }

        /// <summary>
        /// 妙妙内存清理工具回调 - 专业内存清理选项
        /// </summary>
        [ComVisible(true)]
        public void MemoryCleanupTool()
        {
            try {
                System.IO.File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                    $"{DateTime.Now}: MemoryCleanupTool called!\r\n"
                );
            } catch {}

            try
            {
                // 初始化优化器
                PerformanceOptimizer.Initialize(_swApp);
                var optimizer = PerformanceOptimizer.Instance;
                
                // 获取当前状态
                var beforeSnapshot = optimizer.GetResourceSnapshot();
                
                // 显示主菜单
                int choice = _swApp.SendMsgToUser2(
                    "🎯 妙妙内存清理工具\n\n" +
                    $"当前状态:\n{beforeSnapshot.ToShortString()}\n\n" +
                    "请选择清理方式:\n\n" +
                    "【是】强制GC回收 ⚡\n" +
                    "    - 完整垃圾回收（可能卡顿1-2秒）\n" +
                    "    - 释放托管内存和COM引用\n\n" +
                    "【否】工作集压缩 ⚠️\n" +
                    "    - 释放物理内存到页面文件\n" +
                    "    - 会导致后续操作变慢\n\n" +
                    "【取消】深度清理组合拳 💪\n" +
                    "    - GC回收 + 工作集压缩",
                    (int)swMessageBoxIcon_e.swMbQuestion,
                    (int)swMessageBoxBtn_e.swMbYesNoCancel
                );
                
                if (choice == (int)swMessageBoxResult_e.swMbHitYes)
                {
                    // 强制GC回收
                    PerformanceOptimizer.ForceGarbageCollection();
                    System.Threading.Thread.Sleep(500);
                    var afterGC = optimizer.GetResourceSnapshot();
                    
                    long memoryFreed = beforeSnapshot.MemoryMB - afterGC.MemoryMB;
                    int gdiFreed = (int)beforeSnapshot.GDIObjects - (int)afterGC.GDIObjects;
                    
                    _swApp.SendMsgToUser2(
                        "✅ 强制GC回收完成！\n\n" +
                        $"清理前: {beforeSnapshot.ToShortString()}\n" +
                        $"清理后: {afterGC.ToShortString()}\n\n" +
                        $"释放内存: {memoryFreed} MB\n" +
                        $"释放GDI: {gdiFreed}",
                        (int)swMessageBoxIcon_e.swMbInformation,
                        (int)swMessageBoxBtn_e.swMbOk
                    );
                }
                else if (choice == (int)swMessageBoxResult_e.swMbHitNo)
                {
                    // 工作集压缩（需要确认）
                    int confirmCompress = _swApp.SendMsgToUser2(
                        "⚠️ 工作集压缩警告\n\n" +
                        "此操作会将工作集中的数据移到页面文件，\n" +
                        "可以释放大量物理内存，但会导致：\n\n" +
                        "• 后续操作从磁盘读取数据，变慢\n" +
                        "• 装配体操作、特征编辑会卡顿\n" +
                        "• 旋转视图、缩放等会有延迟\n" +
                        "• 只在物理内存真正不足时使用\n\n" +
                        "确定要压缩工作集吗？",
                        (int)swMessageBoxIcon_e.swMbWarning,
                        (int)swMessageBoxBtn_e.swMbYesNo
                    );
                    
                    if (confirmCompress == (int)swMessageBoxResult_e.swMbHitYes)
                    {
                        PerformanceOptimizer.CompressWorkingSet();
                        System.Threading.Thread.Sleep(500);
                        var afterCompress = optimizer.GetResourceSnapshot();
                        
                        long memoryFreed = beforeSnapshot.MemoryMB - afterCompress.MemoryMB;
                        
                        _swApp.SendMsgToUser2(
                            "✅ 工作集压缩完成！\n\n" +
                            $"压缩前: {beforeSnapshot.MemoryMB} MB\n" +
                            $"压缩后: {afterCompress.MemoryMB} MB\n" +
                            $"释放: {memoryFreed} MB\n\n" +
                            "⚠️ 后续操作可能会变慢，\n" +
                            "重启 SolidWorks 可恢复正常速度。",
                            (int)swMessageBoxIcon_e.swMbInformation,
                            (int)swMessageBoxBtn_e.swMbOk
                        );
                    }
                }
                else if (choice == (int)swMessageBoxResult_e.swMbHitCancel)
                {
                    // 深度清理组合拳
                    int confirmDeep = _swApp.SendMsgToUser2(
                        "💪 深度清理组合拳\n\n" +
                        "将依次执行：\n" +
                        "1. 强制GC回收（清理托管内存）\n" +
                        "2. 工作集压缩（释放物理内存）\n\n" +
                        "⚠️ 警告：\n" +
                        "• 会导致1-2秒卡顿\n" +
                        "• 后续操作会明显变慢\n" +
                        "• 只在内存严重不足时使用\n\n" +
                        "确定要执行深度清理吗？",
                        (int)swMessageBoxIcon_e.swMbWarning,
                        (int)swMessageBoxBtn_e.swMbYesNo
                    );
                    
                    if (confirmDeep == (int)swMessageBoxResult_e.swMbHitYes)
                    {
                        // 执行深度清理
                        PerformanceOptimizer.DeepCleanup();
                        System.Threading.Thread.Sleep(1000);
                        var afterDeep = optimizer.GetResourceSnapshot();
                        
                        long memoryFreed = beforeSnapshot.MemoryMB - afterDeep.MemoryMB;
                        int gdiFreed = (int)beforeSnapshot.GDIObjects - (int)afterDeep.GDIObjects;
                        
                        _swApp.SendMsgToUser2(
                            "✅ 深度清理完成！\n\n" +
                            $"清理前: {beforeSnapshot.ToShortString()}\n" +
                            $"清理后: {afterDeep.ToShortString()}\n\n" +
                            $"释放内存: {memoryFreed} MB\n" +
                            $"释放GDI: {gdiFreed}\n\n" +
                            "💡 提示：重启 SolidWorks 可恢复最佳性能。",
                            (int)swMessageBoxIcon_e.swMbInformation,
                            (int)swMessageBoxBtn_e.swMbOk
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _swApp.SendMsgToUser2(
                    $"妙妙内存清理工具执行出错:\n{ex.Message}",
                    (int)swMessageBoxIcon_e.swMbWarning,
                    (int)swMessageBoxBtn_e.swMbOk
                );
            }
        }

        /// <summary>
        /// 妙妙内存清理工具按钮启用状态回调
        /// </summary>
        [ComVisible(true)]
        public int MemoryCleanupToolEnable()
        {
            return 1; // 始终启用
        }

        /// <summary>
        /// 快速设置材料和属性回调
        /// </summary>
        [ComVisible(true)]
        public void QuickMaterialProperties()
        {
            try
            {
                IModelDoc2 doc = _swApp.ActiveDoc as IModelDoc2;
                if (doc == null || doc.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    _swApp.SendMsgToUser2("请先打开一个零件文档。", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                    return;
                }

                PartDoc part = (PartDoc)doc;
                string matDb = "";
                string matName = part.GetMaterialPropertyName2("", out matDb);

                // 获取自定义属性
                CustomPropertyManager swPropMgr = doc.Extension.get_CustomPropertyManager("");
                string[] propNames = (string[])swPropMgr.GetNames(); // 显式转换为 string[]
                var props = new System.Collections.Generic.Dictionary<string, string>();
                if (propNames != null)
                {
                    for (int i = 0; i < propNames.Length; i++)
                    {
                        string name = propNames[i];
                        string val = "";
                        string resolvedVal = "";
                        swPropMgr.Get4(name, false, out val, out resolvedVal);
                        props[name] = val;
                    }
                }

                // 获取所有可用材料数据库（注：ISldWorks 接口中没有 GetMaterialDatabaseNames 方法，暂时使用空数组）
                string[] dbNames = new string[0];
                
                var payload = new
                {
                    currentMaterial = new { name = matName, database = matDb },
                    properties = props,
                    databases = dbNames
                };

                // 启动 Electron 并发送数据
                LaunchElectronApp(); 
                
                if (_electronServer != null)
                {
                    _electronServer.ShowWindow();
                    _electronServer.Send("quick-material-open", payload);
                }
            }
            catch (Exception ex)
            {
                _swApp.SendMsgToUser2($"Error: {ex.Message}", (int)swMessageBoxIcon_e.swMbWarning, (int)swMessageBoxBtn_e.swMbOk);
            }
        }

        [ComVisible(true)]
        public int QuickMaterialPropertiesEnable()
        {
            // Only enable for Parts
            IModelDoc2 doc = _swApp.ActiveDoc as IModelDoc2;
            if (doc != null && doc.GetType() == (int)swDocumentTypes_e.swDocPART)
            {
                return 1;
            }
            return 0;
        }

        public int OnFileOpenPostNotify(string FileName)
        {
            try
            {
                if (FileName.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
                {
                    // 异步更新缓存
                    Task.Run(async () =>
                    {
                        try
                        {
                            var fileInfo = new System.IO.FileInfo(FileName);
                            // 创建缓存数据 (实际项目中应遍历装配体结构)
                            var data = new AssemblyCacheData();
                            
                            // 简单记录一下，表示缓存已创建
                            data.FilePath = FileName;
                            
                            await _cacheManager.SetAssemblyDataAsync(FileName, fileInfo.LastWriteTime, data);
                            
                            try {
                                System.IO.File.AppendAllText(
                                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                                    $"{DateTime.Now}: Cache updated for {System.IO.Path.GetFileName(FileName)}\r\n"
                                );
                            } catch {}
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Cache update error: {ex.Message}");
                        }
                    });
                }
            }
            catch {}
            return 0;
        }

        public int OnFileOpenPreNotify(string FileName)
        {
            try
            {
                if (FileName.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase))
                {
                    var fileInfo = new System.IO.FileInfo(FileName);
                    // 检查缓存是否存在
                    var task = _cacheManager.GetAssemblyDataAsync(FileName, fileInfo.LastWriteTime);
                    task.Wait(200); // 最多等待200ms，避免阻塞
                    
                    if (task.IsCompleted && task.Result != null)
                    {
                        // 缓存命中
                        try {
                            System.IO.File.AppendAllText(
                                @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                                $"{DateTime.Now}: Cache HIT for {System.IO.Path.GetFileName(FileName)}\r\n"
                            );
                        } catch {}
                        
                        // 可以在这里通知用户
                        // _swApp.SendMsgToUser2("已启用缓存加速加载", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
                    }
                }
            }
            catch {}
            return 0;
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
