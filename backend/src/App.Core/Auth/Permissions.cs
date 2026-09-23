namespace App.Core.Auth;

/// <summary>
/// 权限点清单（唯一事实源，新增权限点在此续行并同步 <c>specs/028-erp-rbac/design.md</c> §0.2 总表）。
/// key 规则：<c>&lt;域&gt;.&lt;动作&gt;</c>，全小写驼峰；菜单级权限 = <c>&lt;域&gt;.view</c>，按钮级各自成点。
/// 权限点是代码常量（改动需发版），角色 → 权限点的映射存库（运行时可变），避免前后端清单漂移。
/// </summary>
public static class Permissions
{
    // ===== 商品分类 =====
    /// <summary>查看商品分类</summary>
    public const string CategoriesView = "categories.view";

    /// <summary>新增商品分类</summary>
    public const string CategoriesCreate = "categories.create";

    /// <summary>编辑商品分类</summary>
    public const string CategoriesUpdate = "categories.update";

    /// <summary>删除商品分类</summary>
    public const string CategoriesDelete = "categories.delete";

    // ===== 商品 =====
    /// <summary>查看商品</summary>
    public const string ProductsView = "products.view";

    /// <summary>新增商品</summary>
    public const string ProductsCreate = "products.create";

    /// <summary>编辑商品</summary>
    public const string ProductsUpdate = "products.update";

    /// <summary>启用 / 停用商品</summary>
    public const string ProductsStatus = "products.status";

    /// <summary>导出商品</summary>
    public const string ProductsExport = "products.export";

    // ===== 往来单位 =====
    /// <summary>查看往来单位</summary>
    public const string PartnersView = "partners.view";

    /// <summary>新增往来单位</summary>
    public const string PartnersCreate = "partners.create";

    /// <summary>编辑往来单位</summary>
    public const string PartnersUpdate = "partners.update";

    /// <summary>启用 / 停用往来单位</summary>
    public const string PartnersStatus = "partners.status";

    /// <summary>导出往来单位</summary>
    public const string PartnersExport = "partners.export";

    // ===== 仓库（038） =====
    /// <summary>查看仓库</summary>
    public const string WarehousesView = "warehouses.view";

    /// <summary>新增仓库</summary>
    public const string WarehousesCreate = "warehouses.create";

    /// <summary>编辑仓库</summary>
    public const string WarehousesUpdate = "warehouses.update";

    /// <summary>启用 / 停用仓库</summary>
    public const string WarehousesStatus = "warehouses.status";

    // ===== 采购订单（024） =====
    /// <summary>查看采购订单</summary>
    public const string PurchaseOrdersView = "purchaseOrders.view";

    /// <summary>新增采购订单</summary>
    public const string PurchaseOrdersCreate = "purchaseOrders.create";

    /// <summary>编辑采购订单</summary>
    public const string PurchaseOrdersUpdate = "purchaseOrders.update";

    /// <summary>作废采购订单</summary>
    public const string PurchaseOrdersVoid = "purchaseOrders.void";

    /// <summary>关闭采购订单</summary>
    public const string PurchaseOrdersClose = "purchaseOrders.close";

    // ===== 采购入库 =====
    /// <summary>查看采购入库单</summary>
    public const string PurchasesView = "purchases.view";

    /// <summary>新增采购入库单</summary>
    public const string PurchasesCreate = "purchases.create";

    /// <summary>作废采购入库单</summary>
    public const string PurchasesVoid = "purchases.void";

    /// <summary>导出采购入库单</summary>
    public const string PurchasesExport = "purchases.export";

    // ===== 采购退货 =====
    /// <summary>查看采购退货单</summary>
    public const string PurchaseReturnsView = "purchaseReturns.view";

    /// <summary>新增采购退货单</summary>
    public const string PurchaseReturnsCreate = "purchaseReturns.create";

    /// <summary>作废采购退货单</summary>
    public const string PurchaseReturnsVoid = "purchaseReturns.void";

