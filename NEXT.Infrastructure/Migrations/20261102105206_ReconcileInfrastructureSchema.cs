using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReconcileInfrastructureSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Compatible with the documented additive SQL upgrades already applied to some databases.
            // Never delete or merge existing duplicates automatically.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM security."Users" WHERE "NormalizedEmail" IS NOT NULL
                               GROUP BY "NormalizedEmail" HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION USING ERRCODE = '23505',
                            MESSAGE = 'Duplicate normalized emails must be resolved before this migration can run.';
                    END IF;
                END $$;
                ALTER TABLE "Customers" ADD COLUMN IF NOT EXISTS "AvatarUrl" bytea NULL;
                ALTER TABLE "Suppliers" ADD COLUMN IF NOT EXISTS "AvatarUrl" bytea NULL;
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_StockMovements_ReferenceId_Type"
                    ON "StockMovements" ("ReferenceId", "Type")
                    WHERE "ReferenceType" IN ('Receipt', 'Transfer');
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Warehouses_IsDefault"
                    ON "Warehouses" ("IsDefault") WHERE "IsDefault" = TRUE AND "DeletedAt" IS NULL;
                DROP INDEX IF EXISTS security."EmailIndex";
                CREATE UNIQUE INDEX "EmailIndex" ON security."Users" ("NormalizedEmail");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_IsDefault",
                table: "Warehouses");

            migrationBuilder.DropIndex(
                name: "EmailIndex",
                schema: "security",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_ReferenceId_Type",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "AvatarUrl",
                table: "Customers");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "security",
                table: "Users",
                column: "NormalizedEmail");
        }
    }
}
