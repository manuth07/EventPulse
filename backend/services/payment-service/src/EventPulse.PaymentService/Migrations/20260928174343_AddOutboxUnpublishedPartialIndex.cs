using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxUnpublishedPartialIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedAtUtc_Unpublished",
                table: "OutboxMessages",
                column: "CreatedAtUtc",
                filter: "\"PublishedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_CreatedAtUtc_Unpublished",
                table: "OutboxMessages");
        }
    }
}
