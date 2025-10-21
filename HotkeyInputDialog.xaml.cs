using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using NetworkAdapterHelper.Models;

namespace NetworkAdapterHelper
{
    /// <summary>
    /// 快捷键输入对话框
    /// </summary>
    public partial class HotkeyInputDialog : Window
    {
        #region Win32 API 声明

        /// <summary>
        /// 键盘钩子委托
        /// </summary>
        /// <param name="nCode">钩子代码</param>
        /// <param name="wParam">wParam参数</param>
        /// <param name="lParam">lParam参数</param>
        /// <returns>处理结果</returns>
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 设置Windows钩子
        /// </summary>
        /// <param name="idHook">钩子类型</param>
        /// <param name="lpfn">钩子过程</param>
        /// <param name="hMod">模块句柄</param>
        /// <param name="dwThreadId">线程ID</param>
        /// <returns>钩子句柄</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        /// <summary>
        /// 卸载钩子
        /// </summary>
        /// <param name="hhk">钩子句柄</param>
        /// <returns>是否成功</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        /// <summary>
        /// 调用下一个钩子
        /// </summary>
        /// <param name="hhk">钩子句柄</param>
        /// <param name="nCode">钩子代码</param>
        /// <param name="wParam">wParam参数</param>
        /// <param name="lParam">lParam参数</param>
        /// <returns>处理结果</returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        /// <summary>
        /// 获取模块句柄
        /// </summary>
        /// <param name="lpModuleName">模块名称</param>
        /// <returns>模块句柄</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        #endregion

        #region 私有字段
        /// <summary>
        /// 当前按下的键集合
        /// </summary>
        private readonly HashSet<Key> _pressedKeys = new HashSet<Key>();

        /// <summary>
        /// 修饰键集合
        /// </summary>
        private readonly HashSet<Key> _modifierKeys = new HashSet<Key>();

        /// <summary>
        /// 已捕获的修饰键集合（保持状态）
        /// </summary>
        private readonly HashSet<Key> _capturedModifierKeys = new HashSet<Key>();

        /// <summary>
        /// 主键
        /// </summary>
        private Key? _mainKey;

        /// <summary>
        /// 是否已完成快捷键捕获
        /// </summary>
        private bool _isCapturComplete;

        /// <summary>
        /// 键盘钩子句柄
        /// </summary>
        private IntPtr _hookId = IntPtr.Zero;

        /// <summary>
        /// 键盘钩子过程
        /// </summary>
        private readonly LowLevelKeyboardProc _proc;

        #endregion

        /// <summary>
        /// 快捷键字符串
        /// </summary>
        public string HotkeyString { get; private set; } = string.Empty;

        /// <summary>
        /// 是否已设置快捷键
        /// </summary>
        public bool IsHotkeySet { get; private set; }

        /// <summary>
        /// 当前正在设置的操作类型
        /// </summary>
        public string CurrentAction { get; set; } = string.Empty;

        /// <summary>
        /// 当前快捷键字符串（用于显示）
        /// </summary>
        private string _currentHotkeyForDisplay = string.Empty;

        /// <summary>
        /// 构造函数
        /// </summary>
        public HotkeyInputDialog()
        {
            InitializeComponent();
            
            // 初始化键盘钩子过程
            _proc = HookCallback;
            
            InitializeDialog();
            
            // 绑定预览键盘事件到窗口，保证捕获所有按键
            this.PreviewKeyDown += HotkeyInputArea_KeyDown;
            this.PreviewKeyUp += HotkeyInputArea_KeyUp;
            
            // 绑定窗口关闭事件
            this.Closing += HotkeyInputDialog_Closing;
            
            // 设置键盘钩子以屏蔽系统快捷键
            SetHook();
        }

        /// <summary>
        /// 设置当前快捷键（用于显示）
        /// </summary>
        /// <param name="currentHotkey">当前快捷键字符串</param>
        public void SetCurrentHotkey(string currentHotkey)
        {
            _currentHotkeyForDisplay = currentHotkey ?? string.Empty;
            // 立即更新显示
            UpdateHotkeyDisplay();
        }

