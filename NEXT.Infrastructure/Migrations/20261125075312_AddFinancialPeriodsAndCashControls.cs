using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFinancialPeriodsAndCashControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AccountOpenings_Valid",
                table: "AccountOpenings");

            migrationBuilder.AddColumn<int>(
                name: "CashAccount",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.CreateTable(
                name: "CashReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Account = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    BookBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    ExternalBalance = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    DepositsInTransit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    OutstandingPayments = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    PositionHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Evidence = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                    table.PrimaryKey("PK_CashReconciliations", x => x.Id);
                    table.CheckConstraint("CK_CashReconciliation_Values", "\"Account\" IN (5,13,14,15) AND \"DepositsInTransit\" >= 0 AND \"OutstandingPayments\" >= 0 AND (\"Account\" <> 5 OR (\"DepositsInTransit\" = 0 AND \"OutstandingPayments\" = 0))");
                    table.ForeignKey(
                        name: "FK_CashReconciliations_Users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromAccount = table.Column<int>(type: "integer", nullable: false),
                    ToAccount = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
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
                    table.PrimaryKey("PK_CashTransfers", x => x.Id);
                    table.CheckConstraint("CK_CashTransfer_Values", "\"FromAccount\" IN (5,13,14,15) AND \"ToAccount\" IN (5,13,14,15) AND \"FromAccount\" <> \"ToAccount\" AND \"Amount\" > 0 AND \"OperationId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.ForeignKey(
                        name: "FK_CashTransfers_Users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinancialPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Start = table.Column<DateOnly>(type: "date", nullable: false),
                    End = table.Column<DateOnly>(type: "date", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialPeriods", x => x.Id);
                    table.CheckConstraint("CK_FinancialPeriod_Dates", "\"Start\" > DATE '0001-01-01' AND \"End\" >= \"Start\"");
                });

            migrationBuilder.CreateTable(
                name: "FinancialPeriodEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PeriodId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("PK_FinancialPeriodEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialPeriodEvents_FinancialPeriods_PeriodId",
                        column: x => x.PeriodId,
                        principalTable: "FinancialPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinancialPeriodEvents_Users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("ALTER TABLE \"Accounts\" DISABLE TRIGGER \"TR_Accounts_Fixed\";");
            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "Category", "Code", "IsTechnical", "Name", "NormalSide", "Type" },
                values: new object[,]
                {
                    { new Guid("a1100000-0000-0000-0000-000000000013"), 1, "BANK", false, "البنك", 0, 13 },
                    { new Guid("a1100000-0000-0000-0000-000000000014"), 1, "WALLET", false, "المحفظة", 0, 14 },
                    { new Guid("a1100000-0000-0000-0000-000000000015"), 1, "CLEARING", false, "تحصيلات قيد التسوية", 0, 15 }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_CashAccount",
                table: "Payments",
                sql: "\"CashAccount\" IN (5,13,14,15)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Kind",
                table: "LedgerEntries",
                sql: "\"Kind\" BETWEEN 1 AND 9 AND \"ReferenceType\" BETWEEN 1 AND 7");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Accounts_Type",
                table: "Accounts",
                sql: "\"Type\" BETWEEN 1 AND 15");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AccountOpenings_Valid",
                table: "AccountOpenings",
                sql: "\"Account\" IN (5, 9, 13, 14, 15) AND \"Date\" <> DATE '0001-01-01' AND \"VerifiedBalance\" >= 0 AND \"VerifiedBalance\" <> \"BeforeBalance\" AND length(\"PositionHash\") = 64");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_ActorId",
                table: "CashReconciliations",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_CashReconciliations_OperationId",
                table: "CashReconciliations",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CashTransfers_ActorId",
                table: "CashTransfers",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_CashTransfers_OperationId",
                table: "CashTransfers",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPeriodEvents_ActorId",
                table: "FinancialPeriodEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPeriodEvents_PeriodId_Version",
                table: "FinancialPeriodEvents",
                columns: new[] { "PeriodId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinancialPeriods_Start",
                table: "FinancialPeriods",
                column: "Start",
                unique: true);
            migrationBuilder.Sql("ALTER TABLE \"Accounts\" ENABLE TRIGGER \"TR_Accounts_Fixed\";");
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
                (header."Kind" = 9 AND header."ReferenceType" = 7 AND header."InvoiceId" IS NULL AND header."PaymentId" IS NULL) OR
                (header."Kind" = 8 AND header."ReferenceType" = 5 AND header."InvoiceId" IS NULL AND header."PaymentId" IS NULL)) THEN
                RAISE EXCEPTION 'AccountingInvalidReference' USING ERRCODE = '23514';
                END IF;
                IF header."Kind" IN (2, 5) THEN
                SELECT "Amount", "PaymentType", "InvoiceId", "CustomerId", "PaymentDate"
                INTO payment_amount, payment_type, payment_invoice, payment_customer, payment_date
                FROM "Payments" WHERE "Id" = header."PaymentId";
                SELECT coalesce(sum("Debit" - "Credit"), 0) INTO cash FROM "AccountTransactions"
                WHERE "LedgerEntryId" = header."Id" AND "AccountId" = ('a1100000-0000-0000-0000-' || lpad((SELECT "CashAccount"::text FROM "Payments" WHERE "Id" = header."PaymentId"), 12, '0'))::uuid;
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
                EXISTS (SELECT 1 FROM "LedgerEntries" WHERE "Id" = h."ReversesEntryId" AND "Kind" IN (8,9))) THEN
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
                coalesce(sum("Debit" - "Credit") FILTER (WHERE "AccountId" IN ('a1100000-0000-0000-0000-000000000005','a1100000-0000-0000-0000-000000000013','a1100000-0000-0000-0000-000000000014','a1100000-0000-0000-0000-000000000015')), 0)
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
                CREATE OR REPLACE FUNCTION "Accounting_ValidateOpeningEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                target := ('a1100000-0000-0000-0000-' || lpad(o."Account"::text,12,'0'))::uuid;
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
                CREATE FUNCTION "Finance_ProtectDocument"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE='23514'; END $$;
                CREATE TRIGGER "TR_FinancialPeriodEvents_Immutable" BEFORE UPDATE OR DELETE ON "FinancialPeriodEvents" FOR EACH ROW EXECUTE FUNCTION "Finance_ProtectDocument"();
                CREATE TRIGGER "TR_CashTransfers_Immutable" BEFORE UPDATE OR DELETE ON "CashTransfers" FOR EACH ROW EXECUTE FUNCTION "Finance_ProtectDocument"();
                CREATE TRIGGER "TR_CashReconciliations_Immutable" BEFORE UPDATE OR DELETE ON "CashReconciliations" FOR EACH ROW EXECUTE FUNCTION "Finance_ProtectDocument"();
                CREATE FUNCTION "Finance_CheckPeriod"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                PERFORM pg_advisory_xact_lock(21470125);
                IF TG_OP='DELETE' THEN RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE='23514'; END IF;
                IF EXISTS (SELECT 1 FROM "FinancialPeriods" p WHERE p."Id"<>NEW."Id" AND p."Start"<=NEW."End" AND p."End">=NEW."Start") THEN
                RAISE EXCEPTION 'FinancialPeriodOverlap' USING ERRCODE='23514'; END IF;
                IF TG_OP='UPDATE' AND ((to_jsonb(NEW)-ARRAY['IsClosed','Version','UpdatedAt','UpdatedBy']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['IsClosed','Version','UpdatedAt','UpdatedBy']) OR NEW."Version"<>OLD."Version"+1 OR NEW."IsClosed"=OLD."IsClosed") THEN
                RAISE EXCEPTION 'FinancialPeriodStale' USING ERRCODE='23514'; END IF;
                IF NEW."IsClosed" AND NEW."End">CURRENT_DATE THEN RAISE EXCEPTION 'FinancialPeriodFutureClose' USING ERRCODE='23514'; END IF;
                IF TG_OP='INSERT' AND (NEW."Version"<>1 OR NEW."IsClosed") THEN RAISE EXCEPTION 'FinancialPeriodInvalid' USING ERRCODE='23514'; END IF;
                RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_FinancialPeriods_Control" BEFORE INSERT OR UPDATE OR DELETE ON "FinancialPeriods" FOR EACH ROW EXECUTE FUNCTION "Finance_CheckPeriod"();
                CREATE FUNCTION "Finance_RequirePeriodAudit"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                IF NOT EXISTS (SELECT 1 FROM "FinancialPeriodEvents" e WHERE e."PeriodId"=NEW."Id" AND e."Version"=NEW."Version" AND e."IsClosed"=NEW."IsClosed" AND length(trim(e."Reason"))>0) THEN
                RAISE EXCEPTION 'FinancialPeriodAuditRequired' USING ERRCODE='23514'; END IF;
                RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_FinancialPeriods_Audit" AFTER INSERT OR UPDATE ON "FinancialPeriods" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Finance_RequirePeriodAudit"();
                CREATE FUNCTION "Finance_ValidatePeriodEvent"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                IF NOT EXISTS (SELECT 1 FROM "FinancialPeriods" p WHERE p."Id"=NEW."PeriodId" AND p."Version">=NEW."Version") OR length(trim(NEW."Reason"))=0 OR length(trim(NEW."ActorName"))=0 THEN
                RAISE EXCEPTION 'FinancialPeriodAuditRequired' USING ERRCODE='23514'; END IF;
                RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_FinancialPeriodEvents_Valid" AFTER INSERT ON "FinancialPeriodEvents" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Finance_ValidatePeriodEvent"();
                CREATE FUNCTION "Finance_RequireOpenPeriod"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                PERFORM pg_advisory_xact_lock(21470125);
                IF NEW."EntryDate" = DATE '0001-01-01' OR NEW."EntryDate">CURRENT_DATE THEN RAISE EXCEPTION 'AccountingPostingDateInvalid' USING ERRCODE='23514'; END IF;
                IF EXISTS (SELECT 1 FROM "FinancialPeriods") AND NOT EXISTS (SELECT 1 FROM "FinancialPeriods" WHERE "Start"<=NEW."EntryDate" AND "End">=NEW."EntryDate" AND NOT "IsClosed") THEN
                RAISE EXCEPTION 'FinancialPeriodNotOpen' USING ERRCODE='23514'; END IF;
                RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_LedgerEntries_OpenPeriod" BEFORE INSERT OR UPDATE ON "LedgerEntries" FOR EACH ROW EXECUTE FUNCTION "Finance_RequireOpenPeriod"();
                CREATE FUNCTION "Finance_ValidateTransfer"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE d "CashTransfers"%ROWTYPE; h "LedgerEntries"%ROWTYPE; n integer; prior numeric; target uuid; source uuid;
                BEGIN
                IF TG_TABLE_NAME='CashTransfers' THEN
                SELECT * INTO d FROM "CashTransfers" WHERE "Id"=NEW."Id";
                SELECT * INTO h FROM "LedgerEntries" WHERE "ReferenceType"=7 AND "ReferenceId"=d."Id";
                ELSE
                SELECT * INTO h FROM "LedgerEntries" WHERE "Id"=NEW."Id";
                IF h."Kind"<>9 THEN RETURN NULL; END IF;
                SELECT * INTO d FROM "CashTransfers" WHERE "Id"=h."ReferenceId";
                END IF;
                IF d."Id" IS NULL OR h."Id" IS NULL OR h."PostedAt" IS NULL OR h."Kind"<>9 OR h."ReferenceType"<>7 OR h."OperationId"<>d."OperationId" OR h."EntryDate"<>d."Date" OR h."ActorId"<>d."ActorId" OR h."Description"<>d."Reason" THEN
                RAISE EXCEPTION 'CashTransferInvalid' USING ERRCODE='23514'; END IF;
                source:=('a1100000-0000-0000-0000-' || lpad(d."FromAccount"::text,12,'0'))::uuid;
                target:=('a1100000-0000-0000-0000-' || lpad(d."ToAccount"::text,12,'0'))::uuid;
                SELECT count(*) INTO n FROM "AccountTransactions" WHERE "LedgerEntryId"=h."Id";
                IF n<>2 OR NOT EXISTS(SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId"=h."Id" AND "AccountId"=source AND "Credit"=d."Amount" AND "Debit"=0 AND "CustomerId" IS NULL AND "DebtItemId" IS NULL) OR
                NOT EXISTS(SELECT 1 FROM "AccountTransactions" WHERE "LedgerEntryId"=h."Id" AND "AccountId"=target AND "Debit"=d."Amount" AND "Credit"=0 AND "CustomerId" IS NULL AND "DebtItemId" IS NULL) THEN
                RAISE EXCEPTION 'CashTransferInvalid' USING ERRCODE='23514'; END IF;
                SELECT coalesce(sum(t."Debit"-t."Credit"),0) INTO prior FROM "AccountTransactions" t JOIN "LedgerEntries" e ON e."Id"=t."LedgerEntryId" WHERE t."AccountId"=source AND e."PostedAt" IS NOT NULL AND e."Id"<>h."Id" AND e."EntryDate"<=d."Date";
                IF prior<d."Amount" THEN RAISE EXCEPTION 'CashInsufficientBalance' USING ERRCODE='23514'; END IF;
                RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_CashTransfers_Posted" AFTER INSERT ON "CashTransfers" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Finance_ValidateTransfer"();
                CREATE CONSTRAINT TRIGGER "TR_LedgerEntries_TransferSource" AFTER INSERT OR UPDATE ON "LedgerEntries" DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Finance_ValidateTransfer"();
                CREATE FUNCTION "Finance_ValidateReconciliation"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE amount numeric; target uuid;
                BEGIN
                PERFORM pg_advisory_xact_lock(21470125);
                target:=('a1100000-0000-0000-0000-' || lpad(NEW."Account"::text,12,'0'))::uuid;
                SELECT coalesce(sum(t."Debit"-t."Credit"),0) INTO amount FROM "AccountTransactions" t JOIN "LedgerEntries" e ON e."Id"=t."LedgerEntryId" WHERE t."AccountId"=target AND e."PostedAt" IS NOT NULL AND e."EntryDate"<=NEW."Date";
                IF amount<>NEW."BookBalance" OR length(NEW."PositionHash")<>64 OR length(trim(NEW."Evidence"))=0 OR length(trim(NEW."Reason"))=0 OR NEW."Date"=DATE '0001-01-01' OR NEW."Date">CURRENT_DATE THEN
                RAISE EXCEPTION 'CashReconciliationInvalid' USING ERRCODE='23514'; END IF;
                RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_CashReconciliations_Validate" BEFORE INSERT ON "CashReconciliations" FOR EACH ROW EXECUTE FUNCTION "Finance_ValidateReconciliation"();
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                IF EXISTS(SELECT 1 FROM "FinancialPeriods") OR EXISTS(SELECT 1 FROM "CashTransfers") OR EXISTS(SELECT 1 FROM "CashReconciliations") OR EXISTS(SELECT 1 FROM "Payments" WHERE "CashAccount"<>5) OR EXISTS(SELECT 1 FROM "AccountTransactions" WHERE "AccountId" IN ('a1100000-0000-0000-0000-000000000013','a1100000-0000-0000-0000-000000000014','a1100000-0000-0000-0000-000000000015')) THEN
                RAISE EXCEPTION 'Cannot downgrade after financial controls or separate cash accounts have been used.'; END IF;
                END $$;
                DROP TRIGGER "TR_LedgerEntries_TransferSource" ON "LedgerEntries";
                DROP TRIGGER "TR_LedgerEntries_OpenPeriod" ON "LedgerEntries";
                DROP TABLE "CashReconciliations", "CashTransfers", "FinancialPeriodEvents", "FinancialPeriods";
                DROP FUNCTION "Finance_ValidatePeriodEvent"();
                DROP FUNCTION "Finance_ValidateReconciliation"(), "Finance_ValidateTransfer"(), "Finance_RequireOpenPeriod"(), "Finance_RequirePeriodAudit"(), "Finance_CheckPeriod"(), "Finance_ProtectDocument"();
                ALTER TABLE "Payments" DROP COLUMN "CashAccount";
                ALTER TABLE "Accounts" DISABLE TRIGGER "TR_Accounts_Fixed";
                DELETE FROM "Accounts" WHERE "Type" IN (13,14,15);
                ALTER TABLE "Accounts" ENABLE TRIGGER "TR_Accounts_Fixed";
                ALTER TABLE "Accounts" DROP CONSTRAINT "CK_Accounts_Type", ADD CONSTRAINT "CK_Accounts_Type" CHECK ("Type" BETWEEN 1 AND 12);
                ALTER TABLE "LedgerEntries" DROP CONSTRAINT "CK_LedgerEntries_Kind", ADD CONSTRAINT "CK_LedgerEntries_Kind" CHECK ("Kind" BETWEEN 1 AND 8 AND "ReferenceType" BETWEEN 1 AND 6);
                ALTER TABLE "AccountOpenings" DROP CONSTRAINT "CK_AccountOpenings_Valid", ADD CONSTRAINT "CK_AccountOpenings_Valid" CHECK ("Account" IN (5,9) AND "Date"<>DATE '0001-01-01' AND "VerifiedBalance">=0 AND "VerifiedBalance"<>"BeforeBalance" AND length("PositionHash")=64);
                CREATE OR REPLACE FUNCTION "Accounting_ValidateOpeningEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                """);
        }
    }
}
