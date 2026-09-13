using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Categories.CreateCategory;

/// <summary>
/// 新增商品分类用例：校验名称唯一（大小写不敏感）→ 落库
/// </summary>
public sealed class CreateCategoryRequestHandler : IRequestHandler<CreateCategoryRequest, CategoryDto>
{
    private readonly ICategoryRepository _categoryRepository;

    /// <summary>
    /// 初始化新增商品分类用例处理器
    /// </summary>
    public CreateCategoryRequestHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
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

        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _categoryRepository.AddAsync(category, cancellationToken);
        return new CategoryDto
        {
            Id = category.Id.ToString(),
            Name = category.Name,
            CreatedAt = category.CreatedAt,
        };
    }
}
