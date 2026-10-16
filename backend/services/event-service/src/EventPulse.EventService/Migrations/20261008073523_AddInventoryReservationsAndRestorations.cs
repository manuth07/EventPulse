using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.EventService.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryReservationsAndRestorations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryReservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryRestorations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    TicketTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RestoredQuantity = table.Column<int>(type: "integer", nullable: false),
                    RestoredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryRestorations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryRestorations_TicketTypes_TicketTypeId",
                        column: x => x.TicketTypeId,
                        principalTable: "TicketTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_BookingId",
                table: "InventoryReservations",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_ExpiresAtUtc",
                table: "InventoryReservations",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_TicketTypeId",
                table: "InventoryReservations",
                column: "TicketTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_TicketTypeId_Status",
                table: "InventoryReservations",
                columns: new[] { "TicketTypeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRestorations_BookingId",
                table: "InventoryRestorations",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRestorations_CancellationKey",
                table: "InventoryRestorations",
                column: "CancellationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRestorations_TicketTypeId",
                table: "InventoryRestorations",
                column: "TicketTypeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryReservations");

            migrationBuilder.DropTable(
                name: "InventoryRestorations");
        }
    }
}
