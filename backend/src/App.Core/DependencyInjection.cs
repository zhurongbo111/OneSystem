using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Auth.Login;
using App.Core.Features.LoginLogs;
using App.Core.Features.LoginLogs.GetLoginLogs;
using App.Core.Features.Users;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.GetCurrentUser;
using App.Core.Features.Users.GetUserById;
using App.Core.Features.Users.GetUsers;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Core.Features.Users.UpdateUserStatus;
using App.Core.Features.Categories;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Categories.DeleteCategory;
using App.Core.Features.Categories.GetCategories;
using App.Core.Features.Categories.UpdateCategory;
using App.Core.Features.Products;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.GetProductById;
using App.Core.Features.Products.GetProductPickList;
using App.Core.Features.Products.GetProducts;
using App.Core.Features.Products.UpdateProduct;
using App.Core.Features.Products.UpdateProductStatus;
using App.Core.Mediation;
using App.Core.Responses;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace App.Core;

/// <summary>
/// App.Core 服务注册扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 App.Core 层服务（PasswordHasher、TokenService、IMediator、RequestHandler、RequestValidator）。
    /// 仓储实现与 IUnitOfWork 实现见 App.Infrastructure。
    /// </summary>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        // 密码哈希技术组件无状态，注册为单例（对齐 TokenService）
        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<TokenService>();

        // 用例中介：Controller 只注入 IMediator，经 Send(Request) 分发到已注册的用例处理器
        services.AddScoped<IMediator, Mediator>();

        // 用例处理器：每 API 一个 RequestHandler，统一注册为 IRequestHandler<TRequest,TResponse> 接口映射
        services.AddScoped<IRequestHandler<LoginRequest, LoginResponse>, LoginRequestHandler>();
        services.AddScoped<IRequestHandler<GetCurrentUserRequest, UserDto>, GetCurrentUserRequestHandler>();

        // 用户管理用例（user-management）
        services.AddScoped<IRequestHandler<GetUsersRequest, PagedResult<UserListItemDto>>, GetUsersRequestHandler>();
        services.AddScoped<IRequestHandler<GetUserByIdRequest, UserDetailDto>, GetUserByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateUserRequest, UserDetailDto>, CreateUserRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateUserRequest, UserDetailDto>, UpdateUserRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateUserStatusRequest, UserDetailDto>, UpdateUserStatusRequestHandler>();
        services.AddScoped<IRequestHandler<ResetPasswordRequest, object?>, ResetPasswordRequestHandler>();

        // 用户登录日志用例（user-management）
        services.AddScoped<IRequestHandler<GetLoginLogsRequest, PagedResult<LoginLogListItemDto>>, GetLoginLogsRequestHandler>();

        // 商品管理用例（erp-product）
        services.AddScoped<IRequestHandler<GetProductsRequest, PagedResult<ProductDto>>, GetProductsRequestHandler>();
        services.AddScoped<IRequestHandler<GetProductByIdRequest, ProductDto>, GetProductByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateProductRequest, ProductDto>, CreateProductRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateProductRequest, ProductDto>, UpdateProductRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateProductStatusRequest, ProductDto>, UpdateProductStatusRequestHandler>();
        services.AddScoped<IRequestHandler<GetProductPickListRequest, IReadOnlyList<ProductPickDto>>, GetProductPickListRequestHandler>();

        // 商品分类用例（erp-product）
        services.AddScoped<IRequestHandler<GetCategoriesRequest, IReadOnlyList<CategoryDto>>, GetCategoriesRequestHandler>();
        services.AddScoped<IRequestHandler<CreateCategoryRequest, CategoryDto>, CreateCategoryRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateCategoryRequest, CategoryDto>, UpdateCategoryRequestHandler>();
        services.AddScoped<IRequestHandler<DeleteCategoryRequest, object?>, DeleteCategoryRequestHandler>();

        // 格式校验器（FluentValidation）：校验规则集中在对应用例目录；无校验器的用例（如按 id 详情）不注册
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<GetUsersRequest>, GetUsersRequestValidator>();
        services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
        services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserRequestValidator>();
        services.AddScoped<IValidator<UpdateUserStatusRequest>, UpdateUserStatusRequestValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddScoped<IValidator<GetLoginLogsRequest>, GetLoginLogsRequestValidator>();
        services.AddScoped<IValidator<GetProductsRequest>, GetProductsRequestValidator>();
        services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
        services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
        services.AddScoped<IValidator<UpdateProductStatusRequest>, UpdateProductStatusRequestValidator>();
        services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator>();

        return services;
    }
}
