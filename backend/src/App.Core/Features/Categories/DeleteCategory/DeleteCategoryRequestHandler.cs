using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Categories.DeleteCategory;

/// <summary>
/// 删除商品分类用例：校验分类存在 + 无商品引用 → 物理删除分类行。
/// 被商品引用的分类不可删除（改为编辑改名）。
/// </summary>
public sealed class DeleteCategoryRequestHandler : IRequestHandler<DeleteCategoryRequest, object?>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除商品分类用例处理器
    /// </summary>
    public DeleteCategoryRequestHandler(ICategoryRepository categoryRepository, IAuditLogger auditLogger)
    {
        _categoryRepository = categoryRepository;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除商品分类请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeleteCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品分类不存在");
        }

        // 查库约束：被商品引用的分类禁止删除
        if (await _categoryRepository.ReferencedByProductsAsync(category.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.CategoryInUse, "该分类已被商品使用，不可删除，请改为编辑名称");
        }

        await _categoryRepository.DeleteAsync(category.Id, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("name", "分类名称", category.Name, null);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Category,
            Action = AuditAction.Delete,
            ResourceId = category.Id,
            ResourceNo = category.Name,
            Summary = $"删除分类 {category.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = DateTimeOffset.UtcNow,
        }, cancellationToken);

        return null;
    }
}
