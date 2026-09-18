using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <summary>
    /// 成本列（specs/026-erp-cost design.md §2.1）：
    /// Inventory 追加结存成本额 CostAmount 与移动加权平均单价 AverageCost（派生值）；
    /// StockMovements 追加本次变动成本单价 UnitCost 与成本金额 TotalCost（与 Quantity 同号）；
    /// StockTakeItems 追加期初成本单价 UnitCost（库存盘点模式下为 0，不参与成本）。
    /// 迁移内**不回填历史成本**（移动加权需按流水时序推演，SQL 无法表达），由上线后执行一次成本重算补齐。
    /// </summary>
    public partial class AddErpCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "StockTakeItems",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "StockMovements",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "StockMovements",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageCost",
                table: "Inventory",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CostAmount",
                table: "Inventory",
                type: "numeric(18,4)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "StockTakeItems");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "AverageCost",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "CostAmount",
                table: "Inventory");
        }
    }
}
