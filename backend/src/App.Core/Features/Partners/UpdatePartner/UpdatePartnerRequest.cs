using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Partners.UpdatePartner;

/// <summary>
/// 编辑往来单位请求（名称创建后不可修改，请求体不含 name）
/// </summary>
public sealed class UpdatePartnerRequest : IRequest<PartnerDto>
{
    /// <summary>往来单位 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }

    /// <summary>单位类型（1 供应商 / 2 客户 / 3 两者）</summary>
    public PartnerType Type { get; init; }

    /// <summary>联系人，可空（≤ 20 字符）</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空（11 位手机号）</summary>
    public string? Phone { get; init; }

    /// <summary>地址，可空（≤ 100 字符）</summary>
    public string? Address { get; init; }

    /// <summary>备注，可空（≤ 200 字符）</summary>
    public string? Remark { get; init; }

    /// <summary>账期天数（0 = 现结；上限 3650，`036`）</summary>
    public int PaymentTermDays { get; init; }

    /// <summary>信用额度（0 = 不限；`036`）</summary>
    public decimal CreditLimit { get; init; }
}
