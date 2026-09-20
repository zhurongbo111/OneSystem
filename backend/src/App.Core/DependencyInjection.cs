using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Exports;
using App.Core.Features.Auth.Login;
using App.Core.Features.Categories;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Costs;
using App.Core.Features.Costs.RecalculateCosts;
using App.Core.Features.Categories.DeleteCategory;
using App.Core.Features.Categories.GetCategories;
using App.Core.Features.Categories.GetCategoriesPaged;
using App.Core.Features.Categories.UpdateCategory;
using App.Core.Features.Inventory;
using App.Core.Features.Inventory.ExportInventory;
using App.Core.Features.Inventory.GetInventory;
using App.Core.Features.LoginLogs;
using App.Core.Features.LoginLogs.GetLoginLogs;
using App.Core.Features.Partners;
using App.Core.Features.Partners.CreatePartner;
using App.Core.Features.Partners.ExportPartners;
using App.Core.Features.Partners.GetPartnerById;
using App.Core.Features.Partners.GetPartners;
using App.Core.Features.Partners.UpdatePartner;
using App.Core.Features.Partners.UpdatePartnerStatus;
using App.Core.Features.Products;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.ExportProducts;
using App.Core.Features.Products.GetProductById;
using App.Core.Features.Products.GetProductPickList;
using App.Core.Features.Products.GetProducts;
using App.Core.Features.Products.UpdateProduct;
using App.Core.Features.Products.UpdateProductStatus;
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
using App.Core.Features.Reports.ExportInventoryFlow;
using App.Core.Features.Reports.ExportPurchaseSummary;
using App.Core.Features.Reports.ExportSalesSummary;
using App.Core.Features.Reports.ExportStockBalance;
using App.Core.Features.Reports.GetCostProfitReport;
using App.Core.Features.Reports.GetInventoryFlow;
using App.Core.Features.Reports.GetPurchaseSummary;
using App.Core.Features.Reports.GetSalesSummary;
using App.Core.Features.Reports.GetStockBalance;
using App.Core.Features.SalesOrders;
using App.Core.Features.SalesOrders.CloseSalesOrder;
using App.Core.Features.SalesOrders.CreateSalesOrder;
using App.Core.Features.SalesOrders.GetSalesOrderById;
using App.Core.Features.SalesOrders.GetSalesOrders;
using App.Core.Features.SalesOrders.UpdateSalesOrder;
using App.Core.Features.SalesOrders.VoidSalesOrder;
using App.Core.Features.SalesShipments;
using App.Core.Features.SalesShipments.CreateSalesShipment;
using App.Core.Features.SalesShipments.ExportSalesShipments;
using App.Core.Features.SalesShipments.GetSalesOrderLines;
using App.Core.Features.SalesShipments.GetSalesOrderPicks;
using App.Core.Features.SalesShipments.GetSalesShipmentById;
using App.Core.Features.SalesShipments.GetSalesShipments;
using App.Core.Features.SalesShipments.VoidSalesShipment;
using App.Core.Features.SalesReturns;
using App.Core.Features.SalesReturns.CreateSalesReturn;
using App.Core.Features.SalesReturns.ExportSalesReturns;
using App.Core.Features.SalesReturns.GetSalesReturnById;
using App.Core.Features.SalesReturns.GetSalesReturns;
using App.Core.Features.SalesReturns.VoidSalesReturn;
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
using App.Core.Features.Users;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.GetCurrentUser;
using App.Core.Features.Users.GetUserById;
using App.Core.Features.Users.GetUsers;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Core.Features.Users.UpdateUserStatus;
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
        services.AddScoped<IValidator<GetInventoryRequest>, GetInventoryRequestValidator>();
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
        services.AddScoped<IValidator<GetPurchaseReturnsRequest>, GetPurchaseReturnsRequestValidator>();
        services.AddScoped<IValidator<CreatePurchaseReturnRequest>, CreatePurchaseReturnRequestValidator>();
        services.AddScoped<IValidator<GetSalesShipmentsRequest>, GetSalesShipmentsRequestValidator>();
        services.AddScoped<IValidator<CreateSalesShipmentRequest>, CreateSalesShipmentRequestValidator>();
        services.AddScoped<IValidator<GetSalesOrderPicksRequest>, GetSalesOrderPicksRequestValidator>();
        services.AddScoped<IValidator<GetSalesOrderLinesRequest>, GetSalesOrderLinesRequestValidator>();
        services.AddScoped<IValidator<GetSalesReturnsRequest>, GetSalesReturnsRequestValidator>();
        services.AddScoped<IValidator<CreateSalesReturnRequest>, CreateSalesReturnRequestValidator>();
        services.AddScoped<IValidator<CreateSettlementRequest>, CreateSettlementRequestValidator>();
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

        return services;
    }
}
