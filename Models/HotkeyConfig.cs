using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NetworkAdapterHelper.Models
{
    /// <summary>
    /// 快捷键动作枚举
    /// </summary>
    public enum HotkeyAction
    {
        /// <summary>
        /// 启用所有适配器
        /// </summary>
        EnableAll,
        
        /// <summary>
        /// 禁用所有适配器
        /// </summary>
        DisableAll,
        
        /// <summary>
        /// 切换适配器
        /// </summary>
        SwitchAdapters
    }

    /// <summary>
    /// 修饰键枚举
    /// </summary>
    [Flags]
    public enum ModifierKeys
    {
        /// <summary>
        /// 无修饰键
        /// </summary>
        None = 0,
        
        /// <summary>
        /// Alt键
        /// </summary>
        Alt = 1,
        
        /// <summary>
        /// Ctrl键
        /// </summary>
        Control = 2,
        
        /// <summary>
        /// Shift键
        /// </summary>
        Shift = 4,
        
        /// <summary>
        /// Windows键
        /// </summary>
        Windows = 8
    }

    /// <summary>
    /// 快捷键配置数据模型
    /// </summary>
    public class HotkeyConfig : INotifyPropertyChanged
    {
        private int _id;
        private HotkeyAction _action;
        private ModifierKeys _modifierKeys;
        private Key _key;
        private bool _isEnabled;

        /// <summary>
        /// 快捷键配置ID
        /// </summary>
        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 快捷键动作
        /// </summary>
        public HotkeyAction Action
        {
            get => _action;
            set
            {
                if (_action != value)
                {
                    _action = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActionDisplayName));
                    OnPropertyChanged(nameof(ActionDescription));
                }
            }
        }

        /// <summary>
        /// 修饰键组合
        /// </summary>
        public ModifierKeys ModifierKeys
        {
            get => _modifierKeys;
            set
            {
                if (_modifierKeys != value)
                {
                    _modifierKeys = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HotkeyDisplayText));
                }
            }
        }

        /// <summary>
        /// 主键
        /// </summary>
        public Key Key
        {
            get => _key;
            set
            {
                if (_key != value)
                {
                    _key = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HotkeyDisplayText));
                }
            }
        }

        /// <summary>
        /// 是否启用此快捷键
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
                }
            }
        }

        /// <summary>
        /// 获取动作显示名称
        /// </summary>
        public string ActionDisplayName
        {
            get
            {
                return Action switch
                {
                    HotkeyAction.EnableAll => "全部启用",
                    HotkeyAction.DisableAll => "全部禁用",
                    HotkeyAction.SwitchAdapters => "网络切换",
                    _ => "未知动作"
                };
            }
        }

        /// <summary>
        /// 获取动作描述
        /// </summary>
        public string ActionDescription
        {
            get
            {
                return Action switch
                {
                    HotkeyAction.EnableAll => "启用所有网络适配器",
                    HotkeyAction.DisableAll => "禁用所有网络适配器",
                    HotkeyAction.SwitchAdapters => "在配置的两个适配器间切换",
                    _ => "未知动作"
                };
            }
        }

        /// <summary>
        /// 获取快捷键显示文本
        /// </summary>
        public string HotkeyDisplayText
        {
            get
            {
                var parts = new List<string>();

                if (ModifierKeys.HasFlag(Models.ModifierKeys.Control))
                    parts.Add("Ctrl");
                if (ModifierKeys.HasFlag(Models.ModifierKeys.Alt))
                    parts.Add("Alt");
                if (ModifierKeys.HasFlag(Models.ModifierKeys.Shift))
                    parts.Add("Shift");
                if (ModifierKeys.HasFlag(Models.ModifierKeys.Windows))
                    parts.Add("Win");

                if (Key != Key.None)
                    parts.Add(Key.ToString());

                return string.Join(" + ", parts);
            }
        }

        /// <summary>
        /// 检查快捷键是否有效
        /// </summary>
        public bool IsValid => ModifierKeys != Models.ModifierKeys.None && Key != Key.None;

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
        /// 重写ToString方法
        /// </summary>
        /// <returns>快捷键配置的字符串表示</returns>
        public override string ToString()
        {
            return $"{ActionDisplayName}: {HotkeyDisplayText}";
        }

        /// <summary>
        /// 重写Equals方法
        /// </summary>
        /// <param name="obj">要比较的对象</param>
        /// <returns>如果相等返回true，否则返回false</returns>
        public override bool Equals(object? obj)
        {
            if (obj is HotkeyConfig other)
            {
                return Id == other.Id;
            }
            return false;
        }

        /// <summary>
        /// 重写GetHashCode方法
        /// </summary>
        /// <returns>哈希码</returns>
        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        /// <summary>
        /// 创建默认的快捷键配置
        /// </summary>
        /// <returns>默认快捷键配置列表</returns>
        public static List<HotkeyConfig> CreateDefaultConfigs()
        {
            return new List<HotkeyConfig>
            {
                new HotkeyConfig
                {
                    Id = 1,
                    Action = HotkeyAction.EnableAll,
                    ModifierKeys = Models.ModifierKeys.Control | Models.ModifierKeys.Alt,
                    Key = Key.F1,
                    IsEnabled = true
                },
                new HotkeyConfig
                {
                    Id = 2,
                    Action = HotkeyAction.DisableAll,
                    ModifierKeys = Models.ModifierKeys.Control | Models.ModifierKeys.Alt,
                    Key = Key.F2,
                    IsEnabled = true
                },
                new HotkeyConfig
                {
                    Id = 3,
                    Action = HotkeyAction.SwitchAdapters,
                    ModifierKeys = Models.ModifierKeys.Control | Models.ModifierKeys.Alt,
                    Key = Key.F3,
                    IsEnabled = true
                }
            };
        }
    }
}