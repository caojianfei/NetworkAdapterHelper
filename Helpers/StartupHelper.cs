using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace NetworkAdapterHelper.Helpers
{
    /// <summary>
    /// 开机启动帮助类，负责管理应用程序的开机自启动
    /// </summary>
    public static class StartupHelper
    {
        private const string AppName = "NetworkAdapterHelper";
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        /// <param name="enable">是否启用开机自启动</param>
        /// <returns>操作结果</returns>
        public static (bool Success, string Message) SetStartup(bool enable)
        {
            try
            {
                // 获取当前应用程序的完整路径
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;
                
                if (string.IsNullOrEmpty(exePath))
                {
                    return (false, "无法获取应用程序路径");
                }

                // 打开注册表项（当前用户）
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null)
                    {
                        return (false, "无法打开注册表项");
                    }

                    if (enable)
                    {
                        // 添加到启动项，使用引号包裹路径以处理空格
                        key.SetValue(AppName, $"\"{exePath}\"");
                        return (true, "已成功添加到开机启动");
                    }
                    else
                    {
                        // 从启动项中移除
                        if (key.GetValue(AppName) != null)
                        {
                            key.DeleteValue(AppName, false);
                        }
                        return (true, "已成功从开机启动中移除");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine($"设置开机启动失败 - 权限不足: {ex.Message}");
                return (false, "权限不足，请以管理员身份运行应用程序");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"设置开机启动失败: {ex.Message}");
                return (false, $"设置失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查是否已设置开机自启动
        /// </summary>
        /// <returns>如果已设置返回true，否则返回false</returns>
        public static bool IsStartupEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null)
                    {
                        return false;
                    }

                    var value = key.GetValue(AppName);
                    return value != null && !string.IsNullOrEmpty(value.ToString());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"检查开机启动状态失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取注册表中的启动路径
        /// </summary>
        /// <returns>启动路径，如果未设置返回null</returns>
        public static string? GetStartupPath()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null)
                    {
                        return null;
                    }

                    var value = key.GetValue(AppName);
                    return value?.ToString();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"获取启动路径失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验证启动路径是否正确
        /// </summary>
        /// <returns>如果路径正确返回true，否则返回false</returns>
        public static bool ValidateStartupPath()
        {
            try
            {
                var currentPath = Process.GetCurrentProcess().MainModule?.FileName;
                var registryPath = GetStartupPath();

                if (string.IsNullOrEmpty(currentPath) || string.IsNullOrEmpty(registryPath))
                {
                    return false;
                }

                // 去除引号进行比较
                var cleanRegistryPath = registryPath.Trim('"');
                return string.Equals(currentPath, cleanRegistryPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"验证启动路径失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 修复启动路径（当应用程序位置改变时）
        /// </summary>
        /// <returns>操作结果</returns>
        public static (bool Success, string Message) FixStartupPath()
        {
            try
            {
                if (IsStartupEnabled())
                {
                    // 重新设置启动项以更新路径
                    return SetStartup(true);
                }
                return (true, "无需修复");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"修复启动路径失败: {ex.Message}");
                return (false, $"修复失败: {ex.Message}");
            }
        }
    }
}