    /// <summary>采购退货单结算</summary>
    public const string PurchaseReturnsSettle = "purchaseReturns.settle";

    /// <summary>导出采购退货单</summary>
    public const string PurchaseReturnsExport = "purchaseReturns.export";

    // ===== 销售订单（024） =====
    /// <summary>查看销售订单</summary>
    public const string SalesOrdersView = "salesOrders.view";

    /// <summary>新增销售订单</summary>
    public const string SalesOrdersCreate = "salesOrders.create";

    /// <summary>编辑销售订单</summary>
    public const string SalesOrdersUpdate = "salesOrders.update";

    /// <summary>作废销售订单</summary>
    public const string SalesOrdersVoid = "salesOrders.void";

    /// <summary>关闭销售订单</summary>
    public const string SalesOrdersClose = "salesOrders.close";

    // ===== 销售出库 =====
    /// <summary>查看销售出库单</summary>
    public const string SalesView = "sales.view";

    /// <summary>新增销售出库单</summary>
    public const string SalesCreate = "sales.create";

    /// <summary>作废销售出库单</summary>
    public const string SalesVoid = "sales.void";

    /// <summary>导出销售出库单</summary>
    public const string SalesExport = "sales.export";

    // ===== 销售退货 =====
    /// <summary>查看销售退货单</summary>
    public const string SalesReturnsView = "salesReturns.view";

    /// <summary>新增销售退货单</summary>
    public const string SalesReturnsCreate = "salesReturns.create";

    /// <summary>作废销售退货单</summary>
    public const string SalesReturnsVoid = "salesReturns.void";

    /// <summary>销售退货单结算</summary>
    public const string SalesReturnsSettle = "salesReturns.settle";

    /// <summary>导出销售退货单</summary>
    public const string SalesReturnsExport = "salesReturns.export";

    // ===== 库存查询 =====
    /// <summary>查看库存</summary>
    public const string InventoryView = "inventory.view";

    /// <summary>导出库存</summary>
    public const string InventoryExport = "inventory.export";

    // ===== 库存流水 =====
    /// <summary>查看库存流水</summary>
    public const string StockMovementsView = "stockMovements.view";

    /// <summary>导出库存流水</summary>
    public const string StockMovementsExport = "stockMovements.export";

    // ===== 库存盘点 =====
    /// <summary>查看盘点单</summary>
    public const string StockTakesView = "stockTakes.view";

    /// <summary>新增盘点单</summary>
    public const string StockTakesCreate = "stockTakes.create";

    /// <summary>导出盘点单</summary>
    public const string StockTakesExport = "stockTakes.export";

    // ===== 调拨单（039） =====
    /// <summary>查看调拨单</summary>
    public const string TransfersView = "transfers.view";

    /// <summary>新增调拨单</summary>
    public const string TransfersCreate = "transfers.create";

    /// <summary>作废调拨单</summary>
    public const string TransfersVoid = "transfers.void";

    /// <summary>导出调拨单</summary>
    public const string TransfersExport = "transfers.export";

    // ===== 批次管理（040） =====
    /// <summary>查看批次</summary>
    public const string BatchesView = "batches.view";

    /// <summary>新增批次</summary>
    public const string BatchesCreate = "batches.create";

    /// <summary>编辑批次</summary>
    public const string BatchesUpdate = "batches.update";

    // ===== 收付款 =====
    /// <summary>查看收付款单</summary>
    public const string SettlementsView = "settlements.view";

    /// <summary>新增收付款单</summary>
    public const string SettlementsCreate = "settlements.create";

    /// <summary>作废收付款单</summary>
    public const string SettlementsVoid = "settlements.void";

    /// <summary>导出收付款单</summary>
    public const string SettlementsExport = "settlements.export";

    // ===== 往来对账 =====
    /// <summary>查看往来对账</summary>
    public const string ReconciliationView = "reconciliation.view";

    // ===== 发票管理（032） =====
    /// <summary>查看发票</summary>
    public const string InvoicesView = "invoices.view";

