namespace App.Core.Entities;

/// <summary>
/// 站内信字段约束常量（单一来源，specs/041-erp-stock-alert/design.md §2.3）：
/// EF 配置（列长）与各 RequestValidator 一律取本类常量，禁止硬编码字面量。
/// </summary>
public static class NotificationFieldConstraints
{
    /// <summary>标题最大长度（列长与模板文案上限）</summary>
    public const int TitleMaxLength = 50;

    /// <summary>内容最大长度</summary>
    public const int ContentMaxLength = 500;

    /// <summary>业务对象键最大长度</summary>
    public const int ResourceKeyMaxLength = 100;

    /// <summary>跳转路由名最大长度</summary>
    public const int LinkRouteNameMaxLength = 50;

    /// <summary>跳转 query（JSON 文本）最大长度</summary>
    public const int LinkQueryMaxLength = 500;

    /// <summary>顶栏铃铛下拉展示的最近消息条数</summary>
    public const int RecentCount = 5;

    /// <summary>列表关键词（匹配标题列）长度上限：与标题列长一致（超列长不可能命中）</summary>
    public const int KeywordMaxLength = 50;
}
