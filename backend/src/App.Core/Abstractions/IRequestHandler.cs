namespace App.Core.Abstractions;

/// <summary>
/// 用例处理器统一入口：一个 API 对应一个 RequestHandler，全部实现本接口以保证入口签名一致。
/// </summary>
/// <typeparam name="TRequest">入参模型；无请求参数的用例使用空请求模型占位</typeparam>
/// <typeparam name="TResponse">出参模型（即接口 <c>data</c> 的模型）</typeparam>
public interface IRequestHandler<TRequest, TResponse>
    where TRequest : class
{
    /// <summary>
    /// 处理用例请求（入口方法，Handler 内完成校验 → 业务逻辑 → 返回出参）
    /// </summary>
    /// <param name="request">入参</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>出参模型</returns>
    Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
}
