using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.BookingService.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesReportingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalesReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingReference = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaleAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    PaymentTimestampUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RefundStatus = table.Column<int>(type: "integer", nullable: false),
                    RefundedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaleRefundAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    RefundId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    AdjustedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaleRefundAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaleRefundAdjustments_SalesReports_SalesReportId",
                        column: x => x.SalesReportId,
                        principalTable: "SalesReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SalesReportItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SalesReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    RefundedQuantity = table.Column<int>(type: "integer", nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalesReportItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SalesReportItems_SalesReports_SalesReportId",
                        column: x => x.SalesReportId,
                        principalTable: "SalesReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SaleRefundAdjustments_SalesReportId_RefundId",
                table: "SaleRefundAdjustments",
                columns: new[] { "SalesReportId", "RefundId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReportItems_BookingItemId",
                table: "SalesReportItems",
                column: "BookingItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReportItems_SalesReportId",
                table: "SalesReportItems",
                column: "SalesReportId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReportItems_TicketTypeId",
                table: "SalesReportItems",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_BookingId",
                table: "SalesReports",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_CustomerId",
                table: "SalesReports",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_EventId",
                table: "SalesReports",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_OrganizerId",
                table: "SalesReports",
                column: "OrganizerId");

            migrationBuilder.CreateIndex(
                name: "IX_SalesReports_PaymentId",
                table: "SalesReports",
                column: "PaymentId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SaleRefundAdjustments");

            migrationBuilder.DropTable(
                name: "SalesReportItems");

            migrationBuilder.DropTable(
                name: "SalesReports");
        }
    }
}
