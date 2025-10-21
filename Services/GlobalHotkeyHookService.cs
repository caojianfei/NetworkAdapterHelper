using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using NetworkAdapterHelper.Models;

namespace NetworkAdapterHelper.Services
{
    /// <summary>
    /// 使用 Win32 低级键盘钩子实现的全局快捷键服务
    /// </summary>
    public class GlobalHotkeyHookService : IDisposable
    {
        #region Win32 API
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        // 虚拟键值
        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_LSHIFT = 0xA0;
        private const int VK_RSHIFT = 0xA1;
        private const int VK_LCONTROL = 0xA2;
        private const int VK_RCONTROL = 0xA3;
        private const int VK_LMENU = 0xA4;
        private const int VK_RMENU = 0xA5;
        #endregion

        private static readonly Lazy<GlobalHotkeyHookService> _instance = new(() => new GlobalHotkeyHookService());
        public static GlobalHotkeyHookService Instance => _instance.Value;

        private IntPtr _keyboardHookId = IntPtr.Zero;
        private LowLevelKeyboardProc? _keyboardProc;
        private readonly HashSet<int> _pressedVKeys = new();
        private List<HotkeyConfig> _configs = new();
        private bool _disposed;

        /// <summary>
        /// 快捷键触发事件
        /// </summary>
        public event EventHandler<HotkeyTriggeredEventArgs>? HotkeyTriggered;

        private GlobalHotkeyHookService() { }

        /// <summary>
        /// 初始化并安装全局低级键盘钩子
        /// </summary>
        public void Initialize()
        {
            if (_keyboardHookId != IntPtr.Zero) return;

            _keyboardProc = KeyboardHookCallback;

            var curProcess = Process.GetCurrentProcess();
            var curModule = curProcess.MainModule;
            IntPtr hModule = IntPtr.Zero;
            if (curModule != null)
            {
                hModule = GetModuleHandle(curModule.ModuleName!);
            }

            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, hModule, 0);

            if (_keyboardHookId == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                Debug.WriteLine($"安装全局键盘钩子失败，错误代码: {err}");
            }
        }

        /// <summary>
        /// 更新需要监听的快捷键配置
        /// </summary>
        public void UpdateHotkeys(IEnumerable<HotkeyConfig> configs)
        {
            _configs = configs?.Where(c => c.IsEnabled && c.IsValid).ToList() ?? new List<HotkeyConfig>();
        }

        /// <summary>
        /// 卸载钩子
        /// </summary>
        public void Disable()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
            }
            _pressedVKeys.Clear();
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                var kbStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                int vk = kbStruct.vkCode;

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    _pressedVKeys.Add(vk);

                    // 修饰键不触发主键判断
                    if (IsModifierVirtualKey(vk))
                    {
                        return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
                    }

                    var currentMods = GetCurrentModifierFlags();
                    var key = KeyInterop.KeyFromVirtualKey(vk);

                    foreach (var cfg in _configs)
                    {
                        if (cfg.Key == key && cfg.ModifierKeys == currentMods)
                        {
                            HotkeyTriggered?.Invoke(this, new HotkeyTriggeredEventArgs(cfg));
                            break;
                        }
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    _pressedVKeys.Remove(vk);
                }
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        private bool IsModifierVirtualKey(int vk)
        {
            return vk == VK_SHIFT || vk == VK_CONTROL || vk == VK_MENU ||
                   vk == VK_LWIN || vk == VK_RWIN ||
                   vk == VK_LSHIFT || vk == VK_RSHIFT ||
                   vk == VK_LCONTROL || vk == VK_RCONTROL ||
                   vk == VK_LMENU || vk == VK_RMENU;
        }

        private NetworkAdapterHelper.Models.ModifierKeys GetCurrentModifierFlags()
        {
            var mods = NetworkAdapterHelper.Models.ModifierKeys.None;

            if (_pressedVKeys.Contains(VK_CONTROL) || _pressedVKeys.Contains(VK_LCONTROL) || _pressedVKeys.Contains(VK_RCONTROL))
            {
                mods |= NetworkAdapterHelper.Models.ModifierKeys.Control;
            }
            if (_pressedVKeys.Contains(VK_MENU) || _pressedVKeys.Contains(VK_LMENU) || _pressedVKeys.Contains(VK_RMENU))
            {
                mods |= NetworkAdapterHelper.Models.ModifierKeys.Alt;
            }
            if (_pressedVKeys.Contains(VK_SHIFT) || _pressedVKeys.Contains(VK_LSHIFT) || _pressedVKeys.Contains(VK_RSHIFT))
            {
                mods |= NetworkAdapterHelper.Models.ModifierKeys.Shift;
            }
            if (_pressedVKeys.Contains(VK_LWIN) || _pressedVKeys.Contains(VK_RWIN))
            {
                mods |= NetworkAdapterHelper.Models.ModifierKeys.Windows;
            }

            return mods;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Disable();
        }
    }
}