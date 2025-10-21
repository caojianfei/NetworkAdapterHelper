using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using NetworkAdapterHelper.Models;
using Application = System.Windows.Application;

namespace NetworkAdapterHelper.Services
{
    /// <summary>
    /// 系统托盘服务
    /// </summary>
    public class TrayService : IDisposable
    {
        private static readonly Lazy<TrayService> _instance = new(() => new TrayService());
        
        /// <summary>
        /// 获取服务单例实例
        /// </summary>
        public static TrayService Instance => _instance.Value;

        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private bool _disposed = false;

        /// <summary>
        /// 显示主窗口事件
        /// </summary>
        public event EventHandler? ShowMainWindow;

        /// <summary>
        /// 显示设置窗口事件
        /// </summary>
        public event EventHandler? ShowSettingsWindow;

        /// <summary>
        /// 启用所有适配器事件
        /// </summary>
        public event EventHandler? EnableAllAdapters;

        /// <summary>
        /// 禁用所有适配器事件
        /// </summary>
        public event EventHandler? DisableAllAdapters;

        /// <summary>
        /// 切换适配器事件
        /// </summary>
        public event EventHandler? SwitchAdapters;

        /// <summary>
        /// 退出应用程序事件
        /// </summary>
        public event EventHandler? ExitApplication;

        /// <summary>
        /// 私有构造函数，实现单例模式
        /// </summary>
        private TrayService()
        {
        }

        /// <summary>
        /// 初始化系统托盘
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Initialize()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    return OperationResult.CreateWarning("系统托盘已经初始化");
                }

                _notifyIcon = new NotifyIcon
                {
                    Icon = GetApplicationIcon(),
                    Text = "网络适配器助手",
                    Visible = true
                };

                // 创建右键菜单
                CreateContextMenu();

                // 绑定事件
                _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;
                _notifyIcon.ContextMenuStrip = _contextMenu;

