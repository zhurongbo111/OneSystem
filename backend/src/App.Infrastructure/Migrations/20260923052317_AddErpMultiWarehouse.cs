using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <summary>
    /// 多仓库（specs/038-erp-multi-warehouse/design.md §2.5）：一次到位完成
    /// 「建仓表 + 插入默认仓 + 库存 / 流水 / 单据加仓列 + 回填默认仓 + 唯一键升级」。
    /// 迁移内使用 <c>migrationBuilder.Sql</c> **仅限数据回填**（业务代码仍禁止裸 SQL，后端规则 §5.1）。
    /// </summary>
    public partial class AddErpMultiWarehouse : Migration
    {
        /// <summary>默认仓固定主键（迁移内 SQL 回填直接引用，见 design.md §2.5 第 2 步）</summary>
        private const string DefaultWarehouseId = "00000000-0000-0000-0000-000000000001";

        /// <summary>默认仓名称</summary>
        private const string DefaultWarehouseName = "默认仓";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ① 建仓表（编码 / 名称唯一）+ 插入默认仓（固定 GUID，供后续回填引用）
            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Address = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Contact = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Code",
                table: "Warehouses",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_Name",
                table: "Warehouses",
                column: "Name",
                unique: true);

            migrationBuilder.InsertData(
                table: "Warehouses",
                columns: ["Id", "Code", "Name", "Address", "Contact", "Phone", "IsDefault", "Status", "Remark", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy"],
                values: new object[]
                {
                    new Guid(DefaultWarehouseId),
                    "DEFAULT",
                    DefaultWarehouseName,
                    null,
                    null,
                    null,
                    true,
                    (short)1,
                    "系统内置默认仓（038 迁移创建，不可停用、不可删除）",
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    null,
                    null
                });

            // ② 库存维度升级：加列（先可空）→ 回填默认仓与仓级安全库存 → 改 NOT NULL → 唯一键切换
            migrationBuilder.DropIndex(
                name: "IX_Inventory_ProductId",
                table: "Inventory");

            migrationBuilder.AddColumn<int>(
                name: "SafetyStock",
                table: "Inventory",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "Inventory",
                type: "uuid",
                nullable: true);

            // 既有库存行全部归入默认仓；仓级安全库存取商品档案阈值作为初始值
            migrationBuilder.Sql(
                $"UPDATE \"Inventory\" SET \"WarehouseId\" = '{DefaultWarehouseId}';");
            migrationBuilder.Sql(
                "UPDATE \"Inventory\" SET \"SafetyStock\" = COALESCE((SELECT p.\"SafetyStock\" FROM \"Products\" p WHERE p.\"Id\" = \"Inventory\".\"ProductId\"), 0);");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: "Inventory",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ProductId_WarehouseId",
                table: "Inventory",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_WarehouseId",
                table: "Inventory",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Warehouses_WarehouseId",
                table: "Inventory",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ③ 流水维度升级：加列（先可空）→ 回填默认仓 → 改 NOT NULL → 索引 + 外键
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                $"UPDATE \"StockMovements\" SET \"WarehouseId\" = '{DefaultWarehouseId}';");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: "StockMovements",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_WarehouseId_ProductId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "WarehouseId", "ProductId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Warehouses_WarehouseId",
                table: "StockMovements",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ④ 五类单据带仓：加列（先可空）→ 回填默认仓与仓名快照 → 改 NOT NULL
            AddDocumentWarehouseColumns(migrationBuilder, "PurchaseReceipts");
            AddDocumentWarehouseColumns(migrationBuilder, "SalesShipments");
            AddDocumentWarehouseColumns(migrationBuilder, "PurchaseReturns");
            AddDocumentWarehouseColumns(migrationBuilder, "SalesReturns");
            AddDocumentWarehouseColumns(migrationBuilder, "StockTakes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 五类单据：删仓列
            DropDocumentWarehouseColumns(migrationBuilder, "StockTakes");
            DropDocumentWarehouseColumns(migrationBuilder, "SalesReturns");
            DropDocumentWarehouseColumns(migrationBuilder, "PurchaseReturns");
            DropDocumentWarehouseColumns(migrationBuilder, "SalesShipments");
            DropDocumentWarehouseColumns(migrationBuilder, "PurchaseReceipts");

            // 流水
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Warehouses_WarehouseId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_WarehouseId_ProductId_CreatedAt",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "StockMovements");

            // 库存：还原「ProductId 唯一」的单仓口径并删仓列
            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_Warehouses_WarehouseId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_ProductId_WarehouseId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_WarehouseId",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "SafetyStock",
                table: "Inventory");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ProductId",
                table: "Inventory",
                column: "ProductId",
                unique: true);

            // 仓库表（Down 仅用于本地回退，不做数据保护）
            migrationBuilder.DropTable(
                name: "Warehouses");
        }

        /// <summary>为单据表加仓列并回填默认仓（可空 → 回填 → NOT NULL）</summary>
        private static void AddDocumentWarehouseColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "WarehouseId",
                table: table,
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WarehouseName",
                table: table,
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql(
                $"UPDATE \"{table}\" SET \"WarehouseId\" = '{DefaultWarehouseId}', \"WarehouseName\" = '{DefaultWarehouseName}';");

            migrationBuilder.AlterColumn<Guid>(
                name: "WarehouseId",
                table: table,
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WarehouseName",
                table: table,
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <summary>删除单据表的仓列（Down 用）</summary>
        private static void DropDocumentWarehouseColumns(MigrationBuilder migrationBuilder, string table)
        {
            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: table);

            migrationBuilder.DropColumn(
                name: "WarehouseName",
                table: table);
        }
    }
}