    /// <summary>新增发票</summary>
    public const string InvoicesCreate = "invoices.create";

    /// <summary>作废发票</summary>
    public const string InvoicesVoid = "invoices.void";

    /// <summary>导出发票</summary>
    public const string InvoicesExport = "invoices.export";

    // ===== 客户价格（036） =====
    /// <summary>查看客户价格</summary>
    public const string PartnerPricesView = "partnerPrices.view";

    /// <summary>新增客户价格</summary>
    public const string PartnerPricesCreate = "partnerPrices.create";

    /// <summary>编辑客户价格</summary>
    public const string PartnerPricesUpdate = "partnerPrices.update";

    /// <summary>删除客户价格</summary>
    public const string PartnerPricesDelete = "partnerPrices.delete";

    // ===== 报表 =====
    /// <summary>查看报表</summary>
    public const string ReportsView = "reports.view";

    /// <summary>导出报表</summary>
    public const string ReportsExport = "reports.export";

    // ===== 成本核算 =====
    /// <summary>成本重算</summary>
    public const string CostsRecalculate = "costs.recalculate";

    // ===== 用户管理 =====
    /// <summary>查看用户</summary>
    public const string UsersView = "users.view";

    /// <summary>新增用户</summary>
    public const string UsersCreate = "users.create";

    /// <summary>编辑用户</summary>
    public const string UsersUpdate = "users.update";

    /// <summary>启用 / 禁用用户</summary>
    public const string UsersStatus = "users.status";

    /// <summary>重置用户密码</summary>
    public const string UsersResetPassword = "users.resetPassword";

    // ===== 登录日志 =====
    /// <summary>查看登录日志</summary>
    public const string LoginLogsView = "loginLogs.view";

    // ===== 角色权限 =====
    /// <summary>查看角色</summary>
    public const string RolesView = "roles.view";

    /// <summary>新增角色</summary>
    public const string RolesCreate = "roles.create";

    /// <summary>编辑角色</summary>
    public const string RolesUpdate = "roles.update";

    /// <summary>删除角色</summary>
    public const string RolesDelete = "roles.delete";

    // ===== 操作日志（029） =====
    /// <summary>查看操作日志</summary>
    public const string AuditLogsView = "auditLogs.view";

    // ===== 组织人事（030） =====
    /// <summary>查看部门</summary>
    public const string DepartmentsView = "departments.view";

    /// <summary>新增部门</summary>
    public const string DepartmentsCreate = "departments.create";

    /// <summary>编辑部门</summary>
    public const string DepartmentsUpdate = "departments.update";

    /// <summary>删除部门</summary>
    public const string DepartmentsDelete = "departments.delete";

    /// <summary>启用 / 停用部门</summary>
    public const string DepartmentsStatus = "departments.status";

    /// <summary>查看岗位</summary>
    public const string PositionsView = "positions.view";

    /// <summary>新增岗位</summary>
    public const string PositionsCreate = "positions.create";

    /// <summary>编辑岗位</summary>
    public const string PositionsUpdate = "positions.update";

    /// <summary>删除岗位</summary>
    public const string PositionsDelete = "positions.delete";

    /// <summary>启用 / 停用岗位</summary>
    public const string PositionsStatus = "positions.status";

    /// <summary>查看员工档案</summary>
    public const string EmployeesView = "employees.view";

    /// <summary>新增员工</summary>
    public const string EmployeesCreate = "employees.create";

    /// <summary>编辑员工</summary>
    public const string EmployeesUpdate = "employees.update";

    /// <summary>在职 / 离职切换</summary>
    public const string EmployeesStatus = "employees.status";

    /// <summary>导出员工档案</summary>
    public const string EmployeesExport = "employees.export";

    // ===== 财务主数据（031） =====
    /// <summary>查看会计科目</summary>
    public const string AccountsView = "accounts.view";

    /// <summary>新增会计科目</summary>
    public const string AccountsCreate = "accounts.create";

