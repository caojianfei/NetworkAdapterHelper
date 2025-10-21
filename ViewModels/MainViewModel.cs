using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NetworkAdapterHelper.Models;
using NetworkAdapterHelper.Services;

namespace NetworkAdapterHelper.ViewModels
{
    /// <summary>
    /// 主窗口视图模型
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        #region 私有字段

        private readonly NetworkAdapterService _networkService;
        private readonly ConfigurationService _configService;
        private readonly HotkeyService _hotkeyService;
        private readonly TrayService _trayService;

        private System.Windows.Threading.DispatcherTimer? _refreshTimer;
        private ApplicationConfig? _currentConfig;
        private bool _isLoading;
        private string _statusMessage = "就绪";
        private NetworkAdapter? _selectedAdapter;

        #endregion

        #region 公共属性

        /// <summary>
        /// 网络适配器列表
        /// </summary>
        public ObservableCollection<NetworkAdapter> Adapters { get; } = new();

        /// <summary>
        /// 当前选中的适配器
        /// </summary>
        public NetworkAdapter? SelectedAdapter
        {
            get => _selectedAdapter;
            set
            {
                if (SetProperty(ref _selectedAdapter, value))
                {
                    OnPropertyChanged(nameof(IsAdapterSelected));
                    OnPropertyChanged(nameof(CanToggleAdapter));
                }
            }
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 是否有选中的适配器
        /// </summary>
        public bool IsAdapterSelected => SelectedAdapter != null;

        /// <summary>
        /// 是否可以切换适配器状态
        /// </summary>
        public bool CanToggleAdapter => SelectedAdapter != null && !IsLoading;

        /// <summary>
        /// 是否可以执行批量操作
        /// </summary>
        public bool CanExecuteBatchOperations => !IsLoading && Adapters?.Any() == true;

        /// <summary>
        /// 是否配置了适配器切换
        /// </summary>
        public bool IsSwitchConfigured => _currentConfig?.IsSwitchConfigured ?? false;

        /// <summary>
        /// 适配器A名称
        /// </summary>
        public string AdapterAName => GetAdapterDisplayName(_currentConfig?.SelectedAdapterA);

        /// <summary>
        /// 适配器B名称
        /// </summary>
        public string AdapterBName => GetAdapterDisplayName(_currentConfig?.SelectedAdapterB);

        #endregion

        #region 命令

        /// <summary>
        /// 刷新适配器列表命令
        /// </summary>
        public ICommand RefreshCommand { get; }

        /// <summary>
        /// 切换适配器状态命令
        /// </summary>
        public ICommand ToggleAdapterCommand { get; }

        /// <summary>
        /// 启用所有适配器命令
        /// </summary>
        public ICommand EnableAllCommand { get; }

        /// <summary>
        /// 禁用所有适配器命令
        /// </summary>
        public ICommand DisableAllCommand { get; }

        /// <summary>
        /// 切换配置的适配器命令
        /// </summary>
        public ICommand SwitchAdaptersCommand { get; }

        /// <summary>
        /// 显示设置窗口命令
        /// </summary>
        public ICommand ShowSettingsCommand { get; }

        /// <summary>
        /// 最小化到托盘命令
        /// </summary>
        public ICommand MinimizeToTrayCommand { get; }

        /// <summary>
        /// 退出应用程序命令
        /// </summary>
        public ICommand ExitCommand { get; }

        #endregion

        #region 事件

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 显示设置窗口事件
        /// </summary>
        public event EventHandler? ShowSettingsRequested;

        /// <summary>
        /// 最小化到托盘事件
        /// </summary>
        public event EventHandler? MinimizeToTrayRequested;

        /// <summary>
        /// 退出应用程序事件
        /// </summary>
        public event EventHandler? ExitRequested;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        public MainViewModel()
        {
            // 获取服务实例
            _networkService = NetworkAdapterService.Instance;
            _configService = ConfigurationService.Instance;
            _hotkeyService = HotkeyService.Instance;
            _trayService = TrayService.Instance;

            // 初始化命令
            RefreshCommand = new RelayCommand(async () => await RefreshAdaptersAsync());
            ToggleAdapterCommand = new RelayCommand(async () => await ToggleSelectedAdapterAsync(), () => CanToggleAdapter);
            EnableAllCommand = new RelayCommand(async () => await EnableAllAdaptersAsync(), () => CanExecuteBatchOperations);
            DisableAllCommand = new RelayCommand(async () => await DisableAllAdaptersAsync(), () => CanExecuteBatchOperations);
            SwitchAdaptersCommand = new RelayCommand(async () => await SwitchConfiguredAdaptersAsync(), () => IsSwitchConfigured && !IsLoading);
            ShowSettingsCommand = new RelayCommand(() => ShowSettingsRequested?.Invoke(this, EventArgs.Empty));
            MinimizeToTrayCommand = new RelayCommand(() => MinimizeToTrayRequested?.Invoke(this, EventArgs.Empty));
            ExitCommand = new RelayCommand(() => ExitRequested?.Invoke(this, EventArgs.Empty));

            // 订阅服务事件
            _networkService.AdapterStateChanged += OnAdapterStateChanged;
            _configService.ConfigurationChanged += OnConfigurationChanged;
            _hotkeyService.HotkeyTriggered += OnHotkeyTriggered;

            // 订阅托盘服务事件
            _trayService.ShowMainWindow += OnShowMainWindowFromTray;
            _trayService.ShowSettingsWindow += OnShowSettingsFromTray;
            _trayService.EnableAllAdapters += OnEnableAllFromTray;
            _trayService.DisableAllAdapters += OnDisableAllFromTray;
            _trayService.SwitchAdapters += OnSwitchAdaptersFromTray;
            _trayService.ExitApplication += OnExitFromTray;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 初始化视图模型
        /// </summary>
        /// <returns>初始化任务</returns>
        public async Task InitializeAsync()
        {
            try
            {
                StatusMessage = "正在初始化...";
                IsLoading = true;

                // 加载配置
                var configResult = await _configService.LoadConfigurationAsync();
                if (configResult.Success && configResult.Data != null)
                {
                    _currentConfig = configResult.Data;
                    OnPropertyChanged(nameof(IsSwitchConfigured));

                    // 根据刷新间隔设置定时器
                    UpdateRefreshTimer();
                }

                // 刷新适配器列表
                await RefreshAdaptersAsync();

                // 适配器列表刷新后，更新适配器名称显示
                OnPropertyChanged(nameof(AdapterAName));
                OnPropertyChanged(nameof(AdapterBName));

                // 根据配置设置默认选中的适配器
                SetDefaultSelectedAdapter();

                StatusMessage = "初始化完成";
            }
            catch (Exception ex)
            {
                StatusMessage = $"初始化失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 刷新适配器列表
        /// </summary>
        /// <returns>刷新任务</returns>
        public async Task RefreshAdaptersAsync()
        {
            try
            {
                StatusMessage = "正在刷新适配器列表...";
                IsLoading = true;

                var result = await _networkService.GetAllAdaptersAsync();
                
                if (result.Success && result.Data != null)
                {
                    // 更新适配器列表
                    Adapters.Clear();
                    foreach (var adapter in result.Data)
                    {
                        Adapters.Add(adapter);
                    }

                    StatusMessage = $"找到 {Adapters.Count} 个网络适配器";
                    
                    // 更新托盘图标状态
                    var hasConnectedAdapter = Adapters.Any(a => a.IsEnabled);
                    _trayService.UpdateTrayIcon(hasConnectedAdapter);
                    
                    // 更新适配器名称显示
                    OnPropertyChanged(nameof(AdapterAName));
                    OnPropertyChanged(nameof(AdapterBName));
                }
                else
                {
                    StatusMessage = result.Message ?? "刷新适配器列表失败";
                }

                // 更新命令可用状态
                OnPropertyChanged(nameof(CanExecuteBatchOperations));
            }
            catch (Exception ex)
            {
                StatusMessage = $"刷新失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 根据配置设置默认选中的适配器
        /// </summary>
        private void SetDefaultSelectedAdapter()
        {
            if (_currentConfig == null || Adapters.Count == 0)
                return;

            // 优先选择配置中的适配器A
            if (!string.IsNullOrEmpty(_currentConfig.SelectedAdapterA))
            {
                var adapterA = Adapters.FirstOrDefault(a => a.DeviceId == _currentConfig.SelectedAdapterA);
                if (adapterA != null)
                {
                    SelectedAdapter = adapterA;
                    return;
                }
            }

            // 如果适配器A不存在，尝试选择适配器B
            if (!string.IsNullOrEmpty(_currentConfig.SelectedAdapterB))
            {
                var adapterB = Adapters.FirstOrDefault(a => a.DeviceId == _currentConfig.SelectedAdapterB);
                if (adapterB != null)
                {
                    SelectedAdapter = adapterB;
                    return;
                }
            }

            // 如果配置的适配器都不存在，选择第一个可用的适配器
            if (Adapters.Count > 0)
            {
                SelectedAdapter = Adapters.First();
            }
        }

        /// <summary>
        /// 切换选中适配器的状态
        /// </summary>
        /// <returns>切换任务</returns>
        private async Task ToggleSelectedAdapterAsync()
        {
            var adapter = SelectedAdapter;
            if (adapter == null) return;
        
            try
            {
                StatusMessage = $"正在{(adapter.IsEnabled ? "禁用" : "启用")} {adapter.Name}...";
                IsLoading = true;
        
                OperationResult result = adapter.IsEnabled
                    ? await _networkService.DisableAdapterAsync(adapter.DeviceId)
                    : await _networkService.EnableAdapterAsync(adapter.DeviceId);
        
                StatusMessage = result.Message ?? "操作完成";
        
                if (result.Success)
                {
                    // 仅更新启用状态与时间，不覆盖连接状态（Status 由刷新填充）
                    adapter.IsEnabled = !adapter.IsEnabled;
                    adapter.LastUpdated = DateTime.Now;
        
                    OnPropertyChanged(nameof(SelectedAdapter));
                    OnPropertyChanged(nameof(CanExecuteBatchOperations));
        
                    var action = adapter.IsEnabled ? "启用" : "禁用";
                    _trayService.ShowNotification("网络适配器", $"已{action} {adapter.Name}");
        
                    var hasConnectedAdapter = Adapters.Any(a => a.IsEnabled);
                    _trayService.UpdateTrayIcon(hasConnectedAdapter);
                }
        
                // 启用/禁用后刷新列表，确保连接状态等由 WMI 重新填充
                await RefreshAdaptersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"操作失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 启用所有适配器
        /// </summary>
        /// <returns>启用任务</returns>
        private async Task EnableAllAdaptersAsync()
        {
            try
            {
                StatusMessage = "正在启用所有适配器...";
                IsLoading = true;

                var result = await _networkService.EnableAllAdaptersAsync();
                StatusMessage = result.Message ?? "操作完成";

                if (result.Success)
                {
                    _trayService.ShowNotification("网络适配器", "已启用所有适配器");
                }

                // 刷新适配器列表
                await RefreshAdaptersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"操作失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 禁用所有适配器
        /// </summary>
        /// <returns>禁用任务</returns>
        private async Task DisableAllAdaptersAsync()
        {
            try
            {
                StatusMessage = "正在禁用所有适配器...";
                IsLoading = true;

                var result = await _networkService.DisableAllAdaptersAsync();
                StatusMessage = result.Message ?? "操作完成";

                if (result.Success)
                {
                    _trayService.ShowNotification("网络适配器", "已禁用所有适配器");
                }

                // 刷新适配器列表
                await RefreshAdaptersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"操作失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 切换配置的适配器
        /// </summary>
        /// <returns>切换任务</returns>
        private async Task SwitchConfiguredAdaptersAsync()
        {
            if (_currentConfig == null || !_currentConfig.IsSwitchConfigured) return;

            try
            {
                StatusMessage = "正在切换适配器...";
                IsLoading = true;

                var result = await _networkService.SwitchAdaptersAsync(
                    _currentConfig.SelectedAdapterA!, 
                    _currentConfig.SelectedAdapterB!);
                
                StatusMessage = result.Message ?? "切换完成";

                if (result.Success)
                {
                    _trayService.ShowNotification("网络适配器", result.Message ?? "适配器切换成功");
                }

                // 刷新适配器列表
                await RefreshAdaptersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"切换失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 获取适配器显示名称
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>显示名称</returns>
        private string GetAdapterDisplayName(string? deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return "未配置";

            var adapter = Adapters.FirstOrDefault(a => a.DeviceId == deviceId);
            return adapter?.Name ?? "未知适配器";
        }

        /// <summary>
        /// 设置属性值并触发属性变更通知
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="field">字段引用</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>如果值发生变化返回true，否则返回false</returns>
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// 触发属性变更通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 事件处理

        /// <summary>
        /// 适配器状态变化事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="adapter">变化的适配器</param>
        private void OnAdapterStateChanged(object? sender, NetworkAdapter adapter)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // 更新列表中对应的适配器
                var existingAdapter = Adapters.FirstOrDefault(a => a.DeviceId == adapter.DeviceId);
                if (existingAdapter != null)
                {
                    var index = Adapters.IndexOf(existingAdapter);
                    Adapters[index] = adapter;
                }

                // 更新托盘图标状态
                var hasConnectedAdapter = Adapters.Any(a => a.IsEnabled);
                _trayService.UpdateTrayIcon(hasConnectedAdapter);
            });
        }

        /// <summary>
        /// 配置变更事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="config">新配置</param>
        private void OnConfigurationChanged(object? sender, ApplicationConfig config)
        {
            _currentConfig = config;
            OnPropertyChanged(nameof(IsSwitchConfigured));
            OnPropertyChanged(nameof(AdapterAName));
            OnPropertyChanged(nameof(AdapterBName));

            // 根据新配置更新刷新定时器
            UpdateRefreshTimer();

            // 更新托盘菜单
            _trayService.UpdateContextMenu(config);
        }

        /// <summary>
        /// 快捷键触发事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void OnHotkeyTriggered(object? sender, HotkeyTriggeredEventArgs e)
        {
            try
            {
                switch (e.HotkeyConfig.Action)
                {
                    case HotkeyAction.EnableAll:
                        await EnableAllAdaptersAsync();
                        break;
                    case HotkeyAction.DisableAll:
                        await DisableAllAdaptersAsync();
                        break;
                    case HotkeyAction.SwitchAdapters:
                        await SwitchConfiguredAdaptersAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"快捷键操作失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 从托盘显示主窗口事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnShowMainWindowFromTray(object? sender, EventArgs e)
        {
            // 这个事件将由主窗口处理
        }

        /// <summary>
        /// 从托盘显示设置窗口事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnShowSettingsFromTray(object? sender, EventArgs e)
        {
            ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 从托盘启用所有适配器事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void OnEnableAllFromTray(object? sender, EventArgs e)
        {
            await EnableAllAdaptersAsync();
        }

        /// <summary>
        /// 从托盘禁用所有适配器事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void OnDisableAllFromTray(object? sender, EventArgs e)
        {
            await DisableAllAdaptersAsync();
        }

        /// <summary>
        /// 从托盘切换适配器事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private async void OnSwitchAdaptersFromTray(object? sender, EventArgs e)
        {
            await SwitchConfiguredAdaptersAsync();
        }

        /// <summary>
        /// 从托盘退出应用程序事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnExitFromTray(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region IDisposable 实现

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 取消订阅服务事件
            if (_networkService != null)
            {
                _networkService.AdapterStateChanged -= OnAdapterStateChanged;
            }

            if (_configService != null)
            {
                _configService.ConfigurationChanged -= OnConfigurationChanged;
            }

            if (_hotkeyService != null)
            {
                _hotkeyService.HotkeyTriggered -= OnHotkeyTriggered;
            }

            // 取消订阅托盘服务事件
            if (_trayService != null)
            {
                _trayService.ShowMainWindow -= OnShowMainWindowFromTray;
                _trayService.ShowSettingsWindow -= OnShowSettingsFromTray;
                _trayService.EnableAllAdapters -= OnEnableAllFromTray;
                _trayService.DisableAllAdapters -= OnDisableAllFromTray;
                _trayService.SwitchAdapters -= OnSwitchAdaptersFromTray;
                _trayService.ExitApplication -= OnExitFromTray;
            }

            // 停止并清理刷新定时器
            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Tick -= RefreshTimer_Tick;
                _refreshTimer = null;
            }

            // 清理事件
            PropertyChanged = null;
            ShowSettingsRequested = null;
            MinimizeToTrayRequested = null;
            ExitRequested = null;
        }

        // 定时刷新相关
        private void UpdateRefreshTimer()
        {
            var intervalSec = _currentConfig?.RefreshInterval ?? 0;
            if (intervalSec <= 0)
            {
                // 禁用刷新
                if (_refreshTimer != null)
                {
                    _refreshTimer.Stop();
                }
                return;
            }

            if (_refreshTimer == null)
            {
                _refreshTimer = new System.Windows.Threading.DispatcherTimer();
                _refreshTimer.Tick += RefreshTimer_Tick;
            }

            _refreshTimer.Interval = TimeSpan.FromSeconds(intervalSec);
            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (IsLoading)
                {
                    return; // 避免重入
                }
                await RefreshAdaptersAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"定时刷新失败: {ex.Message}";
            }
        }

        #endregion
    }

    /// <summary>
    /// 简单的命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">可执行判断方法</param>
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 可执行状态变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">参数</param>
        public void Execute(object? parameter)
        {
            _execute();
        }
    }

    /// <summary>
    /// 泛型命令实现
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="execute">执行方法</param>
        /// <param name="canExecute">可执行判断方法</param>
        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 可执行状态变更事件
        /// </summary>
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 判断是否可执行
        /// </summary>
        /// <param name="parameter">参数</param>
        /// <returns>是否可执行</returns>
        public bool CanExecute(object? parameter)
        {
            if (parameter is T typedParameter)
            {
                return _canExecute?.Invoke(typedParameter) ?? true;
            }
            return _canExecute?.Invoke(default(T)) ?? true;
        }

        /// <summary>
        /// 执行命令
        /// </summary>
        /// <param name="parameter">参数</param>
        public void Execute(object? parameter)
        {
            if (parameter is T typedParameter)
            {
                _execute(typedParameter);
            }
            else
            {
                _execute(default(T));
            }
        }
    }

}