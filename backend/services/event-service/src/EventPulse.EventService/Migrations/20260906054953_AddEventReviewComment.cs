using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.EventService.Migrations
{
    /// <inheritdoc />
    public partial class AddEventReviewComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "Events",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "Events");
        }
    }
}
