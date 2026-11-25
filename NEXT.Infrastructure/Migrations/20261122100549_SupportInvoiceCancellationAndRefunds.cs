using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupportInvoiceCancellationAndRefunds : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "InvoiceStatusHistory",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "Code", "IsTechnical", "Name", "NormalSide", "Type" },
                values: new object[] { new Guid("a1100000-0000-0000-0000-000000000008"), "CUSTOMER-REFUNDS", false, "المردودات المستحقة للعملاء", 1, 8 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries",
                sql: "\"Kind\" BETWEEN 1 AND 7 AND \"ReferenceType\" BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts",
                sql: "\"Type\" BETWEEN 1 AND 8");
            migrationBuilder.Sql("""
                ALTER TABLE "Accounts" ENABLE TRIGGER "TR_Accounts_Fixed";
            """);

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
            """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "Accounting_ProtectLine"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE posted timestamptz; owner_id uuid; phone text;
                BEGIN
                IF TG_OP <> 'INSERT' THEN
                RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514';
                END IF;
                SELECT "PostedAt" INTO posted FROM "LedgerEntries" WHERE "Id" = NEW."LedgerEntryId" FOR UPDATE;
                IF NOT FOUND OR posted IS NOT NULL THEN
                RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514';
                END IF;
                IF NEW."DebtItemId" IS NOT NULL THEN
                PERFORM 1 FROM "DebtItems" WHERE "Id" = NEW."DebtItemId" FOR UPDATE;
                SELECT a."CustomerId", c."Phone" INTO owner_id, phone FROM "DebtItems" d
                JOIN "CustomerDebtAccounts" a ON a."Id" = d."DebtAccountId"
                JOIN "Customers" c ON c."Id" = a."CustomerId"
                WHERE d."Id" = NEW."DebtItemId" AND c."DeletedAt" IS NULL AND c."IsActive";
                IF NOT FOUND OR owner_id IS DISTINCT FROM NEW."CustomerId" OR nullif(btrim(phone), '') IS NULL THEN
                RAISE EXCEPTION 'AccountingDebtCustomerInvalid' USING ERRCODE = '23514';
                END IF;
                END IF;
                IF NEW."AccountId" = 'a1100000-0000-0000-0000-000000000008' THEN
                PERFORM 1 FROM "Invoices" i JOIN "LedgerEntries" e ON e."InvoiceId" = i."Id"
                WHERE e."Id" = NEW."LedgerEntryId" FOR UPDATE OF i;
                IF NOT FOUND THEN RAISE EXCEPTION 'AccountingInvalidReference' USING ERRCODE = '23514'; END IF;
                END IF;
                RETURN NEW;
                END $$;
            """);

            migrationBuilder.Sql("""
                CREATE FUNCTION "Accounting_ValidateRefundBalance"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE invoice_id uuid; owner_id uuid; invoice_status integer; received numeric; balance numeric;
                BEGIN
                IF NEW."AccountId" <> 'a1100000-0000-0000-0000-000000000008' THEN RETURN NULL; END IF;
                SELECT i."Id", i."CustomerId", i."Status", i."PaidAmount" INTO invoice_id, owner_id, invoice_status, received
                FROM "Invoices" i JOIN "LedgerEntries" e ON e."InvoiceId" = i."Id" WHERE e."Id" = NEW."LedgerEntryId";
                SELECT coalesce(sum(t."Credit" - t."Debit"), 0) INTO balance FROM "AccountTransactions" t
                JOIN "LedgerEntries" e ON e."Id" = t."LedgerEntryId"
                WHERE t."AccountId" = NEW."AccountId" AND e."InvoiceId" = invoice_id AND e."PostedAt" IS NOT NULL;
                IF owner_id IS NULL OR owner_id IS DISTINCT FROM NEW."CustomerId" OR invoice_status <> 3 OR balance < 0 OR balance > received THEN
                RAISE EXCEPTION 'InvoiceRefundInvalid' USING ERRCODE = '23514';
                END IF;
                RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_AccountTransactions_RefundBalance" AFTER INSERT ON "AccountTransactions"
                DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateRefundBalance"();
            """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "Kind" = 7) OR
                EXISTS (SELECT 1 FROM "AccountTransactions" WHERE "AccountId" = 'a1100000-0000-0000-0000-000000000008') THEN
                RAISE EXCEPTION 'Cannot roll back while cancellation or refund transactions exist.';
                END IF;
                END $$;
                DROP TRIGGER "TR_AccountTransactions_RefundBalance" ON "AccountTransactions";
                DROP FUNCTION "Accounting_ValidateRefundBalance"();
                ALTER TABLE "Accounts" DISABLE TRIGGER "TR_Accounts_Fixed";
            """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts");

            migrationBuilder.DeleteData(
                table: "Accounts",
                keyColumn: "Id",
                keyValue: new Guid("a1100000-0000-0000-0000-000000000008"));

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "InvoiceStatusHistory");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries",
                sql: "\"Kind\" BETWEEN 1 AND 6 AND \"ReferenceType\" BETWEEN 1 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts",
                sql: "\"Type\" BETWEEN 1 AND 7");
            migrationBuilder.Sql("""
                ALTER TABLE "Accounts" ENABLE TRIGGER "TR_Accounts_Fixed";
            """);

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
                IF NOT ((header."Kind" IN (1, 4) AND header."ReferenceType" = 1) OR
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
            """);

            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION "Accounting_ProtectLine"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE posted timestamptz; owner_id uuid; phone text;
                BEGIN
                IF TG_OP <> 'INSERT' THEN
                RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514';
                END IF;
                SELECT "PostedAt" INTO posted FROM "LedgerEntries" WHERE "Id" = NEW."LedgerEntryId" FOR UPDATE;
                IF NOT FOUND OR posted IS NOT NULL THEN
                RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514';
                END IF;
                IF NEW."DebtItemId" IS NOT NULL THEN
                PERFORM 1 FROM "DebtItems" WHERE "Id" = NEW."DebtItemId" FOR UPDATE;
                SELECT a."CustomerId", c."Phone" INTO owner_id, phone FROM "DebtItems" d
                JOIN "CustomerDebtAccounts" a ON a."Id" = d."DebtAccountId"
                JOIN "Customers" c ON c."Id" = a."CustomerId"
                WHERE d."Id" = NEW."DebtItemId" AND c."DeletedAt" IS NULL AND c."IsActive";
                IF NOT FOUND OR owner_id IS DISTINCT FROM NEW."CustomerId" OR nullif(btrim(phone), '') IS NULL THEN
                RAISE EXCEPTION 'AccountingDebtCustomerInvalid' USING ERRCODE = '23514';
                END IF;
                END IF;
                RETURN NEW;
                END $$;
            """);

        }
    }
}
