using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NetworkAdapterHelper.Models;

namespace NetworkAdapterHelper.Services
{
    /// <summary>
    /// 配置管理服务
    /// </summary>
    public class ConfigurationService
    {
        private static readonly Lazy<ConfigurationService> _instance = new(() => new ConfigurationService());
        
        /// <summary>
        /// 获取服务单例实例
        /// </summary>
        public static ConfigurationService Instance => _instance.Value;

        private readonly string _configDirectory;
        private readonly string _configFilePath;
        private readonly JsonSerializerOptions _jsonOptions;

        /// <summary>
        /// 配置变更事件
        /// </summary>
        public event EventHandler<ApplicationConfig>? ConfigurationChanged;

        /// <summary>
        /// 私有构造函数，实现单例模式
        /// </summary>
        private ConfigurationService()
        {
            // 配置文件存储在用户的AppData目录
            _configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NetworkAdapterHelper");
            
            _configFilePath = Path.Combine(_configDirectory, "config.json");

            // 配置JSON序列化选项
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        /// <summary>
        /// 加载应用程序配置（同步版本，避免死锁）
        /// </summary>
        /// <returns>应用程序配置</returns>
        public ApplicationConfig LoadConfiguration()
        {
            try
            {
                // 确保配置目录存在
                EnsureConfigDirectoryExists();

                if (!File.Exists(_configFilePath))
                {
                    return ApplicationConfig.CreateDefault();
                }

                var json = File.ReadAllText(_configFilePath, System.Text.Encoding.UTF8);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return ApplicationConfig.CreateDefault();
                }

                var config = JsonSerializer.Deserialize<ApplicationConfig>(json, _jsonOptions);
                return config ?? ApplicationConfig.CreateDefault();
            }
            catch
            {
                return ApplicationConfig.CreateDefault();
            }
        }

        /// <summary>
        /// 加载应用程序配置
        /// </summary>
        /// <returns>配置加载结果</returns>
        public async Task<OperationResult<ApplicationConfig>> LoadConfigurationAsync()
        {
            try
            {
                // 确保配置目录存在
                EnsureConfigDirectoryExists();

                // 如果配置文件不存在，创建默认配置
                if (!File.Exists(_configFilePath))
                {
                    var defaultConfig = ApplicationConfig.CreateDefault();
                    var saveResult = await SaveConfigurationAsync(defaultConfig);
                    
                    if (saveResult.Success)
                    {
                        return OperationResult<ApplicationConfig>.CreateSuccess(
                            defaultConfig, "已创建默认配置文件");
                    }
                    else
                    {
                        return OperationResult<ApplicationConfig>.CreateError(
                            "创建默认配置文件失败", saveResult.Exception);
                    }
                }

                // 读取配置文件
                var jsonContent = await File.ReadAllTextAsync(_configFilePath);
                
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    var defaultConfig = ApplicationConfig.CreateDefault();
                    return OperationResult<ApplicationConfig>.CreateSuccess(
                        defaultConfig, "配置文件为空，使用默认配置");
                }

                // 反序列化配置
                var config = JsonSerializer.Deserialize<ApplicationConfig>(jsonContent, _jsonOptions);
                
                if (config == null)
                {
                    var defaultConfig = ApplicationConfig.CreateDefault();
                    return OperationResult<ApplicationConfig>.CreateSuccess(
                        defaultConfig, "配置文件格式错误，使用默认配置");
                }

                // 验证配置
                var (isValid, errors) = config.Validate();
                if (!isValid)
                {
                    return OperationResult<ApplicationConfig>.CreateWarning(
                        $"配置验证警告: {string.Join(", ", errors)}", config);
                }

                return OperationResult<ApplicationConfig>.CreateSuccess(
                    config, "配置加载成功");
            }
            catch (JsonException ex)
            {
                var defaultConfig = ApplicationConfig.CreateDefault();
                return OperationResult<ApplicationConfig>.CreateWarning(
                    $"配置文件格式错误，使用默认配置: {ex.Message}", defaultConfig);
            }
            catch (Exception ex)
            {
                return OperationResult<ApplicationConfig>.CreateError(
                    "加载配置失败", ex);
            }
        }

