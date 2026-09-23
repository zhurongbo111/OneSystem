using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Features.Accounts.CreateAccount;
using App.Core.Features.Accounts.DeleteAccount;
using App.Core.Features.Accounts.UpdateAccount;
using App.Core.Features.Accounts.UpdateAccountStatus;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Inventory.UpdateInventorySafetyStock;
using App.Core.Features.Invoices.CreateInvoice;
using App.Core.Features.Invoices.VoidInvoice;
using App.Core.Features.Categories.DeleteCategory;
using App.Core.Features.Categories.UpdateCategory;
using App.Core.Features.Costs.RecalculateCosts;
using App.Core.Features.Departments.CreateDepartment;
using App.Core.Features.Departments.DeleteDepartment;
using App.Core.Features.Departments.UpdateDepartment;
using App.Core.Features.Departments.UpdateDepartmentStatus;
using App.Core.Features.Employees.CreateEmployee;
using App.Core.Features.Employees.UpdateEmployee;
using App.Core.Features.Employees.UpdateEmployeeStatus;
using App.Core.Features.Partners.CreatePartner;
using App.Core.Features.Partners.UpdatePartner;
using App.Core.Features.Partners.UpdatePartnerStatus;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.UpdateProduct;
using App.Core.Features.Products.UpdateProductStatus;
using App.Core.Features.PurchaseOrders.ClosePurchaseOrder;
using App.Core.Features.PurchaseOrders.CreatePurchaseOrder;
using App.Core.Features.PurchaseOrders.UpdatePurchaseOrder;
using App.Core.Features.PurchaseOrders.VoidPurchaseOrder;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Core.Features.Positions.CreatePosition;
using App.Core.Features.Positions.DeletePosition;
using App.Core.Features.Positions.UpdatePosition;
using App.Core.Features.Positions.UpdatePositionStatus;
using App.Core.Features.PurchaseReturns.CreatePurchaseReturn;
using App.Core.Features.PurchaseReturns.VoidPurchaseReturn;
using App.Core.Features.Quotations.ConvertToOrder;
using App.Core.Features.Quotations.CreateQuotation;
using App.Core.Features.Quotations.UpdateQuotation;
using App.Core.Features.Quotations.VoidQuotation;
using App.Core.Features.Roles.CreateRole;
using App.Core.Features.Roles.DeleteRole;
using App.Core.Features.Roles.UpdateRole;
using App.Core.Features.SalesOrders.CloseSalesOrder;
using App.Core.Features.SalesOrders.CreateSalesOrder;
using App.Core.Features.SalesOrders.UpdateSalesOrder;
using App.Core.Features.SalesOrders.VoidSalesOrder;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Core.Features.SalesReturns.VoidSalesReturn;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.SalesShipments.VoidSalesShipment;
using App.Core.Features.Settlements.CreateSettlement;
using App.Core.Features.Settlements.VoidSettlement;
using App.Core.Features.StockTakes.CreateStockTake;
using App.Core.Features.TaxRates.CreateTaxRate;
using App.Core.Features.TaxRates.DeleteTaxRate;
using App.Core.Features.TaxRates.UpdateTaxRate;
using App.Core.Features.TaxRates.UpdateTaxRateStatus;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Core.Features.Users.UpdateUserStatus;
using App.Core.Features.Warehouses.CreateWarehouse;
using App.Core.Features.Warehouses.SetDefaultWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouseStatus;

namespace App.Tests;

