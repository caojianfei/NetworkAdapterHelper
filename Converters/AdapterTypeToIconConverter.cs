using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;
using NetworkAdapterHelper.Models;
using System.Collections.ObjectModel;

namespace NetworkAdapterHelper
{
    /// <summary>
    /// 适配器类型到图标的转换器
    /// </summary>
    public class AdapterTypeToIconConverter : IValueConverter, IMultiValueConverter
    {
        /// <summary>
        /// 将适配器启用状态转换为颜色
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isEnabled)
            {
                return isEnabled ? Colors.Green : Colors.Red;
            }

            return Colors.Gray;
        }

        /// <summary>
        /// 反向转换（不支持）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 多值转换，用于计算启用的适配器数量
        /// </summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length > 0 && values[0] is ObservableCollection<NetworkAdapter> adapters)
            {
                return adapters.Count(a => a.IsEnabled);
            }

            return 0;
        }

        /// <summary>
        /// 多值反向转换（不支持）
        /// </summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 布尔值到可见性的转换器
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值转换为可见性
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
            }

            return System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// 反向转换
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
            {
                return visibility == System.Windows.Visibility.Visible;
            }

            return false;
        }
    }

    /// <summary>
    /// 反向布尔值到可见性的转换器
    /// </summary>
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将布尔值反向转换为可见性
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            }

            return System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// 反向转换
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is System.Windows.Visibility visibility)
            {
                return visibility == System.Windows.Visibility.Collapsed;
            }

            return true;
        }
    }

    /// <summary>
    /// 适配器类型到图标字符的转换器
    /// </summary>
    public class AdapterTypeToIconStringConverter : IValueConverter
    {
        /// <summary>
        /// 将适配器类型转换为图标字符
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string adapterType)
            {
                return adapterType.ToLower() switch
                {
                    "ethernet" => "🌐",
                    "wireless" => "📶",
                    "bluetooth" => "🔵",
                    "virtual" => "💻",
                    _ => "🔌"
                };
            }

            return "🔌";
        }

        /// <summary>
        /// 反向转换（不支持）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 适配器状态到颜色的转换器
    /// </summary>
    public class AdapterStatusToColorConverter : IValueConverter
    {
        /// <summary>
        /// 将适配器状态转换为颜色
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string status)
            {
                return status.ToLower() switch
                {
                    "connected" => new SolidColorBrush(Colors.Green),
                    "disconnected" => new SolidColorBrush(Colors.Orange),
                    "disabled" => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }

            return new SolidColorBrush(Colors.Gray);
        }

        /// <summary>
        /// 反向转换（不支持）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}