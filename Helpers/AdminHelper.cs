using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace NetworkAdapterHelper.Helpers
{
    /// <summary>
    /// 管理员权限帮助类，提供权限检查和提升功能
    /// </summary>
    public static class AdminHelper
    {
        /// <summary>
        /// 检查当前进程是否以管理员权限运行
        /// </summary>
        /// <returns>如果以管理员权限运行返回true，否则返回false</returns>
        public static bool IsRunningAsAdministrator()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查管理员权限时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 以管理员权限重新启动应用程序
        /// </summary>
        /// <param name="arguments">启动参数</param>
        /// <returns>如果成功启动返回true，否则返回false</returns>
        public static bool RestartAsAdministrator(string arguments = "")
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule?.FileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas", // 以管理员权限运行
                    WorkingDirectory = Environment.CurrentDirectory
                };

                Process.Start(processInfo);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"以管理员权限重启应用程序失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查并提示用户提升权限
        /// </summary>
        /// <param name="showDialog">是否显示提示对话框</param>
        /// <returns>如果已有管理员权限或用户同意提升权限返回true，否则返回false</returns>
        public static bool EnsureAdministratorPrivileges(bool showDialog = true)
        {
            try
            {
                if (IsRunningAsAdministrator())
                {
                    return true;
                }

                if (showDialog)
                {
                    var result = MessageBox.Show(
                        "网络适配器助手需要管理员权限才能正常工作。\n" +
                        "• 启用和禁用网络适配器\n" +
                        "• 注册全局快捷键\n" +
                        "• 访问系统网络配置\n\n" +
                        "是否要以管理员权限重新启动应用程序？",
                        "需要管理员权限",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (RestartAsAdministrator())
                        {
                            Application.Current.Shutdown();
                            return true;
                        }
                        else
                        {
                            MessageBox.Show(
                                "无法以管理员权限启动应用程序。\n" +
                                "请右键点击应用程序图标，选择\"以管理员身份运行\"。",
                                "权限提升失败",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            return false;
                        }
                    }
                    else
                    {
                        MessageBox.Show(
                            "应用程序将在受限模式下运行。\n" +
                            "某些功能可能无法正常工作。\n\n" +
                            "要获得完整功能，请以管理员身份运行应用程序。",
                            "受限模式",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return false;
                    }
                }
                else
                {
                    return RestartAsAdministrator();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"确保管理员权限时发生错误: {ex.Message}");
                
                if (showDialog)
                {
                    MessageBox.Show(
                        $"检查管理员权限时发生错误: {ex.Message}",
                        "错误",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                
                return false;
            }
        }

        /// <summary>
        /// 获取当前用户的权限级别描述
        /// </summary>
        /// <returns>权限级别描述字符串</returns>
        public static string GetCurrentPrivilegeLevel()
        {
            try
            {
                if (IsRunningAsAdministrator())
                {
                    return "管理员权限";
                }
                else
                {
                    using (var identity = WindowsIdentity.GetCurrent())
                    {
                        var principal = new WindowsPrincipal(identity);
                        
                        if (principal.IsInRole(WindowsBuiltInRole.PowerUser))
                        {
                            return "高级用户权限";
                        }
                        else if (principal.IsInRole(WindowsBuiltInRole.User))
                        {
                            return "标准用户权限";
                        }
                        else
                        {
                            return "受限用户权限";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取权限级别时发生错误: {ex.Message}");
                return "未知权限级别";
            }
        }

        /// <summary>
        /// 检查是否可以执行需要管理员权限的操作
        /// </summary>
        /// <param name="operationName">操作名称</param>
        /// <param name="showWarning">是否显示警告</param>
        /// <returns>如果可以执行返回true，否则返回false</returns>
        public static bool CanPerformAdminOperation(string operationName = "此操作", bool showWarning = true)
        {
            try
            {
                if (IsRunningAsAdministrator())
                {
                    return true;
                }

                if (showWarning)
                {
                    MessageBox.Show(
                        $"{operationName}需要管理员权限。\n\n" +
                        "请以管理员身份运行应用程序后重试。",
                        "权限不足",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查管理员操作权限时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 验证当前进程的完整性级别
        /// </summary>
        /// <returns>完整性级别描述</returns>
        public static string GetIntegrityLevel()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    
                    if (IsRunningAsAdministrator())
                    {
                        return "高完整性级别 (管理员)";
                    }
                    else if (principal.IsInRole(WindowsBuiltInRole.User))
                    {
                        return "中等完整性级别 (标准用户)";
                    }
                    else
                    {
                        return "低完整性级别 (受限用户)";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取完整性级别时发生错误: {ex.Message}");
                return "未知完整性级别";
            }
        }

        /// <summary>
        /// 检查UAC（用户账户控制）是否启用
        /// </summary>
        /// <returns>如果UAC启用返回true，否则返回false</returns>
        public static bool IsUacEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("EnableLUA");
                        return value != null && (int)value == 1;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"检查UAC状态时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取详细的权限状态信息
        /// </summary>
        /// <returns>权限状态信息</returns>
        public static string GetDetailedPrivilegeInfo()
        {
            try
            {
                var info = new System.Text.StringBuilder();
                
                info.AppendLine($"当前权限级别: {GetCurrentPrivilegeLevel()}");
                info.AppendLine($"完整性级别: {GetIntegrityLevel()}");
                info.AppendLine($"UAC状态: {(IsUacEnabled() ? "已启用" : "已禁用")}");
                info.AppendLine($"进程ID: {Process.GetCurrentProcess().Id}");
                
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    info.AppendLine($"用户名: {identity.Name}");
                    info.AppendLine($"认证类型: {identity.AuthenticationType}");
                }

                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"获取权限信息失败: {ex.Message}";
            }
        }
    }
}