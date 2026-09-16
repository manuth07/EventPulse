using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.EventService.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTypeBookedQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookedQuantity",
                table: "TicketTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BookedQuantity",
                table: "TicketTypes");
        }
    }
}
