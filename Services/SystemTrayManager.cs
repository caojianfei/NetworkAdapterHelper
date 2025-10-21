using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows;
using NetworkAdapterHelper.Models;
using NetworkAdapterHelper.Services;

namespace NetworkAdapterHelper.Services
{
    /// <summary>
    /// 系统托盘管理器，负责处理系统托盘图标和右键菜单
    /// </summary>
    public class SystemTrayManager : IDisposable
    {
        private NotifyIcon? _notifyIcon;
        private ContextMenuStrip? _contextMenu;
        private readonly NetworkAdapterService _adapterService;
        private readonly HotkeyService _hotkeyService;
        private readonly ConfigurationService _configService;
        private bool _disposed = false;

        /// <summary>
        /// 主窗口显示事件
        /// </summary>
        public event EventHandler? ShowMainWindow;

        /// <summary>
        /// 设置窗口显示事件
        /// </summary>
        public event EventHandler? ShowSettingsWindow;

        /// <summary>
        /// 应用程序退出事件
        /// </summary>
        public event EventHandler? ExitApplication;

        /// <summary>
        /// 初始化系统托盘管理器
        /// </summary>
        /// <param name="adapterService">网络适配器服务</param>
        /// <param name="hotkeyService">快捷键服务</param>
        /// <param name="configService">配置服务</param>
        public SystemTrayManager(NetworkAdapterService adapterService, 
            HotkeyService hotkeyService, ConfigurationService configService)
        {
            _adapterService = adapterService ?? throw new ArgumentNullException(nameof(adapterService));
            _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));