    /// <summary>编辑会计科目</summary>
    public const string AccountsUpdate = "accounts.update";

    /// <summary>删除会计科目</summary>
    public const string AccountsDelete = "accounts.delete";

    /// <summary>启用 / 停用会计科目</summary>
    public const string AccountsStatus = "accounts.status";

    /// <summary>查看税率</summary>
    public const string TaxRatesView = "taxRates.view";

    /// <summary>新增税率</summary>
    public const string TaxRatesCreate = "taxRates.create";

    /// <summary>编辑税率</summary>
    public const string TaxRatesUpdate = "taxRates.update";

    /// <summary>删除税率</summary>
    public const string TaxRatesDelete = "taxRates.delete";

    /// <summary>启用 / 停用税率</summary>
    public const string TaxRatesStatus = "taxRates.status";

    // ===== 总账凭证（033） =====
    /// <summary>查看凭证 / 会计期间</summary>
    public const string VouchersView = "vouchers.view";

    /// <summary>录入手工凭证</summary>
    public const string VouchersCreate = "vouchers.create";

    /// <summary>作废凭证</summary>
    public const string VouchersVoid = "vouchers.void";

    /// <summary>会计期间结账 / 反结账</summary>
    public const string VouchersClose = "vouchers.close";

    /// <summary>维护科目映射</summary>
    public const string VouchersUpdateMapping = "vouchers.updateMapping";

    // ===== 资金出纳（034） =====
    /// <summary>查看资金账户</summary>
    public const string BankAccountsView = "bankAccounts.view";

    /// <summary>新增资金账户</summary>
    public const string BankAccountsCreate = "bankAccounts.create";

    /// <summary>编辑资金账户</summary>
    public const string BankAccountsUpdate = "bankAccounts.update";

    /// <summary>删除资金账户</summary>
    public const string BankAccountsDelete = "bankAccounts.delete";

    /// <summary>启用 / 停用资金账户</summary>
    public const string BankAccountsStatus = "bankAccounts.status";

    /// <summary>查看资金日记账</summary>
    public const string CashJournalsView = "cashJournals.view";

    // ===== 财务报表（033） =====
    /// <summary>查看科目余额表 / 资产负债表 / 利润表</summary>
    public const string FinancialReportsView = "financialReports.view";

    // ===== 站内消息（041） =====
    /// <summary>查看站内消息</summary>
    public const string NotificationsView = "notifications.view";

    // ===== 单据审批（042） =====
    /// <summary>查看单据审批</summary>
    public const string ApprovalsView = "approvals.view";

    /// <summary>审批单据</summary>
    public const string ApprovalsApprove = "approvals.approve";

