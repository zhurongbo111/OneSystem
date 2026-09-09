namespace App.Core.Responses;

/// <summary>
/// 统一响应基类：code = 0 表示成功，非 0 一律为错误
/// </summary>
public class ApiResponse
{
    /// <summary>业务码，0 表示成功</summary>
    public int Code { get; init; }

    /// <summary>提示信息，成功时固定为 "success"</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// 带数据的统一响应
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResponse<T> : ApiResponse
{
    /// <summary>业务数据</summary>
    public T? Data { get; init; }
}
