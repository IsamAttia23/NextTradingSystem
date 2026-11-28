using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDebtAccountCycles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Flush existing deferred events before DDL, and validate backfilled rows before later ALTER TABLE statements.
            migrationBuilder.Sql("SET CONSTRAINTS ALL IMMEDIATE;");
            migrationBuilder.DropIndex(
                name: "IX_CustomerDebtAccounts_CustomerId",
                table: "CustomerDebtAccounts");

            migrationBuilder.AddColumn<Guid>(
                name: "DebtAccountId",
                table: "Invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClosedAt",
                table: "CustomerDebtAccounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClosedBy",
                table: "CustomerDebtAccounts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CycleNumber",
                table: "CustomerDebtAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_DebtAccountId",
                table: "Invoices",
                column: "DebtAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebtAccounts_ClosedBy",
                table: "CustomerDebtAccounts",
                column: "ClosedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebtAccounts_CustomerId",
                table: "CustomerDebtAccounts",
                column: "CustomerId",
                unique: true,
                filter: "\"ClosedAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebtAccounts_CustomerId_CycleNumber",
                table: "CustomerDebtAccounts",
                columns: new[] { "CustomerId", "CycleNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerDebtAccounts_Users_ClosedBy",
                table: "CustomerDebtAccounts",
                column: "ClosedBy",
                principalSchema: "security",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Invoices_CustomerDebtAccounts_DebtAccountId",
                table: "Invoices",
                column: "DebtAccountId",
                principalTable: "CustomerDebtAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""

                CREATE OR REPLACE FUNCTION "Accounting_ProtectDebtAccount"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'AccountingDebtAccountImmutable' USING ERRCODE='23514'; END IF;
                 IF OLD."ClosedAt" IS NOT NULL AND (to_jsonb(NEW)-ARRAY['RowVersion','UpdatedAt','UpdatedBy']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['RowVersion','UpdatedAt','UpdatedBy']) THEN
                  RAISE EXCEPTION 'DebtBookAccountClosed' USING ERRCODE='23514'; END IF;
                 IF (to_jsonb(NEW)-ARRAY['LastActivityAt','ClosedAt','ClosedBy','RowVersion','UpdatedAt','UpdatedBy']) IS DISTINCT FROM
                    (to_jsonb(OLD)-ARRAY['LastActivityAt','ClosedAt','ClosedBy','RowVersion','UpdatedAt','UpdatedBy']) THEN
                  RAISE EXCEPTION 'AccountingDebtAccountImmutable' USING ERRCODE='23514'; END IF;
                 RETURN NEW;
                END $$;
                ALTER TABLE "Invoices" DISABLE TRIGGER "TR_Invoices_RecognizedImmutable";
                UPDATE "Invoices" i SET "DebtAccountId"=a."Id" FROM "CustomerDebtAccounts" a
                WHERE i."CustomerId"=a."CustomerId" AND i."Type"=0 AND i."LegacyInvoiceTypeCode" IS NULL AND i."ParentInvoiceId" IS NULL AND i."Status" IN (6,7);
                ALTER TABLE "Invoices" ENABLE TRIGGER "TR_Invoices_RecognizedImmutable";
                UPDATE "CustomerDebtAccounts" a SET "ClosedAt"=now(),"LastActivityAt"=greatest(a."LastActivityAt",now()),"RowVersion"=uuid_send(gen_random_uuid())
                WHERE NOT EXISTS(SELECT 1 FROM "Invoices" i WHERE i."DebtAccountId"=a."Id" AND i."RemainingAmount">0)
                AND NOT EXISTS(SELECT 1 FROM "DebtItems" d WHERE d."DebtAccountId"=a."Id" AND NOT EXISTS(SELECT 1 FROM "AccountTransactions" t JOIN "LedgerEntries" e ON e."Id"=t."LedgerEntryId" WHERE t."DebtItemId"=d."Id" AND e."PostedAt" IS NOT NULL))
                AND (SELECT coalesce(sum(t."Debit"-t."Credit"),0) FROM "DebtItems" d JOIN "AccountTransactions" t ON t."DebtItemId"=d."Id" JOIN "LedgerEntries" e ON e."Id"=t."LedgerEntryId" WHERE d."DebtAccountId"=a."Id" AND e."PostedAt" IS NOT NULL)=0;
                ALTER TABLE "CustomerDebtAccounts" ADD CONSTRAINT "CK_DebtAccount_Cycle" CHECK ("CycleNumber">0 AND ("ClosedAt" IS NOT NULL OR "ClosedBy" IS NULL));
                ALTER TABLE "DebtAccountPayments" ADD CONSTRAINT "CK_DebtAccountPayment_Description" CHECK ("Notes" IS NOT NULL AND length(btrim("Notes"))>0) NOT VALID;

                CREATE FUNCTION "DebtBook_ProtectCycleMembership"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE aid uuid; owner uuid; closed timestamptz; require_open boolean := true;
                BEGIN
                 IF TG_TABLE_NAME='Invoices' THEN
                  IF TG_OP='UPDATE' AND OLD."DebtAccountId" IS NOT NULL THEN
                   IF NEW."DebtAccountId" IS DISTINCT FROM OLD."DebtAccountId" THEN RAISE EXCEPTION 'DebtBookCycleImmutable' USING ERRCODE='23514'; END IF;
                   require_open := false;
                  END IF;
                  aid := NEW."DebtAccountId";
                 ELSE aid := NEW."DebtAccountId";
                 END IF;
                 IF aid IS NULL THEN RETURN NEW; END IF;
                 SELECT "CustomerId","ClosedAt" INTO owner,closed FROM "CustomerDebtAccounts" WHERE "Id"=aid;
                 IF owner IS NULL OR (require_open AND closed IS NOT NULL) THEN RAISE EXCEPTION 'DebtBookAccountClosed' USING ERRCODE='23514'; END IF;
                 IF TG_TABLE_NAME='Invoices' THEN IF NEW."CustomerId" IS DISTINCT FROM owner THEN RAISE EXCEPTION 'DebtBookCycleMismatch' USING ERRCODE='23514'; END IF; END IF;
                 RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Invoices_DebtCycle" BEFORE INSERT OR UPDATE ON "Invoices" FOR EACH ROW EXECUTE FUNCTION "DebtBook_ProtectCycleMembership"();
                CREATE TRIGGER "TR_DebtItems_OpenCycle" BEFORE INSERT ON "DebtItems" FOR EACH ROW EXECUTE FUNCTION "DebtBook_ProtectCycleMembership"();
                CREATE TRIGGER "TR_DebtAccountPayments_OpenCycle" BEFORE INSERT ON "DebtAccountPayments" FOR EACH ROW EXECUTE FUNCTION "DebtBook_ProtectCycleMembership"();

                CREATE FUNCTION "DebtBook_ValidateClosedCycle"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE aid uuid; closed timestamptz; balance numeric;
                BEGIN
                 IF TG_TABLE_NAME='CustomerDebtAccounts' THEN aid:=NEW."Id";
                 ELSE SELECT "DebtAccountId" INTO aid FROM "DebtItems" WHERE "Id"=NEW."DebtItemId"; END IF;
                 IF aid IS NULL THEN RETURN NULL; END IF;
                 SELECT "ClosedAt" INTO closed FROM "CustomerDebtAccounts" WHERE "Id"=aid;
                 IF closed IS NULL THEN RETURN NULL; END IF;
                 SELECT coalesce(sum(t."Debit"-t."Credit"),0) INTO balance FROM "DebtItems" d JOIN "AccountTransactions" t ON t."DebtItemId"=d."Id" JOIN "LedgerEntries" e ON e."Id"=t."LedgerEntryId" WHERE d."DebtAccountId"=aid AND e."PostedAt" IS NOT NULL;
                 IF balance<>0 OR EXISTS(SELECT 1 FROM "Invoices" WHERE "DebtAccountId"=aid AND "RemainingAmount">0)
                 OR EXISTS(SELECT 1 FROM "DebtItems" d WHERE d."DebtAccountId"=aid AND NOT EXISTS(SELECT 1 FROM "AccountTransactions" t JOIN "LedgerEntries" e ON e."Id"=t."LedgerEntryId" WHERE t."DebtItemId"=d."Id" AND e."PostedAt" IS NOT NULL))
                 THEN RAISE EXCEPTION 'DebtBookUnsettledAccount' USING ERRCODE='23514'; END IF;
                 RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_DebtAccounts_ClosedBalance" AFTER INSERT OR UPDATE ON "CustomerDebtAccounts" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "DebtBook_ValidateClosedCycle"();
                CREATE CONSTRAINT TRIGGER "TR_Transactions_ClosedCycle" AFTER INSERT ON "AccountTransactions" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "DebtBook_ValidateClosedCycle"();
                CREATE OR REPLACE FUNCTION "DebtBook_ValidateAccountPayment"() RETURNS trigger LANGUAGE plpgsql AS $$
                                DECLARE d "DebtAccountPayments"%ROWTYPE; customer uuid; total numeric; n integer;
                                BEGIN
                                IF TG_TABLE_NAME='DebtAccountPayments' THEN
                                SELECT * INTO d FROM "DebtAccountPayments" WHERE "Id"=NEW."Id";
                                ELSE
                                IF NEW."DebtAccountPaymentId" IS NULL THEN RETURN NULL; END IF;
                                SELECT * INTO d FROM "DebtAccountPayments" WHERE "Id"=NEW."DebtAccountPaymentId";
                                END IF;
                                SELECT "CustomerId" INTO customer FROM "CustomerDebtAccounts" WHERE "Id"=d."DebtAccountId";
                                SELECT count(*),coalesce(sum("Amount"),0) INTO n,total FROM "Payments" WHERE "DebtAccountPaymentId"=d."Id";
                                IF d."Id" IS NULL OR customer IS NULL OR d."Amount"<=0 OR total<>d."Amount" OR n=0 OR
                                d."Number" <> 'ACC-' || replace(d."Id"::text,'-','') OR EXISTS (
                                SELECT 1 FROM "Payments" p LEFT JOIN "Invoices" i ON i."Id"=p."InvoiceId" WHERE p."DebtAccountPaymentId"=d."Id" AND
                                (p."CustomerId" IS DISTINCT FROM customer OR p."Amount"<=0 OR p."PaymentType"<>0 OR p."PaymentMethod"<>d."PaymentMethod" OR p."CashAccount"<>d."CashAccount" OR p."PaymentDate"<>d."Date" OR p."CreatedBy" IS DISTINCT FROM d."ActorId" OR
                                (p."InvoiceId" IS NOT NULL AND (p."Purpose"<>1 OR i."CustomerId" IS DISTINCT FROM customer OR i."DebtAccountId" IS DISTINCT FROM d."DebtAccountId" OR i."Status" NOT IN (6,7))) OR
                                (p."InvoiceId" IS NULL AND p."Purpose"<>2) OR
                                NOT EXISTS (SELECT 1 FROM "LedgerEntries" e WHERE e."PaymentId"=p."Id" AND e."Kind"=2 AND e."PostedAt" IS NOT NULL) OR
                                (p."InvoiceId" IS NULL AND EXISTS (SELECT 1 FROM "LedgerEntries" e JOIN "AccountTransactions" t ON t."LedgerEntryId"=e."Id" JOIN "DebtItems" item ON item."Id"=t."DebtItemId" WHERE e."PaymentId"=p."Id" AND (item."Kind"<>2 OR item."DebtAccountId"<>d."DebtAccountId"))))) THEN
                                RAISE EXCEPTION 'DebtBookPostingMismatch' USING ERRCODE='23514'; END IF;
                                RETURN NULL;
                                END $$;
                SET CONSTRAINTS ALL DEFERRED;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""

                DO $$ BEGIN IF EXISTS(SELECT 1 FROM "CustomerDebtAccounts" WHERE "CycleNumber">1) THEN RAISE EXCEPTION 'Cannot downgrade after multiple customer account cycles have been used.'; END IF; END $$;
                DROP TRIGGER "TR_Transactions_ClosedCycle" ON "AccountTransactions";
                DROP TRIGGER "TR_DebtAccounts_ClosedBalance" ON "CustomerDebtAccounts";
                DROP FUNCTION "DebtBook_ValidateClosedCycle"();
                DROP TRIGGER "TR_Invoices_DebtCycle" ON "Invoices";
                DROP TRIGGER "TR_DebtItems_OpenCycle" ON "DebtItems";
                DROP TRIGGER "TR_DebtAccountPayments_OpenCycle" ON "DebtAccountPayments";
                DROP FUNCTION "DebtBook_ProtectCycleMembership"();
                ALTER TABLE "CustomerDebtAccounts" DROP CONSTRAINT "CK_DebtAccount_Cycle";
                ALTER TABLE "DebtAccountPayments" DROP CONSTRAINT "CK_DebtAccountPayment_Description";
                CREATE OR REPLACE FUNCTION "DebtBook_ValidateAccountPayment"() RETURNS trigger LANGUAGE plpgsql AS $$
                                DECLARE d "DebtAccountPayments"%ROWTYPE; customer uuid; total numeric; n integer;
                                BEGIN
                                IF TG_TABLE_NAME='DebtAccountPayments' THEN
                                SELECT * INTO d FROM "DebtAccountPayments" WHERE "Id"=NEW."Id";
                                ELSE
                                IF NEW."DebtAccountPaymentId" IS NULL THEN RETURN NULL; END IF;
                                SELECT * INTO d FROM "DebtAccountPayments" WHERE "Id"=NEW."DebtAccountPaymentId";
                                END IF;
                                SELECT "CustomerId" INTO customer FROM "CustomerDebtAccounts" WHERE "Id"=d."DebtAccountId";
                                SELECT count(*),coalesce(sum("Amount"),0) INTO n,total FROM "Payments" WHERE "DebtAccountPaymentId"=d."Id";
                                IF d."Id" IS NULL OR customer IS NULL OR d."Amount"<=0 OR total<>d."Amount" OR n=0 OR
                                d."Number" <> 'ACC-' || replace(d."Id"::text,'-','') OR EXISTS (
                                SELECT 1 FROM "Payments" p LEFT JOIN "Invoices" i ON i."Id"=p."InvoiceId" WHERE p."DebtAccountPaymentId"=d."Id" AND
                                (p."CustomerId" IS DISTINCT FROM customer OR p."Amount"<=0 OR p."PaymentType"<>0 OR p."PaymentMethod"<>d."PaymentMethod" OR p."CashAccount"<>d."CashAccount" OR p."PaymentDate"<>d."Date" OR p."CreatedBy" IS DISTINCT FROM d."ActorId" OR
                                (p."InvoiceId" IS NOT NULL AND (p."Purpose"<>1 OR i."CustomerId" IS DISTINCT FROM customer OR i."Status" NOT IN (6,7))) OR
                                (p."InvoiceId" IS NULL AND p."Purpose"<>2) OR
                                NOT EXISTS (SELECT 1 FROM "LedgerEntries" e WHERE e."PaymentId"=p."Id" AND e."Kind"=2 AND e."PostedAt" IS NOT NULL) OR
                                (p."InvoiceId" IS NULL AND EXISTS (SELECT 1 FROM "LedgerEntries" e JOIN "AccountTransactions" t ON t."LedgerEntryId"=e."Id" JOIN "DebtItems" item ON item."Id"=t."DebtItemId" WHERE e."PaymentId"=p."Id" AND (item."Kind"<>2 OR item."DebtAccountId"<>d."DebtAccountId"))))) THEN
                                RAISE EXCEPTION 'DebtBookPostingMismatch' USING ERRCODE='23514'; END IF;
                                RETURN NULL;
                                END $$;
                CREATE OR REPLACE FUNCTION "Accounting_ProtectDebtAccount"() RETURNS trigger LANGUAGE plpgsql AS $$
                                BEGIN
                                    IF TG_OP = 'DELETE' THEN
                                        RAISE EXCEPTION 'AccountingDebtAccountImmutable' USING ERRCODE = '23514';
                                    END IF;
                                    IF (to_jsonb(NEW) - ARRAY['LastActivityAt', 'RowVersion', 'UpdatedAt', 'UpdatedBy']) IS DISTINCT FROM
                                        (to_jsonb(OLD) - ARRAY['LastActivityAt', 'RowVersion', 'UpdatedAt', 'UpdatedBy']) THEN
                                        RAISE EXCEPTION 'AccountingDebtAccountImmutable' USING ERRCODE = '23514';
                                    END IF;
                                    RETURN NEW;
                                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_CustomerDebtAccounts_Users_ClosedBy",
                table: "CustomerDebtAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_Invoices_CustomerDebtAccounts_DebtAccountId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_DebtAccountId",
                table: "Invoices");

            migrationBuilder.DropIndex(
                name: "IX_CustomerDebtAccounts_ClosedBy",
                table: "CustomerDebtAccounts");

            migrationBuilder.DropIndex(
                name: "IX_CustomerDebtAccounts_CustomerId",
                table: "CustomerDebtAccounts");

            migrationBuilder.DropIndex(
                name: "IX_CustomerDebtAccounts_CustomerId_CycleNumber",
                table: "CustomerDebtAccounts");

            migrationBuilder.DropColumn(
                name: "DebtAccountId",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "CustomerDebtAccounts");

            migrationBuilder.DropColumn(
                name: "ClosedBy",
                table: "CustomerDebtAccounts");

            migrationBuilder.DropColumn(
                name: "CycleNumber",
                table: "CustomerDebtAccounts");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebtAccounts_CustomerId",
                table: "CustomerDebtAccounts",
                column: "CustomerId",
                unique: true);
        }
    }
}
