namespace App.Core.Responses;

/// <summary>
/// 统一响应构造工厂，供 Controller / 中间件直接使用
/// </summary>
public static class ApiResponseFactory
{
    /// <summary>构造成功响应（code = 0，带数据）</summary>
    /// <param name="data">业务数据</param>
    public static ApiResponse<T> Ok<T>(T data) => new() { Code = Errors.ErrorCode.Success, Message = "success", Data = data };

    /// <summary>构造失败响应（带数据占位）</summary>
    /// <param name="code">业务错误码</param>
    /// <param name="message">错误信息</param>
    public static ApiResponse<T> Fail<T>(int code, string message) => new() { Code = code, Message = message };

    /// <summary>构造失败响应</summary>
    /// <param name="code">业务错误码</param>
    /// <param name="message">错误信息</param>
    public static ApiResponse Fail(int code, string message) => new() { Code = code, Message = message };
}
