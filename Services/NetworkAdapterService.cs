using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using NetworkAdapterHelper.Models;

namespace NetworkAdapterHelper.Services
{
    /// <summary>
    /// 网络适配器管理服务
    /// </summary>
    public class NetworkAdapterService : IDisposable
    {
        private static readonly Lazy<NetworkAdapterService> _instance = new(() => new NetworkAdapterService());
        
        /// <summary>
        /// 获取服务单例实例
        /// </summary>
        public static NetworkAdapterService Instance => _instance.Value;

        /// <summary>
        /// 网络适配器状态变化事件
        /// </summary>
        public event EventHandler<NetworkAdapter>? AdapterStateChanged;

        /// <summary>
        /// 私有构造函数，实现单例模式
        /// </summary>
        private NetworkAdapterService()
        {
        }

        /// <summary>
        /// 获取所有网络适配器
        /// </summary>
        /// <returns>网络适配器列表的操作结果</returns>
        public async Task<OperationResult<List<NetworkAdapter>>> GetAllAdaptersAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    var adapters = new List<NetworkAdapter>();
                    
                    // 使用WMI查询网络适配器
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT * FROM Win32_NetworkAdapter WHERE NetConnectionStatus IS NOT NULL");
                    
                    using var collection = searcher.Get();
                    
                    foreach (ManagementObject adapter in collection)
                    {
                        try
                        {
                            var networkAdapter = CreateNetworkAdapterFromWmi(adapter);
                            if (networkAdapter != null)
                            {
                                adapters.Add(networkAdapter);
                            }
                        }
                        catch (Exception ex)
                        {
                            // 记录单个适配器解析错误，但继续处理其他适配器
                            System.Diagnostics.Debug.WriteLine($"解析适配器时发生错误: {ex.Message}");
                        }
                    }
                    
                    // 按适配器类型和名称排序
                    adapters = adapters.OrderBy(a => a.Type).ThenBy(a => a.Name).ToList();
                    