        /// <summary>
        /// 初始化对话框
        /// </summary>
        private void InitializeDialog()
        {
            try
            {
                // 设置窗口属性
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                Topmost = true;

                // 初始化显示
                UpdateHotkeyDisplay();
                
                // 设置焦点到窗口
                this.Focus();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化快捷键对话框失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 键盘按下事件
        /// </summary>
        private void HotkeyInputArea_KeyDown(object? sender, KeyEventArgs e)
        {
            try
            {
                e.Handled = true;
                
                var key = e.Key == Key.System ? e.SystemKey : e.Key;
                
                // 如果已经完成捕获，重新开始
                if (_isCapturComplete)
                {
                    ResetCapture();
                }
                
                // 处理修饰键
                if (IsModifierKey(key))
                {
                    if (!_modifierKeys.Contains(key))
                    {
                        _modifierKeys.Add(key);
                        _capturedModifierKeys.Add(key);
                        _pressedKeys.Add(key);
                        UpdateHotkeyDisplay();
                    }
                    return;
                }

                // 设置主键
                if (!_mainKey.HasValue)
                {
                    _mainKey = key;
                    _pressedKeys.Add(key);
                    
                    // 完成捕获
                    CompleteHotkeyCapture();
                    UpdateHotkeyDisplay();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理键盘按下事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 键盘释放事件
        /// </summary>
        private void HotkeyInputArea_KeyUp(object? sender, KeyEventArgs e)
        {
            try
            {
                e.Handled = true;
                
                var key = e.Key == Key.System ? e.SystemKey : e.Key;
                
                // 移除当前按下的键，但保持已捕获的状态
                if (_pressedKeys.Contains(key))
                {
                    _pressedKeys.Remove(key);
                    
                    if (IsModifierKey(key))
                    {
                        _modifierKeys.Remove(key);
                    }
                    
                    // 如果还没有完成捕获，更新显示
                    if (!_isCapturComplete)
                    {
                        UpdateHotkeyDisplay();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"处理键盘释放事件失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否为修饰键
        /// </summary>
        /// <param name="key">键值</param>
        /// <returns>是否为修饰键</returns>
        private bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl ||
                   key == Key.LeftAlt || key == Key.RightAlt ||
                   key == Key.LeftShift || key == Key.RightShift ||
                   key == Key.LWin || key == Key.RWin;
        }

        /// <summary>
        /// 更新快捷键显示
        /// </summary>
        private void UpdateHotkeyDisplay()
        {
            try
            {
                // 如果没有按键输入，显示当前快捷键或等待输入
                if (_pressedKeys.Count == 0 && !_mainKey.HasValue)
                {
                    if (!string.IsNullOrEmpty(_currentHotkeyForDisplay))
                    {
                        HotkeyDisplay.Text = $"当前: {_currentHotkeyForDisplay}";
                    }
                    else
                    {
                        HotkeyDisplay.Text = "等待输入...";
                    }
                    OkButton.IsEnabled = false;
                    return;
                }

                var displayText = BuildHotkeyDisplayText();
                HotkeyDisplay.Text = displayText;
                
                // 只有在有修饰键和主键时才启用确定按钮
                OkButton.IsEnabled = IsValidHotkeyCombo();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"更新快捷键显示失败: {ex.Message}");
                HotkeyDisplay.Text = "显示错误";
            }
        }

        /// <summary>
        /// 构建快捷键显示文本
        /// </summary>
        /// <returns>显示文本</returns>
        private string BuildHotkeyDisplayText()
        {
            try
            {
                var parts = new List<string>();

                // 使用已捕获的修饰键或当前按下的修饰键
                var modifiersToUse = _isCapturComplete ? _capturedModifierKeys : _modifierKeys;

                // 添加修饰键
                if (modifiersToUse.Contains(Key.LeftCtrl) || modifiersToUse.Contains(Key.RightCtrl))
                    parts.Add("Ctrl");
                if (modifiersToUse.Contains(Key.LeftAlt) || modifiersToUse.Contains(Key.RightAlt))
                    parts.Add("Alt");
                if (modifiersToUse.Contains(Key.LeftShift) || modifiersToUse.Contains(Key.RightShift))
                    parts.Add("Shift");
                if (modifiersToUse.Contains(Key.LWin) || modifiersToUse.Contains(Key.RWin))
                    parts.Add("Win");

                // 添加主键
                if (_mainKey.HasValue)
                {
                    parts.Add(GetKeyDisplayName(_mainKey.Value));
                }

                return string.Join(" + ", parts);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"构建快捷键显示文本失败: {ex.Message}");
                return "错误";
            }
        }

        /// <summary>
        /// 获取键的显示名称
        /// </summary>
        /// <param name="key">键值</param>
        /// <returns>显示名称</returns>
        private string GetKeyDisplayName(Key key)
        {
            var keyNames = new Dictionary<Key, string>
            {
                { Key.Space, "Space" },
                { Key.Enter, "Enter" },
                { Key.Tab, "Tab" },
                { Key.Escape, "Esc" },
                { Key.Back, "Backspace" },
                { Key.Delete, "Delete" },
                { Key.Insert, "Insert" },
                { Key.Home, "Home" },
                { Key.End, "End" },
                { Key.PageUp, "PageUp" },
                { Key.PageDown, "PageDown" },
                { Key.Up, "Up" },
                { Key.Down, "Down" },
                { Key.Left, "Left" },
                { Key.Right, "Right" },
                { Key.F1, "F1" }, { Key.F2, "F2" }, { Key.F3, "F3" }, { Key.F4, "F4" },
                { Key.F5, "F5" }, { Key.F6, "F6" }, { Key.F7, "F7" }, { Key.F8, "F8" },
                { Key.F9, "F9" }, { Key.F10, "F10" }, { Key.F11, "F11" }, { Key.F12, "F12" }
            };

            if (keyNames.TryGetValue(key, out string? displayName) && displayName != null)
                return displayName;

            // 数字键
            if (key >= Key.D0 && key <= Key.D9)
                return ((char)('0' + (key - Key.D0))).ToString();

            // 字母键
            if (key >= Key.A && key <= Key.Z)
                return key.ToString();

            // 默认返回键名
            return key.ToString();
        }

        /// <summary>
        /// 检查是否为有效的快捷键组合
        /// </summary>
        /// <returns>是否有效</returns>
        private bool IsValidHotkeyCombo()
        {
            // 必须有至少一个修饰键和一个主键
            var modifiersToCheck = _isCapturComplete ? _capturedModifierKeys : _modifierKeys;
            return modifiersToCheck.Count > 0 && _mainKey.HasValue;
        }

        /// <summary>
        /// 完成快捷键捕获
        /// </summary>
        private void CompleteHotkeyCapture()
        {
            try
            {
                if (IsValidHotkeyCombo())
                {
                    var hotkeyString = BuildHotkeyDisplayText();
                    
                    // 检测快捷键冲突
                    var conflictAction = DetectHotkeyConflict(hotkeyString, CurrentAction);
                    if (!string.IsNullOrEmpty(conflictAction))
                    {
                        // 显示冲突提示
                        var result = MessageBox.Show(
                            $"快捷键 '{hotkeyString}' 已被 '{conflictAction}' 使用。\n\n是否要覆盖现有的快捷键设置？",
                            "快捷键冲突",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);

                        if (result == MessageBoxResult.No)
                        {
                            // 用户选择不覆盖，重置捕获状态
                            ResetCapture();
                            return;
                        }
                    }

                    _isCapturComplete = true;
                    HotkeyString = hotkeyString;
                    IsHotkeySet = true;
                    OkButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"完成快捷键捕获时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置捕获状态
        /// </summary>
        private void ResetCapture()
        {
            try
            {
                _pressedKeys.Clear();
                _modifierKeys.Clear();
                _capturedModifierKeys.Clear();
                _mainKey = null;
                _isCapturComplete = false;
                HotkeyString = string.Empty;
                IsHotkeySet = false;
                OkButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"重置捕获状态时发生错误: {ex.Message}");
            }
        }

        #region 键盘钩子方法

        /// <summary>
        /// 设置键盘钩子
        /// </summary>
        private void SetHook()
        {
            try
            {
                using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    if (curModule != null)
                    {
                        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
                            GetModuleHandle(curModule.ModuleName), 0);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置键盘钩子失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 移除键盘钩子
        /// </summary>
        private void UnhookKeyboard()
        {
            try
            {
                if (_hookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookId);
                    _hookId = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"移除键盘钩子失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 键盘钩子回调函数
        /// </summary>
        /// <param name="nCode">钩子代码</param>
        /// <param name="wParam">wParam参数</param>
        /// <param name="lParam">lParam参数</param>
        /// <returns>处理结果</returns>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    // 检查是否为按键按下事件
                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        // 获取虚拟键码
                        int vkCode = Marshal.ReadInt32(lParam);
                        
                        // 屏蔽常见的系统快捷键
                        if (ShouldBlockSystemHotkey(vkCode))
                        {
                            // 阻止系统快捷键传递给系统
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"键盘钩子回调错误: {ex.Message}");
            }

            // 继续传递给下一个钩子
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 判断是否应该屏蔽系统快捷键
        /// </summary>
        /// <param name="vkCode">虚拟键码</param>
        /// <returns>是否屏蔽</returns>
        private bool ShouldBlockSystemHotkey(int vkCode)
        {
            // 获取当前修饰键状态
            bool isCtrlPressed = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) != 0;
            bool isAltPressed = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Alt) != 0;
            bool isShiftPressed = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Shift) != 0;
            bool isWinPressed = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.LWin) != 0 ||
                               (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.RWin) != 0;



            // 屏蔽 Alt+Tab, Alt+F4 等
            if (isAltPressed)
            {
                if (vkCode == 0x09 || // Tab
                    vkCode == 0x73)   // F4
                {
                    return true;
                }
            }

            // 屏蔽 Ctrl+Alt+Del, Ctrl+Shift+Esc 等
            if (isCtrlPressed && isAltPressed && vkCode == 0x2E) // Delete
            {
                return true;
            }

            if (isCtrlPressed && isShiftPressed && vkCode == 0x1B) // Escape
            {
                return true;
            }

            return false;
        }

        #endregion

        /// <summary>
        /// 窗口关闭事件处理
        /// </summary>
        /// <param name="sender">事件发送者</param>
        /// <param name="e">事件参数</param>
        private void HotkeyInputDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // 移除键盘钩子
            UnhookKeyboard();
        }

        /// <summary>
        /// 检测快捷键冲突
        /// </summary>
        /// <param name="hotkeyString">要检测的快捷键字符串</param>
        /// <param name="currentAction">当前设置的操作类型</param>
        /// <returns>冲突的操作名称，如果没有冲突返回null</returns>
        private string? DetectHotkeyConflict(string hotkeyString, string currentAction)
        {
            if (string.IsNullOrEmpty(hotkeyString))
                return null;

            try
            {
                // 获取配置服务
                var configService = Services.ConfigurationService.Instance;
                var config = configService.LoadConfiguration();

                // 遍历所有快捷键配置检查冲突
                foreach (var hotkeyConfig in config.HotkeySettings)
                {
                    // 获取操作名称
                    string actionName = hotkeyConfig.Action switch
                    {
                        HotkeyAction.EnableAll => "启用所有适配器",
                        HotkeyAction.DisableAll => "禁用所有适配器",
                        HotkeyAction.SwitchAdapters => "切换适配器状态",
                        _ => "未知操作"
                    };
                    
                    // 跳过当前正在设置的操作
                    if (actionName == currentAction)
                        continue;

                    // 构建已配置的快捷键字符串
                    var existingHotkeyString = BuildHotkeyString(hotkeyConfig);

                    // 检查是否冲突
                    if (!string.IsNullOrEmpty(existingHotkeyString) && 
                        string.Equals(hotkeyString, existingHotkeyString, StringComparison.OrdinalIgnoreCase))
                    {
                        return actionName;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检测快捷键冲突时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据快捷键配置构建快捷键字符串
        /// </summary>
        /// <param name="config">快捷键配置</param>
        /// <returns>快捷键字符串</returns>
        private string BuildHotkeyString(HotkeyConfig config)
        {
            if (config == null || config.Key == Key.None)
                return string.Empty;

            var parts = new List<string>();

            // 添加修饰键
            if (config.ModifierKeys.HasFlag(Models.ModifierKeys.Control))
                parts.Add("Ctrl");
            if (config.ModifierKeys.HasFlag(Models.ModifierKeys.Alt))
                parts.Add("Alt");
            if (config.ModifierKeys.HasFlag(Models.ModifierKeys.Shift))
                parts.Add("Shift");
            if (config.ModifierKeys.HasFlag(Models.ModifierKeys.Windows))
                parts.Add("Win");

            // 添加主键 - 使用GetKeyDisplayName保持显示一致性
            parts.Add(GetKeyDisplayName(config.Key));

            return string.Join(" + ", parts);
        }

        /// <summary>
        /// 确定按钮点击事件
        /// </summary>
        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (IsValidHotkeyCombo())
                {
                    HotkeyString = BuildHotkeyDisplayText();
                    IsHotkeySet = true;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("请输入有效的快捷键组合！", "提示", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"设置快捷键时发生错误: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 清除按钮点击事件
        /// </summary>
        private void ClearButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                // 重置所有状态
                ResetCapture();
                
                // 更新显示
                UpdateHotkeyDisplay();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清除快捷键时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消按钮点击事件
        /// </summary>
        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取消设置快捷键时发生错误: {ex.Message}");
                Close();
            }
        }

        /// <summary>
        /// 显示快捷键输入对话框
        /// </summary>
        /// <param name="owner">父窗口</param>
        /// <param name="currentHotkey">当前快捷键</param>
        /// <param name="actionName">操作名称，用于冲突检测</param>
        /// <returns>快捷键输入结果</returns>
        public static (bool success, string hotkey) ShowHotkeyInputDialog(Window? owner = null, string currentHotkey = "", string actionName = "")
        {
            try
            {
                var dialog = new HotkeyInputDialog();
                
                if (owner != null)
                {
                    dialog.Owner = owner;
                }

                // 设置当前操作名称
                dialog.CurrentAction = actionName;

                // 如果有当前快捷键，设置它
                if (!string.IsNullOrEmpty(currentHotkey))
                {
                    dialog.SetCurrentHotkey(currentHotkey);
                }

                var result = dialog.ShowDialog();
                
                if (result == true && dialog.IsHotkeySet)
                {
                    return (true, dialog.HotkeyString);
                }
                
                return (false, string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"显示快捷键输入对话框失败: {ex.Message}", "错误", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return (false, string.Empty);
            }
        }

        /// <summary>
        /// 验证快捷键字符串格式
        /// </summary>
        /// <param name="hotkeyString">快捷键字符串</param>
        /// <returns>是否有效</returns>
        public static bool IsValidHotkeyString(string hotkeyString)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hotkeyString))
                    return false;

                var parts = hotkeyString.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
                
                // 至少需要两个部分（修饰键 + 主键）
                if (parts.Length < 2)
                    return false;

                // 检查是否包含修饰键
                var modifiers = new[] { "Ctrl", "Alt", "Shift", "Win" };
                bool hasModifier = parts.Any(part => modifiers.Contains(part));
                
                return hasModifier;
            }
            catch
            {
                return false;
            }
        }
    }
}