using System;
using System.Threading;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace SharkTools
{
    /// <summary>
    /// 线程监控器 - 检测主线程阻塞情况
    /// </summary>
    public class ThreadMonitor : IDisposable
    {
        private System.Threading.Timer _checkTimer;
        private Control _uiControl; // 用于Invoke到主线程
        private bool _isChecking = false;
        private const int CheckInterval = 5000; // 5秒检查一次
        private const int WarningThreshold = 3000; // 超过3秒未响应视为阻塞

        public ThreadMonitor(Control uiControl)
        {
            _uiControl = uiControl;
        }

        public void Start()
        {
            if (_checkTimer == null)
            {
                _checkTimer = new System.Threading.Timer(CheckCallback, null, CheckInterval, CheckInterval);
                ErrorHandler.Log("ThreadMonitor", "线程监控已启动");
            }
        }

        public void Stop()
        {
            if (_checkTimer != null)
            {
                _checkTimer.Dispose();
                _checkTimer = null;
                ErrorHandler.Log("ThreadMonitor", "线程监控已停止");
            }
        }

        private void CheckCallback(object state)
        {
            if (_isChecking || _uiControl == null || _uiControl.IsDisposed) return;

            _isChecking = true;

            try
            {
                // 检查控件句柄是否已创建
                if (!_uiControl.IsHandleCreated)
                {
                    return;
                }

                // 在另一个线程池线程中尝试 Invoke，以便我们可以设置超时
                // 因为 Timer 回调本身是在线程池线程，但我们需要一个可以 Wait 的 Task
                var task = Task.Run(() => 
                {
                    try
                    {
                        if (_uiControl != null && !_uiControl.IsDisposed && _uiControl.IsHandleCreated)
                        {
                            // 同步 Invoke，如果主线程忙，这里会阻塞
                            _uiControl.Invoke(new Action(() => 
                            {
                                // 主线程空操作
                            }));
                        }
                    }
                    catch 
                    {
                        // 忽略 Invoke 过程中的异常（如窗口关闭）
                    }
                });

                // 等待主线程响应
                if (!task.Wait(WarningThreshold))
                {
                    // 超时未响应
                    ErrorHandler.LogWarning("ThreadMonitor", $"检测到主线程阻塞！已超过 {WarningThreshold}ms 无响应");
                    
                    // 可以在这里触发警告事件
                    ErrorHandler.ShowUserMessage("检测到 SolidWorks 主线程响应缓慢，请稍候...", ErrorHandler.MessageType.Warning);
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.LogError("ThreadMonitor", "监控检查失败", ex);
            }
            finally
            {
                _isChecking = false;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
