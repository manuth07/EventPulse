using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.EventService.Migrations
{
    /// <inheritdoc />
    public partial class MigrateLegacyEventCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Events\" SET \"Category\" = 'Music' WHERE \"Category\" = 'Musical Concert';");
            migrationBuilder.Sql("UPDATE \"Events\" SET \"Category\" = 'Arts & Theatre' WHERE \"Category\" = 'Theatre / Performance';");
            migrationBuilder.Sql("UPDATE \"EventUpdateRequests\" SET \"Category\" = 'Music' WHERE \"Category\" = 'Musical Concert';");
            migrationBuilder.Sql("UPDATE \"EventUpdateRequests\" SET \"Category\" = 'Arts & Theatre' WHERE \"Category\" = 'Theatre / Performance';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE \"Events\" SET \"Category\" = 'Musical Concert' WHERE \"Category\" = 'Music';");
            migrationBuilder.Sql("UPDATE \"Events\" SET \"Category\" = 'Theatre / Performance' WHERE \"Category\" = 'Arts & Theatre';");
            migrationBuilder.Sql("UPDATE \"EventUpdateRequests\" SET \"Category\" = 'Musical Concert' WHERE \"Category\" = 'Music';");
            migrationBuilder.Sql("UPDATE \"EventUpdateRequests\" SET \"Category\" = 'Theatre / Performance' WHERE \"Category\" = 'Arts & Theatre';");
        }
    }
}
