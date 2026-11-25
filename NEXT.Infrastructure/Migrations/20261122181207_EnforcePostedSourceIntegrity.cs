using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePostedSourceIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE FUNCTION "Accounting_ProtectPostedPayment"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "PaymentId" = OLD."Id" AND "PostedAt" IS NOT NULL) THEN
                        RAISE EXCEPTION 'AccountingPostedPaymentImmutable' USING ERRCODE = '23514';
                    END IF;
                    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Payments_PostedImmutable" BEFORE UPDATE OR DELETE ON "Payments"
                    FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectPostedPayment"();

                CREATE FUNCTION "Accounting_ProtectRecognizedInvoice"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "InvoiceId" = OLD."Id" AND "Kind" = 1 AND "PostedAt" IS NOT NULL) AND
                        (to_jsonb(NEW) - ARRAY['PaidAmount', 'RemainingAmount', 'PaymentStatus', 'PaymentMethod', 'Status', 'LastActionAt', 'RowVersion', 'UpdatedAt', 'UpdatedBy']) IS DISTINCT FROM
                        (to_jsonb(OLD) - ARRAY['PaidAmount', 'RemainingAmount', 'PaymentStatus', 'PaymentMethod', 'Status', 'LastActionAt', 'RowVersion', 'UpdatedAt', 'UpdatedBy']) THEN
                        RAISE EXCEPTION 'AccountingRecognizedInvoiceImmutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Invoices_RecognizedImmutable" BEFORE UPDATE ON "Invoices"
                    FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectRecognizedInvoice"();

                CREATE FUNCTION "Accounting_ValidateInvoiceSettlement"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE i "Invoices"%ROWTYPE; receipts numeric; debt numeric;
                BEGIN
                    SELECT * INTO i FROM "Invoices" WHERE "Id" = NEW."Id";
                    IF NOT EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "InvoiceId" = i."Id" AND "PostedAt" IS NOT NULL) THEN RETURN NULL; END IF;
                    SELECT coalesce(sum("Amount"), 0) INTO receipts FROM "Payments" WHERE "InvoiceId" = i."Id" AND "PaymentType" = 0;
                    IF i."CustomerId" IS NULL OR i."PaidAmount" <> receipts OR i."PaidAmount" < 0 OR i."TotalAmount" < i."PaidAmount" OR
                        (i."Status" = 3 AND i."RemainingAmount" <> 0) OR
                        (i."Status" <> 3 AND i."RemainingAmount" <> i."TotalAmount" - receipts) THEN
                        RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "InvoiceId" = i."Id" AND "Kind" = 1 AND "PostedAt" IS NOT NULL) THEN
                        SELECT coalesce(sum(t."Debit" - t."Credit"), 0) INTO debt FROM "AccountTransactions" t
                            JOIN "LedgerEntries" e ON e."Id" = t."LedgerEntryId" LEFT JOIN "DebtItems" d ON d."Id" = t."DebtItemId"
                            WHERE t."AccountId" = 'a1100000-0000-0000-0000-000000000004' AND e."PostedAt" IS NOT NULL
                            AND (e."InvoiceId" = i."Id" OR d."InvoiceId" = i."Id");
                        IF debt <> i."RemainingAmount" THEN
                            RAISE EXCEPTION 'AccountingInvoicePostingMismatch' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_Invoices_Settlement" AFTER UPDATE ON "Invoices"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateInvoiceSettlement"();

                CREATE FUNCTION "Accounting_ValidateOperationalEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE CONSTRAINT TRIGGER "TR_LedgerEntries_SourceMeaning" AFTER INSERT OR UPDATE ON "LedgerEntries"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateOperationalEntry"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_Invoices_Settlement" ON "Invoices";
                DROP FUNCTION "Accounting_ValidateInvoiceSettlement"();
                DROP TRIGGER "TR_Invoices_RecognizedImmutable" ON "Invoices";
                DROP FUNCTION "Accounting_ProtectRecognizedInvoice"();
                DROP TRIGGER "TR_LedgerEntries_SourceMeaning" ON "LedgerEntries";
                DROP FUNCTION "Accounting_ValidateOperationalEntry"();
                DROP TRIGGER "TR_Payments_PostedImmutable" ON "Payments";
                DROP FUNCTION "Accounting_ProtectPostedPayment"();
                """);
        }
    }
}