/// <summary>
/// 范围表守卫测试（specs/029-erp-audit-log tasks.md 4.6）：
/// ① 逐个断言 design.md §0.1 登记的「写用例 → 资源 × 动作」组合已注入 <see cref="IAuditLogger"/>；
/// ② 反向扫描 App.Core 全部写用例（请求名以写动词开头），杜绝新增写用例漏接日志。
/// </summary>
public class AuditLogScopeGuardTests
{
    /// <summary>design.md §0.1 范围表登记的写用例 → 资源 × 动作组合</summary>
    public static TheoryData<Type, AuditResource, AuditAction> ScopeCases()
        => new()
        {
            { typeof(CreateCategoryRequestHandler), AuditResource.Category, AuditAction.Create },
            { typeof(UpdateCategoryRequestHandler), AuditResource.Category, AuditAction.Update },
            { typeof(DeleteCategoryRequestHandler), AuditResource.Category, AuditAction.Delete },
            { typeof(CreateProductRequestHandler), AuditResource.Product, AuditAction.Create },
            { typeof(UpdateProductRequestHandler), AuditResource.Product, AuditAction.Update },
            { typeof(UpdateProductStatusRequestHandler), AuditResource.Product, AuditAction.StatusChange },
            { typeof(CreatePartnerRequestHandler), AuditResource.Partner, AuditAction.Create },
            { typeof(UpdatePartnerRequestHandler), AuditResource.Partner, AuditAction.Update },
            { typeof(UpdatePartnerStatusRequestHandler), AuditResource.Partner, AuditAction.StatusChange },
            { typeof(CreateUserRequestHandler), AuditResource.User, AuditAction.Create },
            { typeof(UpdateUserRequestHandler), AuditResource.User, AuditAction.Update },
            { typeof(UpdateUserStatusRequestHandler), AuditResource.User, AuditAction.StatusChange },
            { typeof(ResetPasswordRequestHandler), AuditResource.User, AuditAction.Update },
            { typeof(CreateRoleRequestHandler), AuditResource.Role, AuditAction.Create },
            { typeof(UpdateRoleRequestHandler), AuditResource.Role, AuditAction.Update },
            { typeof(DeleteRoleRequestHandler), AuditResource.Role, AuditAction.Delete },
            { typeof(CreatePurchaseOrderRequestHandler), AuditResource.PurchaseOrder, AuditAction.Create },
            { typeof(UpdatePurchaseOrderRequestHandler), AuditResource.PurchaseOrder, AuditAction.Update },
            { typeof(VoidPurchaseOrderRequestHandler), AuditResource.PurchaseOrder, AuditAction.Void },
            { typeof(ClosePurchaseOrderRequestHandler), AuditResource.PurchaseOrder, AuditAction.Close },
            { typeof(CreateSalesOrderRequestHandler), AuditResource.SalesOrder, AuditAction.Create },
            { typeof(UpdateSalesOrderRequestHandler), AuditResource.SalesOrder, AuditAction.Update },
            { typeof(VoidSalesOrderRequestHandler), AuditResource.SalesOrder, AuditAction.Void },
            { typeof(CloseSalesOrderRequestHandler), AuditResource.SalesOrder, AuditAction.Close },
            { typeof(CreatePurchaseReceiptRequestHandler), AuditResource.PurchaseReceipt, AuditAction.Create },
            { typeof(VoidPurchaseReceiptRequestHandler), AuditResource.PurchaseReceipt, AuditAction.Void },
            { typeof(CreatePurchaseReturnRequestHandler), AuditResource.PurchaseReturn, AuditAction.Create },
            { typeof(VoidPurchaseReturnRequestHandler), AuditResource.PurchaseReturn, AuditAction.Void },
            { typeof(CreateSalesShipmentRequestHandler), AuditResource.SalesShipment, AuditAction.Create },
            { typeof(VoidSalesShipmentRequestHandler), AuditResource.SalesShipment, AuditAction.Void },
            { typeof(CreateSalesReturnRequestHandler), AuditResource.SalesReturn, AuditAction.Create },
            { typeof(VoidSalesReturnRequestHandler), AuditResource.SalesReturn, AuditAction.Void },
            { typeof(CreateSettlementRequestHandler), AuditResource.Settlement, AuditAction.Settle },
            { typeof(VoidSettlementRequestHandler), AuditResource.Settlement, AuditAction.Void },
            { typeof(CreateStockTakeRequestHandler), AuditResource.StockTake, AuditAction.Adjust },
            { typeof(RecalculateCostsRequestHandler), AuditResource.Cost, AuditAction.Recalculate },
            { typeof(CreateDepartmentRequestHandler), AuditResource.Department, AuditAction.Create },
            { typeof(UpdateDepartmentRequestHandler), AuditResource.Department, AuditAction.Update },
            { typeof(DeleteDepartmentRequestHandler), AuditResource.Department, AuditAction.Delete },
            { typeof(UpdateDepartmentStatusRequestHandler), AuditResource.Department, AuditAction.StatusChange },
            { typeof(CreatePositionRequestHandler), AuditResource.Position, AuditAction.Create },
            { typeof(UpdatePositionRequestHandler), AuditResource.Position, AuditAction.Update },
            { typeof(DeletePositionRequestHandler), AuditResource.Position, AuditAction.Delete },
            { typeof(UpdatePositionStatusRequestHandler), AuditResource.Position, AuditAction.StatusChange },
            { typeof(CreateEmployeeRequestHandler), AuditResource.Employee, AuditAction.Create },
            { typeof(UpdateEmployeeRequestHandler), AuditResource.Employee, AuditAction.Update },
            { typeof(UpdateEmployeeStatusRequestHandler), AuditResource.Employee, AuditAction.StatusChange },
            { typeof(CreateAccountRequestHandler), AuditResource.Account, AuditAction.Create },
            { typeof(UpdateAccountRequestHandler), AuditResource.Account, AuditAction.Update },
            { typeof(DeleteAccountRequestHandler), AuditResource.Account, AuditAction.Delete },
            { typeof(UpdateAccountStatusRequestHandler), AuditResource.Account, AuditAction.StatusChange },
            { typeof(CreateTaxRateRequestHandler), AuditResource.TaxRate, AuditAction.Create },
            { typeof(UpdateTaxRateRequestHandler), AuditResource.TaxRate, AuditAction.Update },
            { typeof(DeleteTaxRateRequestHandler), AuditResource.TaxRate, AuditAction.Delete },
            { typeof(UpdateTaxRateStatusRequestHandler), AuditResource.TaxRate, AuditAction.StatusChange },
            { typeof(CreateInvoiceRequestHandler), AuditResource.Invoice, AuditAction.Create },
            { typeof(VoidInvoiceRequestHandler), AuditResource.Invoice, AuditAction.Void },
            { typeof(CreateQuotationRequestHandler), AuditResource.Quotation, AuditAction.Create },
            { typeof(UpdateQuotationRequestHandler), AuditResource.Quotation, AuditAction.Update },
            { typeof(VoidQuotationRequestHandler), AuditResource.Quotation, AuditAction.Void },
            { typeof(ConvertQuotationRequestHandler), AuditResource.Quotation, AuditAction.Update },
            { typeof(CreateWarehouseRequestHandler), AuditResource.Warehouse, AuditAction.Create },
            { typeof(UpdateWarehouseRequestHandler), AuditResource.Warehouse, AuditAction.Update },
            { typeof(UpdateWarehouseStatusRequestHandler), AuditResource.Warehouse, AuditAction.StatusChange },
            { typeof(SetDefaultWarehouseRequestHandler), AuditResource.Warehouse, AuditAction.Update },
            { typeof(UpdateInventorySafetyStockRequestHandler), AuditResource.Inventory, AuditAction.Update },
        };

