using System;
using System.Windows;
using NetworkAdapterHelper.Services;

namespace NetworkAdapterHelper
{
    /// <summary>
    /// 诊断对话框，用于显示快捷键钩子状态和执行诊断操作
    /// </summary>
    public partial class DiagnosticsDialog : Window
    {
        public DiagnosticsDialog()
        {
            InitializeComponent();
            Loaded += DiagnosticsDialog_Loaded;
        }

        private void DiagnosticsDialog_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        /// <summary>
        /// 刷新状态显示
        /// </summary>
        private void RefreshStatus()
        {
            try
            {
                var hookStatus = GlobalHotkeyHookService.Instance.GetHookStatus();
                HookStatusText.Text = $"{hookStatus}\n\n最后更新: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
            catch (Exception ex)
            {
                HookStatusText.Text = $"获取状态失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 重新安装钩子按钮点击
        /// </summary>
        private void ReinstallHook_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                GlobalHotkeyHookService.Instance.ForceReinstall();
                MessageBox.Show("钩子已重新安装", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"重新安装钩子失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 刷新状态按钮点击
        /// </summary>
        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            RefreshStatus();
        }

        /// <summary>
        /// 关闭按钮点击
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 显示诊断对话框
        /// </summary>
        public static void Show(Window? owner = null)
        {
            var dialog = new DiagnosticsDialog();
            if (owner != null)
            {
                dialog.Owner = owner;
            }
            dialog.ShowDialog();
        }
    }
}
