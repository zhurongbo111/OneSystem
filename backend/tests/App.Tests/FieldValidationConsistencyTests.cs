using App.Core.Entities;
using App.Core.Features.Auth.Login;
using App.Core.Features.LoginLogs.GetLoginLogs;
using App.Core.Features.Purchases.CreatePurchaseOrder;
using App.Core.Features.Purchases.GetPurchaseOrders;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.GetUsers;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Infrastructure;

namespace App.Tests;

/// <summary>
/// 字段约束一致性测试：保证"格式校验"与"数据库约束"同源、同一字段在不同用例中规则一致。
/// 守护点：
/// 1. EF 模型实际列长度必须等于 <see cref="UserFieldConstraints"/> 常量（防止配置与常量分叉）；
/// 2. 密码区间在登录 / 创建 / 重置三处一致；
/// 3. 显示名、邮箱、手机号在创建 / 编辑两处一致；
/// 4. 查询关键词长度不超过对应列长度。
/// </summary>
public class FieldValidationConsistencyTests
{
    private static int GetMaxLength<TEntity>(AppDbContext dbContext, string propertyName)
        => dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!.GetMaxLength()!.Value;

    private static string BuildEmail(int totalLength)
    {
        const string suffix = "@example.com";
        return new string('a', totalLength - suffix.Length) + suffix;
    }

    // ============================== EF 模型 ←→ 常量 ==============================

