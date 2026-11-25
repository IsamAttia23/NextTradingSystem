using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryFinancialPosting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Accounts" DISABLE TRIGGER "TR_Accounts_Fixed";
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "Stocks",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "SupplierId",
                table: "StockMovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalCost",
                table: "StockMovements",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Accounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000001"),
                column: "Category",
                value: 4);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000002"),
                column: "Category",
                value: 5);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000003"),
                column: "Category",
                value: 5);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000004"),
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000005"),
                column: "Category",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000006"),
                column: "Category",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000007"),
                column: "Category",
                value: 3);

            migrationBuilder.UpdateData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000008"),
                column: "Category",
                value: 2);

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "Category", "Code", "IsTechnical", "Name", "NormalSide", "Type" },
                values: new object[,]
                {
                    { new Guid("a1100000-0000-0000-0000-000000000009"), 1, "INVENTORY", false, "المخزون", 0, 9 },
                    { new Guid("a1100000-0000-0000-0000-000000000010"), 5, "COST-OF-SALES", false, "تكلفة المبيعات", 0, 10 },
                    { new Guid("a1100000-0000-0000-0000-000000000011"), 2, "GOODS-RECEIVED", false, "بضاعة مستلمة غير مفوترة", 1, 11 },
                    { new Guid("a1100000-0000-0000-0000-000000000012"), 2, "PAYABLES", false, "الموردون", 1, 12 }
                });

            // Existing precision cannot be recovered; preserve the recorded basis without inventing historical journals.
            migrationBuilder.Sql("""
                UPDATE "Stocks" SET "TotalCost" = round("Quantity" * "AverageCost", 2);
                UPDATE "StockMovements" SET "TotalCost" = round("Quantity" * "UnitCost", 2);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stocks_TotalCost",
                table: "Stocks",
                sql: "\"TotalCost\" >= 0 AND (\"Quantity\" <> 0 OR \"TotalCost\" = 0)");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_SupplierId",
                table: "StockMovements",
                column: "SupplierId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_StockMovements_TotalCost",
                table: "StockMovements",
                sql: "\"TotalCost\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries",
                sql: "\"Kind\" BETWEEN 1 AND 8 AND \"ReferenceType\" BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Category",
                table: "Accounts",
                sql: "\"Category\" BETWEEN 1 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts",
                sql: "\"Type\" BETWEEN 1 AND 12");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Suppliers_SupplierId",
                table: "StockMovements",
                column: "SupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "Accounting_ValidateEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE header "LedgerEntries"%ROWTYPE; debits numeric; credits numeric; line_count integer;
                original_count integer; cash numeric; payment_amount numeric; payment_type integer;
                payment_invoice uuid; payment_customer uuid; payment_date date;
                BEGIN
                SELECT * INTO header FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                SELECT count(*), coalesce(sum("Debit"), 0), coalesce(sum("Credit"), 0)
                INTO line_count, debits, credits FROM "AccountTransactions" WHERE "LedgerEntryId" = header."Id";
                IF header."PostedAt" IS NULL OR line_count < 2 OR line_count > 1000 OR debits <> credits OR
                debits <= 0 OR debits > 9999999999999.99 THEN
                RAISE EXCEPTION 'AccountingUnbalancedEntry' USING ERRCODE = '23514';
                END IF;
                IF NOT ((header."Kind" IN (1, 4, 7) AND header."ReferenceType" = 1) OR
                (header."Kind" IN (2, 5) AND header."ReferenceType" = 2) OR
                (header."Kind" = 3 AND header."ReferenceType" IN (3, 4)) OR
                (header."Kind" = 6 AND header."ReferenceType" = 4) OR
                (header."Kind" = 8 AND header."ReferenceType" = 5 AND header."InvoiceId" IS NULL AND header."PaymentId" IS NULL)) THEN
                RAISE EXCEPTION 'AccountingInvalidReference' USING ERRCODE = '23514';
                END IF;
                IF header."Kind" IN (2, 5) THEN
                SELECT "Amount", "PaymentType", "InvoiceId", "CustomerId", "PaymentDate"
                INTO payment_amount, payment_type, payment_invoice, payment_customer, payment_date
                FROM "Payments" WHERE "Id" = header."PaymentId";
                SELECT coalesce(sum("Debit" - "Credit"), 0) INTO cash FROM "AccountTransactions"
                WHERE "LedgerEntryId" = header."Id" AND "AccountId" = 'a1100000-0000-0000-0000-000000000005';
                IF payment_amount IS NULL OR payment_amount <= 0 OR payment_invoice IS DISTINCT FROM header."InvoiceId" OR
                payment_date IS DISTINCT FROM header."EntryDate" OR EXISTS (SELECT 1 FROM "AccountTransactions"
                WHERE "LedgerEntryId" = header."Id" AND "CustomerId" IS DISTINCT FROM payment_customer) OR
                (header."Kind" = 2 AND (payment_type <> 0 OR cash <> payment_amount)) OR
                (header."Kind" = 5 AND (payment_type <> 1 OR cash <> -payment_amount)) THEN
                RAISE EXCEPTION 'AccountingPaymentMismatch' USING ERRCODE = '23514';
                END IF;
                END IF;
                IF header."ReferenceType" = 3 AND NOT EXISTS (
                SELECT 1 FROM "DebtItems" d JOIN "AccountTransactions" t ON t."DebtItemId" = d."Id"
                WHERE d."Id" = header."ReferenceId" AND d."Kind" = 2 AND t."LedgerEntryId" = header."Id"
                GROUP BY d."Id", d."OriginalAmount" HAVING sum(t."Debit" - t."Credit") = d."OriginalAmount") THEN
                RAISE EXCEPTION 'AccountingInvalidReference' USING ERRCODE = '23514';
                END IF;
                IF header."ReversesEntryId" IS NOT NULL THEN
                IF NOT EXISTS (SELECT 1 FROM "LedgerEntries" e WHERE e."Id" = header."ReversesEntryId"
                AND e."PostedAt" IS NOT NULL AND e."Kind" <> 6 AND e."EntryDate" <= header."EntryDate"
                AND e."InvoiceId" IS NOT DISTINCT FROM header."InvoiceId"
                AND e."PaymentId" IS NOT DISTINCT FROM header."PaymentId") THEN
                RAISE EXCEPTION 'AccountingReversalUnavailable' USING ERRCODE = '23514';
                END IF;
                SELECT count(*) INTO original_count FROM "AccountTransactions" WHERE "LedgerEntryId" = header."ReversesEntryId";
                IF original_count <> line_count OR EXISTS (
                SELECT "Sequence", "AccountId", "CustomerId", "DebtItemId", "Debit", "Credit"
                FROM "AccountTransactions" WHERE "LedgerEntryId" = header."ReversesEntryId"
                EXCEPT
                SELECT "Sequence", "AccountId", "CustomerId", "DebtItemId", "Credit", "Debit"
                FROM "AccountTransactions" WHERE "LedgerEntryId" = header."Id") THEN
                RAISE EXCEPTION 'AccountingReversalUnavailable' USING ERRCODE = '23514';
                END IF;
                END IF;
                RETURN NULL;
                END $$;
                CREATE OR REPLACE FUNCTION "Accounting_ValidateOperationalEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE h "LedgerEntries"%ROWTYPE; i "Invoices"%ROWTYPE; n integer;
                sales numeric; advances numeric; debts numeric; refunds numeric; cash numeric; purpose integer;
                received numeric; prior_advances numeric; cost numeric; expense numeric; inventory numeric;
                BEGIN
                SELECT * INTO h FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                IF h."Kind" = 6 AND (h."InvoiceId" IS NOT NULL OR h."PaymentId" IS NOT NULL OR
                EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "DebtItemId" IS NOT NULL) OR
                EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "Id" = h."ReversesEntryId" AND "Kind" = 8)) THEN
                RAISE EXCEPTION 'AccountingUseSourceCorrection' USING ERRCODE = '23514';
                END IF;
                IF h."EntryDate" = DATE '0001-01-01' THEN
                RAISE EXCEPTION 'AccountingPostingDateInvalid' USING ERRCODE = '23514';
                END IF;
                IF h."InvoiceId" IS NULL THEN RETURN NULL; END IF;
                SELECT * INTO i FROM "Invoices" WHERE "Id" = h."InvoiceId" FOR UPDATE;
                IF NOT FOUND OR i."Type" <> 0 OR i."LegacyInvoiceTypeCode" IS NOT NULL OR
                i."ParentInvoiceId" IS NOT NULL OR i."CustomerId" IS NULL OR
                EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "CustomerId" IS DISTINCT FROM i."CustomerId") OR
                EXISTS (SELECT 1 FROM "AccountTransactions" t JOIN "DebtItems" d ON d."Id" = t."DebtItemId"
                WHERE t."LedgerEntryId" = h."Id" AND (d."InvoiceId" IS DISTINCT FROM i."Id" OR d."Kind" <> 1 OR
                (h."Kind" = 1 AND d."OriginalAmount" <> i."RemainingAmount"))) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                SELECT coalesce(sum("Amount"), 0) INTO received FROM "Payments" WHERE "InvoiceId" = i."Id" AND "PaymentType" = 0;
                IF i."PaidAmount" <> received OR i."PaidAmount" < 0 OR i."PaidAmount" > i."TotalAmount" OR
                (i."Status" = 3 AND i."RemainingAmount" <> 0) OR
                (i."Status" <> 3 AND i."RemainingAmount" <> i."TotalAmount" - received) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                SELECT coalesce(sum(t."Credit" - t."Debit"), 0) INTO prior_advances FROM "AccountTransactions" t
                JOIN "LedgerEntries" e ON e."Id" = t."LedgerEntryId" WHERE e."InvoiceId" = i."Id" AND e."Id" <> h."Id"
                AND e."PostedAt" IS NOT NULL AND t."AccountId" = 'a1100000-0000-0000-0000-000000000006';
                SELECT count(*),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000001'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000006'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000004'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000008'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000005'), 0)
                INTO n, sales, advances, debts, refunds, cash FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id";
                IF h."Kind" = 1 THEN
                SELECT coalesce(sum("TotalCost"), 0) INTO cost FROM "StockMovements"
                WHERE "ReferenceType" = 'Invoice' AND "ReferenceId" = i."Id" AND "Type" = 1;
                SELECT coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000010'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000009'), 0)
                INTO expense, inventory FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id";
                IF NOT EXISTS (SELECT 1 FROM "InvoiceItems" WHERE "InvoiceId" = i."Id") OR EXISTS (
                SELECT 1 FROM
                (SELECT "ProductId", sum("Quantity") AS qty FROM "InvoiceItems" WHERE "InvoiceId" = i."Id" GROUP BY "ProductId") expected
                FULL JOIN
                (SELECT "ProductId", sum("Quantity") AS qty FROM "StockMovements" WHERE "ReferenceType" = 'Invoice' AND "ReferenceId" = i."Id" AND "Type" = 1 GROUP BY "ProductId") actual
                ON actual."ProductId" = expected."ProductId"
                WHERE actual.qty IS DISTINCT FROM expected.qty) OR EXISTS (
                SELECT 1 FROM "StockMovements" WHERE "ReferenceType" = 'Invoice' AND "ReferenceId" = i."Id"
                AND ("Type" <> 1 OR "WarehouseId" <> i."WarehouseId")) THEN
                RAISE EXCEPTION 'AccountingInventoryPostingMismatch' USING ERRCODE = '23514';
                END IF;
                IF i."Status" NOT IN (6, 7) OR i."TaxAmount" <> 0 OR i."TotalAmount" < 0 OR
                i."PaidAmount" < 0 OR i."RemainingAmount" < 0 OR i."PaidAmount" + i."RemainingAmount" <> i."TotalAmount" OR
                sales <> -i."TotalAmount" OR advances <> i."PaidAmount" OR prior_advances <> i."PaidAmount" OR debts <> i."RemainingAmount" OR
                expense <> cost OR inventory <> -cost OR
                n <> (CASE WHEN i."TotalAmount" > 0 THEN 1 ELSE 0 END) + (CASE WHEN i."PaidAmount" > 0 THEN 1 ELSE 0 END) + (CASE WHEN i."RemainingAmount" > 0 THEN 1 ELSE 0 END) + (CASE WHEN cost > 0 THEN 2 ELSE 0 END) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                ELSIF h."Kind" = 7 THEN
                IF i."Status" <> 3 OR i."RemainingAmount" <> 0 OR i."PaidAmount" <= 0 OR n <> 2 OR
                advances <> i."PaidAmount" OR prior_advances <> i."PaidAmount" OR refunds <> -i."PaidAmount" THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                ELSIF h."Kind" IN (2, 5) THEN
                SELECT "Purpose" INTO purpose FROM "Payments" WHERE "Id" = h."PaymentId";
                IF (h."Kind" = 2 AND (i."Status" = 3 OR purpose IS DISTINCT FROM 1 OR n <> 2 OR cash <= 0 OR
                (i."Status" IN (6, 7) AND NOT EXISTS (SELECT 1 FROM "LedgerEntries" e WHERE e."InvoiceId" = i."Id" AND e."Kind" = 1 AND e."PostedAt" IS NOT NULL)) OR
                NOT ((advances = -cash AND debts = 0) OR (debts = -cash AND advances = 0)))) OR
                (h."Kind" = 5 AND (i."Status" <> 3 OR purpose IS DISTINCT FROM 3 OR n <> 2 OR cash >= 0 OR refunds <> -cash)) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                ELSE
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
                END $$;
                
                CREATE FUNCTION "Accounting_ProtectStockMovement"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'AccountingPostedInventoryImmutable' USING ERRCODE = '23514'; END IF;
                IF NEW."ReferenceType" = 'Invoice' THEN
                PERFORM 1 FROM "Invoices" WHERE "Id" = NEW."ReferenceId" FOR UPDATE;
                IF EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "InvoiceId" = NEW."ReferenceId" AND "Kind" = 1 AND "PostedAt" IS NOT NULL) THEN
                RAISE EXCEPTION 'AccountingPostedInventoryImmutable' USING ERRCODE = '23514';
                END IF;
                ELSIF NEW."ReferenceType" = 'Receipt' AND EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 8 AND "ReferenceType" = 5 AND "ReferenceId" = NEW."ReferenceId" AND "PostedAt" IS NOT NULL) THEN
                RAISE EXCEPTION 'AccountingPostedInventoryImmutable' USING ERRCODE = '23514';
                END IF;
                RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_StockMovements_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "StockMovements"
                FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectStockMovement"();
                
                CREATE FUNCTION "Accounting_ValidateInventoryEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE h "LedgerEntries"%ROWTYPE; m "StockMovements"%ROWTYPE; n integer; value numeric; inventory numeric; counter numeric; expected uuid;
                BEGIN
                SELECT * INTO h FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                IF h."Kind" NOT IN (1, 8) AND EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "AccountId" IN (
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
                CREATE CONSTRAINT TRIGGER "TR_LedgerEntries_InventorySource" AFTER INSERT OR UPDATE ON "LedgerEntries"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateInventoryEntry"();
                
                CREATE FUNCTION "Accounting_ValidateStockMovement"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE CONSTRAINT TRIGGER "TR_StockMovements_Accounting" AFTER INSERT ON "StockMovements"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateStockMovement"();
                
                ALTER TABLE "Accounts" ENABLE TRIGGER "TR_Accounts_Fixed";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 8) OR EXISTS (
                SELECT 1 FROM "AccountTransactions" WHERE "AccountId" IN (
                'a1100000-0000-0000-0000-000000000009', 'a1100000-0000-0000-0000-000000000010',
                'a1100000-0000-0000-0000-000000000011', 'a1100000-0000-0000-0000-000000000012')) THEN
                RAISE EXCEPTION 'Cannot downgrade after inventory accounting has been used.';
                END IF;
                END $$;
                ALTER TABLE "Accounts" DISABLE TRIGGER "TR_Accounts_Fixed";
                DROP TRIGGER "TR_StockMovements_Accounting" ON "StockMovements";
                DROP FUNCTION "Accounting_ValidateStockMovement"();
                DROP TRIGGER "TR_StockMovements_Immutable" ON "StockMovements";
                DROP FUNCTION "Accounting_ProtectStockMovement"();
                DROP TRIGGER "TR_LedgerEntries_InventorySource" ON "LedgerEntries";
                DROP FUNCTION "Accounting_ValidateInventoryEntry"();
                CREATE OR REPLACE FUNCTION "Accounting_ValidateEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE header "LedgerEntries"%ROWTYPE; debits numeric; credits numeric; line_count integer;
                original_count integer; cash numeric; payment_amount numeric; payment_type integer;
                payment_invoice uuid; payment_customer uuid; payment_date date;
                BEGIN
                SELECT * INTO header FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                SELECT count(*), coalesce(sum("Debit"), 0), coalesce(sum("Credit"), 0)
                INTO line_count, debits, credits FROM "AccountTransactions" WHERE "LedgerEntryId" = header."Id";
                IF header."PostedAt" IS NULL OR line_count < 2 OR line_count > 1000 OR debits <> credits OR
                debits <= 0 OR debits > 9999999999999.99 THEN
                RAISE EXCEPTION 'AccountingUnbalancedEntry' USING ERRCODE = '23514';
                END IF;
                IF NOT ((header."Kind" IN (1, 4, 7) AND header."ReferenceType" = 1) OR
                (header."Kind" IN (2, 5) AND header."ReferenceType" = 2) OR
                (header."Kind" = 3 AND header."ReferenceType" IN (3, 4)) OR
                (header."Kind" = 6 AND header."ReferenceType" = 4)) THEN
                RAISE EXCEPTION 'AccountingInvalidReference' USING ERRCODE = '23514';
                END IF;
                IF header."Kind" IN (2, 5) THEN
                SELECT "Amount", "PaymentType", "InvoiceId", "CustomerId", "PaymentDate"
                INTO payment_amount, payment_type, payment_invoice, payment_customer, payment_date
                FROM "Payments" WHERE "Id" = header."PaymentId";
                SELECT coalesce(sum("Debit" - "Credit"), 0) INTO cash FROM "AccountTransactions"
                WHERE "LedgerEntryId" = header."Id" AND "AccountId" = 'a1100000-0000-0000-0000-000000000005';
                IF payment_amount IS NULL OR payment_amount <= 0 OR payment_invoice IS DISTINCT FROM header."InvoiceId" OR
                payment_date IS DISTINCT FROM header."EntryDate" OR EXISTS (SELECT 1 FROM "AccountTransactions"
                WHERE "LedgerEntryId" = header."Id" AND "CustomerId" IS DISTINCT FROM payment_customer) OR
                (header."Kind" = 2 AND (payment_type <> 0 OR cash <> payment_amount)) OR
                (header."Kind" = 5 AND (payment_type <> 1 OR cash <> -payment_amount)) THEN
                RAISE EXCEPTION 'AccountingPaymentMismatch' USING ERRCODE = '23514';
                END IF;
                END IF;
                IF header."ReferenceType" = 3 AND NOT EXISTS (
                SELECT 1 FROM "DebtItems" d JOIN "AccountTransactions" t ON t."DebtItemId" = d."Id"
                WHERE d."Id" = header."ReferenceId" AND d."Kind" = 2 AND t."LedgerEntryId" = header."Id"
                GROUP BY d."Id", d."OriginalAmount" HAVING sum(t."Debit" - t."Credit") = d."OriginalAmount") THEN
                RAISE EXCEPTION 'AccountingInvalidReference' USING ERRCODE = '23514';
                END IF;
                IF header."ReversesEntryId" IS NOT NULL THEN
                IF NOT EXISTS (SELECT 1 FROM "LedgerEntries" e WHERE e."Id" = header."ReversesEntryId"
                AND e."PostedAt" IS NOT NULL AND e."Kind" <> 6 AND e."EntryDate" <= header."EntryDate"
                AND e."InvoiceId" IS NOT DISTINCT FROM header."InvoiceId"
                AND e."PaymentId" IS NOT DISTINCT FROM header."PaymentId") THEN
                RAISE EXCEPTION 'AccountingReversalUnavailable' USING ERRCODE = '23514';
                END IF;
                SELECT count(*) INTO original_count FROM "AccountTransactions" WHERE "LedgerEntryId" = header."ReversesEntryId";
                IF original_count <> line_count OR EXISTS (
                SELECT "Sequence", "AccountId", "CustomerId", "DebtItemId", "Debit", "Credit"
                FROM "AccountTransactions" WHERE "LedgerEntryId" = header."ReversesEntryId"
                EXCEPT
                SELECT "Sequence", "AccountId", "CustomerId", "DebtItemId", "Credit", "Debit"
                FROM "AccountTransactions" WHERE "LedgerEntryId" = header."Id") THEN
                RAISE EXCEPTION 'AccountingReversalUnavailable' USING ERRCODE = '23514';
                END IF;
                END IF;
                RETURN NULL;
                END $$;
                CREATE OR REPLACE FUNCTION "Accounting_ValidateOperationalEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE h "LedgerEntries"%ROWTYPE; i "Invoices"%ROWTYPE; n integer;
                sales numeric; advances numeric; debts numeric; refunds numeric; cash numeric; purpose integer;
                received numeric; prior_advances numeric;
                BEGIN
                SELECT * INTO h FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                IF h."Kind" = 6 AND (h."InvoiceId" IS NOT NULL OR h."PaymentId" IS NOT NULL OR
                EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "DebtItemId" IS NOT NULL)) THEN
                RAISE EXCEPTION 'AccountingUseSourceCorrection' USING ERRCODE = '23514';
                END IF;
                IF h."EntryDate" = DATE '0001-01-01' THEN
                RAISE EXCEPTION 'AccountingPostingDateInvalid' USING ERRCODE = '23514';
                END IF;
                IF h."InvoiceId" IS NULL THEN RETURN NULL; END IF;
                SELECT * INTO i FROM "Invoices" WHERE "Id" = h."InvoiceId" FOR UPDATE;
                IF NOT FOUND OR i."Type" <> 0 OR i."LegacyInvoiceTypeCode" IS NOT NULL OR
                i."ParentInvoiceId" IS NOT NULL OR i."CustomerId" IS NULL OR
                EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND "CustomerId" IS DISTINCT FROM i."CustomerId") OR
                EXISTS (SELECT 1 FROM "AccountTransactions" t JOIN "DebtItems" d ON d."Id" = t."DebtItemId"
                WHERE t."LedgerEntryId" = h."Id" AND (d."InvoiceId" IS DISTINCT FROM i."Id" OR d."Kind" <> 1 OR
                (h."Kind" = 1 AND d."OriginalAmount" <> i."RemainingAmount"))) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                SELECT coalesce(sum("Amount"), 0) INTO received FROM "Payments" WHERE "InvoiceId" = i."Id" AND "PaymentType" = 0;
                IF i."PaidAmount" <> received OR i."PaidAmount" < 0 OR i."PaidAmount" > i."TotalAmount" OR
                (i."Status" = 3 AND i."RemainingAmount" <> 0) OR
                (i."Status" <> 3 AND i."RemainingAmount" <> i."TotalAmount" - received) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                SELECT coalesce(sum(t."Credit" - t."Debit"), 0) INTO prior_advances FROM "AccountTransactions" t
                JOIN "LedgerEntries" e ON e."Id" = t."LedgerEntryId" WHERE e."InvoiceId" = i."Id" AND e."Id" <> h."Id"
                AND e."PostedAt" IS NOT NULL AND t."AccountId" = 'a1100000-0000-0000-0000-000000000006';
                SELECT count(*),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000001'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000006'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000004'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000008'), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000005'), 0)
                INTO n, sales, advances, debts, refunds, cash FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id";
                IF h."Kind" = 1 THEN
                IF i."Status" NOT IN (6, 7) OR i."TaxAmount" <> 0 OR i."TotalAmount" <= 0 OR
                i."PaidAmount" < 0 OR i."RemainingAmount" < 0 OR i."PaidAmount" + i."RemainingAmount" <> i."TotalAmount" OR
                sales <> -i."TotalAmount" OR advances <> i."PaidAmount" OR prior_advances <> i."PaidAmount" OR debts <> i."RemainingAmount" OR
                n <> 1 + (CASE WHEN i."PaidAmount" > 0 THEN 1 ELSE 0 END) + (CASE WHEN i."RemainingAmount" > 0 THEN 1 ELSE 0 END) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                ELSIF h."Kind" = 7 THEN
                IF i."Status" <> 3 OR i."RemainingAmount" <> 0 OR i."PaidAmount" <= 0 OR n <> 2 OR
                advances <> i."PaidAmount" OR prior_advances <> i."PaidAmount" OR refunds <> -i."PaidAmount" THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                ELSIF h."Kind" IN (2, 5) THEN
                SELECT "Purpose" INTO purpose FROM "Payments" WHERE "Id" = h."PaymentId";
                IF (h."Kind" = 2 AND (i."Status" = 3 OR purpose IS DISTINCT FROM 1 OR n <> 2 OR cash <= 0 OR
                (i."Status" IN (6, 7) AND NOT EXISTS (SELECT 1 FROM "LedgerEntries" e WHERE e."InvoiceId" = i."Id" AND e."Kind" = 1 AND e."PostedAt" IS NOT NULL)) OR
                NOT ((advances = -cash AND debts = 0) OR (debts = -cash AND advances = 0)))) OR
                (h."Kind" = 5 AND (i."Status" <> 3 OR purpose IS DISTINCT FROM 3 OR n <> 2 OR cash >= 0 OR refunds <> -cash)) THEN
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                ELSE
                RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Suppliers_SupplierId",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stocks_TotalCost",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_SupplierId",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_StockMovements_TotalCost",
                table: "StockMovements");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Category",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts");

            migrationBuilder.DeleteData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000012"));

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "SupplierId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "TotalCost",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "Accounts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries",
                sql: "\"Kind\" BETWEEN 1 AND 7 AND \"ReferenceType\" BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts",
                sql: "\"Type\" BETWEEN 1 AND 8");
            migrationBuilder.Sql("ALTER TABLE \"Accounts\" ENABLE TRIGGER \"TR_Accounts_Fixed\";");
        }
    }
}
