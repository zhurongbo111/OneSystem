using App.Core.Abstractions;
using App.Core.Features.GeneralLedger;

namespace App.Core.Features.AccountMappings.GetAccountMappings;

/// <summary>
/// 科目映射查询请求（无参；返回全部映射键，未配置的键科目字段为空串）
/// </summary>
public sealed class GetAccountMappingsRequest : IRequest<IReadOnlyList<AccountMappingDto>>
{
}
