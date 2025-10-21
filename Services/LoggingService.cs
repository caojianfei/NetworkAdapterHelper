using System;
using System.Diagnostics;
using System.IO;

namespace NetworkAdapterHelper.Services
{
    /// <summary>
    /// 简单日志服务：通过 Trace/Debug 监听器按配置写入文件
    /// </summary>
    public sealed class LoggingService : IDisposable
    {
        private static readonly Lazy<LoggingService> _instance = new(() => new LoggingService());
        public static LoggingService Instance => _instance.Value;

        private TextWriterTraceListener? _fileListener;
        private string? _logFilePath;
        private bool _isEnabled;
        private const string ListenerName = "FileLogger";

        private LoggingService() { }

        /// <summary>
        /// 根据配置启用或关闭日志记录
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            if (enabled == _isEnabled) return;
            _isEnabled = enabled;

            if (_isEnabled)
            {
                EnableInternal();
            }
            else
            {
                DisableInternal();
            }
        }

        /// <summary>
        /// 初始化并绑定到配置服务的变更事件
        /// </summary>
        public void Initialize(ConfigurationService configService)
        {
            if (configService == null) throw new ArgumentNullException(nameof(configService));

            // 初始状态
            var config = configService.LoadConfiguration();
            SetEnabled(config.EnableLogging);

            // 动态更新
            configService.ConfigurationChanged += (s, newConfig) =>
            {
                SetEnabled(newConfig.EnableLogging);
            };
        }

        private void EnableInternal()
        {
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "NetworkAdapterHelper");
                Directory.CreateDirectory(logDir);

                _logFilePath = Path.Combine(logDir, "app.log");

                // 创建监听器并添加到 Debug/Trace
                _fileListener = new TextWriterTraceListener(_logFilePath)
                {
                    Name = ListenerName
                };

                // 避免重复添加
                RemoveExistingListener(ListenerName);
                Trace.Listeners.Add(_fileListener);
                Trace.AutoFlush = true;

                // 写入启动记录
                Trace.WriteLine($"[Logging] Enabled at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"启用日志失败: {ex.Message}");
            }
        }

        private void DisableInternal()
        {
            try
            {
                Trace.WriteLine($"[Logging] Disabled at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                RemoveExistingListener(ListenerName);
                _fileListener?.Flush();
                _fileListener?.Close();
                _fileListener = null;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"禁用日志失败: {ex.Message}");
            }
        }

        private static void RemoveExistingListener(string name)
        {
            // 从 Trace 中移除同名监听器
            for (int i = Trace.Listeners.Count - 1; i >= 0; i--)
            {
                if (Trace.Listeners[i].Name == name)
                {
                    Trace.Listeners[i].Flush();
                    Trace.Listeners[i].Close();
                    Trace.Listeners.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            try
            {
                DisableInternal();
            }
            catch
            {
                // ignore
            }
        }
    }
}