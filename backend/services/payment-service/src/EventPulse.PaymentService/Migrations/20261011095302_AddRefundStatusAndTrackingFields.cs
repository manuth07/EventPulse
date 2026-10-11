using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventPulse.PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundStatusAndTrackingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "RefundRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "RefundRecords",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "usd");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "RefundRecords",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "RefundRecords",
                type: "text",
                nullable: false,
                defaultValue: "Completed");

            migrationBuilder.AddColumn<string>(
                name: "StripeRefundId",
                table: "RefundRecords",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "RefundRecords");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "RefundRecords");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "RefundRecords");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "RefundRecords");

            migrationBuilder.DropColumn(
                name: "StripeRefundId",
                table: "RefundRecords");
        }
    }
}
