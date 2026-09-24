using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure;

/// <summary>
/// 应用 DbContext（PostgreSQL / Npgsql）。
/// 表结构通过 EF Core Migrations 管理；实体配置集中在 Persistence/Configurations 下。
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// 初始化 DbContext
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>用户表</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>用户登录日志表</summary>
    public DbSet<UserLoginLog> UserLoginLogs => Set<UserLoginLog>();

    /// <summary>商品分类表</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>商品表</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>库存台账表（商品 × 仓库，038-erp-multi-warehouse）</summary>
    public DbSet<Inventory> Inventory => Set<Inventory>();

    /// <summary>仓库表（038-erp-multi-warehouse）</summary>
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    /// <summary>往来单位表（供应商 / 客户合并）</summary>
    public DbSet<Partner> Partners => Set<Partner>();

    /// <summary>采购入库单表</summary>
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    /// <summary>采购入库单明细表</summary>
    public DbSet<PurchaseReceiptItem> PurchaseReceiptItems => Set<PurchaseReceiptItem>();

    /// <summary>采购订单表</summary>
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    /// <summary>采购订单明细表</summary>
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    /// <summary>销售出库单表</summary>
    public DbSet<SalesShipment> SalesShipments => Set<SalesShipment>();

    /// <summary>销售出库单明细表</summary>
    public DbSet<SalesShipmentItem> SalesShipmentItems => Set<SalesShipmentItem>();

    /// <summary>销售订单表</summary>
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    /// <summary>销售订单明细表</summary>
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();

    /// <summary>采购退货单表</summary>
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();

    /// <summary>采购退货单明细表</summary>
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();

    /// <summary>销售退货单表</summary>
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();

    /// <summary>销售退货单明细表</summary>
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();

    /// <summary>库存变动流水表（纯追加）</summary>
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    /// <summary>盘点 / 期初建账单据表</summary>
    public DbSet<StockTake> StockTakes => Set<StockTake>();

    /// <summary>盘点 / 期初建账明细表</summary>
    public DbSet<StockTakeItem> StockTakeItems => Set<StockTakeItem>();

    /// <summary>收付款单表</summary>
    public DbSet<Settlement> Settlements => Set<Settlement>();

    /// <summary>收付款单核销明细表</summary>
    public DbSet<SettlementItem> SettlementItems => Set<SettlementItem>();

    /// <summary>角色表</summary>
    public DbSet<Role> Roles => Set<Role>();

    /// <summary>角色权限关联表</summary>
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    /// <summary>用户角色关联表</summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>业务操作审计日志表（纯追加）</summary>
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>部门表（树形，ParentId 自引用）</summary>
    public DbSet<Department> Departments => Set<Department>();

    /// <summary>岗位表（独立字典）</summary>
    public DbSet<Position> Positions => Set<Position>();

    /// <summary>员工档案表</summary>
    public DbSet<Employee> Employees => Set<Employee>();

    /// <summary>会计科目表（树形，ParentId 自引用）</summary>
    public DbSet<Account> Accounts => Set<Account>();

    /// <summary>税率字典表</summary>
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();

    /// <summary>发票主表</summary>
    public DbSet<Invoice> Invoices => Set<Invoice>();

    /// <summary>发票关联单据明细表</summary>
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    /// <summary>会计期间表（按「年-月」唯一）</summary>
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    /// <summary>记账凭证主表</summary>
    public DbSet<Voucher> Vouchers => Set<Voucher>();

    /// <summary>凭证分录表</summary>
    public DbSet<VoucherEntry> VoucherEntries => Set<VoucherEntry>();

    /// <summary>科目映射表（业务事件 → 会计科目）</summary>
    public DbSet<AccountMapping> AccountMappings => Set<AccountMapping>();

    /// <summary>资金账户表（现金 / 银行存款，034-erp-cash）</summary>
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    /// <summary>客户协议价表（客户 × 商品，036-erp-partner-price）</summary>
    public DbSet<PartnerPrice> PartnerPrices => Set<PartnerPrice>();

    /// <summary>报价单主表（037-erp-quotation）</summary>
    public DbSet<Quotation> Quotations => Set<Quotation>();

    /// <summary>报价单明细表（037-erp-quotation）</summary>
    public DbSet<QuotationItem> QuotationItems => Set<QuotationItem>();

    /// <summary>调拨单表（039-erp-transfer）</summary>
    public DbSet<Transfer> Transfers => Set<Transfer>();

    /// <summary>调拨单明细表（039-erp-transfer）</summary>
    public DbSet<TransferItem> TransferItems => Set<TransferItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}