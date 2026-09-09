namespace App.Core.Abstractions;

/// <summary>
/// 用例中介者：Controller / 中间层的统一用例入口，把请求分发到已注册的 RequestHandler 并返回结果。
/// 请求须实现 <see cref="IRequest{TResponse}"/>，处理器须在 AddCore 显式注册。
/// </summary>
public interface IMediator
{
    /// <summary>
    /// 发送一个用例请求并等待处理结果
    /// </summary>
    /// <typeparam name="TResponse">响应类型（由请求实现的 IRequest 泛型参数声明）</typeparam>
    /// <param name="request">请求对象</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>用例处理结果</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
