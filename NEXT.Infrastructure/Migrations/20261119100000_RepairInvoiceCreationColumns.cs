using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations;

public partial class RepairInvoiceCreationColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The first published UnifyInvoiceSchema did not contain these fields.
        // IF NOT EXISTS also supports databases created with its later definition.
        migrationBuilder.Sql("""
            ALTER TABLE "Invoices"
                ADD COLUMN IF NOT EXISTS "CustomerName" character varying(200),
                ADD COLUMN IF NOT EXISTS "CreatedByFullName" character varying(200),
                ADD COLUMN IF NOT EXISTS "DiscountType" integer,
                ADD COLUMN IF NOT EXISTS "DiscountValue" numeric(15,2) NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These columns belong to the preceding migration's current model.
        // Keep them when rolling back this compatibility repair.
    }
}