    [Theory]
    [MemberData(nameof(ScopeCases))]
    public void 范围登记的写用例_应注入审计日志写入器(Type handlerType, AuditResource resource, AuditAction action)
    {
        // 枚举组合仍在登记范围内（防止枚举取值被改动后本守卫静默失效）
        Assert.True(Enum.IsDefined(resource), $"资源未在 AuditResource 中定义：{resource}");
        Assert.True(Enum.IsDefined(action), $"动作未在 AuditAction 中定义：{action}");

        Assert.True(
            HasAuditLogger(handlerType),
            $"{handlerType.Name} 未注入 {nameof(IAuditLogger)}（design.md §0.1 登记：{resource} × {action}）");
    }

    /// <summary>反向扫描：任何以写动词命名的用例都必须注入审计日志写入器</summary>
    [Fact]
    public void 全部写用例_均应接入操作日志()
    {
        var writeVerbs = new[] { "Create", "Update", "Delete", "Void", "Close", "Settle", "Reset", "Adjust", "Recalculate" };

        var writeHandlers = typeof(CreateProductRequest).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Select(type => new { Type = type, Request = RequestOf(type) })
            .Where(x => x.Request is not null
                && writeVerbs.Any(verb => x.Request!.Name.StartsWith(verb, StringComparison.Ordinal)))
            .ToList();

        Assert.NotEmpty(writeHandlers);

        var missing = writeHandlers.Where(x => !HasAuditLogger(x.Type)).Select(x => x.Type.Name).ToList();
        Assert.Empty(missing);
    }

    /// <summary>取处理器实现的 <see cref="IRequestHandler{TRequest, TResponse}"/> 接口的请求类型</summary>
    private static Type? RequestOf(Type handlerType)
        => handlerType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>))
            ?.GenericTypeArguments[0];

    /// <summary>处理器的构造参数中是否声明了 <see cref="IAuditLogger"/> 依赖</summary>
    private static bool HasAuditLogger(Type handlerType)
        => handlerType.GetConstructors()
            .SelectMany(ctor => ctor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IAuditLogger));
}
