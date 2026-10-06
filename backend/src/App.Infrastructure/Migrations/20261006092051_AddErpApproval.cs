using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "ApprovalStatus",
                table: "SalesShipments",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "ApprovalStatus",
                table: "SalesReturns",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "ApprovalStatus",
                table: "PurchaseReturns",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<short>(
                name: "ApprovalStatus",
                table: "PurchaseReceipts",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "ApprovalRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderType = table.Column<short>(type: "smallint", nullable: false),
                    ThresholdAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Approvals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderType = table.Column<short>(type: "smallint", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PartnerName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SubmittedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecisionRemark = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Approvals", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalesShipments_ApprovalStatus",
                table: "SalesShipments",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturns_ApprovalStatus",
                table: "SalesReturns",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturns_ApprovalStatus",
                table: "PurchaseReturns",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceipts_ApprovalStatus",
                table: "PurchaseReceipts",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalRules_OrderType",
                table: "ApprovalRules",
                column: "OrderType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_OrderType_OrderId",
                table: "Approvals",
                columns: new[] { "OrderType", "OrderId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_Status_SubmittedAt",
                table: "Approvals",
                columns: new[] { "Status", "SubmittedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Approvals_SubmittedBy",
                table: "Approvals",
                column: "SubmittedBy");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalRules");

            migrationBuilder.DropTable(
                name: "Approvals");

            migrationBuilder.DropIndex(
                name: "IX_SalesShipments_ApprovalStatus",
                table: "SalesShipments");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturns_ApprovalStatus",
                table: "SalesReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturns_ApprovalStatus",
                table: "PurchaseReturns");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReceipts_ApprovalStatus",
                table: "PurchaseReceipts");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "SalesShipments");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "SalesReturns");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "PurchaseReturns");

            migrationBuilder.DropColumn(
                name: "ApprovalStatus",
                table: "PurchaseReceipts");
        }
    }
}
