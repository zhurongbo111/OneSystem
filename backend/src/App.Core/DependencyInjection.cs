using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Exports;
using App.Core.Features.AccountingPeriods.ClosePeriod;
using App.Core.Features.AccountingPeriods.GetPeriods;
using App.Core.Features.AccountingPeriods.ReversePeriod;
using App.Core.Features.AccountMappings.GetAccountMappings;
using App.Core.Features.AccountMappings.UpdateAccountMappings;
using App.Core.Features.Accounts;
using App.Core.Features.Accounts.CreateAccount;
using App.Core.Features.Accounts.DeleteAccount;
using App.Core.Features.Accounts.GetAccountById;
using App.Core.Features.Accounts.GetAccounts;
using App.Core.Features.Accounts.UpdateAccount;
using App.Core.Features.Accounts.UpdateAccountStatus;
using App.Core.Features.AuditLogs;
using App.Core.Features.AuditLogs.GetAuditLogById;
using App.Core.Features.AuditLogs.GetAuditLogs;
using App.Core.Features.Auth.Login;
using App.Core.Features.BankAccounts;
using App.Core.Features.BankAccounts.CreateBankAccount;
using App.Core.Features.BankAccounts.DeleteBankAccount;
using App.Core.Features.BankAccounts.GetBankAccountById;
using App.Core.Features.BankAccounts.GetBankAccountSummary;
using App.Core.Features.BankAccounts.GetBankAccounts;
using App.Core.Features.BankAccounts.UpdateBankAccount;
using App.Core.Features.BankAccounts.UpdateBankAccountStatus;
using App.Core.Features.CashJournals;
using App.Core.Features.CashJournals.GetCashJournal;
using App.Core.Features.Categories;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Categories.DeleteCategory;
using App.Core.Features.Categories.GetCategories;
using App.Core.Features.Categories.GetCategoriesPaged;
using App.Core.Features.Categories.UpdateCategory;
using App.Core.Features.Costs;
using App.Core.Features.Costs.RecalculateCosts;
using App.Core.Features.FinancialReports.GetAccountBalance;
using App.Core.Features.FinancialReports.GetBalanceSheet;
using App.Core.Features.FinancialReports.GetIncomeStatement;
using App.Core.Features.GeneralLedger;
using App.Core.Features.Invoices;
using App.Core.Features.Invoices.CreateInvoice;
using App.Core.Features.Invoices.ExportInvoices;
using App.Core.Features.Invoices.GetInvoiceById;
using App.Core.Features.Invoices.GetInvoices;
using App.Core.Features.Invoices.GetInvoicableOrders;
using App.Core.Features.Invoices.VoidInvoice;
using App.Core.Features.Departments;
using App.Core.Features.Departments.CreateDepartment;
using App.Core.Features.Departments.DeleteDepartment;
using App.Core.Features.Departments.GetDepartmentById;
using App.Core.Features.Departments.GetDepartments;
using App.Core.Features.Departments.UpdateDepartment;
using App.Core.Features.Departments.UpdateDepartmentStatus;
using App.Core.Features.Employees;
using App.Core.Features.Employees.CreateEmployee;
using App.Core.Features.Employees.ExportEmployees;
using App.Core.Features.Employees.GetAvailableUsers;
using App.Core.Features.Employees.GetEmployeeById;
using App.Core.Features.Employees.GetEmployees;
using App.Core.Features.Employees.UpdateEmployee;
using App.Core.Features.Employees.UpdateEmployeeStatus;
using App.Core.Features.Inventory;
using App.Core.Features.Inventory.ExportInventory;
using App.Core.Features.Inventory.GetInventory;
using App.Core.Features.Inventory.UpdateInventorySafetyStock;
using App.Core.Features.LoginLogs;
using App.Core.Features.LoginLogs.GetLoginLogs;
using App.Core.Features.Partners;
using App.Core.Features.Partners.CreatePartner;
using App.Core.Features.Partners.ExportPartners;
using App.Core.Features.Partners.GetPartnerById;
using App.Core.Features.Partners.GetPartners;
using App.Core.Features.Partners.UpdatePartner;
using App.Core.Features.Partners.UpdatePartnerStatus;
using App.Core.Features.PartnerPrices;
using App.Core.Features.PartnerPrices.CreatePartnerPrice;
using App.Core.Features.PartnerPrices.DeletePartnerPrice;
using App.Core.Features.PartnerPrices.GetEffectivePrices;
using App.Core.Features.PartnerPrices.GetPartnerPriceById;
using App.Core.Features.PartnerPrices.GetPartnerPrices;
using App.Core.Features.PartnerPrices.UpdatePartnerPrice;
using App.Core.Features.Products;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.ExportProducts;
using App.Core.Features.Products.GetProductById;
using App.Core.Features.Products.GetProductPickList;
using App.Core.Features.Products.GetProducts;
using App.Core.Features.Products.UpdateProduct;
using App.Core.Features.Products.UpdateProductStatus;
using App.Core.Features.Permissions;
using App.Core.Features.Permissions.GetPermissions;
using App.Core.Features.Positions;
using App.Core.Features.Positions.CreatePosition;
using App.Core.Features.Positions.DeletePosition;
using App.Core.Features.Positions.GetPositionById;
using App.Core.Features.Positions.GetPositionPicks;
using App.Core.Features.Positions.GetPositions;
using App.Core.Features.Positions.UpdatePosition;
using App.Core.Features.Positions.UpdatePositionStatus;
using App.Core.Features.PurchaseOrders;
using App.Core.Features.PurchaseOrders.ClosePurchaseOrder;
using App.Core.Features.PurchaseOrders.CreatePurchaseOrder;
using App.Core.Features.PurchaseOrders.GetPurchaseOrderById;
using App.Core.Features.PurchaseOrders.GetPurchaseOrders;
using App.Core.Features.PurchaseOrders.UpdatePurchaseOrder;
using App.Core.Features.PurchaseOrders.VoidPurchaseOrder;
using App.Core.Features.PurchaseReceipts;
using App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;
using App.Core.Features.PurchaseReceipts.ExportPurchaseReceipts;
using App.Core.Features.PurchaseReceipts.GetPurchaseOrderLines;
using App.Core.Features.PurchaseReceipts.GetPurchaseOrderPicks;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceiptById;
using App.Core.Features.PurchaseReceipts.GetPurchaseReceipts;
using App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;
using App.Core.Features.PurchaseReturns;
using App.Core.Features.PurchaseReturns.CreatePurchaseReturn;
using App.Core.Features.PurchaseReturns.ExportPurchaseReturns;
using App.Core.Features.PurchaseReturns.GetPurchaseReturnById;
using App.Core.Features.PurchaseReturns.GetPurchaseReturns;
using App.Core.Features.PurchaseReturns.VoidPurchaseReturn;
using App.Core.Features.Reports;
using App.Core.Features.Reports.ExportCostProfit;
using App.Core.Features.Roles;
using App.Core.Features.Roles.CreateRole;
using App.Core.Features.Roles.DeleteRole;
using App.Core.Features.Roles.GetRoleById;
using App.Core.Features.Roles.GetRoles;
using App.Core.Features.Roles.UpdateRole;
using App.Core.Features.Reports.ExportInventoryFlow;
using App.Core.Features.Reports.ExportPurchaseSummary;
using App.Core.Features.Reports.ExportSalesSummary;
using App.Core.Features.Reports.ExportStockBalance;
using App.Core.Features.Reports.GetCostProfitReport;
using App.Core.Features.Reports.GetInventoryFlow;
using App.Core.Features.Reports.GetPurchaseSummary;
using App.Core.Features.Reports.GetSalesSummary;
using App.Core.Features.Reports.GetStockBalance;
using App.Core.Features.Quotations;
using App.Core.Features.Quotations.ConvertToOrder;
using App.Core.Features.Quotations.CreateQuotation;
using App.Core.Features.Quotations.GetQuotationById;
using App.Core.Features.Quotations.GetQuotations;
using App.Core.Features.Quotations.UpdateQuotation;
using App.Core.Features.Quotations.VoidQuotation;
using App.Core.Features.SalesOrders;
using App.Core.Features.SalesOrders.CloseSalesOrder;
using App.Core.Features.SalesOrders.CreateSalesOrder;
using App.Core.Features.SalesOrders.GetSalesOrderById;
using App.Core.Features.SalesOrders.GetSalesOrders;
using App.Core.Features.SalesOrders.UpdateSalesOrder;
using App.Core.Features.SalesOrders.VoidSalesOrder;
using App.Core.Features.SalesReturns;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Core.Features.SalesReturns.ExportSalesReturns;
using App.Core.Features.SalesReturns.GetSalesReturnById;
using App.Core.Features.SalesReturns.GetSalesReturns;
using App.Core.Features.SalesReturns.VoidSalesReturn;
using App.Core.Features.SalesShipments;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.SalesShipments.ExportSalesShipments;
using App.Core.Features.SalesShipments.GetSalesOrderLines;
using App.Core.Features.SalesShipments.GetSalesOrderPicks;
using App.Core.Features.SalesShipments.GetSalesShipmentById;
using App.Core.Features.SalesShipments.GetSalesShipments;
using App.Core.Features.SalesShipments.VoidSalesShipment;
using App.Core.Features.Settlements;
using App.Core.Features.Settlements.CreateSettlement;
using App.Core.Features.Settlements.ExportSettlements;
using App.Core.Features.Settlements.GetReconciliation;
using App.Core.Features.Settlements.GetSettlementById;
using App.Core.Features.Settlements.GetSettlements;
using App.Core.Features.Settlements.GetUnsettledOrders;
using App.Core.Features.Settlements.VoidSettlement;
using App.Core.Features.StockMovements;
using App.Core.Features.StockMovements.ExportStockMovements;
using App.Core.Features.StockMovements.GetStockMovements;
using App.Core.Features.StockTakes;
using App.Core.Features.StockTakes.CreateStockTake;
using App.Core.Features.StockTakes.ExportStockTakes;
using App.Core.Features.StockTakes.GetStockTakeById;
using App.Core.Features.StockTakes.GetStockTakePickProducts;
using App.Core.Features.StockTakes.GetStockTakes;
using App.Core.Features.TaxRates;
using App.Core.Features.TaxRates.CreateTaxRate;
using App.Core.Features.TaxRates.DeleteTaxRate;
using App.Core.Features.TaxRates.GetTaxRateById;
using App.Core.Features.TaxRates.GetTaxRates;
using App.Core.Features.TaxRates.UpdateTaxRate;
using App.Core.Features.TaxRates.UpdateTaxRateStatus;
using App.Core.Features.Users;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.GetCurrentUser;
using App.Core.Features.Users.GetMyPermissions;
using App.Core.Features.Users.GetUserById;
using App.Core.Features.Users.GetUsers;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Core.Features.Users.UpdateUserStatus;
using App.Core.Features.Vouchers.CreateVoucher;
using App.Core.Features.Vouchers.GetVoucherById;
using App.Core.Features.Vouchers.GetVouchers;
using App.Core.Features.Vouchers.VoidVoucher;
using App.Core.Features.Warehouses;
using App.Core.Features.Warehouses.CreateWarehouse;
using App.Core.Features.Warehouses.GetWarehouseById;
using App.Core.Features.Warehouses.GetWarehousePickList;
using App.Core.Features.Warehouses.GetWarehouses;
using App.Core.Features.Warehouses.SetDefaultWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouseStatus;
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

        // 成本重算互斥锁（erp-cost）：进程内 Singleton，用于拒绝并发重算（40118）
        services.AddSingleton<CostRecalculationLock>();

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
        services.AddScoped<IRequestHandler<GetMyPermissionsRequest, IReadOnlyList<string>>, GetMyPermissionsRequestHandler>();

        // 用户登录日志用例（user-management）
        services.AddScoped<IRequestHandler<GetLoginLogsRequest, PagedResult<LoginLogListItemDto>>, GetLoginLogsRequestHandler>();

        // 操作审计日志用例（erp-audit-log；只读，追加写入由 IAuditLogger 在各写用例内完成）
        services.AddScoped<IRequestHandler<GetAuditLogsRequest, PagedResult<AuditLogListItemDto>>, GetAuditLogsRequestHandler>();
        services.AddScoped<IRequestHandler<GetAuditLogByIdRequest, AuditLogDetailDto>, GetAuditLogByIdRequestHandler>();

        // 角色与权限用例（erp-rbac：角色维护 + 权限点清单）
        services.AddScoped<IRequestHandler<GetRolesRequest, PagedResult<RoleListItemDto>>, GetRolesRequestHandler>();
        services.AddScoped<IRequestHandler<CreateRoleRequest, RoleDetailDto>, CreateRoleRequestHandler>();
        services.AddScoped<IRequestHandler<GetRoleByIdRequest, RoleDetailDto>, GetRoleByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateRoleRequest, RoleDetailDto>, UpdateRoleRequestHandler>();
        services.AddScoped<IRequestHandler<DeleteRoleRequest, object?>, DeleteRoleRequestHandler>();
        services.AddScoped<IRequestHandler<GetPermissionsRequest, IReadOnlyList<PermissionGroupDto>>, GetPermissionsRequestHandler>();

        // 组织人事用例（erp-org-employee：部门树 + 岗位字典 + 员工档案）
        services.AddScoped<IRequestHandler<GetDepartmentsRequest, IReadOnlyList<DepartmentTreeNodeDto>>, GetDepartmentsRequestHandler>();
        services.AddScoped<IRequestHandler<CreateDepartmentRequest, DepartmentDetailDto>, CreateDepartmentRequestHandler>();
        services.AddScoped<IRequestHandler<GetDepartmentByIdRequest, DepartmentDetailDto>, GetDepartmentByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateDepartmentRequest, DepartmentDetailDto>, UpdateDepartmentRequestHandler>();
        services.AddScoped<IRequestHandler<DeleteDepartmentRequest, object?>, DeleteDepartmentRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateDepartmentStatusRequest, DepartmentDetailDto>, UpdateDepartmentStatusRequestHandler>();
        services.AddScoped<IRequestHandler<GetPositionsRequest, PagedResult<PositionListItemDto>>, GetPositionsRequestHandler>();
        services.AddScoped<IRequestHandler<CreatePositionRequest, PositionDetailDto>, CreatePositionRequestHandler>();
        services.AddScoped<IRequestHandler<GetPositionByIdRequest, PositionDetailDto>, GetPositionByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdatePositionRequest, PositionDetailDto>, UpdatePositionRequestHandler>();
        services.AddScoped<IRequestHandler<DeletePositionRequest, object?>, DeletePositionRequestHandler>();
        services.AddScoped<IRequestHandler<UpdatePositionStatusRequest, PositionDetailDto>, UpdatePositionStatusRequestHandler>();
        services.AddScoped<IRequestHandler<GetPositionPicksRequest, IReadOnlyList<PositionPickDto>>, GetPositionPicksRequestHandler>();
        services.AddScoped<IRequestHandler<GetEmployeesRequest, PagedResult<EmployeeListItemDto>>, GetEmployeesRequestHandler>();
        services.AddScoped<IRequestHandler<CreateEmployeeRequest, EmployeeDetailDto>, CreateEmployeeRequestHandler>();
        services.AddScoped<IRequestHandler<GetEmployeeByIdRequest, EmployeeDetailDto>, GetEmployeeByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateEmployeeRequest, EmployeeDetailDto>, UpdateEmployeeRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateEmployeeStatusRequest, EmployeeDetailDto>, UpdateEmployeeStatusRequestHandler>();
        services.AddScoped<IRequestHandler<GetAvailableUsersRequest, IReadOnlyList<EmployeePickUserDto>>, GetAvailableUsersRequestHandler>();
        services.AddScoped<IRequestHandler<ExportEmployeesRequest, ExportResultDto>, ExportEmployeesRequestHandler>();

        // 发票登记用例（erp-invoice：登记 + 关联单据 + 作废 + 可开票候选 + 导出）
        services.AddScoped<IRequestHandler<GetInvoicesRequest, PagedResult<InvoiceListItemDto>>, GetInvoicesRequestHandler>();
        services.AddScoped<IRequestHandler<CreateInvoiceRequest, InvoiceDetailDto>, CreateInvoiceRequestHandler>();
        services.AddScoped<IRequestHandler<GetInvoiceByIdRequest, InvoiceDetailDto>, GetInvoiceByIdRequestHandler>();
        services.AddScoped<IRequestHandler<VoidInvoiceRequest, InvoiceDetailDto>, VoidInvoiceRequestHandler>();
        services.AddScoped<IRequestHandler<GetInvoicableOrdersRequest, PagedResult<InvoicableOrderDto>>, GetInvoicableOrdersRequestHandler>();
        services.AddScoped<IRequestHandler<ExportInvoicesRequest, ExportResultDto>, ExportInvoicesRequestHandler>();

        // 总账用例（erp-general-ledger：会计期间 + 凭证 + 科目映射 + 财务报表）
        services.AddScoped<IRequestHandler<GetPeriodsRequest, IReadOnlyList<PeriodDto>>, GetPeriodsRequestHandler>();
        services.AddScoped<IRequestHandler<ClosePeriodRequest, PeriodDto>, ClosePeriodRequestHandler>();
        services.AddScoped<IRequestHandler<ReversePeriodRequest, PeriodDto>, ReversePeriodRequestHandler>();
        services.AddScoped<IRequestHandler<GetVouchersRequest, PagedResult<VoucherListItemDto>>, GetVouchersRequestHandler>();
        services.AddScoped<IRequestHandler<CreateVoucherRequest, VoucherDetailDto>, CreateVoucherRequestHandler>();
        services.AddScoped<IRequestHandler<GetVoucherByIdRequest, VoucherDetailDto>, GetVoucherByIdRequestHandler>();
        services.AddScoped<IRequestHandler<VoidVoucherRequest, VoucherDetailDto>, VoidVoucherRequestHandler>();
        services.AddScoped<IRequestHandler<GetAccountMappingsRequest, IReadOnlyList<AccountMappingDto>>, GetAccountMappingsRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateAccountMappingsRequest, IReadOnlyList<AccountMappingDto>>, UpdateAccountMappingsRequestHandler>();
        services.AddScoped<IRequestHandler<GetAccountBalanceRequest, IReadOnlyList<AccountBalanceItemDto>>, GetAccountBalanceRequestHandler>();
        services.AddScoped<IRequestHandler<GetBalanceSheetRequest, BalanceSheetDto>, GetBalanceSheetRequestHandler>();
        services.AddScoped<IRequestHandler<GetIncomeStatementRequest, IncomeStatementDto>, GetIncomeStatementRequestHandler>();

        // 财务主数据用例（erp-finance-master：会计科目树 + 税率字典）
        services.AddScoped<IRequestHandler<GetAccountsRequest, IReadOnlyList<AccountTreeNodeDto>>, GetAccountsRequestHandler>();
        services.AddScoped<IRequestHandler<CreateAccountRequest, AccountDetailDto>, CreateAccountRequestHandler>();
        services.AddScoped<IRequestHandler<GetAccountByIdRequest, AccountDetailDto>, GetAccountByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateAccountRequest, AccountDetailDto>, UpdateAccountRequestHandler>();
        services.AddScoped<IRequestHandler<DeleteAccountRequest, object?>, DeleteAccountRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateAccountStatusRequest, AccountDetailDto>, UpdateAccountStatusRequestHandler>();
        services.AddScoped<IRequestHandler<GetTaxRatesRequest, PagedResult<TaxRateListItemDto>>, GetTaxRatesRequestHandler>();
        services.AddScoped<IRequestHandler<CreateTaxRateRequest, TaxRateDetailDto>, CreateTaxRateRequestHandler>();
        services.AddScoped<IRequestHandler<GetTaxRateByIdRequest, TaxRateDetailDto>, GetTaxRateByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateTaxRateRequest, TaxRateDetailDto>, UpdateTaxRateRequestHandler>();
        services.AddScoped<IRequestHandler<DeleteTaxRateRequest, object?>, DeleteTaxRateRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateTaxRateStatusRequest, TaxRateDetailDto>, UpdateTaxRateStatusRequestHandler>();

        // 资金出纳用例（erp-cash：资金账户 + 资金日记账只读聚合）
        services.AddScoped<IRequestHandler<GetBankAccountsRequest, PagedResult<BankAccountListItemDto>>, GetBankAccountsRequestHandler>();
        services.AddScoped<IRequestHandler<CreateBankAccountRequest, BankAccountDetailDto>, CreateBankAccountRequestHandler>();
        services.AddScoped<IRequestHandler<GetBankAccountByIdRequest, BankAccountDetailDto>, GetBankAccountByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateBankAccountRequest, BankAccountDetailDto>, UpdateBankAccountRequestHandler>();
        services.AddScoped<IRequestHandler<DeleteBankAccountRequest, object?>, DeleteBankAccountRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateBankAccountStatusRequest, BankAccountDetailDto>, UpdateBankAccountStatusRequestHandler>();
        services.AddScoped<IRequestHandler<GetBankAccountSummaryRequest, IReadOnlyList<BankAccountBalanceItemDto>>, GetBankAccountSummaryRequestHandler>();
        services.AddScoped<IRequestHandler<GetCashJournalRequest, CashJournalDto>, GetCashJournalRequestHandler>();

        // 商品管理用例（erp-product）
        services.AddScoped<IRequestHandler<GetProductsRequest, PagedResult<ProductDto>>, GetProductsRequestHandler>();
        services.AddScoped<IRequestHandler<GetProductByIdRequest, ProductDto>, GetProductByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateProductRequest, ProductDto>, CreateProductRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateProductRequest, ProductDto>, UpdateProductRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateProductStatusRequest, ProductDto>, UpdateProductStatusRequestHandler>();
        services.AddScoped<IRequestHandler<GetProductPickListRequest, IReadOnlyList<ProductPickDto>>, GetProductPickListRequestHandler>();

        // 商品分类用例（erp-product）
        services.AddScoped<IRequestHandler<GetCategoriesRequest, IReadOnlyList<CategoryDto>>, GetCategoriesRequestHandler>();
        services.AddScoped<IRequestHandler<GetCategoriesPagedRequest, PagedResult<CategoryDto>>, GetCategoriesPagedRequestHandler>();
        services.AddScoped<IRequestHandler<CreateCategoryRequest, CategoryDto>, CreateCategoryRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateCategoryRequest, CategoryDto>, UpdateCategoryRequestHandler>();
        services.AddScoped<IRequestHandler<DeleteCategoryRequest, object?>, DeleteCategoryRequestHandler>();

        // 往来单位用例（erp-partner）
        services.AddScoped<IRequestHandler<GetPartnersRequest, PagedResult<PartnerDto>>, GetPartnersRequestHandler>();
        services.AddScoped<IRequestHandler<CreatePartnerRequest, PartnerDto>, CreatePartnerRequestHandler>();
        services.AddScoped<IRequestHandler<GetPartnerByIdRequest, PartnerDto>, GetPartnerByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdatePartnerRequest, PartnerDto>, UpdatePartnerRequestHandler>();
        services.AddScoped<IRequestHandler<UpdatePartnerStatusRequest, PartnerDto>, UpdatePartnerStatusRequestHandler>();

        // 仓库用例（erp-multi-warehouse，038）
        services.AddScoped<IRequestHandler<UpdateInventorySafetyStockRequest, UpdateInventorySafetyStockResponse>, UpdateInventorySafetyStockRequestHandler>();
        services.AddScoped<IRequestHandler<GetWarehousesRequest, PagedResult<WarehouseDto>>, GetWarehousesRequestHandler>();
        services.AddScoped<IRequestHandler<CreateWarehouseRequest, WarehouseDto>, CreateWarehouseRequestHandler>();
        services.AddScoped<IRequestHandler<GetWarehouseByIdRequest, WarehouseDto>, GetWarehouseByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateWarehouseRequest, WarehouseDto>, UpdateWarehouseRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateWarehouseStatusRequest, WarehouseDto>, UpdateWarehouseStatusRequestHandler>();
        services.AddScoped<IRequestHandler<SetDefaultWarehouseRequest, WarehouseDto>, SetDefaultWarehouseRequestHandler>();
        services.AddScoped<IRequestHandler<GetWarehousePickListRequest, IReadOnlyList<WarehousePickDto>>, GetWarehousePickListRequestHandler>();

        // 客户价格用例（erp-partner-price）
        services.AddScoped<IRequestHandler<GetPartnerPricesRequest, PagedResult<PartnerPriceListItemDto>>, GetPartnerPricesRequestHandler>();
        services.AddScoped<IRequestHandler<CreatePartnerPriceRequest, PartnerPriceDetailDto>, CreatePartnerPriceRequestHandler>();
        services.AddScoped<IRequestHandler<GetPartnerPriceByIdRequest, PartnerPriceDetailDto>, GetPartnerPriceByIdRequestHandler>();
        services.AddScoped<IRequestHandler<UpdatePartnerPriceRequest, PartnerPriceDetailDto>, UpdatePartnerPriceRequestHandler>();
        services.AddScoped<IRequestHandler<DeletePartnerPriceRequest, object?>, DeletePartnerPriceRequestHandler>();
        services.AddScoped<IRequestHandler<GetEffectivePricesRequest, IReadOnlyList<EffectivePriceDto>>, GetEffectivePricesRequestHandler>();

        // 库存查询用例（erp-inventory-query）
        services.AddScoped<IRequestHandler<GetInventoryRequest, PagedResult<InventoryItemDto>>, GetInventoryRequestHandler>();

        // 采购管理用例（erp-purchase）
        services.AddScoped<IRequestHandler<GetPurchaseReceiptsRequest, PagedResult<PurchaseReceiptListItemDto>>, GetPurchaseReceiptsRequestHandler>();
        services.AddScoped<IRequestHandler<GetPurchaseReceiptByIdRequest, PurchaseReceiptDetailDto>, GetPurchaseReceiptByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreatePurchaseReceiptRequest, PurchaseReceiptDetailDto>, CreatePurchaseReceiptRequestHandler>();
        services.AddScoped<IRequestHandler<VoidPurchaseReceiptRequest, PurchaseReceiptDetailDto>, VoidPurchaseReceiptRequestHandler>();

        // 入库开单页「关联采购订单」（erp-order-flow）：候选订单 + 订单明细（含未收数量）
        services.AddScoped<IRequestHandler<GetPurchaseOrderPicksRequest, IReadOnlyList<PurchaseOrderPickDto>>, GetPurchaseOrderPicksRequestHandler>();
        services.AddScoped<IRequestHandler<GetPurchaseOrderLinesRequest, PurchaseOrderLinesDto>, GetPurchaseOrderLinesRequestHandler>();

        // 采购订单用例（erp-order-flow；计划单据，不触碰库存与流水）
        services.AddScoped<IRequestHandler<GetPurchaseOrdersRequest, PagedResult<PurchaseOrderListItemDto>>, GetPurchaseOrdersRequestHandler>();
        services.AddScoped<IRequestHandler<GetPurchaseOrderByIdRequest, PurchaseOrderDetailDto>, GetPurchaseOrderByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreatePurchaseOrderRequest, PurchaseOrderDetailDto>, CreatePurchaseOrderRequestHandler>();
        services.AddScoped<IRequestHandler<UpdatePurchaseOrderRequest, PurchaseOrderDetailDto>, UpdatePurchaseOrderRequestHandler>();
        services.AddScoped<IRequestHandler<VoidPurchaseOrderRequest, PurchaseOrderDetailDto>, VoidPurchaseOrderRequestHandler>();
        services.AddScoped<IRequestHandler<ClosePurchaseOrderRequest, PurchaseOrderDetailDto>, ClosePurchaseOrderRequestHandler>();

        // 采购退货用例（erp-purchase-return）
        services.AddScoped<IRequestHandler<GetPurchaseReturnsRequest, PagedResult<PurchaseReturnListItemDto>>, GetPurchaseReturnsRequestHandler>();
        services.AddScoped<IRequestHandler<GetPurchaseReturnByIdRequest, PurchaseReturnDetailDto>, GetPurchaseReturnByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreatePurchaseReturnRequest, PurchaseReturnDetailDto>, CreatePurchaseReturnRequestHandler>();
        services.AddScoped<IRequestHandler<VoidPurchaseReturnRequest, PurchaseReturnDetailDto>, VoidPurchaseReturnRequestHandler>();

        // 销售管理用例（erp-sale）
        services.AddScoped<IRequestHandler<GetSalesShipmentsRequest, PagedResult<SalesShipmentListItemDto>>, GetSalesShipmentsRequestHandler>();
        services.AddScoped<IRequestHandler<GetSalesShipmentByIdRequest, SalesShipmentDetailDto>, GetSalesShipmentByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateSalesShipmentRequest, SalesShipmentDetailDto>, CreateSalesShipmentRequestHandler>();
        services.AddScoped<IRequestHandler<VoidSalesShipmentRequest, SalesShipmentDetailDto>, VoidSalesShipmentRequestHandler>();

        // 出库开单页「关联销售订单」（erp-order-flow）：候选订单 + 订单明细（含未发数量）
        services.AddScoped<IRequestHandler<GetSalesOrderPicksRequest, IReadOnlyList<SalesOrderPickDto>>, GetSalesOrderPicksRequestHandler>();
        services.AddScoped<IRequestHandler<GetSalesOrderLinesRequest, SalesOrderLinesDto>, GetSalesOrderLinesRequestHandler>();

        // 销售订单用例（erp-order-flow；计划单据，不触碰库存与流水）
        services.AddScoped<IRequestHandler<GetSalesOrdersRequest, PagedResult<SalesOrderListItemDto>>, GetSalesOrdersRequestHandler>();
        services.AddScoped<IRequestHandler<GetSalesOrderByIdRequest, SalesOrderDetailDto>, GetSalesOrderByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateSalesOrderRequest, SalesOrderDetailDto>, CreateSalesOrderRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateSalesOrderRequest, SalesOrderDetailDto>, UpdateSalesOrderRequestHandler>();
        services.AddScoped<IRequestHandler<VoidSalesOrderRequest, SalesOrderDetailDto>, VoidSalesOrderRequestHandler>();
        services.AddScoped<IRequestHandler<CloseSalesOrderRequest, SalesOrderDetailDto>, CloseSalesOrderRequestHandler>();

        // 报价单用例（erp-quotation；意向单据，不触碰库存与资金）
        services.AddScoped<IRequestHandler<GetQuotationsRequest, PagedResult<QuotationListItemDto>>, GetQuotationsRequestHandler>();
        services.AddScoped<IRequestHandler<GetQuotationByIdRequest, QuotationDetailDto>, GetQuotationByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateQuotationRequest, QuotationDetailDto>, CreateQuotationRequestHandler>();
        services.AddScoped<IRequestHandler<UpdateQuotationRequest, QuotationDetailDto>, UpdateQuotationRequestHandler>();
        services.AddScoped<IRequestHandler<VoidQuotationRequest, QuotationDetailDto>, VoidQuotationRequestHandler>();
        services.AddScoped<IRequestHandler<ConvertQuotationRequest, ConvertQuotationResultDto>, ConvertQuotationRequestHandler>();

        // 销售退货用例（erp-sale-return）
        services.AddScoped<IRequestHandler<GetSalesReturnsRequest, PagedResult<SalesReturnListItemDto>>, GetSalesReturnsRequestHandler>();
        services.AddScoped<IRequestHandler<GetSalesReturnByIdRequest, SalesReturnDetailDto>, GetSalesReturnByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateSalesReturnRequest, SalesReturnDetailDto>, CreateSalesReturnRequestHandler>();
        services.AddScoped<IRequestHandler<VoidSalesReturnRequest, SalesReturnDetailDto>, VoidSalesReturnRequestHandler>();

        // 收付款与往来对账用例（erp-settlement）
        services.AddScoped<IRequestHandler<GetSettlementsRequest, PagedResult<SettlementListItemDto>>, GetSettlementsRequestHandler>();
        services.AddScoped<IRequestHandler<GetSettlementByIdRequest, SettlementDetailDto>, GetSettlementByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateSettlementRequest, SettlementDetailDto>, CreateSettlementRequestHandler>();
        services.AddScoped<IRequestHandler<VoidSettlementRequest, SettlementDetailDto>, VoidSettlementRequestHandler>();
        services.AddScoped<IRequestHandler<GetUnsettledOrdersRequest, PagedResult<SettlementCandidateDto>>, GetUnsettledOrdersRequestHandler>();
        services.AddScoped<IRequestHandler<GetReconciliationRequest, PagedResult<ReconciliationListItemDto>>, GetReconciliationRequestHandler>();

        // 库存流水用例（erp-stock-movement）
        services.AddScoped<IRequestHandler<GetStockMovementsRequest, PagedResult<StockMovementListItemDto>>, GetStockMovementsRequestHandler>();

        // 盘点 / 期初建账用例（erp-stock-take）
        services.AddScoped<IRequestHandler<GetStockTakesRequest, PagedResult<StockTakeListItemDto>>, GetStockTakesRequestHandler>();
        services.AddScoped<IRequestHandler<GetStockTakeByIdRequest, StockTakeDetailDto>, GetStockTakeByIdRequestHandler>();
        services.AddScoped<IRequestHandler<CreateStockTakeRequest, StockTakeDetailDto>, CreateStockTakeRequestHandler>();
        services.AddScoped<IRequestHandler<GetStockTakePickProductsRequest, IReadOnlyList<StockTakeProductPickDto>>, GetStockTakePickProductsRequestHandler>();

        // 报表用例（erp-report；纯只读跨表聚合，不新增写路径）
        services.AddScoped<IRequestHandler<GetInventoryFlowRequest, ReportPageDto<InventoryFlowItemDto, InventoryFlowSummaryDto>>, GetInventoryFlowRequestHandler>();
        services.AddScoped<IRequestHandler<GetStockBalanceRequest, ReportPageDto<StockBalanceItemDto, StockBalanceSummaryDto>>, GetStockBalanceRequestHandler>();
        services.AddScoped<IRequestHandler<GetPurchaseSummaryRequest, ReportPageDto<PurchaseSummaryItemDto, PurchaseSummaryTotalDto>>, GetPurchaseSummaryRequestHandler>();
        services.AddScoped<IRequestHandler<GetSalesSummaryRequest, ReportPageDto<SalesSummaryItemDto, SalesSummaryTotalDto>>, GetSalesSummaryRequestHandler>();

        // 成本与毛利报表（erp-cost；与 025 报表同域）
        services.AddScoped<IRequestHandler<GetCostProfitReportRequest, ReportPageDto<CostProfitItemDto, CostProfitTotalDto>>, GetCostProfitReportRequestHandler>();

        // 成本重算 / 初始化（erp-cost；运维动作，按流水时序重放，幂等）
        services.AddScoped<IRequestHandler<RecalculateCostsRequest, CostRecalculateResultDto>, RecalculateCostsRequestHandler>();

        // 导出 Excel 用例（erp-export；横向能力：各域列表 / 报表的另一种出参，复用各域列表筛选与仓储查询）
        services.AddScoped<IRequestHandler<ExportProductsRequest, ExportResultDto>, ExportProductsRequestHandler>();
        services.AddScoped<IRequestHandler<ExportPartnersRequest, ExportResultDto>, ExportPartnersRequestHandler>();
        services.AddScoped<IRequestHandler<ExportInventoryRequest, ExportResultDto>, ExportInventoryRequestHandler>();
        services.AddScoped<IRequestHandler<ExportStockMovementsRequest, ExportResultDto>, ExportStockMovementsRequestHandler>();
        services.AddScoped<IRequestHandler<ExportPurchaseReceiptsRequest, ExportResultDto>, ExportPurchaseReceiptsRequestHandler>();
        services.AddScoped<IRequestHandler<ExportSalesShipmentsRequest, ExportResultDto>, ExportSalesShipmentsRequestHandler>();
        services.AddScoped<IRequestHandler<ExportPurchaseReturnsRequest, ExportResultDto>, ExportPurchaseReturnsRequestHandler>();
        services.AddScoped<IRequestHandler<ExportSalesReturnsRequest, ExportResultDto>, ExportSalesReturnsRequestHandler>();
        services.AddScoped<IRequestHandler<ExportSettlementsRequest, ExportResultDto>, ExportSettlementsRequestHandler>();
        services.AddScoped<IRequestHandler<ExportStockTakesRequest, ExportResultDto>, ExportStockTakesRequestHandler>();
        services.AddScoped<IRequestHandler<ExportInventoryFlowRequest, ExportResultDto>, ExportInventoryFlowRequestHandler>();
        services.AddScoped<IRequestHandler<ExportStockBalanceRequest, ExportResultDto>, ExportStockBalanceRequestHandler>();
        services.AddScoped<IRequestHandler<ExportPurchaseSummaryRequest, ExportResultDto>, ExportPurchaseSummaryRequestHandler>();
        services.AddScoped<IRequestHandler<ExportSalesSummaryRequest, ExportResultDto>, ExportSalesSummaryRequestHandler>();
        services.AddScoped<IRequestHandler<ExportCostProfitRequest, ExportResultDto>, ExportCostProfitRequestHandler>();

        // 格式校验器（FluentValidation）：校验规则集中在对应用例目录；无校验器的用例（如按 id 详情）不注册
        services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
        services.AddScoped<IValidator<GetUsersRequest>, GetUsersRequestValidator>();
        services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
        services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserRequestValidator>();
        services.AddScoped<IValidator<UpdateUserStatusRequest>, UpdateUserStatusRequestValidator>();
        services.AddScoped<IValidator<ResetPasswordRequest>, ResetPasswordRequestValidator>();
        services.AddScoped<IValidator<GetLoginLogsRequest>, GetLoginLogsRequestValidator>();
        services.AddScoped<IValidator<GetAuditLogsRequest>, GetAuditLogsRequestValidator>();
        services.AddScoped<IValidator<GetRolesRequest>, GetRolesRequestValidator>();
        services.AddScoped<IValidator<CreateRoleRequest>, CreateRoleRequestValidator>();
        services.AddScoped<IValidator<UpdateRoleRequest>, UpdateRoleRequestValidator>();
        services.AddScoped<IValidator<GetProductsRequest>, GetProductsRequestValidator>();
        services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
        services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
        services.AddScoped<IValidator<UpdateProductStatusRequest>, UpdateProductStatusRequestValidator>();
        services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
        services.AddScoped<IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator>();
        services.AddScoped<IValidator<GetPartnersRequest>, GetPartnersRequestValidator>();
        services.AddScoped<IValidator<CreatePartnerRequest>, CreatePartnerRequestValidator>();
        services.AddScoped<IValidator<UpdatePartnerRequest>, UpdatePartnerRequestValidator>();
        services.AddScoped<IValidator<UpdatePartnerStatusRequest>, UpdatePartnerStatusRequestValidator>();
        services.AddScoped<IValidator<GetPartnerPricesRequest>, GetPartnerPricesRequestValidator>();
        services.AddScoped<IValidator<CreatePartnerPriceRequest>, CreatePartnerPriceRequestValidator>();
        services.AddScoped<IValidator<UpdatePartnerPriceRequest>, UpdatePartnerPriceRequestValidator>();
        services.AddScoped<IValidator<GetEffectivePricesRequest>, GetEffectivePricesRequestValidator>();
        services.AddScoped<IValidator<GetInventoryRequest>, GetInventoryRequestValidator>();
        services.AddScoped<IValidator<UpdateInventorySafetyStockRequest>, UpdateInventorySafetyStockRequestValidator>();
        services.AddScoped<IValidator<GetWarehousesRequest>, GetWarehousesRequestValidator>();
        services.AddScoped<IValidator<CreateWarehouseRequest>, CreateWarehouseRequestValidator>();
        services.AddScoped<IValidator<UpdateWarehouseRequest>, UpdateWarehouseRequestValidator>();
        services.AddScoped<IValidator<UpdateWarehouseStatusRequest>, UpdateWarehouseStatusRequestValidator>();
        services.AddScoped<IValidator<GetPurchaseReceiptsRequest>, GetPurchaseReceiptsRequestValidator>();
        services.AddScoped<IValidator<CreatePurchaseReceiptRequest>, CreatePurchaseReceiptRequestValidator>();
        services.AddScoped<IValidator<GetPurchaseOrderPicksRequest>, GetPurchaseOrderPicksRequestValidator>();
        services.AddScoped<IValidator<GetPurchaseOrderLinesRequest>, GetPurchaseOrderLinesRequestValidator>();
        services.AddScoped<IValidator<CreatePurchaseOrderRequest>, CreatePurchaseOrderRequestValidator>();
        services.AddScoped<IValidator<UpdatePurchaseOrderRequest>, UpdatePurchaseOrderRequestValidator>();
        services.AddScoped<IValidator<GetPurchaseOrdersRequest>, GetPurchaseOrdersRequestValidator>();
        services.AddScoped<IValidator<CreateSalesOrderRequest>, CreateSalesOrderRequestValidator>();
        services.AddScoped<IValidator<UpdateSalesOrderRequest>, UpdateSalesOrderRequestValidator>();
        services.AddScoped<IValidator<GetSalesOrdersRequest>, GetSalesOrdersRequestValidator>();
        services.AddScoped<IValidator<GetQuotationsRequest>, GetQuotationsRequestValidator>();
        services.AddScoped<IValidator<CreateQuotationRequest>, CreateQuotationRequestValidator>();
        services.AddScoped<IValidator<UpdateQuotationRequest>, UpdateQuotationRequestValidator>();
        services.AddScoped<IValidator<GetPurchaseReturnsRequest>, GetPurchaseReturnsRequestValidator>();
        services.AddScoped<IValidator<CreatePurchaseReturnRequest>, CreatePurchaseReturnRequestValidator>();
        services.AddScoped<IValidator<GetSalesShipmentsRequest>, GetSalesShipmentsRequestValidator>();
        services.AddScoped<IValidator<CreateSalesShipmentRequest>, CreateSalesShipmentRequestValidator>();
        services.AddScoped<IValidator<GetSalesOrderPicksRequest>, GetSalesOrderPicksRequestValidator>();
        services.AddScoped<IValidator<GetSalesOrderLinesRequest>, GetSalesOrderLinesRequestValidator>();
        services.AddScoped<IValidator<GetSalesReturnsRequest>, GetSalesReturnsRequestValidator>();
        services.AddScoped<IValidator<CreateSalesReturnRequest>, CreateSalesReturnRequestValidator>();
        services.AddScoped<IValidator<CreateSettlementRequest>, CreateSettlementRequestValidator>();
        services.AddScoped<IValidator<GetBankAccountsRequest>, GetBankAccountsRequestValidator>();
        services.AddScoped<IValidator<CreateBankAccountRequest>, CreateBankAccountRequestValidator>();
        services.AddScoped<IValidator<UpdateBankAccountRequest>, UpdateBankAccountRequestValidator>();
        services.AddScoped<IValidator<UpdateBankAccountStatusRequest>, UpdateBankAccountStatusRequestValidator>();
        services.AddScoped<IValidator<GetCashJournalRequest>, GetCashJournalRequestValidator>();
        services.AddScoped<IValidator<GetSettlementsRequest>, GetSettlementsRequestValidator>();
        services.AddScoped<IValidator<GetUnsettledOrdersRequest>, GetUnsettledOrdersRequestValidator>();
        services.AddScoped<IValidator<GetReconciliationRequest>, GetReconciliationRequestValidator>();
        services.AddScoped<IValidator<GetStockMovementsRequest>, GetStockMovementsRequestValidator>();
        services.AddScoped<IValidator<GetStockTakesRequest>, GetStockTakesRequestValidator>();
        services.AddScoped<IValidator<CreateStockTakeRequest>, CreateStockTakeRequestValidator>();
        services.AddScoped<IValidator<GetInventoryFlowRequest>, GetInventoryFlowRequestValidator>();
        services.AddScoped<IValidator<GetStockBalanceRequest>, GetStockBalanceRequestValidator>();
        services.AddScoped<IValidator<GetPurchaseSummaryRequest>, GetPurchaseSummaryRequestValidator>();
        services.AddScoped<IValidator<GetSalesSummaryRequest>, GetSalesSummaryRequestValidator>();
        services.AddScoped<IValidator<GetCostProfitReportRequest>, GetCostProfitReportRequestValidator>();
        services.AddScoped<IValidator<RecalculateCostsRequest>, RecalculateCostsRequestValidator>();
        services.AddScoped<IValidator<CreateDepartmentRequest>, CreateDepartmentRequestValidator>();
        services.AddScoped<IValidator<UpdateDepartmentRequest>, UpdateDepartmentRequestValidator>();
        services.AddScoped<IValidator<UpdateDepartmentStatusRequest>, UpdateDepartmentStatusRequestValidator>();
        services.AddScoped<IValidator<GetPositionsRequest>, GetPositionsRequestValidator>();
        services.AddScoped<IValidator<CreatePositionRequest>, CreatePositionRequestValidator>();
        services.AddScoped<IValidator<UpdatePositionRequest>, UpdatePositionRequestValidator>();
        services.AddScoped<IValidator<UpdatePositionStatusRequest>, UpdatePositionStatusRequestValidator>();
        services.AddScoped<IValidator<GetEmployeesRequest>, GetEmployeesRequestValidator>();
        services.AddScoped<IValidator<CreateEmployeeRequest>, CreateEmployeeRequestValidator>();
        services.AddScoped<IValidator<UpdateEmployeeRequest>, UpdateEmployeeRequestValidator>();
        services.AddScoped<IValidator<UpdateEmployeeStatusRequest>, UpdateEmployeeStatusRequestValidator>();
        services.AddScoped<IValidator<ExportEmployeesRequest>, ExportEmployeesRequestValidator>();
        services.AddScoped<IValidator<ExportProductsRequest>, ExportProductsRequestValidator>();
        services.AddScoped<IValidator<ExportPartnersRequest>, ExportPartnersRequestValidator>();
        services.AddScoped<IValidator<ExportInventoryRequest>, ExportInventoryRequestValidator>();
        services.AddScoped<IValidator<ExportStockMovementsRequest>, ExportStockMovementsRequestValidator>();
        services.AddScoped<IValidator<ExportPurchaseReceiptsRequest>, ExportPurchaseReceiptsRequestValidator>();
        services.AddScoped<IValidator<ExportSalesShipmentsRequest>, ExportSalesShipmentsRequestValidator>();
        services.AddScoped<IValidator<ExportPurchaseReturnsRequest>, ExportPurchaseReturnsRequestValidator>();
        services.AddScoped<IValidator<ExportSalesReturnsRequest>, ExportSalesReturnsRequestValidator>();
        services.AddScoped<IValidator<ExportSettlementsRequest>, ExportSettlementsRequestValidator>();
        services.AddScoped<IValidator<ExportStockTakesRequest>, ExportStockTakesRequestValidator>();
        services.AddScoped<IValidator<ExportInventoryFlowRequest>, ExportInventoryFlowRequestValidator>();
        services.AddScoped<IValidator<ExportStockBalanceRequest>, ExportStockBalanceRequestValidator>();
        services.AddScoped<IValidator<ExportPurchaseSummaryRequest>, ExportPurchaseSummaryRequestValidator>();
        services.AddScoped<IValidator<ExportSalesSummaryRequest>, ExportSalesSummaryRequestValidator>();
        services.AddScoped<IValidator<ExportCostProfitRequest>, ExportCostProfitRequestValidator>();
        services.AddScoped<IValidator<CreateAccountRequest>, CreateAccountRequestValidator>();
        services.AddScoped<IValidator<UpdateAccountRequest>, UpdateAccountRequestValidator>();
        services.AddScoped<IValidator<UpdateAccountStatusRequest>, UpdateAccountStatusRequestValidator>();
        services.AddScoped<IValidator<GetTaxRatesRequest>, GetTaxRatesRequestValidator>();
        services.AddScoped<IValidator<CreateTaxRateRequest>, CreateTaxRateRequestValidator>();
        services.AddScoped<IValidator<UpdateTaxRateRequest>, UpdateTaxRateRequestValidator>();
        services.AddScoped<IValidator<UpdateTaxRateStatusRequest>, UpdateTaxRateStatusRequestValidator>();
        services.AddScoped<IValidator<GetInvoicesRequest>, GetInvoicesRequestValidator>();
        services.AddScoped<IValidator<CreateInvoiceRequest>, CreateInvoiceRequestValidator>();
        services.AddScoped<IValidator<GetInvoicableOrdersRequest>, GetInvoicableOrdersRequestValidator>();
        services.AddScoped<IValidator<ExportInvoicesRequest>, ExportInvoicesRequestValidator>();
        services.AddScoped<IValidator<GetPeriodsRequest>, GetPeriodsRequestValidator>();
        services.AddScoped<IValidator<ClosePeriodRequest>, ClosePeriodRequestValidator>();
        services.AddScoped<IValidator<ReversePeriodRequest>, ReversePeriodRequestValidator>();
        services.AddScoped<IValidator<GetVouchersRequest>, GetVouchersRequestValidator>();
        services.AddScoped<IValidator<CreateVoucherRequest>, CreateVoucherRequestValidator>();
        services.AddScoped<IValidator<UpdateAccountMappingsRequest>, UpdateAccountMappingsRequestValidator>();
        services.AddScoped<IValidator<GetAccountBalanceRequest>, GetAccountBalanceRequestValidator>();
        services.AddScoped<IValidator<GetBalanceSheetRequest>, GetBalanceSheetRequestValidator>();
        services.AddScoped<IValidator<GetIncomeStatementRequest>, GetIncomeStatementRequestValidator>();

        return services;
    }
}
