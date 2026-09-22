using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddErpGeneralLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountingPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingPeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AccountMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountMappings_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Vouchers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherNo = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    VoucherDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Summary = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<short>(type: "smallint", nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNo = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    TotalDebit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalCredit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vouchers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vouchers_AccountingPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "AccountingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "VoucherEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VoucherId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineNo = table.Column<int>(type: "integer", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountCode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AccountName = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Summary = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoucherEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoucherEntries_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VoucherEntries_Vouchers_VoucherId",
                        column: x => x.VoucherId,
                        principalTable: "Vouchers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingPeriods_Year_Month",
                table: "AccountingPeriods",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_AccountId",
                table: "AccountMappings",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountMappings_Key",
                table: "AccountMappings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoucherEntries_AccountId",
                table: "VoucherEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherEntries_VoucherId",
                table: "VoucherEntries",
                column: "VoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_PeriodId",
                table: "Vouchers",
                column: "PeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_SourceId",
                table: "Vouchers",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Vouchers_VoucherNo",
                table: "Vouchers",
                column: "VoucherNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountMappings");

            migrationBuilder.DropTable(
                name: "VoucherEntries");

            migrationBuilder.DropTable(
                name: "Vouchers");

            migrationBuilder.DropTable(
                name: "AccountingPeriods");
        }
    }
}