        /// <summary>
        /// 保存应用程序配置
        /// </summary>
        /// <param name="config">要保存的配置</param>
        /// <returns>保存结果</returns>
        public async Task<OperationResult> SaveConfigurationAsync(ApplicationConfig config)
        {
            if (config == null)
            {
                return OperationResult.CreateError("配置对象不能为空");
            }

            try
            {
                // 确保配置目录存在
                EnsureConfigDirectoryExists();

                // 验证配置
                var (isValid, errors) = config.Validate();
                if (!isValid)
                {
                    return OperationResult.CreateError($"配置验证失败: {string.Join(", ", errors)}");
                }

                // 序列化配置
                var jsonContent = JsonSerializer.Serialize(config, _jsonOptions);

                // 创建备份文件
                await CreateBackupAsync();

                // 保存配置文件
                await File.WriteAllTextAsync(_configFilePath, jsonContent);

                // 触发配置变更事件
                ConfigurationChanged?.Invoke(this, config);

                return OperationResult.CreateSuccess("配置保存成功");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("保存配置失败", ex);
            }
        }

        /// <summary>
        /// 更新快捷键配置
        /// </summary>
        /// <param name="hotkeyConfig">快捷键配置</param>
        /// <returns>更新结果</returns>
        public async Task<OperationResult> UpdateHotkeyConfigAsync(HotkeyConfig hotkeyConfig)
        {
            if (hotkeyConfig == null)
            {
                return OperationResult.CreateError("快捷键配置不能为空");
            }

            try
            {
                var configResult = await LoadConfigurationAsync();
                if (!configResult.Success || configResult.Data == null)
                {
                    return OperationResult.CreateError("加载当前配置失败");
                }

                var config = configResult.Data;
                config.UpdateHotkeyConfig(hotkeyConfig);

                return await SaveConfigurationAsync(config);
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("更新快捷键配置失败", ex);
            }
        }

        /// <summary>
        /// 重置配置为默认值
        /// </summary>
        /// <returns>重置结果</returns>
        public async Task<OperationResult<ApplicationConfig>> ResetToDefaultAsync()
        {
            try
            {
                var defaultConfig = ApplicationConfig.CreateDefault();
                var saveResult = await SaveConfigurationAsync(defaultConfig);

                if (saveResult.Success)
                {
                    return OperationResult<ApplicationConfig>.CreateSuccess(
                        defaultConfig, "配置已重置为默认值");
                }
                else
                {
                    return OperationResult<ApplicationConfig>.CreateError(
                        "重置配置失败", saveResult.Exception);
                }
            }
            catch (Exception ex)
            {
                return OperationResult<ApplicationConfig>.CreateError(
                    "重置配置时发生异常", ex);
            }
        }