    /// <summary>
    /// 全量权限点集合（超级管理员解析结果与权限点合法性校验基准）。
    /// 新增权限点必须在此登记，否则 Controller 上的 [RequirePermission] 会被清单守卫测试判为非法 key。
    /// </summary>
    public static IReadOnlyList<string> All { get; } =
    [
        CategoriesView, CategoriesCreate, CategoriesUpdate, CategoriesDelete,
        ProductsView, ProductsCreate, ProductsUpdate, ProductsStatus, ProductsExport,
        PartnersView, PartnersCreate, PartnersUpdate, PartnersStatus, PartnersExport,
        WarehousesView, WarehousesCreate, WarehousesUpdate, WarehousesStatus,
        PurchaseOrdersView, PurchaseOrdersCreate, PurchaseOrdersUpdate, PurchaseOrdersVoid, PurchaseOrdersClose,
        PurchasesView, PurchasesCreate, PurchasesVoid, PurchasesExport,
        PurchaseReturnsView, PurchaseReturnsCreate, PurchaseReturnsVoid, PurchaseReturnsSettle, PurchaseReturnsExport,
        SalesOrdersView, SalesOrdersCreate, SalesOrdersUpdate, SalesOrdersVoid, SalesOrdersClose,
        SalesView, SalesCreate, SalesVoid, SalesExport,
        SalesReturnsView, SalesReturnsCreate, SalesReturnsVoid, SalesReturnsSettle, SalesReturnsExport,
        InventoryView, InventoryExport,
        StockMovementsView, StockMovementsExport,
        StockTakesView, StockTakesCreate, StockTakesExport,
        TransfersView, TransfersCreate, TransfersVoid, TransfersExport,
        BatchesView, BatchesCreate, BatchesUpdate,
        SettlementsView, SettlementsCreate, SettlementsVoid, SettlementsExport,
        ReconciliationView,
        InvoicesView, InvoicesCreate, InvoicesVoid, InvoicesExport,
        PartnerPricesView, PartnerPricesCreate, PartnerPricesUpdate, PartnerPricesDelete,
        ReportsView, ReportsExport,
        CostsRecalculate,
        UsersView, UsersCreate, UsersUpdate, UsersStatus, UsersResetPassword,
        LoginLogsView,
        RolesView, RolesCreate, RolesUpdate, RolesDelete,
        AuditLogsView,
        DepartmentsView, DepartmentsCreate, DepartmentsUpdate, DepartmentsDelete, DepartmentsStatus,
        PositionsView, PositionsCreate, PositionsUpdate, PositionsDelete, PositionsStatus,
        EmployeesView, EmployeesCreate, EmployeesUpdate, EmployeesStatus, EmployeesExport,
        AccountsView, AccountsCreate, AccountsUpdate, AccountsDelete, AccountsStatus,
        TaxRatesView, TaxRatesCreate, TaxRatesUpdate, TaxRatesDelete, TaxRatesStatus,
        VouchersView, VouchersCreate, VouchersVoid, VouchersClose, VouchersUpdateMapping,
        BankAccountsView, BankAccountsCreate, BankAccountsUpdate, BankAccountsDelete, BankAccountsStatus,
        CashJournalsView,
        FinancialReportsView,
        NotificationsView,
        ApprovalsView, ApprovalsApprove,
    ];

    /// <summary>
    /// 默认角色（<c>Staff</c>）排除的权限点：系统管理与运维动作不开放给普通员工。
    /// </summary>
    public static IReadOnlyList<string> ExcludedFromStaff { get; } =
    [
        UsersView, UsersCreate, UsersUpdate, UsersStatus, UsersResetPassword,
        RolesView, RolesCreate, RolesUpdate, RolesDelete,
        AuditLogsView,
        CostsRecalculate,
        LoginLogsView,
    ];

