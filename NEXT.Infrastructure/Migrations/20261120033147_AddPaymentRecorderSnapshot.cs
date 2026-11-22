using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentRecorderSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecordedByFullName",
                table: "Payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE "Payments" AS payment
                SET "RecordedByFullName" = history."ActorFullName"
                FROM "InvoiceStatusHistory" AS history
                WHERE history."OperationId" = payment."Id"
                  AND history."InvoiceId" = payment."InvoiceId"
                  AND history."PaymentAmount" = payment."Amount"
                  AND history."PaymentMethod" = payment."PaymentMethod"
                  AND payment."PaymentType" = 0
                  AND payment."RecordedByFullName" IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecordedByFullName",
                table: "Payments");
        }
    }
}
