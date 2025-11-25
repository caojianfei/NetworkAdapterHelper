using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using NetworkAdapterHelper.Models;
using NetworkAdapterHelper.Services;
using NetworkAdapterHelper.Helpers;

namespace NetworkAdapterHelper.ViewModels
{
    /// <summary>
    /// 设置窗口的视图模型
    /// </summary>
    public class SettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ConfigurationService _configurationService;
    private readonly NetworkAdapterService _networkService;
    private readonly HotkeyService _hotkeyService;
    private ApplicationConfig _configuration = new ApplicationConfig();
        private bool _isLoading;
        private string _statusMessage = "就绪";
        private ObservableCollection<NetworkAdapter> _availableAdapters = new ObservableCollection<NetworkAdapter>();
        private NetworkAdapter? _selectedAdapterA;
        private NetworkAdapter? _selectedAdapterB;

        /// <summary>
        /// 初始化设置视图模型
        /// </summary>
        public SettingsViewModel()
        {
            _configurationService = ConfigurationService.Instance;
            _networkService = NetworkAdapterService.Instance;
            _hotkeyService = HotkeyService.Instance;
            
            // 加载配置
            LoadConfiguration();
            
            // 初始化命令
            InitializeCommands();
        }

        /// <summary>
        /// 异步初始化方法，在窗口加载后调用
        /// </summary>
        public async Task InitializeAsync()
        {
            // 加载适配器列表
            await LoadAdaptersAsync();
        }

        #region 属性

