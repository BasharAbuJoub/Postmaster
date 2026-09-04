using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Postmaster.Sample.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxMessageAttemptCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "OutboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Seed the monotonic counter for rows that were already attempted, so their next
            // attempt does not reuse a correlation suffix that has already been sent.
            migrationBuilder.Sql(
                @"UPDATE ""OutboxMessages"" SET ""AttemptCount"" = ""RetryCount"";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "OutboxMessages");
        }
    }
}