                    return OperationResult<List<NetworkAdapter>>.CreateSuccess(adapters, $"成功获取 {adapters.Count} 个网络适配器");
                });
            }
            catch (Exception ex)
            {
                return OperationResult<List<NetworkAdapter>>.CreateError(
                    "获取网络适配器列表失败", ex, new List<string> { ex.Message });
            }
        }

        /// <summary>
        /// 启用指定的网络适配器
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> EnableAdapterAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                return OperationResult.CreateError("设备ID不能为空");
            }

            try
            {
                return await Task.Run(() =>
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'");
                    
                    using var collection = searcher.Get();
                    
                    foreach (ManagementObject adapter in collection)
                    {
                        try
                        {
                            var result = adapter.InvokeMethod("Enable", null);
                            var returnValue = Convert.ToUInt32(result);
                            
                            if (returnValue == 0)
                            {
                                var networkAdapter = CreateNetworkAdapterFromWmi(adapter);
                                if (networkAdapter != null)
                                {
                                    networkAdapter.IsEnabled = true;
                                    AdapterStateChanged?.Invoke(this, networkAdapter);
                                }
                                return OperationResult.CreateSuccess($"成功启用网络适配器: {adapter["Name"]}");
                            }
                            else
                            {
                                return OperationResult.CreateError($"启用网络适配器失败，错误代码: {returnValue}");
                            }
                        }
                        catch (Exception ex)
                        {
                            return OperationResult.CreateError($"启用网络适配器时发生异常: {ex.Message}", ex);
                        }
                    }
                    
                    return OperationResult.CreateError("未找到指定的网络适配器");
                });
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("启用网络适配器失败", ex);
            }
        }

        /// <summary>
        /// 禁用指定的网络适配器
        /// </summary>
        /// <param name="deviceId">设备ID</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> DisableAdapterAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
            {
                return OperationResult.CreateError("设备ID不能为空");
            }

            try
            {
                return await Task.Run(() =>
                {
                    using var searcher = new ManagementObjectSearcher(
                        $"SELECT * FROM Win32_NetworkAdapter WHERE DeviceID = '{deviceId.Replace("\\", "\\\\")}'");
                    
                    using var collection = searcher.Get();
                    
                    foreach (ManagementObject adapter in collection)
                    {
                        try
                        {
                            var result = adapter.InvokeMethod("Disable", null);
                            var returnValue = Convert.ToUInt32(result);
                            
                            if (returnValue == 0)
                            {
                                var networkAdapter = CreateNetworkAdapterFromWmi(adapter);
                                if (networkAdapter != null)
                                {
                                    networkAdapter.IsEnabled = false;
                                    AdapterStateChanged?.Invoke(this, networkAdapter);
                                }
                                return OperationResult.CreateSuccess($"成功禁用网络适配器: {adapter["Name"]}");
                            }
                            else
                            {
                                return OperationResult.CreateError($"禁用网络适配器失败，错误代码: {returnValue}");
                            }
                        }
                        catch (Exception ex)
                        {
                            return OperationResult.CreateError($"禁用网络适配器时发生异常: {ex.Message}", ex);
                        }
                    }
                    
                    return OperationResult.CreateError("未找到指定的网络适配器");
                });
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("禁用网络适配器失败", ex);
            }
        }

        /// <summary>
        /// 启用所有网络适配器
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> EnableAllAdaptersAsync()
        {
            try
            {
                var adaptersResult = await GetAllAdaptersAsync();
                if (!adaptersResult.Success || adaptersResult.Data == null)
                {
                    return OperationResult.CreateError("获取网络适配器列表失败");
                }

                var successCount = 0;
                var errorCount = 0;
                var errors = new List<string>();

                foreach (var adapter in adaptersResult.Data)
                {
                    if (!adapter.IsEnabled)
                    {
                        var result = await EnableAdapterAsync(adapter.DeviceId);
                        if (result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errors.Add($"{adapter.Name}: {result.Message}");
                        }
                    }
                }

                if (errorCount == 0)
                {
                    return OperationResult.CreateSuccess($"成功启用 {successCount} 个网络适配器");
                }
                else if (successCount > 0)
                {
                    return OperationResult.CreateWarning($"部分操作成功：启用 {successCount} 个，失败 {errorCount} 个");
                }
                else
                {
                    return OperationResult.CreateError("启用所有网络适配器失败", null, errors);
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("启用所有网络适配器时发生异常", ex);
            }
        }

        /// <summary>
        /// 禁用所有网络适配器
        /// </summary>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> DisableAllAdaptersAsync()
        {
            try
            {
                var adaptersResult = await GetAllAdaptersAsync();
                if (!adaptersResult.Success || adaptersResult.Data == null)
                {
                    return OperationResult.CreateError("获取网络适配器列表失败");
                }

                var successCount = 0;
                var errorCount = 0;
                var errors = new List<string>();

                foreach (var adapter in adaptersResult.Data)
                {
                    if (adapter.IsEnabled)
                    {
                        var result = await DisableAdapterAsync(adapter.DeviceId);
                        if (result.Success)
                        {
                            successCount++;
                        }
                        else
                        {
                            errorCount++;
                            errors.Add($"{adapter.Name}: {result.Message}");
                        }
                    }
                }

                if (errorCount == 0)
                {
                    return OperationResult.CreateSuccess($"成功禁用 {successCount} 个网络适配器");
                }
                else if (successCount > 0)
                {
                    return OperationResult.CreateWarning($"部分操作成功：禁用 {successCount} 个，失败 {errorCount} 个");
                }
                else
                {
                    return OperationResult.CreateError("禁用所有网络适配器失败", null, errors);
                }
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("禁用所有网络适配器时发生异常", ex);
            }
        }

        /// <summary>
        /// 在两个指定的适配器之间进行切换
        /// </summary>
        /// <param name="adapterAId">适配器A的设备ID</param>
        /// <param name="adapterBId">适配器B的设备ID</param>
        /// <returns>操作结果</returns>
        public async Task<OperationResult> SwitchAdaptersAsync(string adapterAId, string adapterBId)
        {
            if (string.IsNullOrEmpty(adapterAId) || string.IsNullOrEmpty(adapterBId))
            {
                return OperationResult.CreateError("适配器ID不能为空");
            }

            if (adapterAId == adapterBId)
            {
                return OperationResult.CreateError("不能在同一个适配器之间切换");
            }

            try
            {
                var adaptersResult = await GetAllAdaptersAsync();
                if (!adaptersResult.Success || adaptersResult.Data == null)
                {
                    return OperationResult.CreateError("获取网络适配器列表失败");
                }

                var adapterA = adaptersResult.Data.FirstOrDefault(a => a.DeviceId == adapterAId);
                var adapterB = adaptersResult.Data.FirstOrDefault(a => a.DeviceId == adapterBId);

                if (adapterA == null || adapterB == null)
                {
                    return OperationResult.CreateError("未找到指定的网络适配器");
                }

                // 确定切换逻辑：如果A启用，则禁用A启用B；如果B启用，则禁用B启用A
                OperationResult result1, result2;
                
                if (adapterA.IsEnabled)
                {
                    // A启用，切换到B
                    result1 = await DisableAdapterAsync(adapterAId);
                    await Task.Delay(1000); // 等待适配器状态稳定
                    result2 = await EnableAdapterAsync(adapterBId);
                    
                    if (result1.Success && result2.Success)
                    {
                        return OperationResult.CreateSuccess($"成功从 {adapterA.Name} 切换到 {adapterB.Name}");
                    }
                }
                else if (adapterB.IsEnabled)
                {
                    // B启用，切换到A
                    result1 = await DisableAdapterAsync(adapterBId);
                    await Task.Delay(1000); // 等待适配器状态稳定
                    result2 = await EnableAdapterAsync(adapterAId);
                    
                    if (result1.Success && result2.Success)
                    {
                        return OperationResult.CreateSuccess($"成功从 {adapterB.Name} 切换到 {adapterA.Name}");
                    }
                }
                else
                {
                    // 两个都未启用，默认启用A
                    result2 = await EnableAdapterAsync(adapterAId);
                    if (result2.Success)
                    {
                        return OperationResult.CreateSuccess($"成功启用 {adapterA.Name}");
                    }
                }

                return OperationResult.CreateError("适配器切换失败");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("适配器切换时发生异常", ex);
            }
        }

        /// <summary>
        /// 从WMI对象创建NetworkAdapter实例
        /// </summary>
        /// <param name="wmiObject">WMI管理对象</param>
        /// <returns>NetworkAdapter实例，如果创建失败返回null</returns>
        private NetworkAdapter? CreateNetworkAdapterFromWmi(ManagementObject wmiObject)
        {
            try
            {
                var deviceId = wmiObject["DeviceID"]?.ToString();
                var name = wmiObject["Name"]?.ToString();
                var description = wmiObject["Description"]?.ToString();
                var friendlyName = wmiObject["NetConnectionID"]?.ToString();
                
                if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(name))
                {
                    return null;
                }

                // 判断适配器类型
                var adapterType = DetermineAdapterType(name, description);
                
                // 获取适配器状态
                var netConnectionStatus = wmiObject["NetConnectionStatus"];
                var isEnabled = netConnectionStatus != null && Convert.ToUInt16(netConnectionStatus) == 2;
                
                var status = GetConnectionStatusText(netConnectionStatus);

                return new NetworkAdapter
                {
                    DeviceId = deviceId,
                    Name = name,
                    Description = description ?? string.Empty,
                    FriendlyName = friendlyName ?? string.Empty,
                    IsEnabled = isEnabled,
                    Type = adapterType,
                    Status = status,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建NetworkAdapter时发生错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 根据适配器名称和描述确定适配器类型
        /// </summary>
        /// <param name="name">适配器名称</param>
        /// <param name="description">适配器描述</param>
        /// <returns>适配器类型</returns>
        private AdapterType DetermineAdapterType(string name, string? description)
        {
            var fullText = $"{name} {description}".ToLowerInvariant();

            if (fullText.Contains("wi-fi") || fullText.Contains("wireless") || fullText.Contains("wlan") || 
                fullText.Contains("802.11") || fullText.Contains("wifi"))
            {
                return AdapterType.Wireless;
            }
            
            if (fullText.Contains("bluetooth") || fullText.Contains("bt"))
            {
                return AdapterType.Bluetooth;
            }
            
            if (fullText.Contains("virtual") || fullText.Contains("vmware") || fullText.Contains("virtualbox") ||
                fullText.Contains("hyper-v") || fullText.Contains("tap") || fullText.Contains("vpn"))
            {
                return AdapterType.Virtual;
            }
            
            if (fullText.Contains("ethernet") || fullText.Contains("realtek") || fullText.Contains("intel") ||
                fullText.Contains("gigabit") || fullText.Contains("fast ethernet"))
            {
                return AdapterType.Ethernet;
            }

            return AdapterType.Other;
        }

        /// <summary>
        /// 获取连接状态文本
        /// </summary>
        /// <param name="netConnectionStatus">网络连接状态值</param>
        /// <returns>状态文本</returns>
        private string GetConnectionStatusText(object? netConnectionStatus)
        {
            if (netConnectionStatus == null)
                return "未知";

            var status = Convert.ToUInt16(netConnectionStatus);
            return status switch
            {
                0 => "已断开",
                1 => "正在连接",
                2 => "已连接",
                3 => "正在断开",
                4 => "硬件不存在",
                5 => "硬件已禁用",
                6 => "硬件故障",
                7 => "媒体断开",
                8 => "正在验证",
                9 => "验证成功",
                10 => "验证失败",
                11 => "无效地址",
                12 => "需要凭据",
                _ => "未知状态"
            };
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            // 清理事件订阅
            AdapterStateChanged = null;
        }
    }
}