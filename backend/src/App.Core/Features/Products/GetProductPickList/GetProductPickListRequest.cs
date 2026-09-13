using App.Core.Abstractions;

namespace App.Core.Features.Products.GetProductPickList;

/// <summary>
/// 开单商品选择请求（无参用例以空 Request 占位，不定义 Validator）
/// </summary>
public sealed class GetProductPickListRequest : IRequest<IReadOnlyList<ProductPickDto>>
{
}