                return OperationResult.CreateSuccess("系统托盘初始化成功");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("系统托盘初始化失败", ex);
            }
        }

        /// <summary>
        /// 显示托盘通知
        /// </summary>
        /// <param name="title">通知标题</param>
        /// <param name="message">通知消息</param>
        /// <param name="icon">通知图标类型</param>
        /// <param name="timeout">显示时间（毫秒）</param>
        /// <returns>操作结果</returns>
        public OperationResult ShowNotification(string title, string message, 
            System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info, int timeout = 3000)
        {
            try
            {
                // 配置控制：如果禁用通知，直接返回成功（不显示）
                var config = ConfigurationService.Instance.LoadConfiguration();
                if (!config.ShowNotifications)
                {
                    return OperationResult.CreateSuccess("通知已禁用");
                }

                if (_notifyIcon == null)
                {
                    return OperationResult.CreateError("系统托盘未初始化");
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    title = "网络适配器助手";
                }

                if (string.IsNullOrWhiteSpace(message))
                {
                    return OperationResult.CreateError("通知消息不能为空");
                }

                _notifyIcon.ShowBalloonTip(timeout, title, message, icon);
                return OperationResult.CreateSuccess("通知显示成功");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("显示通知失败", ex);
            }
        }

        /// <summary>
        /// 更新托盘图标状态
        /// </summary>
        /// <param name="isConnected">是否有网络连接</param>
        /// <returns>操作结果</returns>
        public OperationResult UpdateTrayIcon(bool isConnected)
        {
            try
            {
                if (_notifyIcon == null)
                {
                    return OperationResult.CreateError("系统托盘未初始化");
                }

                _notifyIcon.Icon = GetApplicationIcon(isConnected);
                _notifyIcon.Text = isConnected ? "网络适配器助手 - 已连接" : "网络适配器助手 - 未连接";

                return OperationResult.CreateSuccess("托盘图标更新成功");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("更新托盘图标失败", ex);
            }
        }

        /// <summary>
        /// 更新右键菜单状态
        /// </summary>
        /// <param name="config">应用程序配置</param>
        /// <returns>操作结果</returns>
        public OperationResult UpdateContextMenu(ApplicationConfig? config)
        {
            try
            {
                if (_contextMenu == null)
                {
                    return OperationResult.CreateError("右键菜单未初始化");
                }

                // 更新切换适配器菜单项的可用状态
                var switchMenuItem = FindMenuItem("switchAdapters");
                if (switchMenuItem != null && config != null)
                {
                    switchMenuItem.Enabled = config.IsSwitchConfigured;
                    switchMenuItem.Text = config.IsSwitchConfigured 
                        ? $"切换适配器 ({GetAdapterShortName(config.SelectedAdapterA)} ↔ {GetAdapterShortName(config.SelectedAdapterB)})"
                        : "切换适配器 (未配置)";
                }

                return OperationResult.CreateSuccess("右键菜单更新成功");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("更新右键菜单失败", ex);
            }
        }



        /// <summary>
        /// 显示托盘图标
        /// </summary>
        /// <returns>操作结果</returns>
        public OperationResult Show()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = true;
                }
                return OperationResult.CreateSuccess("托盘图标已显示");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("显示托盘图标失败", ex);
            }
        }

        /// <summary>
        /// 创建右键菜单
        /// </summary>
        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            // 显示主窗口
            var showMainMenuItem = new ToolStripMenuItem("显示主窗口", null, (s, e) => ShowMainWindow?.Invoke(this, EventArgs.Empty))
            {
                Name = "showMain",
                Font = new Font(_contextMenu.Font, System.Drawing.FontStyle.Bold)
            };
            _contextMenu.Items.Add(showMainMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 启用所有适配器
            var enableAllMenuItem = new ToolStripMenuItem("启用所有适配器", null, (s, e) => EnableAllAdapters?.Invoke(this, EventArgs.Empty))
            {
                Name = "enableAll"
            };
            _contextMenu.Items.Add(enableAllMenuItem);

            // 禁用所有适配器
            var disableAllMenuItem = new ToolStripMenuItem("禁用所有适配器", null, (s, e) => DisableAllAdapters?.Invoke(this, EventArgs.Empty))
            {
                Name = "disableAll"
            };
            _contextMenu.Items.Add(disableAllMenuItem);

            // 切换适配器
            var switchMenuItem = new ToolStripMenuItem("切换适配器", null, (s, e) => SwitchAdapters?.Invoke(this, EventArgs.Empty))
            {
                Name = "switchAdapters",
                Enabled = false
            };
            _contextMenu.Items.Add(switchMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 设置
            var settingsMenuItem = new ToolStripMenuItem("设置", null, (s, e) => ShowSettingsWindow?.Invoke(this, EventArgs.Empty))
            {
                Name = "settings"
            };
            _contextMenu.Items.Add(settingsMenuItem);

            _contextMenu.Items.Add(new ToolStripSeparator());

            // 退出
            var exitMenuItem = new ToolStripMenuItem("退出", null, (s, e) => ExitApplication?.Invoke(this, EventArgs.Empty))
            {
                Name = "exit"
            };
            _contextMenu.Items.Add(exitMenuItem);
        }

        /// <summary>
        /// 查找指定名称的菜单项
        /// </summary>
        /// <param name="name">菜单项名称</param>
        /// <returns>菜单项，如果未找到返回null</returns>
        private ToolStripMenuItem? FindMenuItem(string name)
        {
            if (_contextMenu == null)
                return null;

            foreach (ToolStripItem item in _contextMenu.Items)
            {
                if (item is ToolStripMenuItem menuItem && menuItem.Name == name)
                {
                    return menuItem;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取应用程序图标
        /// </summary>
        /// <param name="isConnected">是否已连接网络</param>
        /// <returns>应用程序图标</returns>
        private Icon GetApplicationIcon(bool isConnected = true)
        {
            try
            {
                // 首先尝试加载Resources目录下的app.ico文件（与应用程序图标一致）
                var appIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
                if (File.Exists(appIconPath))
                {
                    return new Icon(appIconPath);
                }

                // 尝试加载项目根目录下的icon.ico文件
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }

                // 尝试从嵌入资源中加载图标
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream("NetworkAdapterHelper.Resources.app.ico");
                if (stream != null)
                {
                    return new Icon(stream);
                }

                // 如果资源图标不存在，创建一个简单的图标
                return CreateDefaultIcon(isConnected);
            }
            catch
            {
                // 如果加载失败，创建默认图标
                return CreateDefaultIcon(isConnected);
            }
        }

        /// <summary>
        /// 创建默认图标
        /// </summary>
        /// <param name="isConnected">是否已连接</param>
        /// <returns>默认图标</returns>
        private Icon CreateDefaultIcon(bool isConnected)
        {
            try
            {
                // 创建一个16x16的位图
                using var bitmap = new Bitmap(16, 16);
                using var graphics = Graphics.FromImage(bitmap);
                
                // 设置背景色
                graphics.Clear(Color.Transparent);
                
                // 绘制简单的网络图标
                var color = isConnected ? Color.Green : Color.Red;
                using var brush = new SolidBrush(color);
                graphics.FillEllipse(brush, 2, 2, 12, 12);
                
                // 转换为图标
                var iconHandle = bitmap.GetHicon();
                return Icon.FromHandle(iconHandle);
            }
            catch
            {
                // 如果创建失败，返回系统默认图标
                return SystemIcons.Application;
            }
        }

        /// <summary>
        /// 获取适配器的简短名称
        /// </summary>
        /// <param name="adapterName">适配器全名</param>
        /// <returns>简短名称</returns>
        private string GetAdapterShortName(string? adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName))
                return "未知";

            // 提取适配器名称的关键部分
            if (adapterName.Contains("Wi-Fi") || adapterName.Contains("WLAN"))
                return "WiFi";
            if (adapterName.Contains("Ethernet") || adapterName.Contains("以太网"))
                return "以太网";
            if (adapterName.Contains("Bluetooth"))
                return "蓝牙";
            if (adapterName.Contains("Virtual") || adapterName.Contains("虚拟"))
                return "虚拟";

            // 如果名称太长，截取前10个字符
            return adapterName.Length > 10 ? adapterName.Substring(0, 10) + "..." : adapterName;
        }

        /// <summary>
        /// 托盘图标双击事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
        {
            ShowMainWindow?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源的具体实现
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 释放托盘图标
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        _notifyIcon = null;
                    }

                    // 释放右键菜单
                    if (_contextMenu != null)
                    {
                        _contextMenu.Dispose();
                        _contextMenu = null;
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~TrayService()
        {
            Dispose(false);
        }
    }
}