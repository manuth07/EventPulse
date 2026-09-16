using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.EventService.Migrations
{
    /// <inheritdoc />
    public partial class AddEventCoverBlobName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CoverBlobName",
                table: "Events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CoverBlobName",
                table: "Events");
        }
    }
}
