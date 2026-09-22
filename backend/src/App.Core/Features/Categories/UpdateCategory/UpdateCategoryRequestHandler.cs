using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Categories.UpdateCategory;

/// <summary>
/// 编辑商品分类用例：校验分类存在 + 名称唯一（排除自身，大小写不敏感）→ 落库。
/// 分类无停用状态；被商品引用的分类改名允许（商品列表随之显示新名）。
/// </summary>
public sealed class UpdateCategoryRequestHandler : IRequestHandler<UpdateCategoryRequest, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑商品分类用例处理器
    /// </summary>
    public UpdateCategoryRequestHandler(ICategoryRepository categoryRepository, IAuditLogger auditLogger)
    {
        _categoryRepository = categoryRepository;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑商品分类请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<CategoryDto> HandleAsync(UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(request.Id, cancellationToken);
        if (category is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品分类不存在");
        }

        var name = request.Name.Trim();

        // 查库约束：名称唯一（大小写不敏感，排除自身）
        if (await _categoryRepository.ExistsByNameAsync(name, category.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.CategoryNameExists, "分类名称已存在");
        }

        var beforeName = category.Name;
        var now = DateTimeOffset.UtcNow;

        category.Name = name;
        await _categoryRepository.UpdateAsync(category, cancellationToken);

        // 分类改名后商品列表随之显示新名，日志必须留住改名前的名称（否则历史不可追溯）
        var changeBuilder = new AuditChangeBuilder()
            .Add("name", "分类名称", beforeName, category.Name);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Category,
            Action = AuditAction.Update,
            ResourceId = category.Id,
            ResourceNo = category.Name,
            Summary = $"编辑分类名称 {beforeName} → {category.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return CategoryDtoMapper.ToCategoryDto(category);
    }
}
