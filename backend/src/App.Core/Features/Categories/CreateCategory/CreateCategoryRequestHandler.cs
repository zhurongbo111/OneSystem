using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Categories.CreateCategory;

/// <summary>
/// 新增商品分类用例：校验名称唯一（大小写不敏感）→ 落库
/// </summary>
public sealed class CreateCategoryRequestHandler : IRequestHandler<CreateCategoryRequest, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增商品分类用例处理器
    /// </summary>
    public CreateCategoryRequestHandler(ICategoryRepository categoryRepository, IAuditLogger auditLogger)
    {
        _categoryRepository = categoryRepository;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增商品分类请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<CategoryDto> HandleAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        // 查库约束：名称唯一（大小写不敏感）
        if (await _categoryRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.CategoryNameExists, "分类名称已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = now,
        };

        await _categoryRepository.AddAsync(category, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("name", "分类名称", null, category.Name);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Category,
            Action = AuditAction.Create,
            ResourceId = category.Id,
            ResourceNo = category.Name,
            Summary = $"新增分类 {category.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return CategoryDtoMapper.ToCategoryDto(category);
    }
}
