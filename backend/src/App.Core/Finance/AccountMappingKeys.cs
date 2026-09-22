namespace App.Core.Finance;

/// <summary>
/// 科目映射键定义（**唯一事实源**：specs/033-erp-general-ledger/design.md §0.3）：
/// 自动凭证按「来源类型 → 映射键 → 科目」取科目，不在各单据 Handler 内硬编码科目。
/// 键常量供 <c>VoucherFactory</c> 与种子使用；标签供接口返回中文名（前端不硬编码）。
/// </summary>
public static class AccountMappingKeys
{
    /// <summary>存货（库存商品）</summary>
    public const string Inventory = "Inventory";

    /// <summary>应收账款</summary>
    public const string Receivable = "Receivable";

    /// <summary>应付账款</summary>
    public const string Payable = "Payable";

    /// <summary>主营业务收入</summary>
    public const string Revenue = "Revenue";

    /// <summary>主营业务成本</summary>
    public const string Cost = "Cost";

    /// <summary>库存现金</summary>
    public const string Cash = "Cash";

    /// <summary>银行存款</summary>
    public const string Bank = "Bank";

    /// <summary>本年利润</summary>
    public const string Profit = "Profit";

    /// <summary>全部映射键定义（顺序即接口返回与页面展示顺序）</summary>
    public static IReadOnlyList<AccountMappingDefinition> All { get; } =
    [
        new(Inventory, "存货（库存商品）", "1405"),
        new(Receivable, "应收账款", "1122"),
        new(Payable, "应付账款", "2202"),
        new(Revenue, "主营业务收入", "6001"),
        new(Cost, "主营业务成本", "6401"),
        new(Cash, "库存现金", "1001"),
        new(Bank, "银行存款", "1002"),
        new(Profit, "本年利润", "4103"),
    ];

    /// <summary>
    /// 取映射键的中文标签；未登记的键原样返回
    /// </summary>
    /// <param name="key">映射键</param>
    public static string LabelOf(string key)
        => All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.Ordinal))?.Label ?? key;
}

/// <summary>科目映射键定义项（键 / 中文标签 / 预置指向的会计科目编码）</summary>
/// <param name="Key">映射键</param>
/// <param name="Label">中文标签</param>
/// <param name="PresetAccountCode">预置指向的会计科目编码（031 预置科目）</param>
public sealed record AccountMappingDefinition(string Key, string Label, string PresetAccountCode);
