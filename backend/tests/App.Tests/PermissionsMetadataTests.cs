using App.Core.Auth;

namespace App.Tests;

/// <summary>
/// 权限点清单元数据守卫测试：
/// 清单本身无重复、分组不重复、<c>All</c> 与分组项一一对应，
/// 避免权限点清单内部漂移。
/// </summary>
public class PermissionsMetadataTests
{
    [Fact]
    public void All_应无重复权限点()
    {
        Assert.Equal(Permissions.All.Count, Permissions.All.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void 分组权限点_应无重复且均属于全量清单()
    {
        var allKeysInGroups = Permissions.Groups.SelectMany(g => g.Items).Select(i => i.Key).ToList();

        Assert.Equal(allKeysInGroups.Count, allKeysInGroups.Distinct(StringComparer.Ordinal).Count());
        Assert.All(allKeysInGroups, key => Assert.Contains(key, Permissions.All));
    }

    [Fact]
    public void 全量清单_应全部落在某个分组内()
    {
        var groupedKeys = Permissions.Groups.SelectMany(g => g.Items).Select(i => i.Key).ToHashSet(StringComparer.Ordinal);

        var ungrouped = Permissions.All.Except(groupedKeys, StringComparer.Ordinal).ToList();

        Assert.Empty(ungrouped);
    }

    [Fact]
    public void 分组名_应无重复()
    {
        Assert.Equal(Permissions.Groups.Count, Permissions.Groups.Select(g => g.Name).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void IsKnown_对未登记key应返回false()
    {
        Assert.True(Permissions.IsKnown(Permissions.PurchasesView));
        Assert.False(Permissions.IsKnown("purchases.fly"));
        Assert.False(Permissions.IsKnown(string.Empty));
    }

    [Fact]
    public void ExcludedFromStaff_应全部属于全量清单()
    {
        Assert.All(Permissions.ExcludedFromStaff, key => Assert.Contains(key, Permissions.All));
    }
}
