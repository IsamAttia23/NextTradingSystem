using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations;

public partial class SupportUsdProductCost : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<decimal>(
            name: "CostPrice",
            table: "Products",
            type: "numeric(24,8)",
            precision: 24,
            scale: 8,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(15,2)",
            oldPrecision: 15,
            oldScale: 2);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Refuse a rollback that would silently lose USD precision or overflow.
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM "Products"
                    WHERE "CostPrice" <> round("CostPrice", 2)
                       OR abs("CostPrice") >= 10000000000000) THEN
                    RAISE EXCEPTION 'Product costs cannot be represented as numeric(15,2).';
                END IF;
            END $$;
            """);
        migrationBuilder.AlterColumn<decimal>(
            name: "CostPrice",
            table: "Products",
            type: "numeric(15,2)",
            precision: 15,
            scale: 2,
            nullable: false,
            oldClrType: typeof(decimal),
            oldType: "numeric(24,8)",
            oldPrecision: 24,
            oldScale: 8);
    }
}
