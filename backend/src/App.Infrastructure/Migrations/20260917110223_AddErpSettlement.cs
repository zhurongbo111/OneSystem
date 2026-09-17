using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) 先加新列（默认 0）；2) 按旧状态位回填历史数据；3) 再删旧列
            migrationBuilder.AddColumn<decimal>(
                name: "SettledAmount",
                table: "SalesReturns",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SettledAmount",
                table: "SalesOrders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SettledAmount",
                table: "PurchaseReturns",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "SettledAmount",
                table: "PurchaseOrders",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            // 历史数据回填（specs/023-erp-settlement/design.md §2.4）：迁移前 SettlementStatus = 1（已结算）
            // 即「全额已结」，回填后展示与筛选口径不变；未结算（0）保持 0。
            migrationBuilder.Sql("UPDATE \"SalesReturns\" SET \"SettledAmount\" = \"TotalAmount\" WHERE \"SettlementStatus\" = 1;");
            migrationBuilder.Sql("UPDATE \"SalesOrders\" SET \"SettledAmount\" = \"TotalAmount\" WHERE \"SettlementStatus\" = 1;");
            migrationBuilder.Sql("UPDATE \"PurchaseReturns\" SET \"SettledAmount\" = \"TotalAmount\" WHERE \"SettlementStatus\" = 1;");
            migrationBuilder.Sql("UPDATE \"PurchaseOrders\" SET \"SettledAmount\" = \"TotalAmount\" WHERE \"SettlementStatus\" = 1;");

            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "SettlementStatus",
                table: "PurchaseOrders");

            migrationBuilder.CreateTable(
                name: "Settlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    PartnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartnerName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    SettlementDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Method = table.Column<short>(type: "smallint", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settlements_Partners_PartnerId",
                        column: x => x.PartnerId,
                        principalTable: "Partners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SettlementItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderType = table.Column<short>(type: "smallint", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    OrderDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrderTotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SettlementItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SettlementItems_Settlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "Settlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementItems_OrderId",
                table: "SettlementItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementItems_SettlementId",
                table: "SettlementItems",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_PartnerId",
                table: "Settlements",
                column: "PartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_SettlementNo",
                table: "Settlements",
                column: "SettlementNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SettlementItems");

            migrationBuilder.DropTable(
                name: "Settlements");

            // 回滚：先恢复旧状态位列并按已结算金额回填（> 0 视为已结算），再删新列
            migrationBuilder.AddColumn<short>(
                name: "SettlementStatus",
                table: "SalesReturns",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SettlementStatus",
                table: "SalesOrders",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SettlementStatus",
                table: "PurchaseReturns",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "SettlementStatus",
                table: "PurchaseOrders",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.Sql("UPDATE \"SalesReturns\" SET \"SettlementStatus\" = 1 WHERE \"SettledAmount\" > 0;");
            migrationBuilder.Sql("UPDATE \"SalesOrders\" SET \"SettlementStatus\" = 1 WHERE \"SettledAmount\" > 0;");
            migrationBuilder.Sql("UPDATE \"PurchaseReturns\" SET \"SettlementStatus\" = 1 WHERE \"SettledAmount\" > 0;");
            migrationBuilder.Sql("UPDATE \"PurchaseOrders\" SET \"SettlementStatus\" = 1 WHERE \"SettledAmount\" > 0;");

            migrationBuilder.DropColumn(
                name: "SettledAmount",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "SettledAmount",
                table: "SalesOrders");

            migrationBuilder.DropColumn(
                name: "SettledAmount",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "SettledAmount",
                table: "PurchaseOrders");
        }
    }
}
