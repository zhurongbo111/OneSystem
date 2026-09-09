namespace App.Core.Abstractions;

/// <summary>
/// 用例请求标记：声明该请求对应的处理逻辑，并携带响应类型供 <see cref="IMediator"/> 推断与分发。
/// </summary>
/// <typeparam name="TResponse">响应类型（即统一响应的 data 模型）</typeparam>
public interface IRequest<out TResponse>
{
}
