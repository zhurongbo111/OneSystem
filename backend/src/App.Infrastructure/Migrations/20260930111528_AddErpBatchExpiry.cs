using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpBatchExpiry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Inventory_ProductId_WarehouseId",
                table: "Inventory");

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "TransferItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "StockTakeItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "StockTakeItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "SalesShipmentItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "SalesShipmentItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "SalesReturnItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "SalesReturnItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "PurchaseReturnItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "PurchaseReturnItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "PurchaseReceiptItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BatchNo",
                table: "PurchaseReceiptItems",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBatchManaged",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "Inventory",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Batches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BatchNo = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    ProductionDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Remark = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Batches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Batches_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferItems_BatchId",
                table: "TransferItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItems_BatchId",
                table: "StockTakeItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesShipmentItems_BatchId",
                table: "SalesShipmentItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReturnItems_BatchId",
                table: "SalesReturnItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReturnItems_BatchId",
                table: "PurchaseReturnItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceiptItems_BatchId",
                table: "PurchaseReceiptItems",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_BatchId",
                table: "Inventory",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ProductId_WarehouseId",
                table: "Inventory",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true,
                filter: "\"BatchId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ProductId_WarehouseId_BatchId",
                table: "Inventory",
                columns: new[] { "ProductId", "WarehouseId", "BatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Batches_ExpiryDate",
                table: "Batches",
                column: "ExpiryDate");

            migrationBuilder.CreateIndex(
                name: "IX_Batches_ProductId_BatchNo",
                table: "Batches",
                columns: new[] { "ProductId", "BatchNo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Inventory_Batches_BatchId",
                table: "Inventory",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReceiptItems_Batches_BatchId",
                table: "PurchaseReceiptItems",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseReturnItems_Batches_BatchId",
                table: "PurchaseReturnItems",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesReturnItems_Batches_BatchId",
                table: "SalesReturnItems",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SalesShipmentItems_Batches_BatchId",
                table: "SalesShipmentItems",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Batches_BatchId",
                table: "StockMovements",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItems_Batches_BatchId",
                table: "StockTakeItems",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TransferItems_Batches_BatchId",
                table: "TransferItems",
                column: "BatchId",
                principalTable: "Batches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Inventory_Batches_BatchId",
                table: "Inventory");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReceiptItems_Batches_BatchId",
                table: "PurchaseReceiptItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseReturnItems_Batches_BatchId",
                table: "PurchaseReturnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesReturnItems_Batches_BatchId",
                table: "SalesReturnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_SalesShipmentItems_Batches_BatchId",
                table: "SalesShipmentItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Batches_BatchId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItems_Batches_BatchId",
                table: "StockTakeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_TransferItems_Batches_BatchId",
                table: "TransferItems");

            migrationBuilder.DropTable(
                name: "Batches");

            migrationBuilder.DropIndex(
                name: "IX_TransferItems_BatchId",
                table: "TransferItems");

            migrationBuilder.DropIndex(
                name: "IX_StockTakeItems_BatchId",
                table: "StockTakeItems");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_BatchId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SalesShipmentItems_BatchId",
                table: "SalesShipmentItems");

            migrationBuilder.DropIndex(
                name: "IX_SalesReturnItems_BatchId",
                table: "SalesReturnItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReturnItems_BatchId",
                table: "PurchaseReturnItems");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseReceiptItems_BatchId",
                table: "PurchaseReceiptItems");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_BatchId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_ProductId_WarehouseId",
                table: "Inventory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_ProductId_WarehouseId_BatchId",
                table: "Inventory");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "TransferItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "StockTakeItems");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "StockTakeItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "SalesShipmentItems");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "SalesShipmentItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "SalesReturnItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "PurchaseReturnItems");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "PurchaseReturnItems");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "BatchNo",
                table: "PurchaseReceiptItems");

            migrationBuilder.DropColumn(
                name: "IsBatchManaged",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "Inventory");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_ProductId_WarehouseId",
                table: "Inventory",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true);
        }
    }
}