        /// <summary>
        /// 导出配置到指定文件
        /// </summary>
        /// <param name="filePath">导出文件路径</param>
        /// <returns>导出结果</returns>
        public async Task<OperationResult> ExportConfigurationAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return OperationResult.CreateError("导出文件路径不能为空");
            }

            try
            {
                var configResult = await LoadConfigurationAsync();
                if (!configResult.Success || configResult.Data == null)
                {
                    return OperationResult.CreateError("加载当前配置失败");
                }

                var jsonContent = JsonSerializer.Serialize(configResult.Data, _jsonOptions);
                await File.WriteAllTextAsync(filePath, jsonContent);

                return OperationResult.CreateSuccess($"配置已导出到: {filePath}");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("导出配置失败", ex);
            }
        }

        /// <summary>
        /// 从指定文件导入配置
        /// </summary>
        /// <param name="filePath">导入文件路径</param>
        /// <returns>导入结果</returns>
        public async Task<OperationResult<ApplicationConfig>> ImportConfigurationAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return OperationResult<ApplicationConfig>.CreateError("导入文件路径不能为空");
            }

            if (!File.Exists(filePath))
            {
                return OperationResult<ApplicationConfig>.CreateError("导入文件不存在");
            }

            try
            {
                var jsonContent = await File.ReadAllTextAsync(filePath);
                var config = JsonSerializer.Deserialize<ApplicationConfig>(jsonContent, _jsonOptions);

                if (config == null)
                {
                    return OperationResult<ApplicationConfig>.CreateError("导入文件格式错误");
                }

                var (isValid, errors) = config.Validate();
                if (!isValid)
                {
                    return OperationResult<ApplicationConfig>.CreateError(
                        $"导入的配置验证失败: {string.Join(", ", errors)}");
                }

                var saveResult = await SaveConfigurationAsync(config);
                if (saveResult.Success)
                {
                    return OperationResult<ApplicationConfig>.CreateSuccess(
                        config, "配置导入成功");
                }
                else
                {
                    return OperationResult<ApplicationConfig>.CreateError(
                        "保存导入的配置失败", saveResult.Exception);
                }
            }
            catch (JsonException ex)
            {
                return OperationResult<ApplicationConfig>.CreateError(
                    "导入文件格式错误", ex);
            }
            catch (Exception ex)
            {
                return OperationResult<ApplicationConfig>.CreateError(
                    "导入配置失败", ex);
            }
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        /// <returns>配置文件路径</returns>
        public string GetConfigFilePath()
        {
            return _configFilePath;
        }

        /// <summary>
        /// 检查配置文件是否存在
        /// </summary>
        /// <returns>如果存在返回true，否则返回false</returns>
        public bool ConfigFileExists()
        {
            return File.Exists(_configFilePath);
        }

        /// <summary>
        /// 获取配置文件大小
        /// </summary>
        /// <returns>文件大小（字节），如果文件不存在返回0</returns>
        public long GetConfigFileSize()
        {
            if (!File.Exists(_configFilePath))
                return 0;

            try
            {
                var fileInfo = new FileInfo(_configFilePath);
                return fileInfo.Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 获取配置文件最后修改时间
        /// </summary>
        /// <returns>最后修改时间，如果文件不存在返回null</returns>
        public DateTime? GetConfigFileLastModified()
        {
            if (!File.Exists(_configFilePath))
                return null;

            try
            {
                return File.GetLastWriteTime(_configFilePath);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 确保配置目录存在
        /// </summary>
        private void EnsureConfigDirectoryExists()
        {
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }
        }

        /// <summary>
        /// 创建配置文件备份
        /// </summary>
        /// <returns>备份任务</returns>
        private async Task CreateBackupAsync()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var backupPath = _configFilePath + ".backup";
                    var content = await File.ReadAllTextAsync(_configFilePath);
                    await File.WriteAllTextAsync(backupPath, content);
                }
            }
            catch (Exception ex)
            {
                // 备份失败不影响主要功能，只记录日志
                System.Diagnostics.Debug.WriteLine($"创建配置备份失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 恢复配置文件备份
        /// </summary>
        /// <returns>恢复结果</returns>
        public async Task<OperationResult> RestoreBackupAsync()
        {
            try
            {
                var backupPath = _configFilePath + ".backup";
                
                if (!File.Exists(backupPath))
                {
                    return OperationResult.CreateError("备份文件不存在");
                }

                var content = await File.ReadAllTextAsync(backupPath);
                await File.WriteAllTextAsync(_configFilePath, content);

                return OperationResult.CreateSuccess("配置备份恢复成功");
            }
            catch (Exception ex)
            {
                return OperationResult.CreateError("恢复配置备份失败", ex);
            }
        }
    }
}