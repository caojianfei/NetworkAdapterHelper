using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NetworkAdapterHelper.Models
{
    /// <summary>
    /// 网络适配器类型枚举
    /// </summary>
    public enum AdapterType
    {
        /// <summary>
        /// 以太网适配器
        /// </summary>
        Ethernet,
        
        /// <summary>
        /// 无线网络适配器
        /// </summary>
        Wireless,
        
        /// <summary>
        /// 蓝牙网络适配器
        /// </summary>
        Bluetooth,
        
        /// <summary>
        /// 虚拟网络适配器
        /// </summary>
        Virtual,
        
        /// <summary>
        /// 其他类型适配器
        /// </summary>
        Other
    }

    /// <summary>
    /// 网络适配器数据模型
    /// </summary>
    public class NetworkAdapter : INotifyPropertyChanged
    {
        private bool _isEnabled;
        private string _status = string.Empty;
        private DateTime _lastUpdated;

        /// <summary>
        /// 设备ID，用于唯一标识网络适配器
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// 适配器名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 适配器描述信息
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 适配器友好名称（如"以太网"、"本地连接"等）
        /// </summary>
        public string FriendlyName { get; set; } = string.Empty;

        /// <summary>
        /// 适配器是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled != value)
                {
                    _isEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        /// <summary>
        /// 适配器类型
        /// </summary>
        public AdapterType Type { get; set; }

        /// <summary>
        /// 适配器状态描述
        /// </summary>
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set
            {
                if (_lastUpdated != value)
                {
                    _lastUpdated = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 获取状态显示文本
        /// </summary>
        public string StatusText => IsEnabled ? "已启用" : "已禁用";

        /// <summary>
        /// 获取状态颜色
        /// </summary>
        public string StatusColor => IsEnabled ? "#107C10" : "#D13438";

        /// <summary>
        /// 获取适配器类型图标
        /// </summary>
        public string TypeIcon
        {
            get
            {
                return Type switch
                {
                    AdapterType.Ethernet => "󰈀", // 以太网图标
                    AdapterType.Wireless => "󰖩", // WiFi图标
                    AdapterType.Bluetooth => "󰂯", // 蓝牙图标
                    AdapterType.Virtual => "󰌘", // 虚拟网络图标
                    _ => "󰈀" // 默认图标
                };
            }
        }

        /// <summary>
        /// 获取适配器类型显示名称
        /// </summary>
        public string TypeDisplayName
        {
            get
            {
                return Type switch
                {
                    AdapterType.Ethernet => "以太网",
                    AdapterType.Wireless => "无线网络",
                    AdapterType.Bluetooth => "蓝牙网络",
                    AdapterType.Virtual => "虚拟网络",
                    _ => "其他"
                };
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

        /// <summary>
        /// 重写ToString方法，用于调试和显示
        /// </summary>
        /// <returns>适配器的字符串表示</returns>
        public override string ToString()
        {
            return $"{Name} ({TypeDisplayName}) - {StatusText}";
        }

        /// <summary>
        /// 重写Equals方法，基于DeviceId进行比较
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回true，否则返回false</returns>
        public override bool Equals(object? obj)
        {
            if (obj is NetworkAdapter other)
            {
                return DeviceId.Equals(other.DeviceId, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>
        /// 重写GetHashCode方法
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return DeviceId.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }
}