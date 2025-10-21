using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NetworkAdapterHelper.Services;
using NetworkAdapterHelper.Helpers;

namespace NetworkAdapterHelper;

/// <summary>
/// 应用程序主类，负责应用程序的启动和初始化
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex = null;
    private const string MutexName = "NetworkAdapterHelper_SingleInstance";

    /// <summary>
    /// 应用程序启动事件处理
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        // 检查单实例运行
        if (!CheckSingleInstance())
        {
            MessageBox.Show("网络适配器助手已经在运行中。", "提示", 
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        // 检查并确保管理员权限
        if (!AdminHelper.EnsureAdministratorPrivileges())
        {
            Shutdown();
            return;
        }

        // 设置全局异常处理
        SetupExceptionHandling();

        // 初始化服务
        InitializeServices();

        base.OnStartup(e);
    }

    /// <summary>
    /// 应用程序退出事件处理
    /// </summary>
    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            // 清理服务
            CleanupServices();

            // 释放互斥锁
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
        catch (Exception ex)
        {
            // 记录错误但不阻止退出
            Debug.WriteLine($"应用程序退出时发生错误: {ex.Message}");
        }

        base.OnExit(e);
    }

    /// <summary>
    /// 检查是否为单实例运行
    /// </summary>
    private bool CheckSingleInstance()
    {
        try
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            return createdNew;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"检查单实例时发生错误: {ex.Message}");
            return true; // 如果检查失败，允许继续运行
        }
    }



    /// <summary>
    /// 设置全局异常处理
    /// </summary>
    private void SetupExceptionHandling()
    {
        // 处理UI线程异常
        DispatcherUnhandledException += (sender, e) =>
        {
            HandleException(e.Exception);
            e.Handled = true;
        };

        // 处理非UI线程异常
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            HandleException(e.ExceptionObject as Exception);
        };

        // 处理Task异常
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            HandleException(e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>
    /// 处理未捕获的异常
    /// </summary>
    private void HandleException(Exception? ex)
    {
        try
        {
            var message = ex != null 
                ? $"发生未处理的异常:\n\n{ex.Message}\n\n详细信息:\n{ex}"
                : "发生未知异常";
            
            // 记录到日志
            Debug.WriteLine($"未处理的异常: {ex}");
            
            // 显示错误对话框
            MessageBox.Show(message, "应用程序错误", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // 如果连错误处理都失败了，至少尝试显示一个简单的消息
            MessageBox.Show("应用程序发生严重错误，即将退出。", "严重错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// 初始化服务
    /// </summary>
    private void InitializeServices()
    {
        try
        {
            // 初始化配置服务
            var configService = ConfigurationService.Instance;
            
            // 初始化日志服务并绑定到配置
            LoggingService.Instance.Initialize(configService);
            
            // 初始化网络服务
            var networkService = NetworkAdapterService.Instance;
            
            // 初始化快捷键服务
            var hotkeyService = HotkeyService.Instance;
            
            Debug.WriteLine("所有服务初始化完成");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化服务时发生错误: {ex.Message}", "初始化错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    /// <summary>
    /// 清理服务
    /// </summary>
    private void CleanupServices()
    {
        try
        {
            // 清理快捷键服务
            HotkeyService.Instance?.Dispose();
            
            // 清理托盘服务
            TrayService.Instance?.Dispose();
            
            // 清理网络服务
            NetworkAdapterService.Instance?.Dispose();
            
            // 清理日志服务
            LoggingService.Instance?.Dispose();
            
            Debug.WriteLine("所有服务清理完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清理服务时发生错误: {ex.Message}");
        }
    }
}