            InitializeSystemTray();
        }

        /// <summary>
        /// 初始化系统托盘
        /// </summary>
        private void InitializeSystemTray()
        {
            try
            {
                _notifyIcon = new NotifyIcon
                {
                    Icon = CreateTrayIcon(),
                    Text = "网络适配器助手",
                    Visible = true
                };

                // 创建右键菜单
                CreateContextMenu();
                if (_contextMenu != null)
                {
                    _notifyIcon.ContextMenuStrip = _contextMenu;
                }

                // 绑定事件
                _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
                _notifyIcon.MouseClick += NotifyIcon_MouseClick;

                // 显示启动通知
                ShowNotification("网络适配器助手", "应用程序已启动，点击托盘图标进行操作", System.Windows.Forms.ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化系统托盘失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 创建托盘图标
        /// </summary>
        /// <returns>托盘图标</returns>
        private Icon CreateTrayIcon()
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

                // 如果icon.ico不存在，创建一个简单的网络图标
                using (var bitmap = new Bitmap(16, 16))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    
                    // 绘制网络图标
                    using (var pen = new Pen(Color.DodgerBlue, 2))
                    {
                        // 绘制网络连接线
                        graphics.DrawLine(pen, 2, 8, 6, 8);
                        graphics.DrawLine(pen, 10, 8, 14, 8);
                        graphics.DrawLine(pen, 8, 2, 8, 14);
                        
                        // 绘制节点
                        graphics.FillEllipse(Brushes.DodgerBlue, 1, 7, 2, 2);
                        graphics.FillEllipse(Brushes.DodgerBlue, 13, 7, 2, 2);
                        graphics.FillEllipse(Brushes.DodgerBlue, 7, 1, 2, 2);
                        graphics.FillEllipse(Brushes.DodgerBlue, 7, 13, 2, 2);
                    }
                    
                    return Icon.FromHandle(bitmap.GetHicon());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建托盘图标失败: {ex.Message}");
                // 返回默认图标
                return SystemIcons.Application;
            }
        }

        /// <summary>
        /// 创建右键菜单
        /// </summary>
        private void CreateContextMenu()
        {
            try
            {
                _contextMenu = new ContextMenuStrip();

                // 显示主窗口
                var showMainItem = new ToolStripMenuItem("显示主窗口", null, (s, e) => ShowMainWindow?.Invoke(this, EventArgs.Empty))
                {
                    Font = new Font(_contextMenu.Font, System.Drawing.FontStyle.Bold)
                };
                _contextMenu.Items.Add(showMainItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // 快速操作菜单
                var quickActionsItem = new ToolStripMenuItem("快速操作");
                
                var enableAllItem = new ToolStripMenuItem("启用所有适配器", null, async (s, e) => 
                {
                    try
                    {
                        await _adapterService.EnableAllAdaptersAsync();
                        ShowNotification("操作完成", "已启用所有网络适配器", System.Windows.Forms.ToolTipIcon.Info);
                    }
                    catch (Exception ex)
                    {
                        ShowNotification("操作失败", $"启用适配器失败: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
                    }
                });

                var disableAllItem = new ToolStripMenuItem("禁用所有适配器", null, async (s, e) => 
                {
                    try
                    {
                        await _adapterService.DisableAllAdaptersAsync();
                        ShowNotification("操作完成", "已禁用所有网络适配器", System.Windows.Forms.ToolTipIcon.Info);
                    }
                    catch (Exception ex)
                    {
                        ShowNotification("操作失败", $"禁用适配器失败: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
                    }
                });

                var switchAdaptersItem = new ToolStripMenuItem("切换适配器", null, async (s, e) => 
                {
                    try
                    {
                        var config = _configService.LoadConfiguration();
                        if (!string.IsNullOrEmpty(config.SelectedAdapterA) && !string.IsNullOrEmpty(config.SelectedAdapterB))
                        {
                            await _adapterService.SwitchAdaptersAsync(config.SelectedAdapterA, config.SelectedAdapterB);
                            ShowNotification("操作完成", "已切换网络适配器", System.Windows.Forms.ToolTipIcon.Info);
                        }
                        else
                        {
                            ShowNotification("配置错误", "请先在设置中配置适配器A和适配器B", System.Windows.Forms.ToolTipIcon.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        ShowNotification("操作失败", $"切换适配器失败: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
                    }
                });

                quickActionsItem.DropDownItems.AddRange(new ToolStripItem[] 
                { 
                    enableAllItem, 
                    disableAllItem, 
                    new ToolStripSeparator(),
                    switchAdaptersItem 
                });

                _contextMenu.Items.Add(quickActionsItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // 适配器列表菜单
                var adaptersItem = new ToolStripMenuItem("网络适配器");
                UpdateAdaptersMenu(adaptersItem);
                _contextMenu.Items.Add(adaptersItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // 设置
                var settingsItem = new ToolStripMenuItem("设置", null, (s, e) => ShowSettingsWindow?.Invoke(this, EventArgs.Empty));
                _contextMenu.Items.Add(settingsItem);

                // 刷新
                var refreshItem = new ToolStripMenuItem("刷新", null, (s, e) => 
                {
                    try
                    {
                        RefreshAdaptersMenu();
                        ShowNotification("刷新完成", "网络适配器列表已更新", System.Windows.Forms.ToolTipIcon.Info);
                    }
                    catch (Exception ex)
                    {
                        ShowNotification("刷新失败", $"刷新适配器列表失败: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
                    }
                });
                _contextMenu.Items.Add(refreshItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // 退出
                var exitItem = new ToolStripMenuItem("退出", null, (s, e) => ExitApplication?.Invoke(this, EventArgs.Empty));
                _contextMenu.Items.Add(exitItem);

                // 绑定菜单打开事件
                _contextMenu.Opening += ContextMenu_Opening;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建右键菜单失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 更新适配器菜单
        /// </summary>
        /// <param name="adaptersMenuItem">适配器菜单项</param>
        private async void UpdateAdaptersMenu(ToolStripMenuItem adaptersMenuItem)
        {
            try
            {
                adaptersMenuItem.DropDownItems.Clear();

                var result = await _adapterService.GetAllAdaptersAsync();
                
                if (result.Success && result.Data?.Count > 0)
                {
                    foreach (var adapter in result.Data)
                    {
                        var adapterItem = new ToolStripMenuItem(adapter.Name)
                        {
                            Checked = adapter.IsEnabled,
                            Tag = adapter
                        };

                        adapterItem.Click += async (s, e) =>
                        {
                            try
                            {
                                var menuItem = s as ToolStripMenuItem;
                                var adapterInfo = menuItem?.Tag as NetworkAdapter;
                                
                                if (adapterInfo != null)
                                {
                                    if (adapterInfo.IsEnabled)
                                    {
                                        await _adapterService.DisableAdapterAsync(adapterInfo.DeviceId);
                                        ShowNotification("适配器已禁用", $"已禁用 {adapterInfo.Name}", System.Windows.Forms.ToolTipIcon.Info);
                                    }
                                    else
                                    {
                                        await _adapterService.EnableAdapterAsync(adapterInfo.DeviceId);
                                        ShowNotification("适配器已启用", $"已启用 {adapterInfo.Name}", System.Windows.Forms.ToolTipIcon.Info);
                                    }
                                    
                                    // 刷新菜单
                                    RefreshAdaptersMenu();
                                }
                            }
                            catch (Exception ex)
                            {
                                ShowNotification("操作失败", $"切换适配器状态失败: {ex.Message}", System.Windows.Forms.ToolTipIcon.Error);
                            }
                        };

                        adaptersMenuItem.DropDownItems.Add(adapterItem);
                    }
                }
                else
                {
                    var noAdaptersItem = new ToolStripMenuItem("未找到网络适配器")
                    {
                        Enabled = false
                    };
                    adaptersMenuItem.DropDownItems.Add(noAdaptersItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新适配器菜单失败: {ex.Message}");
                
                var errorItem = new ToolStripMenuItem("加载失败")
                {
                    Enabled = false
                };
                adaptersMenuItem.DropDownItems.Clear();
                adaptersMenuItem.DropDownItems.Add(errorItem);
            }
        }

        /// <summary>
        /// 刷新适配器菜单
        /// </summary>
        private void RefreshAdaptersMenu()
        {
            try
            {
                if (_contextMenu == null) return;
                
                var adaptersMenuItem = _contextMenu.Items.Cast<ToolStripItem>()
                    .FirstOrDefault(item => item.Text == "网络适配器") as ToolStripMenuItem;
                
                if (adaptersMenuItem != null)
                {
                    UpdateAdaptersMenu(adaptersMenuItem);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新适配器菜单失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 右键菜单打开事件处理
        /// </summary>
        private void ContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // 刷新适配器菜单
                RefreshAdaptersMenu();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"菜单打开时刷新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 托盘图标双击事件处理
        /// </summary>
        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            try
            {
                ShowMainWindow?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理托盘图标双击事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 托盘图标鼠标点击事件处理
        /// </summary>
        private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
            try
            {
                if (e.Button == MouseButtons.Left)
                {
                    // 左键单击显示主窗口
                    ShowMainWindow?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理托盘图标点击事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 显示通知
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="text">内容</param>
        /// <param name="icon">图标类型</param>
        /// <param name="timeout">显示时间（毫秒）</param>
        public void ShowNotification(string title, string text, System.Windows.Forms.ToolTipIcon icon = System.Windows.Forms.ToolTipIcon.Info, int timeout = 3000)
        {
            try
            {
                // 配置控制：遵循 ShowNotifications 设置
                var cfg = _configService.LoadConfiguration();
                if (!cfg.ShowNotifications)
                {
                    return; // 禁用时直接返回
                }

                if (_notifyIcon != null && !_disposed)
                {
                    _notifyIcon.ShowBalloonTip(timeout, title, text, icon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"显示通知失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置托盘图标可见性
        /// </summary>
        /// <param name="visible">是否可见</param>
        public void SetVisible(bool visible)
        {
            try
            {
                if (_notifyIcon != null && !_disposed)
                {
                    _notifyIcon.Visible = visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置托盘图标可见性失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 更新托盘图标提示文本
        /// </summary>
        /// <param name="text">提示文本</param>
        public void UpdateTooltipText(string text)
        {
            try
            {
                if (_notifyIcon != null && !_disposed)
                {
                    _notifyIcon.Text = text?.Length > 63 ? text.Substring(0, 60) + "..." : text;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新托盘图标提示文本失败: {ex.Message}");
            }
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
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否正在释放</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                        _notifyIcon.Dispose();
                        _notifyIcon = null!;
                    }

                    if (_contextMenu != null)
                    {
                        _contextMenu.Dispose();
                        _contextMenu = null!;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"释放系统托盘资源失败: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~SystemTrayManager()
        {
            Dispose(false);
        }
    }
}