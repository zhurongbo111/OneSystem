using App.Core.Abstractions;
using App.Infrastructure.Audit;
using App.Infrastructure.Auth;
using App.Infrastructure.Exports;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

/// <summary>
/// App.Infrastructure 服务注册扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 EF Core（PostgreSQL / Npgsql）、IUnitOfWork 与仓储实现。
    /// 连接串为敏感配置，只从环境变量 ConnectionStrings__Default 读取（AGENTS.md §7），
    /// 未配置时退化为不含凭据的本地占位串以允许启动，首次查库会因缺少凭据报错。
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            // 连接串（含账号口令）禁止入库 / 硬编码，只从环境变量 ConnectionStrings__Default 注入；
            // 缺失或为空时退化为不含凭据的本地占位串，保证无数据库访问的场景仍可启动。
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = "Host=localhost;Port=5432;Database=app";
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Excel 导出器（erp-export）：无状态纯计算，注册为单例
        services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();

        // EF Core 仓储实现（首个业务功能起替换脚手架的内存实现）
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserLoginLogRepository, UserLoginLogRepository>();

        // 权限解析（erp-rbac）：Scoped + 单请求内缓存，用户 → 角色 → 权限点并集
        services.AddScoped<IPermissionResolver, PermissionResolver>();

        // 角色与用户角色关联仓储（erp-rbac）
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IPurchaseReceiptRepository, PurchaseReceiptRepository>();
        services.AddScoped<ISalesShipmentRepository, SalesShipmentRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();
        services.AddScoped<IPurchaseReturnRepository, PurchaseReturnRepository>();
        services.AddScoped<ISalesReturnRepository, SalesReturnRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IStockTakeRepository, StockTakeRepository>();
        services.AddScoped<ISettlementRepository, SettlementRepository>();
        services.AddScoped<ISettlementQueryRepository, SettlementQueryRepository>();
        services.AddScoped<IReportQueryRepository, ReportQueryRepository>();

        // 组织人事（erp-org-employee）：部门 / 岗位 / 员工
        services.AddScoped<IDepartmentRepository, DepartmentRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();

        // 财务主数据（erp-finance-master）：会计科目 / 税率
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITaxRateRepository, TaxRateRepository>();

        // 发票登记（erp-invoice）：发票 + 关联明细 + 跨四表只读查询
        services.AddScoped<IInvoiceRepository, InvoiceRepository>();
        services.AddScoped<IInvoiceQueryRepository, InvoiceQueryRepository>();

        // 总账（erp-general-ledger）：会计期间 / 凭证 / 科目映射 / 财务报表只读聚合
        services.AddScoped<IAccountingPeriodRepository, AccountingPeriodRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();
        services.AddScoped<IAccountMappingRepository, AccountMappingRepository>();
        services.AddScoped<IFinancialReportQueryRepository, FinancialReportQueryRepository>();

        // 资金出纳（erp-cash）：资金账户 + 资金日记账只读聚合（日记账不落流水表，由收付款单派生）
        services.AddScoped<IBankAccountRepository, BankAccountRepository>();
        services.AddScoped<ICashJournalQueryRepository, CashJournalQueryRepository>();

        // 客户价格（erp-partner-price）：客户协议价
        services.AddScoped<IPartnerPriceRepository, PartnerPriceRepository>();

        // 报价单（erp-quotation）：报价单 + 明细
        services.AddScoped<IQuotationRepository, QuotationRepository>();

        // 操作审计日志（erp-audit-log）：写入器（Scoped，随调用方事务落库）与只读仓储
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        return services;
    }
}