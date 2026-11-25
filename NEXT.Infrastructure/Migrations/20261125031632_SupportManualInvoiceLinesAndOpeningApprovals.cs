using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupportManualInvoiceLinesAndOpeningApprovals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "InvoiceItems",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ManualName",
                table: "InvoiceItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccountOpenings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Account = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BeforeBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    VerifiedBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    PositionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountOpenings", x => x.Id);
                    table.CheckConstraint("CK_AccountOpenings_Valid", "\"Account\" IN (5, 9) AND \"Date\" <> DATE '0001-01-01' AND \"VerifiedBalance\" >= 0 AND \"VerifiedBalance\" <> \"BeforeBalance\" AND length(\"PositionHash\") = 64");
                    table.ForeignKey(
                        name: "FK_AccountOpenings_Users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries",
                sql: "\"Kind\" BETWEEN 1 AND 8 AND \"ReferenceType\" BETWEEN 1 AND 6");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InvoiceItems_LineSource",
                table: "InvoiceItems",
                sql: "(\"ProductId\" IS NOT NULL AND \"ManualName\" IS NULL) OR (\"ProductId\" IS NULL AND nullif(btrim(\"ManualName\"), '') IS NOT NULL AND \"UnitCost\" = 0 AND \"ProductBatchId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AccountOpenings_Account",
                table: "AccountOpenings",
                column: "Account",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountOpenings_ActorId",
                table: "AccountOpenings",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountOpenings_OperationId",
                table: "AccountOpenings",
                column: "OperationId",
                unique: true);
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
                (header."Kind" = 3 AND header."ReferenceType" IN (3, 4, 6)) OR
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
                (SELECT "ProductId", sum("Quantity") AS qty FROM "InvoiceItems" WHERE "InvoiceId" = i."Id" AND "ProductId" IS NOT NULL GROUP BY "ProductId") expected
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
                
                CREATE FUNCTION "Accounting_ProtectOpening"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514'; END $$;
                CREATE TRIGGER "TR_AccountOpenings_Immutable" BEFORE UPDATE OR DELETE ON "AccountOpenings"
                FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectOpening"();
                
                CREATE FUNCTION "Accounting_ValidateOpeningDocument"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                IF NOT EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 3 AND "ReferenceType" = 6 AND "ReferenceId" = NEW."Id" AND "PostedAt" IS NOT NULL) THEN
                RAISE EXCEPTION 'AccountingOpeningInvalid' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_AccountOpenings_Posted" AFTER INSERT ON "AccountOpenings"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateOpeningDocument"();
                
                CREATE FUNCTION "Accounting_ValidateOpeningEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE h "LedgerEntries"%ROWTYPE; o "AccountOpenings"%ROWTYPE; n integer; target uuid; change numeric; equity numeric; before_value numeric; actual numeric; latest date;
                BEGIN
                SELECT * INTO h FROM "LedgerEntries" WHERE "Id" = NEW."Id";
                IF h."Kind" = 6 AND EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "Id" = h."ReversesEntryId" AND "ReferenceType" = 6) THEN
                RAISE EXCEPTION 'AccountingUseSourceCorrection' USING ERRCODE = '23514';
                END IF;
                IF h."ReferenceType" <> 6 THEN RETURN NULL; END IF;
                SELECT * INTO o FROM "AccountOpenings" WHERE "Id" = h."ReferenceId";
                IF NOT FOUND OR h."Kind" <> 3 OR h."InvoiceId" IS NOT NULL OR h."PaymentId" IS NOT NULL OR
                h."OperationId" <> o."OperationId" OR h."EntryDate" <> o."Date" OR h."ActorId" <> o."ActorId" THEN
                RAISE EXCEPTION 'AccountingOpeningInvalid' USING ERRCODE = '23514';
                END IF;
                target := CASE WHEN o."Account" = 9 THEN 'a1100000-0000-0000-0000-000000000009'::uuid ELSE 'a1100000-0000-0000-0000-000000000005'::uuid END;
                SELECT count(*), coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = target), 0),
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000007'), 0)
                INTO n, change, equity FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id";
                SELECT coalesce(sum(t."Debit" - t."Credit"), 0), max(e."EntryDate") INTO before_value, latest
                FROM "AccountTransactions" t JOIN "LedgerEntries" e ON e."Id" = t."LedgerEntryId"
                WHERE e."Id" <> h."Id" AND e."PostedAt" IS NOT NULL AND t."AccountId" = target;
                IF n <> 2 OR change <> o."VerifiedBalance" - o."BeforeBalance" OR equity <> -change OR before_value <> o."BeforeBalance" OR
                latest > o."Date" OR EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId" = h."Id" AND ("CustomerId" IS NOT NULL OR "DebtItemId" IS NOT NULL)) THEN
                RAISE EXCEPTION 'AccountingOpeningStale' USING ERRCODE = '23514';
                END IF;
                IF o."Account" = 9 THEN
                SELECT coalesce(sum("TotalCost"), 0) INTO actual FROM "Stocks";
                IF actual <> o."VerifiedBalance" THEN RAISE EXCEPTION 'AccountingOpeningStale' USING ERRCODE = '23514'; END IF;
                END IF;
                RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_LedgerEntries_OpeningSource" AFTER INSERT OR UPDATE ON "LedgerEntries"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateOpeningEntry"();
                
                CREATE FUNCTION "Accounting_ProtectInvoiceItem"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE source uuid;
                BEGIN
                source := CASE WHEN TG_OP = 'DELETE' THEN OLD."InvoiceId" ELSE NEW."InvoiceId" END;
                PERFORM 1 FROM "Invoices" WHERE "Id" = source FOR UPDATE;
                IF EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "InvoiceId" = source AND "Kind" = 1 AND "PostedAt" IS NOT NULL) OR
                (TG_OP = 'UPDATE' AND EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "InvoiceId" = OLD."InvoiceId" AND "Kind" = 1 AND "PostedAt" IS NOT NULL)) THEN
                RAISE EXCEPTION 'AccountingRecognizedInvoiceImmutable' USING ERRCODE = '23514';
                END IF;
                IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_InvoiceItems_RecognizedImmutable" BEFORE INSERT OR UPDATE OR DELETE ON "InvoiceItems"
                FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectInvoiceItem"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "AccountOpenings") OR EXISTS (SELECT 1 FROM "InvoiceItems" WHERE "ProductId" IS NULL) THEN
                RAISE EXCEPTION 'Cannot downgrade after manual invoice lines or opening approvals have been used.';
                END IF;
                END $$;
                DROP TRIGGER "TR_InvoiceItems_RecognizedImmutable" ON "InvoiceItems";
                DROP FUNCTION "Accounting_ProtectInvoiceItem"();
                DROP TRIGGER "TR_AccountOpenings_Immutable" ON "AccountOpenings";
                DROP FUNCTION "Accounting_ProtectOpening"();
                DROP TRIGGER "TR_AccountOpenings_Posted" ON "AccountOpenings";
                DROP FUNCTION "Accounting_ValidateOpeningDocument"();
                DROP TRIGGER "TR_LedgerEntries_OpeningSource" ON "LedgerEntries";
                DROP FUNCTION "Accounting_ValidateOpeningEntry"();
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
                CREATE OR REPLACE FUNCTION "Accounting_ValidateInventoryEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                """);
            migrationBuilder.DropTable(
                name: "AccountOpenings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InvoiceItems_LineSource",
                table: "InvoiceItems");

            migrationBuilder.DropColumn(
                name: "ManualName",
                table: "InvoiceItems");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                table: "InvoiceItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries",
                sql: "\"Kind\" BETWEEN 1 AND 8 AND \"ReferenceType\" BETWEEN 1 AND 5");
        }
    }
}
