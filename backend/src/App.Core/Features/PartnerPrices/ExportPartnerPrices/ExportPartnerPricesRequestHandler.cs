using App.Core.Abstractions;
using App.Core.Exports;

namespace App.Core.Features.PartnerPrices.ExportPartnerPrices;

/// <summary>
/// 客户价格列表导出用例（erp-export）：复用客户价格列表的筛选与仓储查询取全量，
/// 导出列 = 列表列定义（去序号 / 操作列），含协议价与商品销售价对比，输出单工作表 xlsx。
/// </summary>
public sealed class ExportPartnerPricesRequestHandler : IRequestHandler<ExportPartnerPricesRequest, ExportResultDto>
{
    private readonly IPartnerPriceRepository _partnerPriceRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化客户价格列表导出用例处理器
    /// </summary>
    public ExportPartnerPricesRequestHandler(
        IPartnerPriceRepository partnerPriceRepository,
        IExcelExporter excelExporter)
    {
        _partnerPriceRepository = partnerPriceRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理客户价格列表导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportPartnerPricesRequest request, CancellationToken cancellationToken = default)
    {
        // 按上限 + 1 取数：超出上限即明确报错，不静默截断（分页参数不参与，导出当前筛选全量）
        var (items, _) = await _partnerPriceRepository.GetPagedAsync(
            request.PartnerId,
            request.ProductId,
            request.Keyword,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var rows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var item in items)
        {
            rows.Add(new object?[]
            {
                item.PartnerName,
                item.ProductCode,
                item.ProductName,
                item.Unit,
                item.Price,
                item.SalePrice,
                item.Price - item.SalePrice,
                item.Remark,
                item.CreatedAt,
            });
        }

        var sheet = new ExcelSheetModel
        {
            Name = ExportDomainNames.PartnerPrices,
            Columns =
            [
                new ExcelColumnModel { Header = "客户", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "商品编码", ValueType = ExcelValueType.Text, Width = 16 },
                new ExcelColumnModel { Header = "商品名称", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
                new ExcelColumnModel { Header = "协议价", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "商品销售价", ValueType = ExcelValueType.Decimal, Width = 14 },
                new ExcelColumnModel { Header = "价差", ValueType = ExcelValueType.Decimal, Width = 12 },
                new ExcelColumnModel { Header = "备注", ValueType = ExcelValueType.Text, Width = 24 },
                new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 },
            ],
            Rows = rows,
        };

        var workbook = new ExcelWorkbookModel { Sheets = [sheet] };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.PartnerPrices, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
