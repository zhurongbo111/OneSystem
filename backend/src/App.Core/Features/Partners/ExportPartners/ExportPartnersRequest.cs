using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Exports;

namespace App.Core.Features.Partners.ExportPartners;

/// <summary>
/// 往来单位列表导出请求（erp-export）：筛选参数与列表查询一致；
/// 分页参数仅为与列表请求同形，导出取「当前筛选全量」，不参与分页（specs/027-erp-export/design.md §0.2）
/// </summary>
public sealed class ExportPartnersRequest : IRequest<ExportResultDto>
{
    /// <summary>页码，从 1 起（导出忽略，仅为与列表参数一致）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（导出忽略，仅为与列表参数一致）</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>关键词（名称 / 联系人模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>单位类型（1 供应商 / 2 客户 / 3 两者），可空</summary>
    public PartnerType? Type { get; init; }

    /// <summary>单位状态（0 停用 / 1 启用），可空</summary>
    public PartnerStatus? Status { get; init; }
}
