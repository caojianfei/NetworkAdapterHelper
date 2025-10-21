using System;
using System.Windows;
using NetworkAdapterHelper.ViewModels;

namespace NetworkAdapterHelper
{
    /// <summary>
    /// 设置窗口类，负责显示应用程序设置界面
    /// </summary>
    public partial class SettingsWindow : Window
    {
        private readonly SettingsViewModel _viewModel;

        /// <summary>
        /// 初始化设置窗口
        /// </summary>
        public SettingsWindow()
        {
            InitializeComponent();
            
            // 初始化ViewModel
            _viewModel = new SettingsViewModel();
            DataContext = _viewModel;
            
            // 订阅ViewModel事件
            _viewModel.SettingsSaved += ViewModel_SettingsSaved;
            _viewModel.SettingsCancelled += ViewModel_SettingsCancelled;
            
            // 订阅窗口事件
            Loaded += SettingsWindow_Loaded;
            Closing += SettingsWindow_Closing;
        }

        /// <summary>
        /// 窗口加载完成事件处理
        /// </summary>
        private async void SettingsWindow_Loaded(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 设置窗口位置
                if (Owner != null)
                {
                    Left = Owner.Left + (Owner.Width - Width) / 2;
                    Top = Owner.Top + (Owner.Height - Height) / 2;
                }
                
                // 异步初始化ViewModel
                if (_viewModel != null)
                {
                    await _viewModel.InitializeAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"窗口初始化失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        private void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 取消订阅事件
                if (_viewModel != null)
                {
                    _viewModel.SettingsSaved -= ViewModel_SettingsSaved;
                    _viewModel.SettingsCancelled -= ViewModel_SettingsCancelled;
                }

                // 清理ViewModel
                _viewModel?.Dispose();
            }
            catch (Exception ex)
            {
                // 记录错误但不阻止窗口关闭
                System.Diagnostics.Debug.WriteLine($"设置窗口关闭时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置保存完成事件处理
        /// </summary>
        private void ViewModel_SettingsSaved(object? sender, EventArgs e)
        {
            try
            {
                // 显示成功消息
                MessageBox.Show("设置已保存成功！", "保存成功", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                // 设置对话框结果并关闭窗口
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存设置后处理失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 设置取消事件处理
        /// </summary>
        private void ViewModel_SettingsCancelled(object? sender, EventArgs e)
        {
            try
            {
                // 设置对话框结果并关闭窗口
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"取消设置时发生错误: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 显示设置窗口
        /// </summary>
        /// <param name="owner">父窗口</param>
        /// <returns>对话框结果</returns>
        public static bool? ShowSettingsDialog(Window? owner = null)
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                
                if (owner != null)
                {
                    settingsWindow.Owner = owner;
                }

                return settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示设置窗口失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 处理键盘快捷键
        /// </summary>
        protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
        {
            try
            {
                // Escape键关闭窗口
                if (e.Key == System.Windows.Input.Key.Escape)
                {
                    _viewModel?.CancelCommand?.Execute(null);
                    e.Handled = true;
                    return;
                }

                // Ctrl+S保存设置
                if (e.Key == System.Windows.Input.Key.S && 
                    (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
                {
                    _viewModel?.SaveCommand?.Execute(null);
                    e.Handled = true;
                    return;
                }

                base.OnKeyDown(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理键盘事件时发生错误: {ex.Message}");
                base.OnKeyDown(e);
            }
        }

        /// <summary>
        /// 获取当前配置的副本
        /// </summary>
        /// <returns>配置副本</returns>
        public Models.ApplicationConfig? GetCurrentConfiguration()
        {
            try
            {
                return _viewModel?.Configuration;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取当前配置时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 设置初始配置
        /// </summary>
        /// <param name="configuration">要设置的配置</param>
        public void SetInitialConfiguration(Models.ApplicationConfig configuration)
        {
            try
            {
                if (_viewModel != null && configuration != null)
                {
                    _viewModel.Configuration = configuration;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置初始配置时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 刷新适配器列表
        /// </summary>
        public async void RefreshAdaptersList()
        {
            try
            {
                if (_viewModel?.RefreshAdaptersCommand?.CanExecute(null) == true)
                {
                    await System.Threading.Tasks.Task.Run(() => 
                        _viewModel.RefreshAdaptersCommand.Execute(null));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新适配器列表失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 验证当前设置
        /// </summary>
        /// <returns>验证是否通过</returns>
        public bool ValidateCurrentSettings()
        {
            try
            {
                // 这里可以添加额外的验证逻辑
                return _viewModel?.Configuration != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"验证设置时发生错误: {ex.Message}");
                return false;
            }
        }
    }
}