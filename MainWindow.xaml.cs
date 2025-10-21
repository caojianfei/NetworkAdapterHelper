using System;
using System.ComponentModel;
using System.Windows;
using NetworkAdapterHelper.ViewModels;
using NetworkAdapterHelper.Services;

namespace NetworkAdapterHelper
{
    /// <summary>
    /// 主窗口类，负责显示网络适配器管理界面
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;
        private readonly SystemTrayManager _trayManager;
        private readonly NetworkAdapterService _adapterService;
        private readonly HotkeyService _hotkeyService;
        private readonly ConfigurationService _configService;

        /// <summary>
        /// 初始化主窗口
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
            
            // 初始化服务
            _configService = ConfigurationService.Instance;
            _adapterService = NetworkAdapterService.Instance;
            _hotkeyService = HotkeyService.Instance;
            _trayManager = new SystemTrayManager(_adapterService, _hotkeyService, _configService);
            
            // 初始化ViewModel
            _viewModel = new MainViewModel();
            DataContext = _viewModel;
            
            // 订阅ViewModel事件
            _viewModel.ShowSettingsRequested += (s, e) => ShowSettingsDialog();
            _viewModel.MinimizeToTrayRequested += (s, e) => MinimizeToTray();
            _viewModel.ExitRequested += (s, e) => ExitApplication();
            
            // 订阅窗口事件
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            StateChanged += MainWindow_StateChanged;
        }

        /// <summary>
        /// 窗口加载完成事件处理
        /// </summary>
        private async void MainWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 订阅托盘管理器事件
                _trayManager.ShowMainWindow += TrayManager_ShowMainWindow;
                _trayManager.ShowSettingsWindow += TrayManager_ShowSettingsWindow;
                _trayManager.ExitApplication += TrayManager_ExitApplication;
                
                // 初始化快捷键服务
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var initResult = _hotkeyService.Initialize(windowHandle);
                if (!initResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"快捷键服务初始化失败: {initResult.Message}");
                }
                
                // 初始化视图模型（包括加载配置和适配器数据）
                await _viewModel.InitializeAsync();
                
                // 加载并注册快捷键
                var config = await _configService.LoadConfigurationAsync();
                if (config.Success && config.Data != null)
                {
                    var hotkeyResult = await _hotkeyService.UpdateHotkeysAsync(config.Data);
                    if (!hotkeyResult.Success)
                    {
                        System.Diagnostics.Debug.WriteLine($"快捷键注册失败: {hotkeyResult.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // 检查是否应该最小化到托盘而不是关闭
            var config = _configService.LoadConfiguration();
            if (config.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                MinimizeToTray();
                return;
            }

            // 清理资源
            CleanupResources();
        }

        /// <summary>
        /// 窗口状态改变事件处理
        /// </summary>
        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // 如果窗口被最小化，则隐藏到托盘
            if (WindowState == WindowState.Minimized)
            {
                var config = _configService.LoadConfiguration();
                if (config.MinimizeToTray)
                {
                    MinimizeToTray();
                }
            }
        }

        /// <summary>
        /// 托盘管理器显示主窗口事件处理
        /// </summary>
        private void TrayManager_ShowMainWindow(object? sender, EventArgs e)
        {
            ShowMainWindow();
        }

        /// <summary>
        /// 托盘管理器显示设置事件处理
        /// </summary>
        private void TrayManager_ShowSettingsWindow(object? sender, EventArgs e)
        {
            ShowSettingsDialog();
        }

        /// <summary>
        /// 托盘管理器退出应用程序事件处理
        /// </summary>
        private void TrayManager_ExitApplication(object? sender, EventArgs e)
        {
            ExitApplication();
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        public void ShowMainWindow()
        {
            try
            {
                // 确保窗口可见
                Show();
                
                // 如果窗口被最小化，恢复正常状态
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }

                // 激活窗口并获得焦点
                Activate();
                Focus();
                
                // 确保窗口在最前面
                Topmost = true;
                Topmost = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 最小化到托盘
        /// </summary>
        public void MinimizeToTray()
        {
            try
            {
                Hide();
                _trayManager.ShowNotification("网络适配器助手", "应用程序已最小化到系统托盘");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"最小化到托盘失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示设置对话框
        /// </summary>
        public void ShowSettingsDialog()
        {
            try
            {
                var result = SettingsWindow.ShowSettingsDialog(this);
                if (result == true)
                {
                    // 设置已保存，刷新数据
                    _viewModel.RefreshCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示设置窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 退出应用程序
        /// </summary>
        public void ExitApplication()
        {
            try
            {
                var result = MessageBox.Show("确定要退出网络适配器助手吗？", "确认退出", 
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    CleanupResources();
                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"退出应用程序失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        private void CleanupResources()
        {
            try
            {
                // 取消订阅事件
                if (_trayManager != null)
                {
                    _trayManager.ShowMainWindow -= TrayManager_ShowMainWindow;
                    _trayManager.ShowSettingsWindow -= TrayManager_ShowSettingsWindow;
                    _trayManager.ExitApplication -= TrayManager_ExitApplication;
                }

                // 清理ViewModel
                _viewModel?.Dispose();

                // 清理服务
                _trayManager?.Dispose();
                HotkeyService.Instance?.Dispose();
                NetworkAdapterService.Instance?.Dispose();
            }
            catch (Exception ex)
            {
                // 记录错误但不显示给用户，因为应用程序正在关闭
                System.Diagnostics.Debug.WriteLine($"清理资源时发生错误: {ex.Message}");
            }
        }


    }
}