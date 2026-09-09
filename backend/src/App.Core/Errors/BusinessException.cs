namespace App.Core.Errors;

/// <summary>
/// 业务异常：由 Handler 抛出，全局异常中间件捕获后按其 Code 返回统一响应
/// </summary>
public class BusinessException : Exception
{
    /// <summary>业务错误码</summary>
    public int Code { get; }

    /// <summary>
    /// 初始化业务异常
    /// </summary>
    /// <param name="code">业务错误码</param>
    /// <param name="message">错误信息</param>
    public BusinessException(int code, string message) : base(message)
    {
        Code = code;
    }
}
