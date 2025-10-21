using System;
using System.Collections.Generic;

namespace NetworkAdapterHelper.Models
{
    /// <summary>
    /// 操作结果类型枚举
    /// </summary>
    public enum OperationResultType
    {
        /// <summary>
        /// 操作成功
        /// </summary>
        Success,
        
        /// <summary>
        /// 操作失败
        /// </summary>
        Error,
        
        /// <summary>
        /// 操作警告
        /// </summary>
        Warning,
        
        /// <summary>
        /// 操作信息
        /// </summary>
        Information
    }

    /// <summary>
    /// 操作结果数据模型
    /// </summary>
    /// <typeparam name="T">结果数据类型</typeparam>
    public class OperationResult<T>
    {
        /// <summary>
        /// 操作是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果类型
        /// </summary>
        public OperationResultType Type { get; set; }

        /// <summary>
        /// 操作消息
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// 操作结果数据
        /// </summary>
        public T? Data { get; set; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 操作时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 额外的错误详情
        /// </summary>
        public List<string> ErrorDetails { get; set; } = new();

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="data">结果数据</param>
        /// <param name="message">成功消息</param>
        /// <returns>成功的操作结果</returns>
        public static OperationResult<T> CreateSuccess(T? data = default, string message = "操作成功")
        {
            return new OperationResult<T>
            {
                Success = true,
                Type = OperationResultType.Success,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常信息</param>
        /// <param name="errorDetails">错误详情</param>
        /// <returns>失败的操作结果</returns>
        public static OperationResult<T> CreateError(string message, Exception? exception = null, List<string>? errorDetails = null)
        {
            return new OperationResult<T>
            {
                Success = false,
                Type = OperationResultType.Error,
                Message = message,
                Exception = exception,
                ErrorDetails = errorDetails ?? new List<string>()
            };
        }

        /// <summary>
        /// 创建警告结果
        /// </summary>
        /// <param name="message">警告消息</param>
        /// <param name="data">结果数据</param>
        /// <returns>警告的操作结果</returns>
        public static OperationResult<T> CreateWarning(string message, T? data = default)
        {
            return new OperationResult<T>
            {
                Success = true,
                Type = OperationResultType.Warning,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// 创建信息结果
        /// </summary>
        /// <param name="message">信息消息</param>
        /// <param name="data">结果数据</param>
        /// <returns>信息的操作结果</returns>
        public static OperationResult<T> CreateInfo(string message, T? data = default)
        {
            return new OperationResult<T>
            {
                Success = true,
                Type = OperationResultType.Information,
                Message = message,
                Data = data
            };
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns>操作结果的字符串表示</returns>
        public override string ToString()
        {
            var status = Success ? "成功" : "失败";
            return $"[{Type}] {status}: {Message}";
        }
    }

    /// <summary>
    /// 无泛型的操作结果类
    /// </summary>
    public class OperationResult : OperationResult<object>
    {
        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <param name="message">成功消息</param>
        /// <returns>成功的操作结果</returns>
        public static OperationResult CreateSuccess(string message = "操作成功")
        {
            return new OperationResult
            {
                Success = true,
                Type = OperationResultType.Success,
                Message = message
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        /// <param name="message">错误消息</param>
        /// <param name="exception">异常信息</param>
        /// <param name="errorDetails">错误详情</param>
        /// <returns>失败的操作结果</returns>
        public static new OperationResult CreateError(string message, Exception? exception = null, List<string>? errorDetails = null)
        {
            return new OperationResult
            {
                Success = false,
                Type = OperationResultType.Error,
                Message = message,
                Exception = exception,
                ErrorDetails = errorDetails ?? new List<string>()
            };
        }

        /// <summary>
        /// 创建警告结果
        /// </summary>
        /// <param name="message">警告消息</param>
        /// <returns>警告的操作结果</returns>
        public static OperationResult CreateWarning(string message)
        {
            return new OperationResult
            {
                Success = true,
                Type = OperationResultType.Warning,
                Message = message
            };
        }

        /// <summary>
        /// 创建信息结果
        /// </summary>
        /// <param name="message">信息消息</param>
        /// <returns>信息的操作结果</returns>
        public static OperationResult CreateInfo(string message)
        {
            return new OperationResult
            {
                Success = true,
                Type = OperationResultType.Information,
                Message = message
            };
        }
    }
}