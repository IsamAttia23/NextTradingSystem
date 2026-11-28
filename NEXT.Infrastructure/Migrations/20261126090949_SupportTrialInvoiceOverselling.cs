using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupportTrialInvoiceOverselling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_TotalCost",
                table: "Stocks");

            migrationBuilder.AddColumn<decimal>(
                name: "CostAdjustment",
                table: "StockMovements",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_TotalCost",
                table: "Stocks",
                sql: "(\"Quantity\" > 0 AND \"TotalCost\" >= 0) OR (\"Quantity\" < 0 AND \"TotalCost\" <= 0) OR (\"Quantity\" = 0 AND \"TotalCost\" = 0)");
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "Accounting_ValidateInventoryEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE h "LedgerEntries"%ROWTYPE; m "StockMovements"%ROWTYPE; n integer; value numeric; inventory numeric; counter numeric; correction numeric; expected uuid;
                BEGIN
                SELECT * INTO h FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                IF h."Kind" NOT IN (1, 8) AND NOT (h."Kind" = 3 AND h."ReferenceType" = 6) AND EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "AccountId" IN (
                'a1100000-0000-0000-0000-000000000009', 'a1100000-0000-0000-0000-000000000010', 'a1100000-0000-0000-0000-000000000011')) THEN
                RAISE EXCEPTION 'AccountingUseSourceCorrection' USING ERRCODE = '23514';
                END IF;
                IF h."Kind" <> 8 THEN RETURN NULL; END IF;
                SELECT count(*) INTO n FROM "StockMovements" WHERE "ReferenceType" = 'Receipt' AND "ReferenceId" = h."ReferenceId";
                SELECT * INTO m FROM "StockMovements" WHERE "ReferenceType" = 'Receipt' AND "ReferenceId" = h."ReferenceId" LIMIT 1;
                IF n <> 1 OR m."Type" NOT IN (0, 7) OR m."TotalCost" < 0 OR m."TotalCost" + m."CostAdjustment" < 0 OR m."Quantity" <= 0 OR
                (m."Type" = 0 AND m."SupplierId" IS NULL) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                expected := CASE WHEN m."Type" = 7 THEN 'a1100000-0000-0000-0000-000000000007'::uuid ELSE 'a1100000-0000-0000-0000-000000000011'::uuid END;
                SELECT count(*), coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000009'), 0),
                coalesce(sum("Credit" - "Debit") FILTER (WHERE "AccountId" = expected), 0)
                INTO n, inventory, counter FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id";
                SELECT coalesce(sum("Debit" - "Credit"), 0) INTO correction FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "AccountId" = 'a1100000-0000-0000-0000-000000000010';
                IF n <> (CASE WHEN m."TotalCost" > 0 THEN 1 ELSE 0 END) + (CASE WHEN m."TotalCost" + m."CostAdjustment" > 0 THEN 1 ELSE 0 END) + (CASE WHEN m."CostAdjustment" <> 0 THEN 1 ELSE 0 END) OR
                inventory <> m."TotalCost" OR counter <> m."TotalCost" + m."CostAdjustment" OR correction <> m."CostAdjustment" OR
                round(m."Quantity" * m."UnitCost", 2) <> m."TotalCost" + m."CostAdjustment" OR EXISTS (
                SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND ("CustomerId" IS NOT NULL OR "DebtItemId" IS NOT NULL)) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
                END $$;
                CREATE OR REPLACE FUNCTION "Accounting_ValidateStockMovement"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE n integer; q numeric; v numeric;
                BEGIN
                IF NEW."ReferenceType" = 'Receipt' AND (NEW."TotalCost" > 0 OR NEW."CostAdjustment" <> 0) AND NOT EXISTS (
                SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 8 AND "ReferenceType" = 5 AND "ReferenceId" = NEW."ReferenceId" AND "PostedAt" IS NOT NULL) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                IF NEW."ReferenceType" = 'Invoice' AND NEW."Type" = 1 AND
                (NEW."TotalCost" > 0 OR EXISTS (SELECT 1 FROM "Invoices" WHERE "Id" = NEW."ReferenceId" AND "TotalAmount" > 0)) AND NOT EXISTS (
                SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 1 AND "InvoiceId" = NEW."ReferenceId" AND "PostedAt" IS NOT NULL) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                IF NEW."ReferenceType" = 'Transfer' THEN
                SELECT count(*), coalesce(sum(CASE WHEN "Type" = 4 THEN "Quantity" ELSE -"Quantity" END), 0),
                coalesce(sum(CASE WHEN "Type" = 4 THEN "TotalCost" ELSE -"TotalCost" END), 0)
                INTO n, q, v FROM "StockMovements" WHERE "ReferenceType" = 'Transfer' AND "ReferenceId" = NEW."ReferenceId";
                IF n <> 2 OR q <> 0 OR v <> 0 OR NOT EXISTS (
                SELECT 1 FROM "StockMovements" WHERE "ReferenceType" = 'Transfer' AND "ReferenceId" = NEW."ReferenceId" AND
                "Type" <> NEW."Type" AND "Type" IN (4, 5) AND "ProductId" = NEW."ProductId" AND "WarehouseId" <> NEW."WarehouseId") THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                END IF;
                RETURN NULL;
                END $$;
                ALTER TABLE "StockMovements" ADD CONSTRAINT "CK_StockMovements_CostAdjustment" CHECK ("CostAdjustment" = 0 OR ("ReferenceType" = 'Receipt' AND "Type" = 0 AND "TotalCost" + "CostAdjustment" >= 0));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS(SELECT 1 FROM "Stocks" WHERE "Quantity" < 0 OR "TotalCost" < 0) OR EXISTS(SELECT 1 FROM "StockMovements" WHERE "CostAdjustment" <> 0) THEN RAISE EXCEPTION 'Cannot downgrade after trial inventory shortages have been recorded.'; END IF; END $$;
                ALTER TABLE "StockMovements" DROP CONSTRAINT "CK_StockMovements_CostAdjustment";
                CREATE OR REPLACE FUNCTION "Accounting_ValidateInventoryEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE h "LedgerEntries"%ROWTYPE; m "StockMovements"%ROWTYPE; n integer; value numeric; inventory numeric; counter numeric; expected uuid;
                BEGIN
                SELECT * INTO h FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                IF h."Kind" NOT IN (1, 8) AND NOT (h."Kind" = 3 AND h."ReferenceType" = 6) AND EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "AccountId" IN (
                'a1100000-0000-0000-0000-000000000009', 'a1100000-0000-0000-0000-000000000010', 'a1100000-0000-0000-0000-000000000011')) THEN
                RAISE EXCEPTION 'AccountingUseSourceCorrection' USING ERRCODE = '23514';
                END IF;
                IF h."Kind" <> 8 THEN RETURN NULL; END IF;
                SELECT count(*) INTO n FROM "StockMovements" WHERE "ReferenceType" = 'Receipt' AND "ReferenceId" = h."ReferenceId";
                SELECT * INTO m FROM "StockMovements" WHERE "ReferenceType" = 'Receipt' AND "ReferenceId" = h."ReferenceId" LIMIT 1;
                IF n <> 1 OR m."Type" NOT IN (0, 7) OR m."TotalCost" <= 0 OR m."Quantity" <= 0 OR
                (m."Type" = 0 AND m."SupplierId" IS NULL) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                expected := CASE WHEN m."Type" = 7 THEN 'a1100000-0000-0000-0000-000000000007'::uuid ELSE 'a1100000-0000-0000-0000-000000000011'::uuid END;
                SELECT count(*), coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000009'), 0),
                coalesce(sum("Credit" - "Debit") FILTER (WHERE "AccountId" = expected), 0)
                INTO n, inventory, counter FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id";
                IF n <> 2 OR inventory <> m."TotalCost" OR counter <> m."TotalCost" OR EXISTS (
                SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND ("CustomerId" IS NOT NULL OR "DebtItemId" IS NOT NULL)) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
                END $$;
                CREATE OR REPLACE FUNCTION "Accounting_ValidateStockMovement"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE n integer; q numeric; v numeric;
                BEGIN
                IF NEW."ReferenceType" = 'Receipt' AND NEW."TotalCost" > 0 AND NOT EXISTS (
                SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 8 AND "ReferenceType" = 5 AND "ReferenceId" = NEW."ReferenceId" AND "PostedAt" IS NOT NULL) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                IF NEW."ReferenceType" = 'Invoice' AND NEW."Type" = 1 AND
                (NEW."TotalCost" > 0 OR EXISTS (SELECT 1 FROM "Invoices" WHERE "Id" = NEW."ReferenceId" AND "TotalAmount" > 0)) AND NOT EXISTS (
                SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 1 AND "InvoiceId" = NEW."ReferenceId" AND "PostedAt" IS NOT NULL) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                IF NEW."ReferenceType" = 'Transfer' THEN
                SELECT count(*), coalesce(sum(CASE WHEN "Type" = 4 THEN "Quantity" ELSE -"Quantity" END), 0),
                coalesce(sum(CASE WHEN "Type" = 4 THEN "TotalCost" ELSE -"TotalCost" END), 0)
                INTO n, q, v FROM "StockMovements" WHERE "ReferenceType" = 'Transfer' AND "ReferenceId" = NEW."ReferenceId";
                IF n <> 2 OR q <> 0 OR v <> 0 OR NOT EXISTS (
                SELECT 1 FROM "StockMovements" WHERE "ReferenceType" = 'Transfer' AND "ReferenceId" = NEW."ReferenceId" AND
                "Type" <> NEW."Type" AND "Type" IN (4, 5) AND "ProductId" = NEW."ProductId" AND "WarehouseId" <> NEW."WarehouseId") THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                END IF;
                RETURN NULL;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_TotalCost",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "CostAdjustment",
                table: "StockMovements");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_TotalCost",
                table: "Stocks",
                sql: "\"TotalCost\" >= 0 AND (\"Quantity\" <> 0 OR \"TotalCost\" = 0)");
        }
    }
}
