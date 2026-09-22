using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 员工仓储的记录型假实现（erp-org-employee 员工导出用例用）：记录导出查询入参、返回预设读模型。
/// 本用例不触及的成员抛 <see cref="NotSupportedException"/>（导出只读，不写）。
/// </summary>
internal sealed class RecordingEmployeeRepository : IEmployeeRepository
{
    /// <summary>预置导出查询结果</summary>
    public IReadOnlyList<EmployeeListItem> Items { get; set; } = [];

    /// <summary>导出查询入参快照（keyword, departmentId, positionId, status, limit）</summary>
    public (string? Keyword, Guid? DepartmentId, Guid? PositionId, EmployeeStatus? Status, int Limit)? LastExportArgs { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<EmployeeListItem>> GetAllForExportAsync(
        string? keyword,
        Guid? departmentId,
        Guid? positionId,
        EmployeeStatus? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        LastExportArgs = (keyword, departmentId, positionId, status, limit);
        return Task.FromResult(Items);
    }

    /// <inheritdoc />
    public Task<(IReadOnlyList<EmployeeListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? departmentId,
        Guid? positionId,
        EmployeeStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<EmployeeDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByNoAsync(string employeeNo, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByPhoneAsync(string phone, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<bool> ExistsByUserIdAsync(Guid userId, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task<IReadOnlyList<EmployeePickUserItem>> GetAvailableUsersAsync(Guid? employeeId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task AddAsync(Employee employee, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    /// <inheritdoc />
    public Task UpdateAsync(Employee employee, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
