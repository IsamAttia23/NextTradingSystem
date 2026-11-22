using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerInvoiceAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve historical journals; duplicates must be reviewed, never deleted by a migration.
            migrationBuilder.Sql("""
                DO $$
                DECLARE conflicts text;
                BEGIN
                    SELECT string_agg("ReferenceType" || ':' || "ReferenceId"::text, ', ')
                    INTO conflicts FROM (
                        SELECT "ReferenceType", "ReferenceId" FROM "JournalEntries"
                        WHERE "ReferenceId" IS NOT NULL AND "ReferenceType" IN ('Invoice', 'Payment')
                        GROUP BY "ReferenceType", "ReferenceId" HAVING count(*) > 1
                    ) duplicate_references;
                    IF conflicts IS NOT NULL THEN
                        RAISE EXCEPTION 'Review duplicate invoice/payment journal references before migration: %', conflicts;
                    END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Customers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_AutomaticPosting",
                table: "JournalEntries",
                columns: new[] { "ReferenceType", "ReferenceId" },
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL AND \"ReferenceType\" IN ('Invoice', 'Payment')");

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AccountId",
                table: "Customers",
                column: "AccountId",
                unique: true,
                filter: "\"AccountId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Accounts_AccountId",
                table: "Customers",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Accounts_AccountId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "UX_AutomaticPosting",
                table: "JournalEntries");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AccountId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Customers");
        }
    }
}
