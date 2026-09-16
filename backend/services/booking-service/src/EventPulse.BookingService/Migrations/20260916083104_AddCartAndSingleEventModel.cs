using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.BookingService.Migrations
{
    /// <inheritdoc />
    public partial class AddCartAndSingleEventModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CartItems_CustomerId_TicketTypeId",
                table: "CartItems");

            migrationBuilder.CreateTable(
                name: "Carts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carts", x => x.Id);
                });

            migrationBuilder.AddColumn<Guid>(
                name: "CartId",
                table: "CartItems",
                type: "uuid",
                nullable: true);

            // Safe dev-data migration: create historical Completed (Status=2) carts for existing CartItems
            migrationBuilder.Sql(@"
                DO $$
                DECLARE
                    r RECORD;
                    new_cart_id uuid;
                BEGIN
                    FOR r IN SELECT DISTINCT ""CustomerId"", ""EventId"" FROM ""CartItems"" WHERE ""CartId"" IS NULL
                    LOOP
                        new_cart_id := gen_random_uuid();
                        INSERT INTO ""Carts"" (""Id"", ""CustomerId"", ""EventId"", ""Status"", ""CreatedAt"", ""UpdatedAt"")
                        VALUES (new_cart_id, r.""CustomerId"", r.""EventId"", 2, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);

                        UPDATE ""CartItems""
                        SET ""CartId"" = new_cart_id
                        WHERE ""CustomerId"" = r.""CustomerId"" AND ""EventId"" = r.""EventId"" AND ""CartId"" IS NULL;
                    END LOOP;
                END $$;
            ");

            migrationBuilder.Sql(@"DELETE FROM ""CartItems"" WHERE ""CartId"" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "CartId",
                table: "CartItems",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CartId_TicketTypeId",
                table: "CartItems",
                columns: new[] { "CartId", "TicketTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CustomerId",
                table: "CartItems",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Carts_CustomerId_Status",
                table: "Carts",
                columns: new[] { "CustomerId", "Status" });

            // Enforce at the DB level: at most ONE Active (Status=1) cart per Customer
            migrationBuilder.Sql(@"CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Carts_CustomerId_Active"" ON ""Carts"" (""CustomerId"") WHERE ""Status"" = 1;");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItems_Carts_CartId",
                table: "CartItems",
                column: "CartId",
                principalTable: "Carts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Carts_CustomerId_Active"";");

            migrationBuilder.DropForeignKey(
                name: "FK_CartItems_Carts_CartId",
                table: "CartItems");

            migrationBuilder.DropTable(
                name: "Carts");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CartId_TicketTypeId",
                table: "CartItems");

            migrationBuilder.DropIndex(
                name: "IX_CartItems_CustomerId",
                table: "CartItems");

            migrationBuilder.DropColumn(
                name: "CartId",
                table: "CartItems");

            migrationBuilder.CreateIndex(
                name: "IX_CartItems_CustomerId_TicketTypeId",
                table: "CartItems",
                columns: new[] { "CustomerId", "TicketTypeId" },
                unique: true);
        }
    }
}
