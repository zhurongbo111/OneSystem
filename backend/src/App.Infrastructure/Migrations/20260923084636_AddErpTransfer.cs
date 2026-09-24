using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransferItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductCode = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    ProductName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Unit = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    BatchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransferNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    FromWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromWarehouseName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ToWarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToWarehouseName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    TransferDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ItemCount = table.Column<int>(type: "integer", nullable: false),
                    TotalQuantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transfers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferItems_TransferId",
                table: "TransferItems",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_TransferNo",
                table: "Transfers",
                column: "TransferNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferItems");

            migrationBuilder.DropTable(
                name: "Transfers");
        }
    }
}
