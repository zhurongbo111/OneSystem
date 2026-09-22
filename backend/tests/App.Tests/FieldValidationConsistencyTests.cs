using App.Core.Entities;
using App.Core.Features.Auth.Login;
using App.Core.Features.LoginLogs.GetLoginLogs;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceipts;
using App.Core.Features.PurchaseReturns.CreatePurchaseReturn;
using App.Core.Features.PurchaseReturns.GetPurchaseReturns;
using App.Core.Features.Reports;
using App.Core.Features.Reports.GetInventoryFlow;
using App.Core.Features.Reports.GetPurchaseSummary;
using App.Core.Features.Reports.GetSalesSummary;
using App.Core.Features.Reports.GetStockBalance;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Core.Features.SalesReturns.GetSalesReturns;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.SalesShipments.GetSalesShipments;
using App.Core.Features.StockMovements.GetStockMovements;
using App.Core.Features.StockTakes.CreateStockTake;
using App.Core.Features.StockTakes.GetStockTakes;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.GetUsers;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Infrastructure;

using Microsoft.EntityFrameworkCore.Metadata;

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

    /// <summary>用户必须绑定至少一个角色，故校验用请求统一携带一个角色 id</summary>
    private static IReadOnlyList<Guid> ValidRoleIds => [Guid.NewGuid()];

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
                .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Password = password, RoleIds = ValidRoleIds })
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
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = ok, Password = "user123", RoleIds = ValidRoleIds }).IsValid);
        Assert.True(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), RoleIds = ValidRoleIds, DisplayName = ok }).IsValid);

        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = tooLong, Password = "user123", RoleIds = ValidRoleIds }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), RoleIds = ValidRoleIds, DisplayName = tooLong }).IsValid);
    }

    [Fact]
    public void 邮箱长度_创建与编辑应一致且不超过数据库列长度()
    {
        var ok = BuildEmail(UserFieldConstraints.EmailMaxLength);
        var tooLong = BuildEmail(UserFieldConstraints.EmailMaxLength + 1);

        Assert.True(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Email = ok, Password = "user123", RoleIds = ValidRoleIds }).IsValid);
        Assert.True(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), RoleIds = ValidRoleIds, DisplayName = "用户一", Email = ok }).IsValid);

        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Email = tooLong, Password = "user123", RoleIds = ValidRoleIds }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), RoleIds = ValidRoleIds, DisplayName = "用户一", Email = tooLong }).IsValid);
    }

    [Fact]
    public void 手机号_创建与编辑应一致_且接受合法值拒绝超长或非法值()
    {
        const string legal = "13800138000";
        var tooLong = new string('1', UserFieldConstraints.PhoneMaxLength + 1);

        Assert.True(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Phone = legal, Password = "user123", RoleIds = ValidRoleIds }).IsValid);
        Assert.True(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), RoleIds = ValidRoleIds, DisplayName = "用户一", Phone = legal }).IsValid);

        // 长度上限虽为 PhoneMaxLength，但格式正则已把合法值限定为 11 位；超长与非法格式都必须被拒绝
        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Phone = tooLong, Password = "user123", RoleIds = ValidRoleIds }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), RoleIds = ValidRoleIds, DisplayName = "用户一", Phone = tooLong }).IsValid);
        Assert.False(new CreateUserRequestValidator()
            .Validate(new CreateUserRequest { Username = "user1", DisplayName = "用户一", Phone = "12345", Password = "user123", RoleIds = ValidRoleIds }).IsValid);
        Assert.False(new UpdateUserRequestValidator()
            .Validate(new UpdateUserRequest { Id = Guid.NewGuid(), RoleIds = ValidRoleIds, DisplayName = "用户一", Phone = "12345" }).IsValid);
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
    public void EF模型_PurchaseReceipts表ReceiptNo列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<PurchaseReceipt>(dbContext, nameof(PurchaseReceipt.ReceiptNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<PurchaseReceipt>(dbContext, nameof(PurchaseReceipt.Remark)));
    }

    [Fact]
    public void 采购单明细数量_边界值应通过且超界拒绝()
    {
        var validator = new CreatePurchaseReceiptRequestValidator();

        Assert.True(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMinValue));
        Assert.True(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMaxValue));
        Assert.False(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMinValue - 1));
        Assert.False(ValidatePurchase(validator, quantity: ProductFieldConstraints.QuantityMaxValue + 1));
    }

    [Fact]
    public void 采购单明细单价_边界值应通过且超界拒绝()
    {
        var validator = new CreatePurchaseReceiptRequestValidator();

        // 允许 0 元单价
        Assert.True(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMinValue));
        Assert.True(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMaxValue));
        Assert.False(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMinValue - 0.01m));
        Assert.False(ValidatePurchase(validator, unitPrice: ProductFieldConstraints.PriceMaxValue + 0.01m));
    }

    [Fact]
    public void 采购单明细行数_上限内通过且超上限拒绝()
    {
        var validator = new CreatePurchaseReceiptRequestValidator();

        Assert.True(ValidatePurchase(validator, itemCount: OrderFieldConstraints.ItemsMaxCount));
        Assert.False(ValidatePurchase(validator, itemCount: OrderFieldConstraints.ItemsMaxCount + 1));
    }

    [Fact]
    public void 采购单查询关键词长度_应不超过OrderNo列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetPurchaseReceiptsRequestValidator()
            .Validate(new GetPurchaseReceiptsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetPurchaseReceiptsRequestValidator()
            .Validate(new GetPurchaseReceiptsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 销售单字段约束（与采购单同组常量，保证两单同规格）==============================

    [Fact]
    public void EF模型_SalesShipments表ShipmentNo列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<SalesShipment>(dbContext, nameof(SalesShipment.ShipmentNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<SalesShipment>(dbContext, nameof(SalesShipment.Remark)));
    }

    [Fact]
    public void 销售单明细数量_边界值应通过且超界拒绝()
    {
        var validator = new CreateSalesShipmentRequestValidator();

        Assert.True(ValidateSales(validator, quantity: ProductFieldConstraints.QuantityMinValue));
        Assert.True(ValidateSales(validator, quantity: ProductFieldConstraints.QuantityMaxValue));
        Assert.False(ValidateSales(validator, quantity: ProductFieldConstraints.QuantityMinValue - 1));
        Assert.False(ValidateSales(validator, quantity: ProductFieldConstraints.QuantityMaxValue + 1));
    }

    [Fact]
    public void 销售单明细单价_边界值应通过且超界拒绝()
    {
        var validator = new CreateSalesShipmentRequestValidator();

        // 允许 0 元单价
        Assert.True(ValidateSales(validator, unitPrice: ProductFieldConstraints.PriceMinValue));
        Assert.True(ValidateSales(validator, unitPrice: ProductFieldConstraints.PriceMaxValue));
        Assert.False(ValidateSales(validator, unitPrice: ProductFieldConstraints.PriceMinValue - 0.01m));
        Assert.False(ValidateSales(validator, unitPrice: ProductFieldConstraints.PriceMaxValue + 0.01m));
    }

    [Fact]
    public void 销售单明细行数_上限内通过且超上限拒绝()
    {
        var validator = new CreateSalesShipmentRequestValidator();

        Assert.True(ValidateSales(validator, itemCount: OrderFieldConstraints.ItemsMaxCount));
        Assert.False(ValidateSales(validator, itemCount: OrderFieldConstraints.ItemsMaxCount + 1));
    }

    [Fact]
    public void 销售单查询关键词长度_应不超过OrderNo列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetSalesShipmentsRequestValidator()
            .Validate(new GetSalesShipmentsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetSalesShipmentsRequestValidator()
            .Validate(new GetSalesShipmentsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 库存流水字段约束（SourceNo 与单据 OrderNo 同组常量）==============================

    [Fact]
    public void EF模型_StockMovements表SourceNo列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        // SourceNo 存单据单号，列长与 OrderNo 同源；Remark 与单据备注同源
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<StockMovement>(dbContext, nameof(StockMovement.SourceNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<StockMovement>(dbContext, nameof(StockMovement.Remark)));
    }

    [Fact]
    public void 库存流水查询关键词长度_应不超过SourceNo列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetStockMovementsRequestValidator()
            .Validate(new GetStockMovementsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetStockMovementsRequestValidator()
            .Validate(new GetStockMovementsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 盘点 / 期初建账字段约束（TakeNo 与单据 OrderNo 同组常量）==============================

    [Fact]
    public void EF模型_StockTakes表TakeNo列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        // TakeNo 存盘点单号，列长与 OrderNo 同源；Remark 与单据备注同源
        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<StockTake>(dbContext, nameof(StockTake.TakeNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<StockTake>(dbContext, nameof(StockTake.Remark)));
    }

    [Fact]
    public void EF模型_StockTakeItems表快照列长度_应等于商品域常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        // 编码 / 名称 / 单位为提交时快照，列长取自商品档案（同一规则同源）
        Assert.Equal(ProductFieldConstraints.CodeMaxLength, GetMaxLength<StockTakeItem>(dbContext, nameof(StockTakeItem.ProductCode)));
        Assert.Equal(ProductFieldConstraints.NameMaxLength, GetMaxLength<StockTakeItem>(dbContext, nameof(StockTakeItem.ProductName)));
        Assert.Equal(ProductFieldConstraints.UnitMaxLength, GetMaxLength<StockTakeItem>(dbContext, nameof(StockTakeItem.Unit)));
    }

    [Fact]
    public void 盘点实盘数量_边界值应通过且超界拒绝()
    {
        var validator = new CreateStockTakeRequestValidator();

        // 实盘允许为 0（语义区别于单据明细数量下限 1），上界引用商品域
        Assert.True(ValidateStockTake(validator, actual: StockTakeFieldConstraints.ActualQuantityMinValue));
        Assert.True(ValidateStockTake(validator, actual: StockTakeFieldConstraints.ActualQuantityMaxValue));
        Assert.False(ValidateStockTake(validator, actual: StockTakeFieldConstraints.ActualQuantityMinValue - 1));
        Assert.False(ValidateStockTake(validator, actual: StockTakeFieldConstraints.ActualQuantityMaxValue + 1));
    }

    [Fact]
    public void 盘点明细行数_上限内通过且超上限拒绝()
    {
        var validator = new CreateStockTakeRequestValidator();

        Assert.True(ValidateStockTake(validator, itemCount: StockTakeFieldConstraints.ItemsMaxCount));
        Assert.False(ValidateStockTake(validator, itemCount: StockTakeFieldConstraints.ItemsMaxCount + 1));
    }

    [Fact]
    public void 盘点查询关键词长度_应不超过TakeNo列长()
    {
        var ok = new string('a', StockTakeFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', StockTakeFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetStockTakesRequestValidator()
            .Validate(new GetStockTakesRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetStockTakesRequestValidator()
            .Validate(new GetStockTakesRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 采购退货单字段约束（与采购 / 销售同组常量，保证同规格）==============================

    [Fact]
    public void EF模型_PurchaseReturns表ReturnNo列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<PurchaseReturn>(dbContext, nameof(PurchaseReturn.ReturnNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<PurchaseReturn>(dbContext, nameof(PurchaseReturn.Remark)));
    }

    [Fact]
    public void EF模型_PurchaseReturnItems表快照列长度_应等于商品域常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        // 名称 / 单位为开单时快照，列长取自商品档案（同一规则同源）
        Assert.Equal(ProductFieldConstraints.NameMaxLength, GetMaxLength<PurchaseReturnItem>(dbContext, nameof(PurchaseReturnItem.ProductName)));
        Assert.Equal(ProductFieldConstraints.UnitMaxLength, GetMaxLength<PurchaseReturnItem>(dbContext, nameof(PurchaseReturnItem.Unit)));
    }

    [Fact]
    public void 采购退货单明细数量_边界值应通过且超界拒绝()
    {
        var validator = new CreatePurchaseReturnRequestValidator();

        Assert.True(ValidatePurchaseReturn(validator, quantity: ProductFieldConstraints.QuantityMinValue));
        Assert.True(ValidatePurchaseReturn(validator, quantity: ProductFieldConstraints.QuantityMaxValue));
        Assert.False(ValidatePurchaseReturn(validator, quantity: ProductFieldConstraints.QuantityMinValue - 1));
        Assert.False(ValidatePurchaseReturn(validator, quantity: ProductFieldConstraints.QuantityMaxValue + 1));
    }

    [Fact]
    public void 采购退货单明细单价_边界值应通过且超界拒绝()
    {
        var validator = new CreatePurchaseReturnRequestValidator();

        // 允许 0 元单价
        Assert.True(ValidatePurchaseReturn(validator, unitPrice: ProductFieldConstraints.PriceMinValue));
        Assert.True(ValidatePurchaseReturn(validator, unitPrice: ProductFieldConstraints.PriceMaxValue));
        Assert.False(ValidatePurchaseReturn(validator, unitPrice: ProductFieldConstraints.PriceMinValue - 0.01m));
        Assert.False(ValidatePurchaseReturn(validator, unitPrice: ProductFieldConstraints.PriceMaxValue + 0.01m));
    }

    [Fact]
    public void 采购退货单明细行数_上限内通过且超上限拒绝()
    {
        var validator = new CreatePurchaseReturnRequestValidator();

        Assert.True(ValidatePurchaseReturn(validator, itemCount: OrderFieldConstraints.ItemsMaxCount));
        Assert.False(ValidatePurchaseReturn(validator, itemCount: OrderFieldConstraints.ItemsMaxCount + 1));
    }

    [Fact]
    public void 采购退货单查询关键词长度_应不超过ReturnNo列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetPurchaseReturnsRequestValidator()
            .Validate(new GetPurchaseReturnsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetPurchaseReturnsRequestValidator()
            .Validate(new GetPurchaseReturnsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    private static bool ValidatePurchaseReturn(CreatePurchaseReturnRequestValidator validator, int quantity = 1, decimal unitPrice = 1m, int itemCount = 1)
    {
        var items = Enumerable
            .Range(0, itemCount)
            .Select(_ => new CreatePurchaseReturnItem { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = unitPrice })
            .ToList();
        return validator.Validate(new CreatePurchaseReturnRequest
        {
            PartnerId = Guid.NewGuid(),
            ReturnDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Items = items,
        }).IsValid;
    }

    // ============================== 销售退货单字段约束（与采购退货同组常量，保证两退货单同规格）==============================

    [Fact]
    public void EF模型_SalesReturns表ReturnNo列长度_应等于字段约束常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        Assert.Equal(OrderFieldConstraints.OrderNoMaxLength, GetMaxLength<SalesReturn>(dbContext, nameof(SalesReturn.ReturnNo)));
        Assert.Equal(OrderFieldConstraints.RemarkMaxLength, GetMaxLength<SalesReturn>(dbContext, nameof(SalesReturn.Remark)));
    }

    [Fact]
    public void EF模型_SalesReturnItems表快照列长度_应等于商品域常量()
    {
        using var dbContext = TestSupport.CreateDbContext();

        // 名称 / 单位为开单时快照，列长取自商品档案（同一规则同源）
        Assert.Equal(ProductFieldConstraints.NameMaxLength, GetMaxLength<SalesReturnItem>(dbContext, nameof(SalesReturnItem.ProductName)));
        Assert.Equal(ProductFieldConstraints.UnitMaxLength, GetMaxLength<SalesReturnItem>(dbContext, nameof(SalesReturnItem.Unit)));
    }

    [Fact]
    public void 退货单明细数量边界_采购退货与销售退货应一致()
    {
        var purchase = new CreatePurchaseReturnRequestValidator();
        var sales = new CreateSalesReturnRequestValidator();

        foreach (var quantity in new[]
                 {
                     ProductFieldConstraints.QuantityMinValue,
                     ProductFieldConstraints.QuantityMaxValue,
                     ProductFieldConstraints.QuantityMinValue - 1,
                     ProductFieldConstraints.QuantityMaxValue + 1,
                 })
        {
            Assert.Equal(ValidatePurchaseReturn(purchase, quantity: quantity), ValidateSalesReturn(sales, quantity: quantity));
        }

        Assert.True(ValidateSalesReturn(sales, quantity: ProductFieldConstraints.QuantityMaxValue));
        Assert.False(ValidateSalesReturn(sales, quantity: ProductFieldConstraints.QuantityMaxValue + 1));
    }

    [Fact]
    public void 退货单明细单价边界_采购退货与销售退货应一致()
    {
        var purchase = new CreatePurchaseReturnRequestValidator();
        var sales = new CreateSalesReturnRequestValidator();

        foreach (var unitPrice in new[]
                 {
                     ProductFieldConstraints.PriceMinValue,
                     ProductFieldConstraints.PriceMaxValue,
                     ProductFieldConstraints.PriceMinValue - 0.01m,
                     ProductFieldConstraints.PriceMaxValue + 0.01m,
                 })
        {
            Assert.Equal(ValidatePurchaseReturn(purchase, unitPrice: unitPrice), ValidateSalesReturn(sales, unitPrice: unitPrice));
        }

        Assert.True(ValidateSalesReturn(sales, unitPrice: ProductFieldConstraints.PriceMinValue));
        Assert.False(ValidateSalesReturn(sales, unitPrice: ProductFieldConstraints.PriceMaxValue + 0.01m));
    }

    [Fact]
    public void 退货单明细行数上限_采购退货与销售退货应一致()
    {
        var purchase = new CreatePurchaseReturnRequestValidator();
        var sales = new CreateSalesReturnRequestValidator();

        foreach (var itemCount in new[] { 1, OrderFieldConstraints.ItemsMaxCount, OrderFieldConstraints.ItemsMaxCount + 1 })
        {
            Assert.Equal(ValidatePurchaseReturn(purchase, itemCount: itemCount), ValidateSalesReturn(sales, itemCount: itemCount));
        }

        Assert.True(ValidateSalesReturn(sales, itemCount: OrderFieldConstraints.ItemsMaxCount));
        Assert.False(ValidateSalesReturn(sales, itemCount: OrderFieldConstraints.ItemsMaxCount + 1));
    }

    [Fact]
    public void 退货单查询关键词长度_采购退货与销售退货应一致且不超过ReturnNo列长()
    {
        var ok = new string('a', OrderFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', OrderFieldConstraints.KeywordMaxLength + 1);

        Assert.True(new GetSalesReturnsRequestValidator()
            .Validate(new GetSalesReturnsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetSalesReturnsRequestValidator()
            .Validate(new GetSalesReturnsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);

        Assert.True(new GetPurchaseReturnsRequestValidator()
            .Validate(new GetPurchaseReturnsRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetPurchaseReturnsRequestValidator()
            .Validate(new GetPurchaseReturnsRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    private static bool ValidateSalesReturn(CreateSalesReturnRequestValidator validator, int quantity = 1, decimal unitPrice = 1m, int itemCount = 1)
    {
        var items = Enumerable
            .Range(0, itemCount)
            .Select(_ => new CreateSalesReturnItem { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = unitPrice })
            .ToList();
        return validator.Validate(new CreateSalesReturnRequest
        {
            PartnerId = Guid.NewGuid(),
            ReturnDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Items = items,
        }).IsValid;
    }

    private static bool ValidateStockTake(CreateStockTakeRequestValidator validator, int actual = 1, int itemCount = 1)
    {
        var items = Enumerable
            .Range(0, itemCount)
            .Select(_ => new CreateStockTakeItem { ProductId = Guid.NewGuid(), ActualQuantity = actual })
            .ToList();
        return validator.Validate(new CreateStockTakeRequest
        {
            Type = StockTakeType.Take,
            TakeDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Items = items,
        }).IsValid;
    }

    private static bool ValidatePurchase(CreatePurchaseReceiptRequestValidator validator, int quantity = 1, decimal unitPrice = 1m, int itemCount = 1)
    {
        var items = Enumerable
            .Range(0, itemCount)
            .Select(_ => new CreatePurchaseReceiptItem { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = unitPrice })
            .ToList();
        return validator.Validate(new CreatePurchaseReceiptRequest
        {
            PartnerId = Guid.NewGuid(),
            OrderDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Items = items,
        }).IsValid;
    }

    private static bool ValidateSales(CreateSalesShipmentRequestValidator validator, int quantity = 1, decimal unitPrice = 1m, int itemCount = 1)
    {
        var items = Enumerable
            .Range(0, itemCount)
            .Select(_ => new CreateSalesShipmentItem { ProductId = Guid.NewGuid(), Quantity = quantity, UnitPrice = unitPrice })
            .ToList();
        return validator.Validate(new CreateSalesShipmentRequest
        {
            PartnerId = Guid.NewGuid(),
            OrderDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Items = items,
        }).IsValid;
    }

    // ============================== 报表字段约束（期间上限与关键词，specs/025-erp-report）==============================

    [Fact]
    public void 报表期间上限_三个含期间用例应引用同一常量()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var maxEnd = start.AddDays(ReportFieldConstraints.MaxRangeDays);
        var exceededEnd = maxEnd.AddDays(1);

        // 边界（= 上限）通过、超一天拒绝：三处行为一致即证明同源引用 ReportFieldConstraints.MaxRangeDays
        Assert.True(new GetInventoryFlowRequestValidator()
            .Validate(new GetInventoryFlowRequest { Start = start, End = maxEnd }).IsValid);
        Assert.True(new GetPurchaseSummaryRequestValidator()
            .Validate(new GetPurchaseSummaryRequest { Start = start, End = maxEnd }).IsValid);
        Assert.True(new GetSalesSummaryRequestValidator()
            .Validate(new GetSalesSummaryRequest { Start = start, End = maxEnd }).IsValid);

        Assert.False(new GetInventoryFlowRequestValidator()
            .Validate(new GetInventoryFlowRequest { Start = start, End = exceededEnd }).IsValid);
        Assert.False(new GetPurchaseSummaryRequestValidator()
            .Validate(new GetPurchaseSummaryRequest { Start = start, End = exceededEnd }).IsValid);
        Assert.False(new GetSalesSummaryRequestValidator()
            .Validate(new GetSalesSummaryRequest { Start = start, End = exceededEnd }).IsValid);
    }

    [Fact]
    public void 报表查询关键词长度_应不超过商品列长度()
    {
        var ok = new string('a', ProductFieldConstraints.KeywordMaxLength);
        var tooLong = new string('a', ProductFieldConstraints.KeywordMaxLength + 1);

        // 库存余额表关键词匹配商品编码（32）/ 名称（50），上限取两者较大者并引用商品域常量
        Assert.True(new GetStockBalanceRequestValidator()
            .Validate(new GetStockBalanceRequest { Page = 1, PageSize = 20, Keyword = ok }).IsValid);
        Assert.False(new GetStockBalanceRequestValidator()
            .Validate(new GetStockBalanceRequest { Page = 1, PageSize = 20, Keyword = tooLong }).IsValid);
    }

    // ============================== 成本字段约束（erp-cost，specs/026-erp-cost）==============================

    [Fact]
    public void 期初成本单价上界_应与商品单价同源()
    {
        var validator = new CreateStockTakeRequestValidator();
        var takeDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.True(validator.Validate(new CreateStockTakeRequest
        {
            Type = StockTakeType.Initial,
            TakeDate = takeDate,
            Items =
            [
                new CreateStockTakeItem
                {
                    ProductId = Guid.NewGuid(),
                    ActualQuantity = 1,
                    UnitCost = ProductFieldConstraints.PriceMaxValue,
                },
            ],
        }).IsValid);

        // 超商品单价上界一分即拒绝（成本与商品单价同源，禁止本域另立上界）
        Assert.False(validator.Validate(new CreateStockTakeRequest
        {
            Type = StockTakeType.Initial,
            TakeDate = takeDate,
            Items =
            [
                new CreateStockTakeItem
                {
                    ProductId = Guid.NewGuid(),
                    ActualQuantity = 1,
                    UnitCost = ProductFieldConstraints.PriceMaxValue + 0.01m,
                },
            ],
        }).IsValid);
    }

    [Fact]
    public void 成本列精度_EF模型应为numeric18_4()
    {
        using var dbContext = TestSupport.CreateDbContext();

        AssertCostPrecision<Inventory>(dbContext, nameof(Inventory.CostAmount));
        AssertCostPrecision<Inventory>(dbContext, nameof(Inventory.AverageCost));
        AssertCostPrecision<StockMovement>(dbContext, nameof(StockMovement.UnitCost));
        AssertCostPrecision<StockMovement>(dbContext, nameof(StockMovement.TotalCost));
        AssertCostPrecision<StockTakeItem>(dbContext, nameof(StockTakeItem.UnitCost));
    }

    private static void AssertCostPrecision<TEntity>(AppDbContext dbContext, string propertyName)
    {
        var property = dbContext.Model.FindEntityType(typeof(TEntity))!.FindProperty(propertyName)!;

        // InMemory 提供程序无法解析列类型（GetColumnType 依赖 RelationalTypeMapping），直接读 EF 注解
        var columnType = property.FindAnnotation(RelationalAnnotationNames.ColumnType)?.Value as string;
        Assert.Equal("numeric(18,4)", columnType);
    }

    private static bool ValidateUsername(CreateUserRequestValidator validator, string username)
        => validator.Validate(new CreateUserRequest
        {
            Username = username,
            DisplayName = "用户一",
            Password = "user123",
            RoleIds = ValidRoleIds,
        }).IsValid;
}