        /// <summary>
        /// 应用程序配置
        /// </summary>
        public ApplicationConfig Configuration
        {
            get => _configuration;
            set
            {
                _configuration = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 可用的网络适配器列表
        /// </summary>
        public ObservableCollection<NetworkAdapter> AvailableAdapters
        {
            get => _availableAdapters;
            set
            {
                _availableAdapters = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 选中的适配器A
        /// </summary>
        public NetworkAdapter? SelectedAdapterA
        {
            get => _selectedAdapterA;
            set
            {
                _selectedAdapterA = value;
                OnPropertyChanged();
                if (value != null)
                {
                    Configuration.SelectedAdapterA = value.DeviceId;
                }
            }
        }

        /// <summary>
        /// 选中的适配器B
        /// </summary>
        public NetworkAdapter? SelectedAdapterB
        {
            get => _selectedAdapterB;
            set
            {
                _selectedAdapterB = value;
                OnPropertyChanged();
                if (value != null)
                {
                    Configuration.SelectedAdapterB = value.DeviceId;
                }
            }
        }

        /// <summary>
        /// 启用所有适配器快捷键文本
        /// </summary>
        public string EnableAllHotkeyText
        {
            get => Configuration?.GetHotkeyConfig(HotkeyAction.EnableAll)?.HotkeyDisplayText ?? "未设置";
            set => OnPropertyChanged();
        }

        /// <summary>
        /// 禁用所有适配器快捷键文本
        /// </summary>
        public string DisableAllHotkeyText
        {
            get => Configuration?.GetHotkeyConfig(HotkeyAction.DisableAll)?.HotkeyDisplayText ?? "未设置";
            set => OnPropertyChanged();
        }

        /// <summary>
        /// 切换适配器快捷键文本
        /// </summary>
        public string SwitchAdaptersHotkeyText
        {
            get => Configuration?.GetHotkeyConfig(HotkeyAction.SwitchAdapters)?.HotkeyDisplayText ?? "未设置";
            set => OnPropertyChanged();
        }

        #endregion

        #region 命令

        /// <summary>
        /// 保存设置命令
        /// </summary>
        public ICommand SaveCommand { get; private set; } = null!;

        /// <summary>
        /// 取消命令
        /// </summary>
        public ICommand CancelCommand { get; private set; } = null!;

        /// <summary>
        /// 重置为默认设置命令
        /// </summary>
        public ICommand ResetToDefaultCommand { get; private set; } = null!;

        /// <summary>
        /// 设置启用所有适配器快捷键命令
        /// </summary>
        public ICommand SetEnableAllHotkeyCommand { get; private set; } = null!;

        /// <summary>
        /// 设置禁用所有适配器快捷键命令
        /// </summary>
        public ICommand SetDisableAllHotkeyCommand { get; private set; } = null!;

        /// <summary>
        /// 设置切换适配器快捷键命令
        /// </summary>
        public ICommand SetSwitchAdaptersHotkeyCommand { get; private set; } = null!;

        /// <summary>
        /// 清除快捷键命令
        /// </summary>
        public ICommand ClearHotkeyCommand { get; private set; } = null!;

        /// <summary>
        /// 导入配置命令
        /// </summary>
        public ICommand ImportConfigCommand { get; private set; } = null!;

        /// <summary>
        /// 导出配置命令
        /// </summary>
        public ICommand ExportConfigCommand { get; private set; } = null!;

        /// <summary>
        /// 刷新适配器列表命令
        /// </summary>
        public ICommand RefreshAdaptersCommand { get; private set; } = null!;

        #endregion

        #region 事件

        /// <summary>
        /// 设置保存完成事件
        /// </summary>
        public event EventHandler? SettingsSaved;

        /// <summary>
        /// 设置取消事件
        /// </summary>
        public event EventHandler? SettingsCancelled;

        #endregion

        #region 私有方法

        /// <summary>
        /// 初始化命令
        /// </summary>
        private void InitializeCommands()
        {
            SaveCommand = new RelayCommand(async () => await SaveSettingsAsync());
            CancelCommand = new RelayCommand(() => CancelSettings());
            ResetToDefaultCommand = new RelayCommand(async () => await ResetToDefaultAsync());
            SetEnableAllHotkeyCommand = new RelayCommand<string>(async (hotkeyType) => await SetHotkeyAsync(hotkeyType));
            SetDisableAllHotkeyCommand = new RelayCommand<string>(async (hotkeyType) => await SetHotkeyAsync(hotkeyType));
            SetSwitchAdaptersHotkeyCommand = new RelayCommand<string>(async (hotkeyType) => await SetHotkeyAsync(hotkeyType));
            ClearHotkeyCommand = new RelayCommand<string>((hotkeyType) => ClearHotkey(hotkeyType));
            ImportConfigCommand = new RelayCommand(async () => await ImportConfigurationAsync());
            ExportConfigCommand = new RelayCommand(async () => await ExportConfigurationAsync());
            RefreshAdaptersCommand = new RelayCommand(async () => await LoadAdaptersAsync());
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfiguration()
        {
            try
            {
                Configuration = _configurationService.LoadConfiguration();
                
                // 从注册表读取实际的开机启动状态，确保UI与系统状态一致
                var actualStartupEnabled = StartupHelper.IsStartupEnabled();
                if (Configuration.StartWithWindows != actualStartupEnabled)
                {
                    Configuration.StartWithWindows = actualStartupEnabled;
                }
                
                StatusMessage = "配置加载完成";
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载配置失败: {ex.Message}";
                Configuration = new ApplicationConfig(); // 使用默认配置
            }
        }

        /// <summary>
        /// 加载适配器列表
        /// </summary>
        private async Task LoadAdaptersAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在加载网络适配器...";

                var result = await _networkService.GetAllAdaptersAsync();
                
                if (result.Success && result.Data != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        AvailableAdapters.Clear();
                        foreach (var adapter in result.Data)
                        {
                            AvailableAdapters.Add(adapter);
                        }

                        // 设置选中的适配器
                        if (!string.IsNullOrEmpty(Configuration.SelectedAdapterA))
                        {
                            SelectedAdapterA = AvailableAdapters.FirstOrDefault(a => a.DeviceId == Configuration.SelectedAdapterA);
                        }

                        if (!string.IsNullOrEmpty(Configuration.SelectedAdapterB))
                        {
                            SelectedAdapterB = AvailableAdapters.FirstOrDefault(a => a.DeviceId == Configuration.SelectedAdapterB);
                        }
                    });

                    StatusMessage = $"已加载 {result.Data.Count} 个网络适配器";
                }
                else
                {
                    StatusMessage = $"加载适配器失败: {result.Message}";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"加载适配器失败: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 保存设置
        /// </summary>
        private async Task SaveSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "正在保存设置...";

                // 验证配置
                if (!ValidateConfiguration())
                {
                    return;
                }

                // 保存配置
                await _configurationService.SaveConfigurationAsync(Configuration);

                // 更新快捷键
                await _hotkeyService.UpdateHotkeysAsync(Configuration);

                // 处理开机启动设置
                var startupResult = StartupHelper.SetStartup(Configuration.StartWithWindows);
                if (!startupResult.Success)
                {
                    StatusMessage = $"警告: {startupResult.Message}";
                    MessageBox.Show($"设置已保存，但开机启动配置失败:\n{startupResult.Message}", "警告", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                StatusMessage = "设置保存成功";
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                StatusMessage = $"保存设置失败: {ex.Message}";
                MessageBox.Show($"保存设置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 取消设置
        /// </summary>
        private void CancelSettings()
        {
            // 重新加载配置以撤销更改
            LoadConfiguration();
            StatusMessage = "已取消更改";
            SettingsCancelled?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 重置为默认设置
        /// </summary>
        private async Task ResetToDefaultAsync()
        {
            try
            {
                var result = MessageBox.Show("确定要重置为默认设置吗？这将清除所有自定义配置。", 
                    "确认重置", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    IsLoading = true;
                    StatusMessage = "正在重置设置...";

                    await _configurationService.ResetToDefaultAsync();
                    LoadConfiguration();
                    await LoadAdaptersAsync();

                    StatusMessage = "已重置为默认设置";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"重置设置失败: {ex.Message}";
                MessageBox.Show($"重置设置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 设置快捷键
        /// </summary>
        private async Task SetHotkeyAsync(string? hotkeyType)
        {
            if (string.IsNullOrEmpty(hotkeyType))
                return;
                
            try
            {
                // 获取当前快捷键
                string currentHotkey = GetCurrentHotkeyText(hotkeyType);
                
                // 获取操作名称用于冲突检测
                string actionName = GetActionName(hotkeyType);
                
                // 显示快捷键输入对话框
                var (success, hotkeyString) = NetworkAdapterHelper.HotkeyInputDialog.ShowHotkeyInputDialog(
                    Application.Current.MainWindow, currentHotkey, actionName);
                
                if (success && !string.IsNullOrEmpty(hotkeyString))
                {
                    // 解析快捷键字符串
                    var (modifierKeys, key) = ParseHotkeyString(hotkeyString);
                    
                    switch (hotkeyType?.ToLower())
                    {
                        case "enableall":
                            var enableAllConfig = Configuration.GetHotkeyConfig(HotkeyAction.EnableAll);
                            if (enableAllConfig != null)
                            {
                                enableAllConfig.ModifierKeys = modifierKeys;
                                enableAllConfig.Key = key;
                            }
                            OnPropertyChanged(nameof(EnableAllHotkeyText));
                            break;
                        case "disableall":
                            var disableAllConfig = Configuration.GetHotkeyConfig(HotkeyAction.DisableAll);
                            if (disableAllConfig != null)
                            {
                                disableAllConfig.ModifierKeys = modifierKeys;
                                disableAllConfig.Key = key;
                            }
                            OnPropertyChanged(nameof(DisableAllHotkeyText));
                            break;
                        case "switch":
                            var switchConfig = Configuration.GetHotkeyConfig(HotkeyAction.SwitchAdapters);
                            if (switchConfig != null)
                            {
                                switchConfig.ModifierKeys = modifierKeys;
                                switchConfig.Key = key;
                            }
                            OnPropertyChanged(nameof(SwitchAdaptersHotkeyText));
                            break;
                    }

                    StatusMessage = $"已设置{hotkeyType}快捷键: {hotkeyString}";
                    
                    // 立即保存配置并更新快捷键注册
                    try
                    {
                        await _configurationService.SaveConfigurationAsync(Configuration);
                        await _hotkeyService.UpdateHotkeysAsync(Configuration);
                        StatusMessage = $"快捷键 {hotkeyString} 设置成功并已生效";
                    }
                    catch (Exception saveEx)
                    {
                        StatusMessage = $"快捷键设置成功，但保存失败: {saveEx.Message}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"设置快捷键失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 清除快捷键
        /// </summary>
        private void ClearHotkey(string? hotkeyType)
        {
            if (string.IsNullOrEmpty(hotkeyType))
                return;
                
            try
            {
                switch (hotkeyType?.ToLower())
                {
                    case "enableall":
                        var enableAllConfig = Configuration.GetHotkeyConfig(HotkeyAction.EnableAll);
                        if (enableAllConfig != null)
                        {
                            enableAllConfig.ModifierKeys = Models.ModifierKeys.None;
                            enableAllConfig.Key = Key.None;
                        }
                        OnPropertyChanged(nameof(EnableAllHotkeyText));
                        break;
                    case "disableall":
                        var disableAllConfig = Configuration.GetHotkeyConfig(HotkeyAction.DisableAll);
                        if (disableAllConfig != null)
                        {
                            disableAllConfig.ModifierKeys = Models.ModifierKeys.None;
                            disableAllConfig.Key = Key.None;
                        }
                        OnPropertyChanged(nameof(DisableAllHotkeyText));
                        break;
                    case "switch":
                        var switchConfig = Configuration.GetHotkeyConfig(HotkeyAction.SwitchAdapters);
                        if (switchConfig != null)
                        {
                            switchConfig.ModifierKeys = Models.ModifierKeys.None;
                            switchConfig.Key = Key.None;
                        }
                        OnPropertyChanged(nameof(SwitchAdaptersHotkeyText));
                        break;
                }

                StatusMessage = $"已清除{hotkeyType}快捷键";
            }
            catch (Exception ex)
            {
                StatusMessage = $"清除快捷键失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 导入配置
        /// </summary>
        private async Task ImportConfigurationAsync()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "导入配置文件",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = "json"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    IsLoading = true;
                    StatusMessage = "正在导入配置...";

                    await _configurationService.ImportConfigurationAsync(openFileDialog.FileName);
                    LoadConfiguration();
                    await LoadAdaptersAsync();

                    StatusMessage = "配置导入成功";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"导入配置失败: {ex.Message}";
                MessageBox.Show($"导入配置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 导出配置
        /// </summary>
        private async Task ExportConfigurationAsync()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "导出配置文件",
                    Filter = "JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                    DefaultExt = "json",
                    FileName = $"NetworkAdapterHelper_Config_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    IsLoading = true;
                    StatusMessage = "正在导出配置...";

                    await _configurationService.ExportConfigurationAsync(saveFileDialog.FileName);

                    StatusMessage = "配置导出成功";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"导出配置失败: {ex.Message}";
                MessageBox.Show($"导出配置失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 验证配置
        /// </summary>
        private bool ValidateConfiguration()
        {
            if (SelectedAdapterA != null && SelectedAdapterB != null && 
                SelectedAdapterA.DeviceId == SelectedAdapterB.DeviceId)
            {
                MessageBox.Show("适配器A和适配器B不能是同一个适配器。", "配置错误", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 转换WPF修饰键到自定义修饰键
        /// </summary>
        /// <param name="wpfModifiers">WPF修饰键</param>
        /// <returns>自定义修饰键</returns>
        private Models.ModifierKeys ConvertToModifierKeys(System.Windows.Input.ModifierKeys wpfModifiers)
        {
            var result = Models.ModifierKeys.None;
            
            if (wpfModifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
                result |= Models.ModifierKeys.Control;
            if (wpfModifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt))
                result |= Models.ModifierKeys.Alt;
            if (wpfModifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift))
                result |= Models.ModifierKeys.Shift;
            if (wpfModifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows))
                result |= Models.ModifierKeys.Windows;
                
            return result;
        }

        /// <summary>
        /// 获取当前快捷键文本
        /// </summary>
        /// <param name="hotkeyType">快捷键类型</param>
        /// <returns>当前快捷键文本</returns>
        private string GetCurrentHotkeyText(string hotkeyType)
        {
            return hotkeyType?.ToLower() switch
            {
                "enableall" => EnableAllHotkeyText,
                "disableall" => DisableAllHotkeyText,
                "switch" => SwitchAdaptersHotkeyText,
                _ => string.Empty
            };
        }

        /// <summary>
        /// 获取操作名称
        /// </summary>
        /// <param name="hotkeyType">快捷键类型</param>
        /// <returns>操作名称</returns>
        private string GetActionName(string hotkeyType)
        {
            return hotkeyType?.ToLower() switch
            {
                "enableall" => "启用所有适配器",
                "disableall" => "禁用所有适配器",
                "switch" => "切换适配器状态",
                _ => ""
            };
        }

        /// <summary>
        /// 解析快捷键字符串
        /// </summary>
        /// <param name="hotkeyString">快捷键字符串，如 "Ctrl + F1"</param>
        /// <returns>修饰键和主键的元组</returns>
        private (Models.ModifierKeys modifierKeys, Key key) ParseHotkeyString(string hotkeyString)
        {
            var modifierKeys = Models.ModifierKeys.None;
            var key = Key.None;

            if (string.IsNullOrEmpty(hotkeyString))
                return (modifierKeys, key);

            var parts = hotkeyString.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                switch (part.Trim().ToLower())
                {
                    case "ctrl":
                        modifierKeys |= Models.ModifierKeys.Control;
                        break;
                    case "alt":
                        modifierKeys |= Models.ModifierKeys.Alt;
                        break;
                    case "shift":
                        modifierKeys |= Models.ModifierKeys.Shift;
                        break;
                    case "win":
                        modifierKeys |= Models.ModifierKeys.Windows;
                        break;
                    default:
                        // 尝试解析为主键
                        var trimmedPart = part.Trim();
                        
                        // 特殊处理数字键：将 "0"-"9" 转换为 Key.D0-Key.D9
                        if (trimmedPart.Length == 1 && char.IsDigit(trimmedPart[0]))
                        {
                            var digit = trimmedPart[0] - '0';
                            key = Key.D0 + digit;
                        }
                        else if (Enum.TryParse<Key>(trimmedPart, true, out var parsedKey))
                        {
                            key = parsedKey;
                        }
                        break;
                }
            }

            return (modifierKeys, key);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            // 清理资源
        }

        #endregion
    }

}