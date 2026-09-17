using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <summary>
    /// 既有一步式单据域重命名（specs/024-erp-order-flow design.md §2.1 / §2.4）：
    /// 表 / 主表单号列 / 明细外键列 / 索引与主键约束统一改名，并把历史单号前缀改写为 GR / GI。
    /// 纯命名重构，不改变列类型与数据语义；历史单据 OrderId 关联列在后续迁移（AddErpOrders）追加。
    /// </summary>
    public partial class AddErpRenameReceiptsShipments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 表重命名：采购单 → 采购入库单，销售单 → 销售出库单
            migrationBuilder.RenameTable(name: "PurchaseOrders", newName: "PurchaseReceipts");
            migrationBuilder.RenameTable(name: "PurchaseOrderItems", newName: "PurchaseReceiptItems");
            migrationBuilder.RenameTable(name: "SalesOrders", newName: "SalesShipments");
            migrationBuilder.RenameTable(name: "SalesOrderItems", newName: "SalesShipmentItems");

            // 2) 列重命名：主表单号 OrderNo → ReceiptNo / ShipmentNo；明细外键 OrderId → ReceiptId / ShipmentId
            migrationBuilder.RenameColumn(name: "OrderNo", table: "PurchaseReceipts", newName: "ReceiptNo");
            migrationBuilder.RenameColumn(name: "OrderId", table: "PurchaseReceiptItems", newName: "ReceiptId");
            migrationBuilder.RenameColumn(name: "OrderNo", table: "SalesShipments", newName: "ShipmentNo");
            migrationBuilder.RenameColumn(name: "OrderId", table: "SalesShipmentItems", newName: "ShipmentId");

            // 3) 索引重命名（唯一索引 + 明细外键索引）
            migrationBuilder.RenameIndex(name: "IX_PurchaseOrders_OrderNo", table: "PurchaseReceipts", newName: "IX_PurchaseReceipts_ReceiptNo");
            migrationBuilder.RenameIndex(name: "IX_PurchaseOrderItems_OrderId", table: "PurchaseReceiptItems", newName: "IX_PurchaseReceiptItems_ReceiptId");
            migrationBuilder.RenameIndex(name: "IX_SalesOrders_OrderNo", table: "SalesShipments", newName: "IX_SalesShipments_ShipmentNo");
            migrationBuilder.RenameIndex(name: "IX_SalesOrderItems_OrderId", table: "SalesShipmentItems", newName: "IX_SalesShipmentItems_ShipmentId");

            // 4) 主键约束重命名（PG 表重命名不会同步约束名，需显式改名以保持与 EF 模型一致）
            migrationBuilder.Sql("ALTER TABLE \"PurchaseReceipts\" RENAME CONSTRAINT \"PK_PurchaseOrders\" TO \"PK_PurchaseReceipts\";");
            migrationBuilder.Sql("ALTER TABLE \"PurchaseReceiptItems\" RENAME CONSTRAINT \"PK_PurchaseOrderItems\" TO \"PK_PurchaseReceiptItems\";");
            migrationBuilder.Sql("ALTER TABLE \"SalesShipments\" RENAME CONSTRAINT \"PK_SalesOrders\" TO \"PK_SalesShipments\";");
            migrationBuilder.Sql("ALTER TABLE \"SalesShipmentItems\" RENAME CONSTRAINT \"PK_SalesOrderItems\" TO \"PK_SalesShipmentItems\";");

            // 5) 历史单号前缀改写：PO… → GR…、SO… → GI…（本仓库 MVP 阶段单号无外部引用，见 design.md §5）
            //    业务代码禁止裸 SQL，此处属迁移内的数据改写（后端规则 §5.1）
            migrationBuilder.Sql("UPDATE \"PurchaseReceipts\" SET \"ReceiptNo\" = 'GR' || substring(\"ReceiptNo\" from 3) WHERE \"ReceiptNo\" LIKE 'PO%';");
            migrationBuilder.Sql("UPDATE \"SalesShipments\" SET \"ShipmentNo\" = 'GI' || substring(\"ShipmentNo\" from 3) WHERE \"ShipmentNo\" LIKE 'SO%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 前缀回退：GR… → PO…、GI… → SO…
            migrationBuilder.Sql("UPDATE \"PurchaseReceipts\" SET \"ReceiptNo\" = 'PO' || substring(\"ReceiptNo\" from 3) WHERE \"ReceiptNo\" LIKE 'GR%';");
            migrationBuilder.Sql("UPDATE \"SalesShipments\" SET \"ShipmentNo\" = 'SO' || substring(\"ShipmentNo\" from 3) WHERE \"ShipmentNo\" LIKE 'GI%';");

            migrationBuilder.Sql("ALTER TABLE \"PurchaseReceipts\" RENAME CONSTRAINT \"PK_PurchaseReceipts\" TO \"PK_PurchaseOrders\";");
            migrationBuilder.Sql("ALTER TABLE \"PurchaseReceiptItems\" RENAME CONSTRAINT \"PK_PurchaseReceiptItems\" TO \"PK_PurchaseOrderItems\";");
            migrationBuilder.Sql("ALTER TABLE \"SalesShipments\" RENAME CONSTRAINT \"PK_SalesShipments\" TO \"PK_SalesOrders\";");
            migrationBuilder.Sql("ALTER TABLE \"SalesShipmentItems\" RENAME CONSTRAINT \"PK_SalesShipmentItems\" TO \"PK_SalesOrderItems\";");

            migrationBuilder.RenameIndex(name: "IX_PurchaseReceipts_ReceiptNo", table: "PurchaseReceipts", newName: "IX_PurchaseOrders_OrderNo");
            migrationBuilder.RenameIndex(name: "IX_PurchaseReceiptItems_ReceiptId", table: "PurchaseReceiptItems", newName: "IX_PurchaseOrderItems_OrderId");
            migrationBuilder.RenameIndex(name: "IX_SalesShipments_ShipmentNo", table: "SalesShipments", newName: "IX_SalesOrders_OrderNo");
            migrationBuilder.RenameIndex(name: "IX_SalesShipmentItems_ShipmentId", table: "SalesShipmentItems", newName: "IX_SalesOrderItems_OrderId");

            migrationBuilder.RenameColumn(name: "ReceiptNo", table: "PurchaseReceipts", newName: "OrderNo");
            migrationBuilder.RenameColumn(name: "ReceiptId", table: "PurchaseReceiptItems", newName: "OrderId");
            migrationBuilder.RenameColumn(name: "ShipmentNo", table: "SalesShipments", newName: "OrderNo");
            migrationBuilder.RenameColumn(name: "ShipmentId", table: "SalesShipmentItems", newName: "OrderId");

            migrationBuilder.RenameTable(name: "PurchaseReceipts", newName: "PurchaseOrders");
            migrationBuilder.RenameTable(name: "PurchaseReceiptItems", newName: "PurchaseOrderItems");
            migrationBuilder.RenameTable(name: "SalesShipments", newName: "SalesOrders");
            migrationBuilder.RenameTable(name: "SalesShipmentItems", newName: "SalesOrderItems");
        }
    }
}
