using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerDebtBook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DebtAccountPaymentId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DebtWithdrawalHash",
                table: "Invoices",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DebtAccountPayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DebtAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "integer", nullable: false),
                    CashAccount = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtAccountPayments", x => x.Id);
                    table.CheckConstraint("CK_DebtAccountPayment_Valid", "\"Amount\" > 0 AND \"CashAccount\" IN (5,13,14,15) AND length(\"RequestHash\") = 64");
                    table.ForeignKey(
                        name: "FK_DebtAccountPayments_CustomerDebtAccounts_DebtAccountId",
                        column: x => x.DebtAccountId,
                        principalTable: "CustomerDebtAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebtAccountPayments_Users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_DebtAccountPaymentId",
                table: "Payments",
                column: "DebtAccountPaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_DebtAccountPayments_ActorId",
                table: "DebtAccountPayments",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_DebtAccountPayments_DebtAccountId",
                table: "DebtAccountPayments",
                column: "DebtAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_DebtAccountPayments_Number",
                table: "DebtAccountPayments",
                column: "Number",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_DebtAccountPayments_DebtAccountPaymentId",
                table: "Payments",
                column: "DebtAccountPaymentId",
                principalTable: "DebtAccountPayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""

                CREATE TRIGGER "TR_DebtAccountPayments_Immutable" BEFORE UPDATE OR DELETE ON "DebtAccountPayments" FOR EACH ROW EXECUTE FUNCTION "Finance_ProtectDocument"();
                CREATE FUNCTION "DebtBook_ValidateAccountPayment"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE CONSTRAINT TRIGGER "TR_DebtAccountPayments_Source" AFTER INSERT ON "DebtAccountPayments" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "DebtBook_ValidateAccountPayment"();
                CREATE CONSTRAINT TRIGGER "TR_Payments_DebtAccountSource" AFTER INSERT ON "Payments" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "DebtBook_ValidateAccountPayment"();
                INSERT INTO "CustomerDebtAccounts" ("Id","CustomerId","LastActivityAt","RowVersion","CreatedAt")
                SELECT gen_random_uuid(),i."CustomerId",max(coalesce(i."LastActionAt",i."InvoiceDate")),uuid_send(gen_random_uuid()),now()
                FROM "Invoices" i WHERE i."CustomerId" IS NOT NULL AND i."Type"=0 AND i."LegacyInvoiceTypeCode" IS NULL AND i."ParentInvoiceId" IS NULL AND i."Status" IN (6,7)
                AND NOT EXISTS (SELECT 1 FROM "CustomerDebtAccounts" a WHERE a."CustomerId"=i."CustomerId")
                GROUP BY i."CustomerId" ON CONFLICT ("CustomerId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS(SELECT 1 FROM "DebtAccountPayments") OR EXISTS(SELECT 1 FROM "Invoices" WHERE "DebtWithdrawalHash" IS NOT NULL) THEN RAISE EXCEPTION 'Cannot downgrade after customer debt book documents have been used.'; END IF; END $$;
                DROP TRIGGER "TR_Payments_DebtAccountSource" ON "Payments";
                DROP TRIGGER "TR_DebtAccountPayments_Source" ON "DebtAccountPayments";
                DROP TRIGGER "TR_DebtAccountPayments_Immutable" ON "DebtAccountPayments";
                DROP FUNCTION "DebtBook_ValidateAccountPayment"();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_DebtAccountPayments_DebtAccountPaymentId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "DebtAccountPayments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_DebtAccountPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DebtAccountPaymentId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "DebtWithdrawalHash",
                table: "Invoices");
        }
    }
}
