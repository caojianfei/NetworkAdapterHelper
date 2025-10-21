using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace NetworkAdapterHelper.Models
{
    /// <summary>
    /// 窗口位置信息
    /// </summary>
    public class WindowPosition : INotifyPropertyChanged
    {
        private double _x = 100;
        private double _y = 100;
        private double _width = 800;
        private double _height = 600;

        /// <summary>
        /// 窗口X坐标
        /// </summary>
        public double X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) > 0.1)
                {
                    _x = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 窗口Y坐标
        /// </summary>
        public double Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) > 0.1)
                {
                    _y = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 窗口宽度
        /// </summary>
        public double Width
        {
            get => _width;
            set
            {
                if (Math.Abs(_width - value) > 0.1)
                {
                    _width = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 窗口高度
        /// </summary>
        public double Height
        {
            get => _height;
            set
            {
                if (Math.Abs(_height - value) > 0.1)
                {
                    _height = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 属性更改事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发属性更改通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 应用程序配置数据模型
    /// </summary>
    public class ApplicationConfig : INotifyPropertyChanged
    {
        private bool _startWithWindows;
        private bool _minimizeToTray = true;
        private bool _minimizeToTrayOnClose = true;
        private bool _runAsAdministrator = true;
        private string _selectedAdapterA = string.Empty;
        private string _selectedAdapterB = string.Empty;
        private WindowPosition _windowPosition = new();
        private List<HotkeyConfig> _hotkeySettings = new();
        // 新增配置字段
        private bool _showNotifications = true;
        private bool _enableLogging = false;
        private int _refreshInterval = 60;

        /// <summary>
        /// 是否开机自启动
        /// </summary>
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set
            {
                if (_startWithWindows != value)
                {
                    _startWithWindows = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否最小化到系统托盘
        /// </summary>
        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                if (_minimizeToTray != value)
                {
                    _minimizeToTray = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 关闭窗口时是否最小化到托盘
        /// </summary>
        public bool MinimizeToTrayOnClose
        {
            get => _minimizeToTrayOnClose;
            set
            {
                if (_minimizeToTrayOnClose != value)
                {
                    _minimizeToTrayOnClose = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否以管理员身份运行
        /// </summary>
        public bool RunAsAdministrator
        {
            get => _runAsAdministrator;
            set
            {
                if (_runAsAdministrator != value)
                {
                    _runAsAdministrator = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 已配置的适配器A的设备ID
        /// </summary>
        public string SelectedAdapterA
        {
            get => _selectedAdapterA;
            set
            {
                if (_selectedAdapterA != value)
                {
                    _selectedAdapterA = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSwitchConfigured));
                }
            }
        }

        /// <summary>
        /// 已配置的适配器B的设备ID
        /// </summary>
        public string SelectedAdapterB
        {
            get => _selectedAdapterB;
            set
            {
                if (_selectedAdapterB != value)
                {
                    _selectedAdapterB = value ?? string.Empty;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsSwitchConfigured));
                }
            }
        }

        /// <summary>
        /// 窗口位置信息
        /// </summary>
        public WindowPosition WindowPosition
        {
            get => _windowPosition;
            set
            {
                if (_windowPosition != value)
                {
                    if (_windowPosition != null)
                    {
                        _windowPosition.PropertyChanged -= OnWindowPositionChanged;
                    }
                    
                    _windowPosition = value ?? new WindowPosition();
                    _windowPosition.PropertyChanged += OnWindowPositionChanged;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 快捷键设置列表
        /// </summary>
        public List<HotkeyConfig> HotkeySettings
        {
            get => _hotkeySettings;
            set
            {
                if (_hotkeySettings != value)
                {
                    // 取消订阅旧列表中项目的事件
                    if (_hotkeySettings != null)
                    {
                        foreach (var hotkey in _hotkeySettings)
                        {
                            hotkey.PropertyChanged -= OnHotkeyChanged;
                        }
                    }

                    _hotkeySettings = value ?? new List<HotkeyConfig>();
                    
                    // 订阅新列表中项目的事件
                    foreach (var hotkey in _hotkeySettings)
                    {
                        hotkey.PropertyChanged += OnHotkeyChanged;
                    }
                    
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否显示通知
        /// </summary>
        public bool ShowNotifications
        {
            get => _showNotifications;
            set
            {
                if (_showNotifications != value)
                {
                    _showNotifications = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否启用日志记录
        /// </summary>
        public bool EnableLogging
        {
            get => _enableLogging;
            set
            {
                if (_enableLogging != value)
                {
                    _enableLogging = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 刷新间隔（秒）
        /// </summary>
        public int RefreshInterval
        {
            get => _refreshInterval;
            set
            {
                if (_refreshInterval != value)
                {
                    _refreshInterval = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 检查是否已配置适配器切换
        /// </summary>
        public bool IsSwitchConfigured => 
            !string.IsNullOrEmpty(SelectedAdapterA) && 
            !string.IsNullOrEmpty(SelectedAdapterB) && 
            SelectedAdapterA != SelectedAdapterB;

        /// <summary>
        /// 属性更改事件
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 构造函数
        /// </summary>
        public ApplicationConfig()
        {
            WindowPosition.PropertyChanged += OnWindowPositionChanged;
            HotkeySettings = HotkeyConfig.CreateDefaultConfigs();
        }

        /// <summary>
        /// 添加一个默认的快捷键配置（示例方法，保留原逻辑）
        /// </summary>
        public void AddDefaultHotkey(HotkeyConfig config)
        {
            if (config != null)
            {
                HotkeySettings.Add(config);
            }
            OnPropertyChanged(nameof(HotkeySettings));
        }

        /// <summary>
        /// 验证配置的有效性
        /// </summary>
        /// <returns>配置验证结果</returns>
        public (bool IsValid, List<string> Errors) Validate()
        {
            var errors = new List<string>();

            // 检查窗口位置
            if (WindowPosition.Width < 400 || WindowPosition.Height < 300)
            {
                errors.Add("窗口尺寸过小，最小尺寸为400x300");
            }

            // 检查快捷键冲突
            var enabledHotkeys = HotkeySettings.Where(h => h.IsEnabled && h.IsValid).ToList();
            for (int i = 0; i < enabledHotkeys.Count; i++)
            {
                for (int j = i + 1; j < enabledHotkeys.Count; j++)
                {
                    if (enabledHotkeys[i].ModifierKeys == enabledHotkeys[j].ModifierKeys &&
                        enabledHotkeys[i].Key == enabledHotkeys[j].Key)
                    {
                        errors.Add($"快捷键冲突：{enabledHotkeys[i].ActionDisplayName} 和 {enabledHotkeys[j].ActionDisplayName}");
                    }
                }
            }

            // 检查适配器切换配置
            if (!string.IsNullOrEmpty(SelectedAdapterA) && SelectedAdapterA == SelectedAdapterB)
            {
                errors.Add("适配器A和适配器B不能是同一个设备");
            }

            // 刷新间隔范围校验（允许设置为0表示禁用定时刷新）
            if (RefreshInterval < 0)
            {
                errors.Add("刷新间隔不能为负数");
            }
            else if (RefreshInterval > 0 && (RefreshInterval < 5 || RefreshInterval > 3600))
            {
                errors.Add("刷新间隔必须在5到3600秒之间，或设为0以禁用");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// 触发属性更改通知
        /// </summary>
        /// <param name="propertyName">属性名称</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        /// <returns>默认应用程序配置</returns>
        public static ApplicationConfig CreateDefault()
        {
            return new ApplicationConfig
            {
                StartWithWindows = false,
                MinimizeToTray = true,
                RunAsAdministrator = true,
                SelectedAdapterA = string.Empty,
                SelectedAdapterB = string.Empty,
                WindowPosition = new WindowPosition
                {
                    X = 100,
                    Y = 100,
                    Width = 800,
                    Height = 600
                },
                HotkeySettings = HotkeyConfig.CreateDefaultConfigs(),
                ShowNotifications = true,
                EnableLogging = false,
                RefreshInterval = 60
            };
        }

        /// <summary>
        /// 窗口位置变化事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnWindowPositionChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(WindowPosition));
        }

        /// <summary>
        /// 快捷键配置变化事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnHotkeyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HotkeySettings));
        }

        /// <summary>
        /// 根据动作获取快捷键配置
        /// </summary>
        /// <param name="action">快捷键动作</param>
        /// <returns>对应的快捷键配置，如果未找到返回null</returns>
        public HotkeyConfig? GetHotkeyConfig(HotkeyAction action)
        {
            return HotkeySettings.FirstOrDefault(h => h.Action == action);
        }

        /// <summary>
        /// 更新快捷键配置
        /// </summary>
        /// <param name="config">要更新的快捷键配置</param>
        public void UpdateHotkeyConfig(HotkeyConfig config)
        {
            var existingConfig = HotkeySettings.FirstOrDefault(h => h.Id == config.Id);
            if (existingConfig != null)
            {
                existingConfig.ModifierKeys = config.ModifierKeys;
                existingConfig.Key = config.Key;
                existingConfig.IsEnabled = config.IsEnabled;
            }
            else
            {
                config.PropertyChanged += OnHotkeyChanged;
                HotkeySettings.Add(config);
            }
            OnPropertyChanged(nameof(HotkeySettings));
        }
    }
}