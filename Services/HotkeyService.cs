using System;
using System.Linq;
using NetworkAdapterHelper.Models;

namespace NetworkAdapterHelper.Services
{
    /// <summary>
    /// 全局快捷键服务
    /// </summary>
    public class HotkeyService : IDisposable
    {

        private static readonly Lazy<HotkeyService> _instance = new(() => new HotkeyService());
        
        /// <summary>
        /// 获取服务单例实例
        /// </summary>
        public static HotkeyService Instance => _instance.Value;

        private bool _initialized = false;
        private bool _disposed = false;

        /// <summary>
        /// 快捷键触发事件
        /// </summary>
        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

        /// <summary>
        /// 私有构造函数，实现单例模式
        /// </summary>
        private HotkeyService()
        {
            // 将钩子服务事件桥接到现有 HotkeyService 事件
            GlobalHotkeyHookService.Instance.HotkeyTriggered += (sender, args) =>
            {
                try
                {
                    HotkeyTriggered?.Invoke(this, args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"转发钩子快捷键事件异常: {ex.Message}");
                }
            };
        }

        /// <summary>
        /// 初始化快捷键服务
        /// </summary>
        /// <param name="windowHandle">窗口句柄</param>
        /// <returns>操作结果</returns>
        public OperationResult Initialize(IntPtr windowHandle)
        {
            try
            {
                if (_initialized)
                {
                    return OperationResult.CreateWarning("快捷键服务已经初始化");
                }

                // 直接初始化全局键盘钩子（与窗口句柄解耦）
                GlobalHotkeyHookService.Instance.Initialize();
                _initialized = true;

                return OperationResult.CreateSuccess("快捷键服务初始化成功");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("快捷键服务初始化失败", ex);
            }
        }

        /// <summary>
        /// 注册快捷键
        /// </summary>
        /// <param name="hotkeyConfig">快捷键配置</param>
        /// <returns>操作结果</returns>

        /// <summary>
        /// 注册多个快捷键
        /// </summary>
        /// <param name="hotkeyConfigs">快捷键配置列表</param>
        /// <returns>操作结果</returns>

        /// <summary>
        /// 取消注册快捷键
        /// </summary>
        /// <param name="hotkeyId">快捷键ID</param>
        /// <returns>操作结果</returns>

        /// <summary>
        /// 取消注册所有快捷键
        /// </summary>
        /// <returns>操作结果</returns>

        /// <summary>
        /// 获取已注册的快捷键列表
        /// </summary>
        /// <returns>已注册的快捷键配置列表</returns>

        /// <summary>
        /// 检查快捷键是否已注册
        /// </summary>
        /// <param name="modifierKeys">修饰键</param>
        /// <param name="key">主键</param>
        /// <returns>如果已注册返回true，否则返回false</returns>

        /// <summary>
        /// 更新快捷键配置
        /// </summary>
        /// <param name="configuration">应用程序配置</param>
        /// <returns>操作结果</returns>
        public async System.Threading.Tasks.Task<OperationResult> UpdateHotkeysAsync(ApplicationConfig configuration)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    // 更新钩子服务监听的快捷键配置
                    var hotkeyConfigs = configuration.HotkeySettings.Where(h => h.IsEnabled && h.IsValid).ToList();
                    GlobalHotkeyHookService.Instance.UpdateHotkeys(hotkeyConfigs);

                    return OperationResult.CreateSuccess($"已更新全局钩子快捷键：{hotkeyConfigs.Count} 个");
                }
                catch (Exception ex)
                {
                    return OperationResult.CreateError("更新快捷键配置失败", ex);
                }
            });
        }

        /// <summary>
        /// Windows消息处理程序
        /// </summary>
        /// <param name="hwnd">窗口句柄</param>
        /// <param name="msg">消息</param>
        /// <param name="wParam">wParam参数</param>
        /// <param name="lParam">lParam参数</param>
        /// <param name="handled">是否已处理</param>
        /// <returns>处理结果</returns>

        /// <summary>
        /// 将自定义修饰键转换为Win32修饰键
        /// </summary>
        /// <param name="modifierKeys">自定义修饰键</param>
        /// <returns>Win32修饰键</returns>

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 卸载全局键盘钩子
                    GlobalHotkeyHookService.Instance.Dispose();
                }

                _initialized = false;
                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~HotkeyService()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// 快捷键触发事件参数
    /// </summary>
    public class HotkeyTriggeredEventArgs : EventArgs
    {
        /// <summary>
        /// 触发的快捷键配置
        /// </summary>
        public HotkeyConfig HotkeyConfig { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="hotkeyConfig">快捷键配置</param>
        public HotkeyTriggeredEventArgs(HotkeyConfig hotkeyConfig)
        {
            HotkeyConfig = hotkeyConfig ?? throw new ArgumentNullException(nameof(hotkeyConfig));
        }
    }
}