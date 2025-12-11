using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;

namespace SharkTools
{
    public static class PerformanceOptimizer
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetProcessHandleCount(IntPtr hProcess, out uint pdwHandleCount);

        [DllImport("user32.dll")]
        static extern uint GetGuiResources(IntPtr hProcess, uint uiFlags);

        private const uint GR_GDIOBJECTS = 0;
        private const uint GR_USEROBJECTS = 1;

        public static void Optimize()
        {
            try
            {
                // 1. 强制 .NET 垃圾回收
                // 只清理插件自身的托管内存，不触碰 SolidWorks 的非托管内存
                GC.Collect();
                // GC.WaitForPendingFinalizers(); // 移除等待终结器，避免潜在的死锁或长时间等待
                // GC.Collect();
                
                // 记录日志
                try {
                    System.IO.File.AppendAllText(
                        @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt", 
                        $"{DateTime.Now}: Executed Safe Optimization (GC Only)\r\n"
                    );
                } catch {}
            }
            catch (Exception)
            {
                // 忽略优化过程中的错误
            }
        }

        public static string GetResourceStatus()
        {
            Process currentProcess = Process.GetCurrentProcess();
            currentProcess.Refresh(); // 刷新进程信息
            long memory = currentProcess.WorkingSet64 / 1024 / 1024;
            uint gdiObjects = GetGuiResources(currentProcess.Handle, GR_GDIOBJECTS);
            uint userObjects = GetGuiResources(currentProcess.Handle, GR_USEROBJECTS);
            
            return $"内存: {memory} MB | GDI: {gdiObjects} | User: {userObjects}";
        }

        public static int GetGDIProcessHandleQuota()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("GDIProcessHandleQuota");
                        if (val != null) return (int)val;
                    }
                }
            }
            catch { }
            return 10000; // Default
        }

        public static bool SetGDIProcessHandleQuota(int value, out string errorMsg)
        {
            errorMsg = string.Empty;
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Windows", true))
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
    }
}
