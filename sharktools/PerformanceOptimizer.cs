using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Management;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SharkTools
{
    /// <summary>
    /// 优化级别枚举
    /// </summary>
    public enum OptimizationLevel
    {
        /// <summary>轻度优化：仅GC回收托管内存</summary>
        Light = 1,
        /// <summary>中度优化：GC + 清理工作集</summary>
        Medium = 2,
        /// <summary>重度优化：GC + 工作集 + SW轻量化</summary>
        Heavy = 3,
        /// <summary>极限优化：全部优化 + 关闭隐藏文档</summary>
        Extreme = 4
    }

    /// <summary>
    /// 资源状态快照
    /// </summary>
    public class ResourceSnapshot
    {
        public long MemoryMB { get; set; }       // 工作集（物理内存）
        public long CommitMB { get; set; }       // 提交内存（虚拟内存）
        public uint GDIObjects { get; set; }
        public uint UserObjects { get; set; }
        public uint HandleCount { get; set; }
        public DateTime Timestamp { get; set; }
        
        public override string ToString()
        {
            return $"工作集: {MemoryMB} MB | 提交: {CommitMB} MB | GDI: {GDIObjects} | User: {UserObjects}";
        }
        
        public string ToShortString()
        {
            return $"工作集: {MemoryMB} MB | 提交: {CommitMB} MB | GDI: {GDIObjects}";
        }
    }

    /// <summary>
    /// 资源泄漏分析结果
    /// </summary>
    public class LeakAnalysisResult
    {
        public bool HasMemoryLeak { get; set; }
        public bool HasGDILeak { get; set; }
        public bool HasHandleLeak { get; set; }
        public double MemoryGrowthRate { get; set; } // MB/分钟
        public double GDIGrowthRate { get; set; }    // 对象/分钟
        public double HandleGrowthRate { get; set; } // 句柄/分钟
        public string Summary { get; set; }
    }

    /// <summary>
    /// 性能优化器 - 增强版
    /// 支持多档位优化、自动优化、资源泄漏检测
    /// </summary>
    public class PerformanceOptimizer : IDisposable
    {
        #region P/Invoke 声明
        
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessHandleCount(IntPtr hProcess, out uint pdwHandleCount);

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr hProcess, uint uiFlags);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        // 窗口操作相关 P/Invoke
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        private const uint GR_GDIOBJECTS = 0;
        private const uint GR_USEROBJECTS = 1;
        private const uint GR_GDIOBJECTS_PEAK = 2;
        private const uint GR_USEROBJECTS_PEAK = 4;

        // GDI 和 User 对象的系统限制
        private const uint GDI_OBJECTS_LIMIT_DEFAULT = 10000;
        private const uint GDI_OBJECTS_WARNING_THRESHOLD = 8000;
        private const uint USER_OBJECTS_LIMIT_DEFAULT = 10000;
        private const uint USER_OBJECTS_WARNING_THRESHOLD = 8000;

        #endregion

        #region 单例和静态成员

        private static PerformanceOptimizer _instance;
        private static readonly object _instanceLock = new object();
        private static readonly object _optimizeLock = new object();

        /// <summary>
        /// 获取或创建单例实例
        /// </summary>
        public static PerformanceOptimizer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new PerformanceOptimizer(null);
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 初始化单例（带SolidWorks引用）
        /// </summary>
        public static void Initialize(ISldWorks swApp)
        {
            lock (_instanceLock)
            {
                if (_instance != null)
                {
                    _instance.Dispose();
                }
                _instance = new PerformanceOptimizer(swApp);
            }
        }

        #endregion

        #region 字段和属性

        private readonly ISldWorks _swApp;
        private System.Threading.Timer _autoOptimizeTimer;
        private System.Threading.Timer _resourceMonitorTimer;  // 资源监控定时器
        private System.Threading.Timer _popupBlockerTimer;     // 弹窗拦截定时器
        private bool _autoOptimizeEnabled = false;
        private int _autoOptimizeIntervalMs = 300000; // 默认5分钟
        private OptimizationLevel _autoOptimizeLevel = OptimizationLevel.Medium;
        private ResourceSnapshot _baselineSnapshot;
        private ResourceSnapshot _lastSnapshot;
        private DateTime _startTime;
        private bool _disposed = false;
        private DateTime _lastNotificationTime = DateTime.MinValue;  // 上次通知时间
        private const int NOTIFICATION_COOLDOWN_MINUTES = 5;  // 通知冷却时间（分钟）
        private bool _popupBlockerEnabled = false;  // 弹窗拦截器是否启用
        private int _popupBlockedCount = 0;  // 已拦截弹窗数量

        // swUserNotification 枚举值
        private const int swUserNotificationPosition_BottomRight = 4;
        private const int swUserNotificationSeverity_Warning = 2;
        private const int swUserNotificationSeverity_Error = 3;
        private const int swUserNotificationResponseType_None = 0;
        private const int swUserNotificationResponseType_Button = 1;

        /// <summary>
        /// 自动优化是否启用
        /// </summary>
        public bool AutoOptimizeEnabled => _autoOptimizeEnabled;

        /// <summary>
        /// 自动优化间隔（毫秒）
        /// </summary>
        public int AutoOptimizeIntervalMs => _autoOptimizeIntervalMs;

        /// <summary>
        /// 自动优化级别
        /// </summary>
        public OptimizationLevel AutoOptimizeLevel => _autoOptimizeLevel;

        /// <summary>
        /// 基线快照
        /// </summary>
        public ResourceSnapshot BaselineSnapshot => _baselineSnapshot;

        #endregion

        #region 构造和销毁

        public PerformanceOptimizer(ISldWorks swApp)
        {
            _swApp = swApp;
            _startTime = DateTime.Now;
            _baselineSnapshot = GetResourceSnapshot();
            _lastSnapshot = _baselineSnapshot;
            Log("PerformanceOptimizer 已初始化");
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopAutoOptimize();
                    StopResourceMonitor();
                    StopPopupBlocker();  // 停止弹窗拦截器
                }
                _disposed = true;
                Log("PerformanceOptimizer 已销毁");
            }
        }

        ~PerformanceOptimizer()
        {
            Dispose(false);
        }

        #endregion

        #region 资源监控

        /// <summary>
        /// 获取当前资源快照
        /// </summary>
        public ResourceSnapshot GetResourceSnapshot()
        {
            Process currentProcess = Process.GetCurrentProcess();
            currentProcess.Refresh();
            
            uint handleCount = 0;
            GetProcessHandleCount(currentProcess.Handle, out handleCount);

            return new ResourceSnapshot
            {
                MemoryMB = currentProcess.WorkingSet64 / 1024 / 1024,
                CommitMB = currentProcess.PrivateMemorySize64 / 1024 / 1024, // 提交内存
                GDIObjects = GetGuiResources(currentProcess.Handle, GR_GDIOBJECTS),
                UserObjects = GetGuiResources(currentProcess.Handle, GR_USEROBJECTS),
                HandleCount = handleCount,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 获取系统虚拟内存状态
        /// </summary>
        public static (long TotalMB, long AvailableMB, int UsagePercent) GetSystemVirtualMemory()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT TotalVirtualMemorySize, FreeVirtualMemory FROM Win32_OperatingSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        // WMI 返回的单位是 KB
                        long totalVirtual = Convert.ToInt64(obj["TotalVirtualMemorySize"]) / 1024;
                        long freeVirtual = Convert.ToInt64(obj["FreeVirtualMemory"]) / 1024;
                        int usagePercent = (int)((totalVirtual - freeVirtual) * 100 / totalVirtual);
                        return (totalVirtual, freeVirtual, usagePercent);
                    }
                }
            }
            catch { }
            return (0, 0, 0);
        }

        /// <summary>
        /// 检查是否存在虚拟内存不足警告
        /// </summary>
        public static (bool IsLow, string Message) CheckVirtualMemory()
        {
            var (total, available, usage) = GetSystemVirtualMemory();
            if (total == 0) return (false, "无法获取虚拟内存信息");
            
            // 可用虚拟内存低于 2GB 或使用率超过 85% 时警告
            if (available < 2048 || usage > 85)
            {
                return (true, $"⚠️ 虚拟内存紧张！可用: {available} MB ({100 - usage}%)\n" +
                    $"这是 SolidWorks 显示\"资源不足\"的主要原因。\n" +
                    $"建议：关闭其他程序或重启 SolidWorks。");
            }
            
            return (false, $"虚拟内存正常: {available} MB 可用 ({100 - usage}%)");
        }

        /// <summary>
        /// 获取资源状态字符串（兼容旧API）
        /// </summary>
        public static string GetResourceStatus()
        {
            return Instance.GetResourceSnapshot().ToShortString();
        }

        /// <summary>
        /// 获取峰值资源使用
        /// </summary>
        public (uint GDIPeak, uint UserPeak) GetPeakResources()
        {
            Process currentProcess = Process.GetCurrentProcess();
            uint gdiPeak = GetGuiResources(currentProcess.Handle, GR_GDIOBJECTS_PEAK);
            uint userPeak = GetGuiResources(currentProcess.Handle, GR_USEROBJECTS_PEAK);
            return (gdiPeak, userPeak);
        }

        /// <summary>
        /// 分析资源泄漏
        /// </summary>
        public LeakAnalysisResult AnalyzeLeaks()
        {
            var current = GetResourceSnapshot();
            double elapsedMinutes = (current.Timestamp - _baselineSnapshot.Timestamp).TotalMinutes;
            
            if (elapsedMinutes < 1)
            {
                return new LeakAnalysisResult
                {
                    Summary = "运行时间不足1分钟，无法准确分析泄漏情况"
                };
            }

            double memoryGrowth = (current.MemoryMB - _baselineSnapshot.MemoryMB) / elapsedMinutes;
            double gdiGrowth = (current.GDIObjects - _baselineSnapshot.GDIObjects) / elapsedMinutes;
            double handleGrowth = (current.HandleCount - _baselineSnapshot.HandleCount) / elapsedMinutes;

            var result = new LeakAnalysisResult
            {
                MemoryGrowthRate = Math.Round(memoryGrowth, 2),
                GDIGrowthRate = Math.Round(gdiGrowth, 2),
                HandleGrowthRate = Math.Round(handleGrowth, 2),
                HasMemoryLeak = memoryGrowth > 10, // 超过10MB/分钟视为泄漏
                HasGDILeak = gdiGrowth > 5,        // 超过5个/分钟视为泄漏
                HasHandleLeak = handleGrowth > 10  // 超过10个/分钟视为泄漏
            };

            // 生成摘要
            var summary = new System.Text.StringBuilder();
            summary.AppendLine($"=== 资源泄漏分析报告 ===");
            summary.AppendLine($"运行时长: {elapsedMinutes:F1} 分钟");
            summary.AppendLine();
            summary.AppendLine($"【内存】");
            summary.AppendLine($"  基准: {_baselineSnapshot.MemoryMB} MB → 当前: {current.MemoryMB} MB");
            summary.AppendLine($"  增长率: {result.MemoryGrowthRate} MB/分钟 {(result.HasMemoryLeak ? "⚠️ 可能存在泄漏" : "✓ 正常")}");
            summary.AppendLine();
            summary.AppendLine($"【GDI对象】");
            summary.AppendLine($"  基准: {_baselineSnapshot.GDIObjects} → 当前: {current.GDIObjects}");
            summary.AppendLine($"  增长率: {result.GDIGrowthRate}/分钟 {(result.HasGDILeak ? "⚠️ 可能存在泄漏" : "✓ 正常")}");
            summary.AppendLine();
            summary.AppendLine($"【句柄】");
            summary.AppendLine($"  基准: {_baselineSnapshot.HandleCount} → 当前: {current.HandleCount}");
            summary.AppendLine($"  增长率: {result.HandleGrowthRate}/分钟 {(result.HasHandleLeak ? "⚠️ 可能存在泄漏" : "✓ 正常")}");

            result.Summary = summary.ToString();
            return result;
        }

        /// <summary>
        /// 检查是否接近资源限制
        /// </summary>
        public (bool IsWarning, string Message) CheckResourceLimits()
        {
            var snapshot = GetResourceSnapshot();
            var (gdiPeak, userPeak) = GetPeakResources();
            int gdiLimit = GetGDIProcessHandleQuota();
            
            var warnings = new System.Collections.Generic.List<string>();
            
            if (snapshot.GDIObjects > GDI_OBJECTS_WARNING_THRESHOLD || snapshot.GDIObjects > gdiLimit * 0.8)
            {
                warnings.Add($"⚠️ GDI对象数量 ({snapshot.GDIObjects}) 接近限制 ({gdiLimit})");
            }
            
            if (snapshot.UserObjects > USER_OBJECTS_WARNING_THRESHOLD)
            {
                warnings.Add($"⚠️ User对象数量 ({snapshot.UserObjects}) 接近限制");
            }
            
            if (snapshot.MemoryMB > 4096)
            {
                warnings.Add($"⚠️ 内存使用 ({snapshot.MemoryMB} MB) 较高");
            }

            if (warnings.Count > 0)
            {
                return (true, string.Join("\n", warnings));
            }
            
            return (false, "资源使用正常");
        }

        #endregion

        #region 优化执行

        /// <summary>
        /// 执行优化（兼容旧API的静态方法）
        /// </summary>
        public static void Optimize()
        {
            Instance.ExecuteOptimization(OptimizationLevel.Light, silent: true);
        }

        /// <summary>
        /// 静态优化方法（指定级别）
        /// </summary>
        public static void Optimize(OptimizationLevel level, bool silent = false)
        {
            Instance.ExecuteOptimization(level, silent);
        }

        /// <summary>
        /// 实例方法执行优化
        /// </summary>
        public ResourceSnapshot ExecuteOptimization(OptimizationLevel level, bool silent = false)
        {
            lock (_optimizeLock)
            {
                var beforeSnapshot = GetResourceSnapshot();
                
                try
                {
                    switch (level)
                    {
                        case OptimizationLevel.Light:
                            OptimizeLight();
                            break;
                        case OptimizationLevel.Medium:
                            OptimizeMedium();
                            break;
                        case OptimizationLevel.Heavy:
                            OptimizeHeavy(_swApp);
                            break;
                        case OptimizationLevel.Extreme:
                            OptimizeExtreme(_swApp);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log($"优化执行错误: {ex.Message}");
                }

                var afterSnapshot = GetResourceSnapshot();
                _lastSnapshot = afterSnapshot;
                
                if (!silent)
                {
                    Log($"优化完成 [{level}]: 内存 {beforeSnapshot.MemoryMB}→{afterSnapshot.MemoryMB} MB, " +
                        $"GDI {beforeSnapshot.GDIObjects}→{afterSnapshot.GDIObjects}");
                }
                
                return afterSnapshot;
            }
        }

        /// <summary>
        /// 轻度优化：只清理插件自身的托管对象，完全不影响SW性能
        /// </summary>
        private static void OptimizeLight()
        {
            // 只做优化模式的GC，让CLR自己决定是否需要回收
            // 这不会导致任何性能问题
            GC.Collect(0, GCCollectionMode.Optimized, false);
        }

        /// <summary>
        /// 中度优化：清理插件托管内存 + 释放COM引用
        /// 不触碰工作集，不影响SW操作性能
        /// </summary>
        private static void OptimizeMedium()
        {
            // 1. 释放可能挂起的COM对象引用
            // 这才是真正有用的操作，释放不再使用的SW对象
            try
            {
                // 强制终结器运行，释放COM引用
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized, false);
                GC.WaitForPendingFinalizers();
                // 回收被终结器释放的对象
                GC.Collect(0, GCCollectionMode.Optimized, false);
            }
            catch { }
            
            // 注意：完全不触碰工作集！
            // EmptyWorkingSet/SetProcessWorkingSetSize 会导致：
            // - 活跃数据被移到页面文件
            // - 下次操作时从磁盘读回，造成卡顿
            // - 装配体删除/修改元素变得很慢
            
            Log("中度优化: 释放COM引用完成 (不压缩工作集)");
        }

        /// <summary>
        /// 重度优化：清理 + 非活动装配体轻量化
        /// </summary>
        private static void OptimizeHeavy(ISldWorks swApp)
        {
            // 1. 执行中度优化（不压缩内存）
            OptimizeMedium();
            
            // 2. SolidWorks 特定优化：轻量化非活动装配体
            int lightweightedCount = 0;
            if (swApp != null)
            {
                try
                {
                    var activeDoc = swApp.ActiveDoc as IModelDoc2;
                    string activeDocPath = activeDoc?.GetPathName();
                    
                    // 只轻量化非当前活动的装配体
                    var modelDoc = swApp.GetFirstDocument() as IModelDoc2;
                    while (modelDoc != null)
                    {
                        string docPath = modelDoc.GetPathName();
                        
                        if (modelDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY &&
                            docPath != activeDocPath)
                        {
                            var assemblyDoc = modelDoc as IAssemblyDoc;
                            if (assemblyDoc != null)
                            {
                                try
                                {
                                    assemblyDoc.LightweightAllResolved();
                                    lightweightedCount++;
                                }
                                catch { }
                            }
                        }
                        modelDoc = modelDoc.GetNext() as IModelDoc2;
                    }
                }
                catch { }
            }
            
            // 注意：完全不收缩工作集！
            Log($"重度优化: 轻量化了 {lightweightedCount} 个非活动装配体");
        }

        /// <summary>
        /// 极限优化：关闭隐藏/不可见文档
        /// 这是唯一真正能释放SW内存的方法
        /// </summary>
        private static void OptimizeExtreme(ISldWorks swApp)
        {
            int closedCount = 0;
            
            if (swApp != null)
            {
                try
                {
                    var activeDoc = swApp.ActiveDoc as IModelDoc2;
                    string activeDocPath = activeDoc?.GetPathName();
                    
                    // 收集所有不可见的文档
                    var modelDoc = swApp.GetFirstDocument() as IModelDoc2;
                    var docsToClose = new System.Collections.Generic.List<string>();
                    
                    while (modelDoc != null)
                    {
                        string docPath = modelDoc.GetPathName();
                        bool isVisible = modelDoc.Visible;
                        
                        // 如果不是当前活动文档且不可见，则标记关闭
                        if (!string.IsNullOrEmpty(docPath) && 
                            docPath != activeDocPath && 
                            !isVisible)
                        {
                            docsToClose.Add(docPath);
                        }
                        
                        modelDoc = modelDoc.GetNext() as IModelDoc2;
                    }
                    
                    // 关闭标记的文档
                    foreach (var docPath in docsToClose)
                    {
                        try
                        {
                            swApp.CloseDoc(docPath);
                            closedCount++;
                        }
                        catch { }
                    }
                    
                    // 对当前装配体执行轻量化
                    if (activeDoc != null && activeDoc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY)
                    {
                        var assemblyDoc = activeDoc as IAssemblyDoc;
                        if (assemblyDoc != null)
                        {
                            try
                            {
                                assemblyDoc.LightweightAllResolved();
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            
            // 关闭文档后做一次GC清理释放的COM对象
            if (closedCount > 0)
            {
                OptimizeMedium();
            }
            
            Log($"极限优化: 关闭了 {closedCount} 个隐藏文档");
        }

        /// <summary>
        /// 额外选项：强制GC回收（会导致短暂卡顿）
        /// </summary>
        public static void ForceGarbageCollection()
        {
            try
            {
                // 强制完整GC，包括大对象堆
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
                
                Log("强制GC回收: 已完成完整垃圾回收");
            }
            catch (Exception ex)
            {
                Log($"强制GC回收失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 额外选项：压缩工作集（⚠️ 会导致后续操作变慢）
        /// </summary>
        public static void CompressWorkingSet()
        {
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                
                // 方法1: EmptyWorkingSet (最激进)
                EmptyWorkingSet(currentProcess.Handle);
                
                // 方法2: SetProcessWorkingSetSize (设置-1,-1强制最小化)
                SetProcessWorkingSetSize(currentProcess.Handle, new IntPtr(-1), new IntPtr(-1));
                
                Log("工作集压缩: 已将工作集最小化 (⚠️ 可能导致后续操作变慢)");
            }
            catch (Exception ex)
            {
                Log($"工作集压缩失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 额外选项：完整深度清理（GC + 工作集压缩）
        /// </summary>
        public static void DeepCleanup()
        {
            ForceGarbageCollection();
            CompressWorkingSet();
            Log("深度清理: 已完成GC和工作集压缩");
        }

        #endregion

        #region 自动优化

        /// <summary>
        /// 启动自动优化
        /// </summary>
        /// <param name="intervalMinutes">间隔分钟数</param>
        /// <param name="level">优化级别</param>
        public void StartAutoOptimize(int intervalMinutes, OptimizationLevel level)
        {
            StopAutoOptimize();
            
            _autoOptimizeIntervalMs = intervalMinutes * 60 * 1000;
            _autoOptimizeLevel = level;
            _autoOptimizeEnabled = true;
            
            _autoOptimizeTimer = new System.Threading.Timer(
                AutoOptimizeCallback,
                null,
                _autoOptimizeIntervalMs,
                _autoOptimizeIntervalMs
            );
            
            Log($"自动优化已启动: 间隔 {intervalMinutes} 分钟, 级别 {level}");
        }

        /// <summary>
        /// 停止自动优化
        /// </summary>
        public void StopAutoOptimize()
        {
            if (_autoOptimizeTimer != null)
            {
                _autoOptimizeTimer.Dispose();
                _autoOptimizeTimer = null;
            }
            _autoOptimizeEnabled = false;
            Log("自动优化已停止");
        }

        private void AutoOptimizeCallback(object state)
        {
            try
            {
                // 自动优化策略：尽量保守，避免影响用户操作
                // 只在真正必要时才做优化，且优先使用最轻量级的方式
                
                var snapshot = GetResourceSnapshot();
                int gdiLimit = GetGDIProcessHandleQuota();
                
                // 只有当GDI对象接近限制时才需要注意
                // 其他情况（虚拟内存、提交内存）通过优化无法真正解决
                // 只能靠关闭文档或重启SW
                
                if (snapshot.GDIObjects > gdiLimit * 0.85)
                {
                    // GDI紧张时，轻量化非活动装配体可能有帮助
                    ExecuteOptimization(OptimizationLevel.Heavy, silent: true);
                    Log($"自动优化: GDI对象较多 ({snapshot.GDIObjects}/{gdiLimit})");
                }
                else
                {
                    // 正常情况只做最轻量的清理
                    // 释放插件可能遗留的COM引用
                    ExecuteOptimization(OptimizationLevel.Light, silent: true);
                }
            }
            catch (Exception ex)
            {
                Log($"自动优化回调错误: {ex.Message}");
            }
        }

        #endregion

        #region GDI 配额管理

        /// <summary>
        /// 获取当前 GDI 进程句柄配额
        /// </summary>
        public static int GetGDIProcessHandleQuota()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("GDIProcessHandleQuota");
                        if (val != null) return (int)val;
                    }
                }
            }
            catch { }
            return 10000; // 默认值
        }

        /// <summary>
        /// 设置 GDI 进程句柄配额
        /// </summary>
        public static bool SetGDIProcessHandleQuota(int value, out string errorMsg)
        {
            errorMsg = string.Empty;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", true))
                {
                    if (key != null)
                    {
                        key.SetValue("GDIProcessHandleQuota", value, RegistryValueKind.DWord);
                        return true;
                    }
                    else
                    {
                        errorMsg = "无法打开注册表键。";
                        return false;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                errorMsg = "需要管理员权限才能修改 GDI 限制。请以管理员身份运行 SolidWorks。";
                return false;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }

        #endregion

        #region SW资源监控弹窗控制

        /// <summary>
        /// 禁用 SolidWorks 资源监控弹窗
        /// </summary>
        /// <returns>是否成功禁用</returns>
        public bool DisableResourceMonitorPopup()
        {
            if (_swApp == null)
            {
                Log("DisableResourceMonitorPopup: SolidWorks 未连接");
                return false;
            }

            try
            {
                // 使用 swconst 枚举值禁用资源监控弹窗
                int toggleValue = (int)swUserPreferenceToggle_e.swSystemNotificationHideGraphicsNotification;
                _swApp.SetUserPreferenceToggle(toggleValue, true);
                Log($"已禁用 SolidWorks 资源监控弹窗 (枚举值: {toggleValue})");
                return true;
            }
            catch (Exception ex)
            {
                Log($"禁用资源监控弹窗失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启用 SolidWorks 资源监控弹窗
        /// </summary>
        /// <returns>是否成功启用</returns>
        public bool EnableResourceMonitorPopup()
        {
            if (_swApp == null)
            {
                Log("EnableResourceMonitorPopup: SolidWorks 未连接");
                return false;
            }

            try
            {
                int toggleValue = (int)swUserPreferenceToggle_e.swSystemNotificationHideGraphicsNotification;
                _swApp.SetUserPreferenceToggle(toggleValue, false);
                Log("已启用 SolidWorks 资源监控弹窗");
                return true;
            }
            catch (Exception ex)
            {
                Log($"启用资源监控弹窗失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取资源监控弹窗是否已禁用
        /// </summary>
        public bool IsResourceMonitorPopupDisabled()
        {
            if (_swApp == null) return false;

            try
            {
                int toggleValue = (int)swUserPreferenceToggle_e.swSystemNotificationHideGraphicsNotification;
                return _swApp.GetUserPreferenceToggle(toggleValue);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 切换资源监控弹窗状态
        /// </summary>
        /// <returns>新状态：true = 已禁用, false = 已启用</returns>
        public bool ToggleResourceMonitorPopup()
        {
            bool currentlyDisabled = IsResourceMonitorPopupDisabled();
            if (currentlyDisabled)
            {
                EnableResourceMonitorPopup();
                return false;
            }
            else
            {
                DisableResourceMonitorPopup();
                return true;
            }
        }

        #endregion

        #region 弹窗拦截功能（Windows API）

        // 资源监控弹窗的可能标题关键词（中文和英文）
        private static readonly string[] PopupTitleKeywords = new string[]
        {
            "资源监视器", "资源监控", "Resource Monitor", "资源不足", 
            "Low Resources", "内存不足", "Low Memory", "Memory Low",
            "系统资源", "System Resources", "GDI", "句柄"
        };

        // 要排除的窗口标题（避免误杀）
        private static readonly string[] ExcludeTitles = new string[]
        {
            "SOLIDWORKS", "任务窗格", "Task Pane", "SharkTools"
        };

        /// <summary>
        /// 启动弹窗拦截器
        /// </summary>
        /// <param name="checkIntervalMs">检查间隔（毫秒），默认500ms</param>
        public void StartPopupBlocker(int checkIntervalMs = 500)
        {
            StopPopupBlocker();
            
            _popupBlockerEnabled = true;
            _popupBlockerTimer = new System.Threading.Timer(
                PopupBlockerCallback,
                null,
                1000,  // 延迟1秒启动
                checkIntervalMs
            );
            
            Log($"弹窗拦截器已启动: 间隔 {checkIntervalMs}ms");
        }

        /// <summary>
        /// 停止弹窗拦截器
        /// </summary>
        public void StopPopupBlocker()
        {
            _popupBlockerEnabled = false;
            if (_popupBlockerTimer != null)
            {
                _popupBlockerTimer.Dispose();
                _popupBlockerTimer = null;
            }
            Log($"弹窗拦截器已停止 (共拦截 {_popupBlockedCount} 个弹窗)");
        }

        /// <summary>
        /// 获取弹窗拦截器状态
        /// </summary>
        public bool IsPopupBlockerEnabled => _popupBlockerEnabled;

        /// <summary>
        /// 获取已拦截弹窗数量
        /// </summary>
        public int PopupBlockedCount => _popupBlockedCount;

        /// <summary>
        /// 弹窗拦截回调
        /// </summary>
        private void PopupBlockerCallback(object state)
        {
            if (!_popupBlockerEnabled) return;

            try
            {
                // 获取 SolidWorks 进程ID
                Process swProcess = GetSolidWorksProcess();
                if (swProcess == null) return;

                uint swPid = (uint)swProcess.Id;
                var hiddenWindows = new System.Collections.Generic.List<string>();

                // 枚举所有顶层窗口
                EnumWindows((hWnd, lParam) =>
                {
                    try
                    {
                        // 检查窗口是否属于 SolidWorks 进程
                        uint windowPid;
                        GetWindowThreadProcessId(hWnd, out windowPid);
                        if (windowPid != swPid) return true;

                        // 只处理可见窗口
                        if (!IsWindowVisible(hWnd)) return true;

                        // 获取窗口标题
                        var titleBuilder = new System.Text.StringBuilder(256);
                        GetWindowText(hWnd, titleBuilder, 256);
                        string title = titleBuilder.ToString();

                        if (string.IsNullOrEmpty(title)) return true;

                        // 检查是否应该排除
                        foreach (string exclude in ExcludeTitles)
                        {
                            if (title.IndexOf(exclude, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                // 如果标题只包含排除关键词，跳过
                                if (title.Equals(exclude, StringComparison.OrdinalIgnoreCase) ||
                                    title.StartsWith("SOLIDWORKS", StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }

                        // 检查是否匹配资源监控弹窗关键词
                        bool isResourcePopup = false;
                        foreach (string keyword in PopupTitleKeywords)
                        {
                            if (title.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                isResourcePopup = true;
                                break;
                            }
                        }

                        if (isResourcePopup)
                        {
                            // 隐藏窗口
                            ShowWindow(hWnd, SW_HIDE);
                            _popupBlockedCount++;
                            hiddenWindows.Add(title);
                            Log($"已拦截弹窗: {title}");
                        }
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);

                // 如果拦截了弹窗，可以显示插件的智能通知替代
                if (hiddenWindows.Count > 0 && _popupBlockedCount == 1)
                {
                    // 首次拦截时显示提示
                    Log($"SharkTools 已接管资源监控通知");
                }
            }
            catch (Exception ex)
            {
                Log($"弹窗拦截回调异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取 SolidWorks 进程
        /// </summary>
        private Process GetSolidWorksProcess()
        {
            try
            {
                var processes = Process.GetProcessesByName("SLDWORKS");
                return processes.Length > 0 ? processes[0] : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 切换弹窗拦截器状态
        /// </summary>
        /// <returns>新状态：true = 已启用, false = 已禁用</returns>
        public bool TogglePopupBlocker()
        {
            if (_popupBlockerEnabled)
            {
                StopPopupBlocker();
                return false;
            }
            else
            {
                StartPopupBlocker();
                return true;
            }
        }

        #endregion

        #region 插件通知功能

        /// <summary>
        /// 显示右下角通知弹窗
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容</param>
        /// <param name="isError">是否为错误级别</param>
        /// <returns>是否成功显示</returns>
        public bool ShowNotification(string title, string message, bool isError = false)
        {
            if (_swApp == null)
            {
                Log("ShowNotification: SolidWorks 未连接");
                return false;
            }

            try
            {
                // 检查冷却时间，避免频繁弹窗
                if ((DateTime.Now - _lastNotificationTime).TotalMinutes < NOTIFICATION_COOLDOWN_MINUTES)
                {
                    Log($"通知冷却中，跳过: {title}");
                    return false;
                }

                // 创建用户通知定义
                string uniqueName = $"SharkTools_ResourceNotification_{DateTime.Now.Ticks}";
                IUserNotificationDefinition notifDef = (IUserNotificationDefinition)_swApp.DefineUserNotification(uniqueName);
                
                if (notifDef == null)
                {
                    Log("创建通知定义失败");
                    return false;
                }

                // 设置通知属性
                notifDef.Title = title;
                notifDef.Message = message;
                notifDef.Position = swUserNotificationPosition_BottomRight;
                notifDef.Severity = isError ? swUserNotificationSeverity_Error : swUserNotificationSeverity_Warning;
                notifDef.IncludeDoNotShowAgain = true;  // 允许用户选择不再显示
                notifDef.ResponseAType = swUserNotificationResponseType_Button;
                notifDef.ResponseAText = "知道了";
                notifDef.ResponseBType = swUserNotificationResponseType_None;

                // 显示通知（不需要Handler回调，用户点击后自动关闭）
                int result = _swApp.ShowUserNotification(notifDef, null);
                
                _lastNotificationTime = DateTime.Now;
                Log($"显示通知: {title} - 结果: {result}");
                return result == 0;  // 0 = success
            }
            catch (Exception ex)
            {
                Log($"显示通知失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 显示资源警告通知
        /// </summary>
        public void ShowResourceWarning(string warningType, string details)
        {
            ShowNotification(
                $"⚠️ {warningType}",
                $"{details}\n\n建议：关闭不需要的文档或重启 SolidWorks",
                isError: false
            );
        }

        /// <summary>
        /// 显示资源严重警告通知
        /// </summary>
        public void ShowResourceCritical(string warningType, string details)
        {
            ShowNotification(
                $"🚨 {warningType}",
                $"{details}\n\n立即操作：保存工作并重启 SolidWorks！",
                isError: true
            );
        }

        /// <summary>
        /// 启动资源监控（定期检查并在必要时发出通知）
        /// </summary>
        /// <param name="intervalMinutes">检查间隔（分钟）</param>
        public void StartResourceMonitor(int intervalMinutes = 2)
        {
            StopResourceMonitor();
            
            int intervalMs = intervalMinutes * 60 * 1000;
            _resourceMonitorTimer = new System.Threading.Timer(
                ResourceMonitorCallback,
                null,
                intervalMs,  // 延迟启动
                intervalMs   // 周期
            );
            
            Log($"资源监控已启动: 间隔 {intervalMinutes} 分钟");
        }

        /// <summary>
        /// 停止资源监控
        /// </summary>
        public void StopResourceMonitor()
        {
            if (_resourceMonitorTimer != null)
            {
                _resourceMonitorTimer.Dispose();
                _resourceMonitorTimer = null;
            }
            Log("资源监控已停止");
        }

        /// <summary>
        /// 资源监控回调 - 检查资源状态并在必要时发出通知
        /// </summary>
        private void ResourceMonitorCallback(object state)
        {
            try
            {
                var snapshot = GetResourceSnapshot();
                int gdiLimit = GetGDIProcessHandleQuota();
                var (vmTotal, vmAvailable, vmUsage) = GetSystemVirtualMemory();
                
                // 1. 检查 GDI 对象（最重要）
                double gdiPercent = (double)snapshot.GDIObjects / gdiLimit * 100;
                if (gdiPercent > 95)
                {
                    ShowResourceCritical(
                        "GDI 对象即将耗尽！",
                        $"当前: {snapshot.GDIObjects} / {gdiLimit} ({gdiPercent:F0}%)\n" +
                        "超过限制将导致 SolidWorks 崩溃！"
                    );
                    return;
                }
                else if (gdiPercent > 85)
                {
                    ShowResourceWarning(
                        "GDI 对象过多",
                        $"当前: {snapshot.GDIObjects} / {gdiLimit} ({gdiPercent:F0}%)"
                    );
                    return;
                }
                
                // 2. 检查虚拟内存
                if (vmAvailable < 1024)  // 小于 1GB
                {
                    ShowResourceCritical(
                        "虚拟内存严重不足！",
                        $"可用虚拟内存: {vmAvailable} MB\n" +
                        "这是 SolidWorks 崩溃的主要原因！"
                    );
                    return;
                }
                else if (vmAvailable < 2048)  // 小于 2GB
                {
                    ShowResourceWarning(
                        "虚拟内存不足",
                        $"可用虚拟内存: {vmAvailable} MB"
                    );
                    return;
                }
                
                // 3. 检查提交内存（仅在非常高时提醒）
                if (snapshot.CommitMB > 8192)  // 超过 8GB
                {
                    ShowResourceWarning(
                        "提交内存过高",
                        $"当前提交内存: {snapshot.CommitMB} MB\n" +
                        "建议定期保存并重启 SolidWorks"
                    );
                }
            }
            catch (Exception ex)
            {
                Log($"资源监控回调错误: {ex.Message}");
            }
        }

        #endregion

        #region SW资源不足原因分析

        /// <summary>
        /// 分析 SolidWorks 资源不足的可能原因
        /// </summary>
        public string AnalyzeSWResourceIssues()
        {
            var sb = new System.Text.StringBuilder();
            var snapshot = GetResourceSnapshot();
            var (gdiPeak, userPeak) = GetPeakResources();
            int gdiLimit = GetGDIProcessHandleQuota();
            var (vmTotal, vmAvailable, vmUsage) = GetSystemVirtualMemory();
            
            sb.AppendLine("=== SolidWorks \"资源不足\" 真相分析 ===\n");
            
            // 0. SW Resource Monitor 说明
            sb.AppendLine("【关于 SW Resource Monitor 警告】");
            sb.AppendLine("  SW 的资源检测机制存在问题，经常误报！");
            sb.AppendLine("  它检测的是\"提交内存\"而非实际使用的物理内存。");
            sb.AppendLine("  提交内存 = SW预留的虚拟地址空间，不代表真正占用。");
            sb.AppendLine();
            
            // 1. 提交内存分析 (最重要)
            sb.AppendLine("【1. 提交内存分析】⭐ 警告的真正原因");
            sb.AppendLine($"  工作集(实际用): {snapshot.MemoryMB} MB");
            sb.AppendLine($"  提交内存(预留): {snapshot.CommitMB} MB");
            sb.AppendLine($"  系统可用虚拟内存: {vmAvailable} MB ({100-vmUsage}%)");
            
            if (snapshot.CommitMB > snapshot.MemoryMB * 3)
            {
                sb.AppendLine($"  ⚠️ 提交内存 >> 工作集，说明SW预留了大量未使用的虚拟地址空间");
                sb.AppendLine("  这会触发\"资源不足\"警告，但实际上物理内存可能充足！");
            }
            
            if (vmAvailable < 4096)
            {
                sb.AppendLine("  ⚠️ 系统虚拟内存紧张，这是触发警告的直接原因");
                sb.AppendLine("  解决方案:");
                sb.AppendLine("    - ★ 重启 SolidWorks（唯一彻底解决方案）");
                sb.AppendLine("    - 增加系统页面文件大小");
                sb.AppendLine("    - 关闭其他程序");
            }
            else
            {
                sb.AppendLine("  ✓ 虚拟内存充足，警告可能是误报");
            }
            
            sb.AppendLine();
            
            // 2. GDI 对象分析
            sb.AppendLine("【2. GDI对象分析】");
            sb.AppendLine($"  当前: {snapshot.GDIObjects} / 限制: {gdiLimit}");
            
            if (snapshot.GDIObjects > gdiLimit * 0.8)
            {
                sb.AppendLine("  ⚠️ GDI对象接近限制，这是真正的资源问题！");
                sb.AppendLine("  建议: 关闭不需要的文档，或使用轻量化模式");
            }
            else
            {
                sb.AppendLine("  ✓ GDI对象正常");
            }
            
            sb.AppendLine();
            
            // 3. 真正有效的解决方案
            sb.AppendLine("【3. 真正有效的解决方案】");
            sb.AppendLine("  ❌ 无效方案:");
            sb.AppendLine("    - 压缩工作集（会导致操作变慢）");
            sb.AppendLine("    - 强制GC（只能清理插件内存，对SW无效）");
            sb.AppendLine();
            sb.AppendLine("  ✓ 有效方案:");
            sb.AppendLine("    - 关闭不需要的文档（释放SW内存）");
            sb.AppendLine("    - 使用轻量化模式打开装配体");
            sb.AppendLine("    - 定期保存并重启SW（释放累积的虚拟内存预留）");
            sb.AppendLine("    - 增加页面文件大小（缓解虚拟内存紧张）");
            
            sb.AppendLine();
            
            // 4. 当前建议
            sb.AppendLine("【4. 当前建议】");
            if (snapshot.CommitMB > 4096 || vmAvailable < 4096)
            {
                sb.AppendLine("  ★ 建议重启 SolidWorks");
                sb.AppendLine("  提交内存过高，只有重启才能真正释放");
            }
            else if (snapshot.GDIObjects > gdiLimit * 0.7)
            {
                sb.AppendLine("  建议: 关闭部分文档或执行极限优化");
            }
            else
            {
                sb.AppendLine("  资源状态正常，如果看到警告可能是SW误报");
                sb.AppendLine("  可以安全地忽略或开启自动优化");
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// 获取优化级别的中文描述
        /// </summary>
        public static string GetLevelDescription(OptimizationLevel level)
        {
            switch (level)
            {
                case OptimizationLevel.Light:
                    return "轻度 - 只清理插件内存，不影响SW操作";
                case OptimizationLevel.Medium:
                    return "中度 - 释放COM引用，不压缩工作集";
                case OptimizationLevel.Heavy:
                    return "重度 - 非活动装配体轻量化";
                case OptimizationLevel.Extreme:
                    return "极限 - 关闭隐藏文档（唯一真正释放内存）";
                default:
                    return level.ToString();
            }
        }

        #endregion

        #region 日志

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(
                    @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt",
                    $"{DateTime.Now}: [PerformanceOptimizer] {message}\r\n"
                );
            }
            catch { }
        }

        #endregion
    }
}