    [Fact]
    public void EF模型_Users表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(UserFieldConstraints.UsernameMaxLength, GetMaxLength<User>(dbContext, nameof(User.Username)));
        Assert.Equal(UserFieldConstraints.DisplayNameMaxLength, GetMaxLength<User>(dbContext, nameof(User.DisplayName)));
        Assert.Equal(UserFieldConstraints.EmailMaxLength, GetMaxLength<User>(dbContext, nameof(User.Email)));
        Assert.Equal(UserFieldConstraints.PhoneMaxLength, GetMaxLength<User>(dbContext, nameof(User.Phone)));
    }

    [Fact]
    public void EF模型_UserLoginLogs表列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(UserFieldConstraints.UsernameMaxLength, GetMaxLength<UserLoginLog>(dbContext, nameof(UserLoginLog.Username)));
        Assert.Equal(UserFieldConstraints.DisplayNameMaxLength, GetMaxLength<UserLoginLog>(dbContext, nameof(UserLoginLog.DisplayName)));
        Assert.Equal(UserFieldConstraints.IpAddressMaxLength, GetMaxLength<UserLoginLog>(dbContext, nameof(UserLoginLog.IpAddress)));
        Assert.Equal(UserFieldConstraints.UserAgentMaxLength, GetMaxLength<UserLoginLog>(dbContext, nameof(UserLoginLog.UserAgent)));
    }

    // ============================== 密码区间三处一致 ==============================

    [Fact]
    public void 密码区间_登录创建重置三处应一致()
    {
        var cases = new (int Length, bool Expected)[]
        {
            (UserFieldConstraints.PasswordMinLength - 1, false),
            (UserFieldConstraints.PasswordMinLength, true),
            (UserFieldConstraints.PasswordMaxLength, true),
            (UserFieldConstraints.PasswordMaxLength + 1, false),
        };

        foreach (var (length, expected) in cases)
        {
            var password = new string('p', length);

            var loginValid = new LoginRequestValidator()
                .Validate(new LoginRequest { Username = "admin", Password = password })
                .IsValid;
            var createValid = new CreateUserRequestValidator()
                .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Password = password })
                .IsValid;
            var resetValid = new ResetPasswordRequestValidator()
                .Validate(new ResetPasswordRequest { Id = Guid.NewGuid(), NewPassword = password })
                .IsValid;

            Assert.Equal(expected, loginValid);
            Assert.Equal(expected, createValid);
            Assert.Equal(expected, resetValid);
        }
    }

    // ============================== 显示名 / 邮箱 / 手机号两处一致 ==============================

    [Fact]
    public void 显示名长度_创建与编辑应一致且不超过数据库列长度()
    {
        var ok = new string('名', UserFieldConstraints.DisplayNameMaxLength);
        var tooLong = new string('名', UserFieldConstraints.DisplayNameMaxLength + 1);

        Assert.True(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = ok, Password = "user123" }).IsValid);
        Assert.True(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), DisplayName = ok }).IsValid);

        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = tooLong, Password = "user123" }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), DisplayName = tooLong }).IsValid);
    }

    [Fact]
    public void 邮箱长度_创建与编辑应一致且不超过数据库列长度()
    {
        var ok = BuildEmail(UserFieldConstraints.EmailMaxLength);
        var tooLong = BuildEmail(UserFieldConstraints.EmailMaxLength + 1);

        Assert.True(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Email = ok, Password = "user123" }).IsValid);
        Assert.True(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), DisplayName = "用户一", Email = ok }).IsValid);

        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Email = tooLong, Password = "user123" }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), DisplayName = "用户一", Email = tooLong }).IsValid);
    }

    [Fact]
    public void 手机号_创建与编辑应一致_且接受合法值拒绝超长或非法值()
    {
        const string legal = "13800138000";
        var tooLong = new string('1', UserFieldConstraints.PhoneMaxLength + 1);

        Assert.True(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Phone = legal, Password = "user123" }).IsValid);
        Assert.True(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), DisplayName = "用户一", Phone = legal }).IsValid);

        // 长度上限虽为 PhoneMaxLength，但格式正则已把合法值限定为 11 位；超长与非法格式都必须被拒绝
        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Phone = tooLong, Password = "user123" }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), DisplayName = "用户一", Phone = tooLong }).IsValid);
        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Phone = "12345", Password = "user123" }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), DisplayName = "用户一", Phone = "12345" }).IsValid);
    }

    // ============================== 用户名规则 ==============================

    [Fact]
    public void 用户名_长度与字符集_应按常量约束校验()
    {
        var validator = new CreateUserRequestValidator();

        Assert.True(ValidateUsername(validator, new string('a', UserFieldConstraints.UsernameMaxLength)));
        Assert.False(ValidateUsername(validator, new string('a', UserFieldConstraints.UsernameMaxLength + 1)));
        Assert.False(ValidateUsername(validator, new string('a', UserFieldConstraints.UsernameMinLength - 1)));
        Assert.False(ValidateUsername(validator, "中文用户名"));
    }

    // ============================== 查询关键词长度 ==============================

    [Fact]
    public void 查询关键词长度_应不超过对应列长度()
    {
        var ok = new string('a', UserFieldConstraints.UsernameMaxLength);
        var tooLong = new string('a', UserFieldConstraints.UsernameMaxLength + 1);

        Assert.True(new GetUsersRequestValidator()
            .Validate(new GetUsersRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetUsersRequestValidator()
            .Validate(new GetUsersRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);

        Assert.True(new GetLoginLogsRequestValidator()
            .Validate(new GetLoginLogsRequest { Page = 1, PageSize = 20, Username = ok }).IsValid);
        Assert.False(new GetLoginLogsRequestValidator()
            .Validate(new GetLoginLogsRequest { Page = 1, PageSize = 20, Username = tooLong }).IsValid);
    }

    // ============================== 采购单字段约束（OrderNo / 明细 / 关键词）==============================

    [Fact]
    public void EF模型_PurchaseOrders表OrderNo列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<PurchaseOrder>(dbContext, nameof(PurchaseOrder.OrderNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<PurchaseOrder>(dbContext, nameof(PurchaseOrder.Remark)));
    }

    [Fact]
    public void 采购单明细数量_边界值应通过且超界拒绝()
    {
        var validator = new CreatePurchaseOrderRequestValidator();

        Assert.True(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMinValue));
        Assert.True(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMaxValue));
        Assert.False(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMinValue - 1));
        Assert.False(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMaxValue + 1));
    }

    [Fact]
    public void 采购单明细单价_边界值应通过且超界拒绝()
    {
        var validator = new CreatePurchaseOrderRequestValidator();

        // 允许 0 元单价
        Assert.True(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMinValue));
        Assert.True(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMaxValue));
        Assert.False(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMinValue - 0.01m));
        Assert.False(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMaxValue + 0.01m));
    }

    [Fact]
    public void 采购单明细行数_上限内通过且超上限拒绝()
    {
        var validator = new CreatePurchaseOrderRequestValidator();

        Assert.True(ValidatePurchase(validator, itemCount: OrderFieldConstraints.ItemsMaxCount));
        Assert.False(ValidatePurchase(validator, itemCount: OrderFieldConstraints.ItemsMaxCount + 1));
    }

    [Fact]
    public void 采购单查询关键词长度_应不超过OrderNo列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetPurchaseOrdersRequestValidator()
            .Validate(new GetPurchaseOrdersRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetPurchaseOrdersRequestValidator()
            .Validate(new GetPurchaseOrdersRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    private static bool ValidatePurchase(CreatePurchaseOrderRequestValidator validator, int quantity = 1, decimal unitPrice = 1m, int itemCount = 1)
    {
        var items = Enumerable
            .Range(0, itemCount)
            .Select(_ => new CreatePurchaseOrderItem { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = unitPrice })
            .ToList();
        return validator.Validate(new CreatePurchaseOrderRequest
        {
            PartnerId = Guid.NewGuid(),
            OrderDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Items = items,
        }).IsValid;
    }

    private static bool ValidateUsername(CreateUserRequestValidator validator, string username)
        => validator.Validate(new CreateUserRequest
        {
            Username = username,
            DisplayName = "用户一",
            Password = "user123",
        }).IsValid;
}