    /// <summary>
    /// 权限点分组元数据（顺序即权限树的展示顺序，对应前端菜单分组）。
    /// </summary>
    public static IReadOnlyList<PermissionGroup> Groups { get; } =
    [
        new("商品分类", [new PermissionItem(CategoriesView, "查看"), new PermissionItem(CategoriesCreate, "新增"), new PermissionItem(CategoriesUpdate, "编辑"), new PermissionItem(CategoriesDelete, "删除")]),
        new("商品管理", [new PermissionItem(ProductsView, "查看"), new PermissionItem(ProductsCreate, "新增"), new PermissionItem(ProductsUpdate, "编辑"), new PermissionItem(ProductsStatus, "启用 / 停用"), new PermissionItem(ProductsExport, "导出")]),
        new("往来单位", [new PermissionItem(PartnersView, "查看"), new PermissionItem(PartnersCreate, "新增"), new PermissionItem(PartnersUpdate, "编辑"), new PermissionItem(PartnersStatus, "启用 / 停用"), new PermissionItem(PartnersExport, "导出")]),
        new("仓库管理", [new PermissionItem(WarehousesView, "查看"), new PermissionItem(WarehousesCreate, "新增"), new PermissionItem(WarehousesUpdate, "编辑"), new PermissionItem(WarehousesStatus, "启用 / 停用")]),
        new("采购订单", [new PermissionItem(PurchaseOrdersView, "查看"), new PermissionItem(PurchaseOrdersCreate, "新增"), new PermissionItem(PurchaseOrdersUpdate, "编辑"), new PermissionItem(PurchaseOrdersVoid, "作废"), new PermissionItem(PurchaseOrdersClose, "关闭")]),
        new("采购入库", [new PermissionItem(PurchasesView, "查看"), new PermissionItem(PurchasesCreate, "新增"), new PermissionItem(PurchasesVoid, "作废"), new PermissionItem(PurchasesExport, "导出")]),
        new("采购退货", [new PermissionItem(PurchaseReturnsView, "查看"), new PermissionItem(PurchaseReturnsCreate, "新增"), new PermissionItem(PurchaseReturnsVoid, "作废"), new PermissionItem(PurchaseReturnsSettle, "结算"), new PermissionItem(PurchaseReturnsExport, "导出")]),
        new("销售订单", [new PermissionItem(SalesOrdersView, "查看"), new PermissionItem(SalesOrdersCreate, "新增"), new PermissionItem(SalesOrdersUpdate, "编辑"), new PermissionItem(SalesOrdersVoid, "作废"), new PermissionItem(SalesOrdersClose, "关闭")]),
        new("销售出库", [new PermissionItem(SalesView, "查看"), new PermissionItem(SalesCreate, "新增"), new PermissionItem(SalesVoid, "作废"), new PermissionItem(SalesExport, "导出")]),
        new("销售退货", [new PermissionItem(SalesReturnsView, "查看"), new PermissionItem(SalesReturnsCreate, "新增"), new PermissionItem(SalesReturnsVoid, "作废"), new PermissionItem(SalesReturnsSettle, "结算"), new PermissionItem(SalesReturnsExport, "导出")]),
        new("库存查询", [new PermissionItem(InventoryView, "查看"), new PermissionItem(InventoryExport, "导出")]),
        new("库存流水", [new PermissionItem(StockMovementsView, "查看"), new PermissionItem(StockMovementsExport, "导出")]),
        new("库存盘点", [new PermissionItem(StockTakesView, "查看"), new PermissionItem(StockTakesCreate, "新增"), new PermissionItem(StockTakesExport, "导出")]),
        new("调拨单", [new PermissionItem(TransfersView, "查看"), new PermissionItem(TransfersCreate, "新增"), new PermissionItem(TransfersVoid, "作废"), new PermissionItem(TransfersExport, "导出")]),
        new("批次管理", [new PermissionItem(BatchesView, "查看"), new PermissionItem(BatchesCreate, "新增"), new PermissionItem(BatchesUpdate, "编辑")]),
        new("收付款", [new PermissionItem(SettlementsView, "查看"), new PermissionItem(SettlementsCreate, "新增"), new PermissionItem(SettlementsVoid, "作废"), new PermissionItem(SettlementsExport, "导出")]),
        new("往来对账", [new PermissionItem(ReconciliationView, "查看")]),
        new("发票管理", [new PermissionItem(InvoicesView, "查看"), new PermissionItem(InvoicesCreate, "新增"), new PermissionItem(InvoicesVoid, "作废"), new PermissionItem(InvoicesExport, "导出")]),
        new("客户价格", [new PermissionItem(PartnerPricesView, "查看"), new PermissionItem(PartnerPricesCreate, "新增"), new PermissionItem(PartnerPricesUpdate, "编辑"), new PermissionItem(PartnerPricesDelete, "删除")]),
        new("报表", [new PermissionItem(ReportsView, "查看"), new PermissionItem(ReportsExport, "导出")]),
        new("成本核算", [new PermissionItem(CostsRecalculate, "成本重算")]),
        new("用户管理", [new PermissionItem(UsersView, "查看"), new PermissionItem(UsersCreate, "新增"), new PermissionItem(UsersUpdate, "编辑"), new PermissionItem(UsersStatus, "启用 / 停用"), new PermissionItem(UsersResetPassword, "重置密码")]),
        new("登录日志", [new PermissionItem(LoginLogsView, "查看")]),
        new("角色权限", [new PermissionItem(RolesView, "查看"), new PermissionItem(RolesCreate, "新增"), new PermissionItem(RolesUpdate, "编辑"), new PermissionItem(RolesDelete, "删除")]),
        new("操作日志", [new PermissionItem(AuditLogsView, "查看")]),
        new("部门管理", [new PermissionItem(DepartmentsView, "查看"), new PermissionItem(DepartmentsCreate, "新增"), new PermissionItem(DepartmentsUpdate, "编辑"), new PermissionItem(DepartmentsDelete, "删除"), new PermissionItem(DepartmentsStatus, "启用 / 停用")]),
        new("岗位管理", [new PermissionItem(PositionsView, "查看"), new PermissionItem(PositionsCreate, "新增"), new PermissionItem(PositionsUpdate, "编辑"), new PermissionItem(PositionsDelete, "删除"), new PermissionItem(PositionsStatus, "启用 / 停用")]),
        new("员工档案", [new PermissionItem(EmployeesView, "查看"), new PermissionItem(EmployeesCreate, "新增"), new PermissionItem(EmployeesUpdate, "编辑"), new PermissionItem(EmployeesStatus, "在职 / 离职"), new PermissionItem(EmployeesExport, "导出")]),
        new("会计科目", [new PermissionItem(AccountsView, "查看"), new PermissionItem(AccountsCreate, "新增"), new PermissionItem(AccountsUpdate, "编辑"), new PermissionItem(AccountsDelete, "删除"), new PermissionItem(AccountsStatus, "启用 / 停用")]),
        new("税率", [new PermissionItem(TaxRatesView, "查看"), new PermissionItem(TaxRatesCreate, "新增"), new PermissionItem(TaxRatesUpdate, "编辑"), new PermissionItem(TaxRatesDelete, "删除"), new PermissionItem(TaxRatesStatus, "启用 / 停用")]),
        new("凭证", [new PermissionItem(VouchersView, "查看"), new PermissionItem(VouchersCreate, "录入"), new PermissionItem(VouchersVoid, "作废"), new PermissionItem(VouchersClose, "结账 / 反结账"), new PermissionItem(VouchersUpdateMapping, "科目映射")]),
        new("资金账户", [new PermissionItem(BankAccountsView, "查看"), new PermissionItem(BankAccountsCreate, "新增"), new PermissionItem(BankAccountsUpdate, "编辑"), new PermissionItem(BankAccountsDelete, "删除"), new PermissionItem(BankAccountsStatus, "启用 / 停用")]),
        new("资金日记账", [new PermissionItem(CashJournalsView, "查看")]),
        new("财务报表", [new PermissionItem(FinancialReportsView, "查看")]),
        new("站内消息", [new PermissionItem(NotificationsView, "查看")]),
        new("单据审批", [new PermissionItem(ApprovalsView, "查看"), new PermissionItem(ApprovalsApprove, "审批")]),
    ];

    /// <summary>
    /// 判断权限点 key 是否为已登记的合法权限点
    /// </summary>
    /// <param name="key">权限点 key</param>
    public static bool IsKnown(string key) => key is not null && All.Contains(key, StringComparer.Ordinal);

    /// <summary>
    /// 取权限点的中文标签（<c>分组.动作名</c>，如 <c>操作日志.查看</c>）；未登记的 key 原样返回。
    /// 用于需要人读权限点差异的场景（如操作日志），前端展示文案仍以 <see cref="Groups"/> 为准。
    /// </summary>
    /// <param name="key">权限点 key</param>
    public static string LabelOf(string key)
    {
        if (key is null)
        {
            return string.Empty;
        }

        foreach (var group in Groups)
        {
            foreach (var item in group.Items)
            {
                if (string.Equals(item.Key, key, StringComparison.Ordinal))
                {
                    return $"{group.Name}.{item.Name}";
                }
            }
        }

        return key;
    }
}
