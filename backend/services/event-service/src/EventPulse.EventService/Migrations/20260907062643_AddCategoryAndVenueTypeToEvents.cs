using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.EventService.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryAndVenueTypeToEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Events",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueType",
                table: "Events",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "VenueType",
                table: "Events");
        }
    }
}
