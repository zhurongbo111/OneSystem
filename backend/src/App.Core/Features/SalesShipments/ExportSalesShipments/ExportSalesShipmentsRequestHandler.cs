using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Exports;

namespace App.Core.Features.SalesShipments.ExportSalesShipments;

/// <summary>
/// 销售出库单导出用例（erp-export）：复用列表筛选取全量单据 + 一次批量取明细分组，
/// 输出「单据 + 明细」两个工作表（明细首列为所属单号）；作废单据照常导出，便于对账核对
/// </summary>
public sealed class ExportSalesShipmentsRequestHandler : IRequestHandler<ExportSalesShipmentsRequest, ExportResultDto>
{
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUserRepository _userRepository;
    private readonly IExcelExporter _excelExporter;

    /// <summary>
    /// 初始化销售出库单导出用例处理器
    /// </summary>
    public ExportSalesShipmentsRequestHandler(
        ISalesShipmentRepository salesShipmentRepository,
        IProductRepository productRepository,
        IUserRepository userRepository,
        IExcelExporter excelExporter)
    {
        _salesShipmentRepository = salesShipmentRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
        _excelExporter = excelExporter;
    }

    /// <summary>
    /// 处理销售出库单导出请求
    /// </summary>
    /// <param name="request">导出请求（筛选参数与列表一致）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ExportResultDto> HandleAsync(ExportSalesShipmentsRequest request, CancellationToken cancellationToken = default)
    {
        var (rows, _) = await _salesShipmentRepository.GetPagedAsync(
            request.Keyword,
            request.PartnerId,
            request.OrderId,
            request.Start,
            request.End,
            request.SettlementState,
            1,
            ExportFieldConstraints.MaxRows + 1,
            cancellationToken);
        // 导出不需要数量合计，只取主表实体
        var orders = rows.Select(x => x.Order).ToList();
        ExportGuards.EnsureWithinRowLimit(orders.Count);

        // 明细一次批量取回（避免逐单查询 N+1），再按单据 id 分组
        var orderIds = orders.Select(o => o.Id).ToList();
        var items = await _salesShipmentRepository.GetItemsByOrderIdsAsync(orderIds, cancellationToken);
        ExportGuards.EnsureWithinRowLimit(items.Count);

        var creatorNames = await _userRepository.GetDisplayNamesByIdsAsync(
            orders.Where(o => o.CreatedBy is not null).Select(o => o.CreatedBy!.Value).Distinct().ToList(),
            cancellationToken);
        var productCodes = await _productRepository.GetCodesByIdsAsync(
            items.Select(i => i.ProductId).Distinct().ToList(),
            cancellationToken);

        var itemsByOrder = items.ToLookup(i => i.ShipmentId);

        var orderRows = new List<IReadOnlyList<object?>>(orders.Count);
        var itemRows = new List<IReadOnlyList<object?>>(items.Count);
        foreach (var order in orders)
        {
            var unsettled = SettlementStateCalculator.UnsettledAmount(order.TotalAmount, order.SettledAmount);
            orderRows.Add(new object?[]
            {
                order.ShipmentNo,
                order.OrderNo ?? string.Empty,
                order.PartnerName,
                order.OrderDate,
                order.TotalAmount,
                ExportLabels.ToText(SettlementStateCalculator.Derive(order.TotalAmount, order.SettledAmount), unsettled),
                ExportLabels.ToText(order.Status),
                order.CreatedAt,
                order.CreatedBy is not null && creatorNames.TryGetValue(order.CreatedBy.Value, out var creator) ? creator : string.Empty,
            });

            foreach (var item in itemsByOrder[order.Id])
            {
                itemRows.Add(new object?[]
                {
                    order.ShipmentNo,
                    productCodes.TryGetValue(item.ProductId, out var code) ? code : string.Empty,
                    item.ProductName,
                    item.Unit,
                    item.Quantity,
                    item.UnitPrice,
                    item.Subtotal,
                });
            }
        }

        var workbook = new ExcelWorkbookModel
        {
            Sheets =
            [
                new ExcelSheetModel
                {
                    Name = ExportDomainNames.DocumentSheet,
                    Columns =
                    [
                        new ExcelColumnModel { Header = "单号", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "关联订单", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "客户", ValueType = ExcelValueType.Text, Width = 24 },
                        new ExcelColumnModel { Header = "单据日期", ValueType = ExcelValueType.Date, Width = 14 },
                        new ExcelColumnModel { Header = "总金额", ValueType = ExcelValueType.Decimal, Width = 14 },
                        new ExcelColumnModel { Header = "结算状态", ValueType = ExcelValueType.Text, Width = 24 },
                        new ExcelColumnModel { Header = "单据状态", ValueType = ExcelValueType.Text, Width = 12 },
                        new ExcelColumnModel { Header = "创建时间", ValueType = ExcelValueType.DateTime, Width = 18 },
                        new ExcelColumnModel { Header = "创建人", ValueType = ExcelValueType.Text, Width = 14 },
                    ],
                    Rows = orderRows,
                },
                new ExcelSheetModel
                {
                    Name = ExportDomainNames.DetailSheet,
                    Columns =
                    [
                        new ExcelColumnModel { Header = "单号", ValueType = ExcelValueType.Text, Width = 20 },
                        new ExcelColumnModel { Header = "商品编码", ValueType = ExcelValueType.Text, Width = 18 },
                        new ExcelColumnModel { Header = "商品名称", ValueType = ExcelValueType.Text, Width = 24 },
                        new ExcelColumnModel { Header = "单位", ValueType = ExcelValueType.Text, Width = 8 },
                        new ExcelColumnModel { Header = "数量", ValueType = ExcelValueType.Integer, Width = 10 },
                        new ExcelColumnModel { Header = "单价", ValueType = ExcelValueType.Decimal, Width = 12 },
                        new ExcelColumnModel { Header = "小计", ValueType = ExcelValueType.Decimal, Width = 14 },
                    ],
                    Rows = itemRows,
                },
            ],
        };

        return new ExportResultDto
        {
            FileName = ExportFileNames.Build(ExportDomainNames.SalesShipments, DateTimeOffset.UtcNow),
            Content = _excelExporter.Build(workbook),
        };
    }
}
