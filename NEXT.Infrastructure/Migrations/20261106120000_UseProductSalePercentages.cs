using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations;

public partial class UseProductSalePercentages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var name in new[] { "RetailPrice", "WholesalePrice", "VipPrice" })
        {
            migrationBuilder.AlterColumn<decimal>(name, "Products", "numeric(28,12)",
                precision: 28, scale: 12, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(15,2)", oldPrecision: 15, oldScale: 2);
        }

        migrationBuilder.Sql("""
            DO $$
            DECLARE rate numeric;
            BEGIN
                SELECT "ValueJson"::text::numeric INTO rate
                FROM "SystemSettings" WHERE "Key" = 'Currency.UsdToSdg';
                -- Without a configured rate, legacy product costs are still stored in SDG.
                rate := COALESCE(rate, 1);
                IF rate <= 0 THEN
                    RAISE EXCEPTION 'Configure a positive Currency.UsdToSdg rate before converting sale prices.';
                END IF;
                IF EXISTS (SELECT 1 FROM "Products" WHERE "CostPrice" < 0
                    OR ("CostPrice" = 0 AND ("RetailPrice" <> 0 OR "WholesalePrice" <> 0 OR "VipPrice" <> 0))
                    OR "RetailPrice" < round("CostPrice" * rate, 2)
                    OR "WholesalePrice" < round("CostPrice" * rate, 2)
                    OR "VipPrice" < round("CostPrice" * rate, 2)) THEN
                    RAISE EXCEPTION 'Review products with zero cost and nonzero sale prices, or sale prices below cost, before converting to percentages.';
                END IF;
                IF EXISTS (SELECT 1 FROM "Products" WHERE "CostPrice" > 0 AND
                    (GREATEST("RetailPrice", "WholesalePrice", "VipPrice") / ("CostPrice" * rate) - 1) * 100 > 9999999999999.99) THEN
                    RAISE EXCEPTION 'A legacy sale price requires a percentage larger than the supported input range.';
                END IF;
                UPDATE "Products" SET
                    "RetailPrice" = CASE WHEN "CostPrice" = 0 THEN 0 ELSE GREATEST(0, ("RetailPrice" / ("CostPrice" * rate) - 1) * 100) END,
                    "WholesalePrice" = CASE WHEN "CostPrice" = 0 THEN 0 ELSE GREATEST(0, ("WholesalePrice" / ("CostPrice" * rate) - 1) * 100) END,
                    "VipPrice" = CASE WHEN "CostPrice" = 0 THEN 0 ELSE GREATEST(0, ("VipPrice" / ("CostPrice" * rate) - 1) * 100) END,
                    "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || "Id"::text), 'hex');
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            DECLARE rate numeric;
            BEGIN
                SELECT "ValueJson"::text::numeric INTO rate
                FROM "SystemSettings" WHERE "Key" = 'Currency.UsdToSdg';
                rate := COALESCE(rate, 1);
                IF rate <= 0 THEN
                    RAISE EXCEPTION 'A positive exchange rate is required to restore sale amounts.';
                END IF;
                UPDATE "Products" SET
                    "RetailPrice" = round("CostPrice" * rate * (1 + "RetailPrice" / 100), 2),
                    "WholesalePrice" = round("CostPrice" * rate * (1 + "WholesalePrice" / 100), 2),
                    "VipPrice" = round("CostPrice" * rate * (1 + "VipPrice" / 100), 2),
                    "RowVersion" = decode(md5(random()::text || clock_timestamp()::text || "Id"::text), 'hex');
            END $$;
            """);
        foreach (var name in new[] { "RetailPrice", "WholesalePrice", "VipPrice" })
        {
            migrationBuilder.AlterColumn<decimal>(name, "Products", "numeric(15,2)",
                precision: 15, scale: 2, nullable: false,
                oldClrType: typeof(decimal), oldType: "numeric(28,12)", oldPrecision: 28, oldScale: 12);
        }
    }
}
