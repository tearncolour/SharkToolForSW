using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SharkTools
{
    /// <summary>
    /// 统一的错误处理和日志管理类
    /// 提供异常捕获、日志记录和优雅恢复机制
    /// </summary>
    public static class ErrorHandler
    {
        // 日志文件路径
        private static readonly string LogFilePath = @"c:\Users\Administrator\Desktop\SharkToolForSW\debug_log.txt";
        private static readonly object _logLock = new object();
        
        // 最大日志文件大小（5MB）
        private const long MaxLogFileSize = 5 * 1024 * 1024;
        
        // 错误计数（用于防止重复错误刷屏）
        private static int _consecutiveErrors = 0;
        private static string _lastErrorMessage = "";
        private static DateTime _lastErrorTime = DateTime.MinValue;
        
        // 用户消息事件委托
        public delegate void UserMessageHandler(string message, MessageType type);
        public static event UserMessageHandler OnUserMessage;
        
        // 日志消息事件委托
        public delegate void LogMessageHandler(string message, MessageType type);
        public static event LogMessageHandler OnLogMessage;

        public enum MessageType
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// 触发用户消息（用于UI显示）
        /// </summary>
        public static void ShowUserMessage(string message, MessageType type = MessageType.Info)
        {
            try
            {
                OnUserMessage?.Invoke(message, type);
                // 用户消息也同时记录到日志
                Log(message, type);
            }
            catch
            {
                // 忽略事件处理中的错误
            }
        }

        /// <summary>
        /// 记录日志并触发事件
        /// </summary>
        private static void Log(string message, MessageType type)
        {
            try
            {
                // 写入文件
                WriteLogToFile(message, type);
                
                // 触发日志事件
                OnLogMessage?.Invoke(message, type);
            }
            catch
            {
                // 忽略日志记录错误
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public static void LogInfo(string message)
        {
            Log(message, MessageType.Info);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public static void LogWarning(string message)
        {
            Log(message, MessageType.Warning);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            string fullMessage = message;
            if (ex != null)
            {
                fullMessage += $"\r\nException: {ex.Message}\r\nStack Trace: {ex.StackTrace}";
            }
            Log(fullMessage, MessageType.Error);
        }

        /// <summary>
        /// 记录普通日志 (兼容旧API)
        /// </summary>
        public static void Log(string source, string message)
        {
            Log($"[{source}] {message}", MessageType.Info);
        }
        
        /// <summary>
        /// 记录警告日志 (兼容旧API)
        /// </summary>
        public static void LogWarning(string source, string message)
        {
            Log($"[{source}] {message}", MessageType.Warning);
        }
        
        /// <summary>
        /// 记录错误日志 (兼容旧API)
        /// </summary>
        public static void LogError(string source, string message, Exception ex = null)
        {
            // 防止重复错误刷屏
            if (ShouldSkipDuplicateError(message))
            {
                return;
            }
            
            var sb = new StringBuilder();
            sb.Append($"[{source}] {message}");
            
            if (ex != null)
            {
                sb.AppendLine();
                sb.Append($"  异常类型: {ex.GetType().Name}");
                sb.AppendLine();
                sb.Append($"  异常消息: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    sb.AppendLine();
                    sb.Append($"  内部异常: {ex.InnerException.Message}");
                }
                
                // 仅在调试时记录堆栈
                #if DEBUG
                sb.AppendLine();
                sb.Append($"  堆栈跟踪:\r\n{ex.StackTrace}");
                #endif
            }
            
            Log(sb.ToString(), MessageType.Error);
        }
        
        /// <summary>
        /// 安全执行操作（带异常捕获和日志）
        /// </summary>
        public static bool SafeExecute(string source, Action action, string errorMessage = null)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                LogError(source, errorMessage ?? "操作执行失败", ex);
                return false;
            }
        }
        
        /// <summary>
        /// 安全执行操作并返回结果
        /// </summary>
        public static T SafeExecute<T>(string source, Func<T> func, T defaultValue = default(T), string errorMessage = null)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                LogError(source, errorMessage ?? "操作执行失败", ex);
                return defaultValue;
            }
        }
        
        /// <summary>
        /// 带重试的安全执行
        /// </summary>
        public static bool SafeExecuteWithRetry(string source, Action action, int maxRetries = 3, int retryDelayMs = 500, string errorMessage = null)
        {
            Exception lastException = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    action();
                    return true;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    
                    if (attempt < maxRetries)
                    {
                        LogWarning(source, $"操作失败，将在{retryDelayMs}ms后重试 (尝试 {attempt}/{maxRetries}): {ex.Message}");
                        Thread.Sleep(retryDelayMs);
                    }
                }
            }
            
            LogError(source, $"{errorMessage ?? "操作执行失败"} (重试{maxRetries}次后仍失败)", lastException);
            return false;
        }
        
        /// <summary>
        /// 检查是否应跳过重复错误
        /// </summary>
        private static bool ShouldSkipDuplicateError(string message)
        {
            lock (_logLock)
            {
                var now = DateTime.Now;
                
                // 如果是相同的错误消息，且在3秒内
                if (message == _lastErrorMessage && (now - _lastErrorTime).TotalSeconds < 3)
                {
                    _consecutiveErrors++;
                    
                    // 每10次重复错误只记录一次
                    if (_consecutiveErrors < 10)
                    {
                        return true;
                    }
                    
                    // 记录后重置计数
                    _consecutiveErrors = 0;
                }
                else
                {
                    // 新的错误消息，重置计数
                    _consecutiveErrors = 0;
                }
                
                _lastErrorMessage = message;
                _lastErrorTime = now;
                return false;
            }
        }
        
        /// <summary>
        /// 写入日志文件
        /// </summary>
        private static void WriteLogToFile(string message, MessageType type)
        {
            lock (_logLock)
            {
                try
                {
                    // 检查日志文件大小，必要时轮转
                    RotateLogFileIfNeeded();
                    
                    string prefix = type == MessageType.Error ? "ERROR" : (type == MessageType.Warning ? "WARN" : "INFO");
                    string logEntry = $"{DateTime.Now:yyyy/MM/dd HH:mm:ss} [{prefix}] {message}\r\n";
                    File.AppendAllText(LogFilePath, logEntry, Encoding.UTF8);
                }
                catch { }
            }
        }
        
        /// <summary>
        /// 日志文件轮转
        /// </summary>
        private static void RotateLogFileIfNeeded()
        {
            try
            {
                if (!File.Exists(LogFilePath))
                    return;
                    
                var fileInfo = new FileInfo(LogFilePath);
                if (fileInfo.Length > MaxLogFileSize)
                {
                    // 备份旧日志
                    string backupPath = LogFilePath.Replace(".txt", $"_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.Move(LogFilePath, backupPath);
                    
                    // 清理超过7天的旧日志
                    CleanupOldLogs();
                }
            }
            catch
            {
                // 轮转失败时静默处理
            }
        }
        
        /// <summary>
        /// 清理旧日志文件
        /// </summary>
        private static void CleanupOldLogs()
        {
            try
            {
                string logDir = Path.GetDirectoryName(LogFilePath);
                string logPattern = Path.GetFileNameWithoutExtension(LogFilePath) + "_*.txt";
                
                foreach (var file in Directory.GetFiles(logDir, logPattern))
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < DateTime.Now.AddDays(-7))
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // 清理失败时静默处理
            }
        }
    }
}
