using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Purpose",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ReturnInvoiceId",
                table: "Payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    NormalSide = table.Column<int>(type: "integer", nullable: false),
                    IsTechnical = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                    table.CheckConstraint("CK_Accounts_NormalSide", "\"NormalSide\" IN (0, 1)");
                    table.CheckConstraint("CK_Accounts_Type", "\"Type\" BETWEEN 1 AND 7");
                });

            migrationBuilder.CreateTable(
                name: "CustomerDebtAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastActivityAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerDebtAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomerDebtAccounts_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LedgerEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostingKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ReferenceType = table.Column<int>(type: "integer", nullable: false),
                    ReferenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReversesEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerEntries", x => x.Id);
                    table.CheckConstraint("CK_LedgerEntries_Identifiers", "\"OperationId\" <> '00000000-0000-0000-0000-000000000000' AND \"ReferenceId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_LedgerEntries_Kind", "\"Kind\" BETWEEN 1 AND 6 AND \"ReferenceType\" BETWEEN 1 AND 4");
                    table.CheckConstraint("CK_LedgerEntries_PostingKey", "\"PostingKey\" = \"Kind\"::text || ':' || \"ReferenceType\"::text || ':' || replace(\"ReferenceId\"::text, '-', '')");
                    table.CheckConstraint("CK_LedgerEntries_Reversal", "(\"Kind\" = 6) = (\"ReversesEntryId\" IS NOT NULL) AND (\"ReversesEntryId\" IS NULL OR (\"ReferenceType\" = 4 AND \"ReferenceId\" = \"ReversesEntryId\"))");
                    table.CheckConstraint("CK_LedgerEntries_Source", "(\"ReferenceType\" <> 1 OR \"InvoiceId\" IS NOT DISTINCT FROM \"ReferenceId\") AND (\"ReferenceType\" <> 2 OR \"PaymentId\" IS NOT DISTINCT FROM \"ReferenceId\")");
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_LedgerEntries_ReversesEntryId",
                        column: x => x.ReversesEntryId,
                        principalTable: "LedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LedgerEntries_Users_ActorId",
                        column: x => x.ActorId,
                        principalSchema: "security",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DebtItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DebtAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DebtItems", x => x.Id);
                    table.CheckConstraint("CK_DebtItems_Amount", "\"OriginalAmount\" > 0");
                    table.CheckConstraint("CK_DebtItems_Source", "(\"Kind\" = 1 AND \"InvoiceId\" IS NOT NULL) OR (\"Kind\" = 2 AND \"InvoiceId\" IS NULL)");
                    table.ForeignKey(
                        name: "FK_DebtItems_CustomerDebtAccounts_DebtAccountId",
                        column: x => x.DebtAccountId,
                        principalTable: "CustomerDebtAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DebtItems_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LedgerEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<short>(type: "smallint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: true),
                    DebtItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Debit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(15,2)", precision: 15, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTransactions", x => x.Id);
                    table.CheckConstraint("CK_AccountTransactions_Amounts", "\"Debit\" >= 0 AND \"Credit\" >= 0 AND ((\"Debit\" > 0 AND \"Credit\" = 0) OR (\"Credit\" > 0 AND \"Debit\" = 0))");
                    table.CheckConstraint("CK_AccountTransactions_Debt", "(\"AccountId\" = 'a1100000-0000-0000-0000-000000000004' AND \"DebtItemId\" IS NOT NULL AND \"CustomerId\" IS NOT NULL) OR (\"AccountId\" <> 'a1100000-0000-0000-0000-000000000004' AND \"DebtItemId\" IS NULL)");
                    table.CheckConstraint("CK_AccountTransactions_Sequence", "\"Sequence\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_DebtItems_DebtItemId",
                        column: x => x.DebtItemId,
                        principalTable: "DebtItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_LedgerEntries_LedgerEntryId",
                        column: x => x.LedgerEntryId,
                        principalTable: "LedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Accounts",
                columns: new[] { "Id", "Code", "IsTechnical", "Name", "NormalSide", "Type" },
                values: new object[,]
                {
                    { new Guid("a1100000-0000-0000-0000-000000000001"), "SALES", false, "المبيعات", 1, 1 },
                    { new Guid("a1100000-0000-0000-0000-000000000002"), "PURCHASES", false, "المشتريات", 0, 2 },
                    { new Guid("a1100000-0000-0000-0000-000000000003"), "EXPENSES", false, "المنصرفات", 0, 3 },
                    { new Guid("a1100000-0000-0000-0000-000000000004"), "RECEIVABLES", false, "الديون", 0, 4 },
                    { new Guid("a1100000-0000-0000-0000-000000000005"), "TREASURY", false, "الخزينة", 0, 5 },
                    { new Guid("a1100000-0000-0000-0000-000000000006"), "CUSTOMER-ADVANCES", true, "مقدمات العملاء والمستحق للرد", 1, 6 },
                    { new Guid("a1100000-0000-0000-0000-000000000007"), "OPENING-BALANCES", true, "مقابل الأرصدة الافتتاحية", 1, 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReturnInvoiceId",
                table: "Payments",
                column: "ReturnInvoiceId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Purpose",
                table: "Payments",
                sql: "\"Purpose\" BETWEEN 0 AND 3");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Code",
                table: "Accounts",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Type",
                table: "Accounts",
                column: "Type",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_AccountId_LedgerEntryId",
                table: "AccountTransactions",
                columns: new[] { "AccountId", "LedgerEntryId" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_CustomerId",
                table: "AccountTransactions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_DebtItemId",
                table: "AccountTransactions",
                column: "DebtItemId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_LedgerEntryId_Sequence",
                table: "AccountTransactions",
                columns: new[] { "LedgerEntryId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerDebtAccounts_CustomerId",
                table: "CustomerDebtAccounts",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DebtItems_DebtAccountId_EffectiveDate_Id",
                table: "DebtItems",
                columns: new[] { "DebtAccountId", "EffectiveDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_DebtItems_InvoiceId",
                table: "DebtItems",
                column: "InvoiceId",
                unique: true,
                filter: "\"InvoiceId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ActorId",
                table: "LedgerEntries",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_EntryDate_Id",
                table: "LedgerEntries",
                columns: new[] { "EntryDate", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_InvoiceId",
                table: "LedgerEntries",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_OperationId",
                table: "LedgerEntries",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_PaymentId",
                table: "LedgerEntries",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_PostingKey",
                table: "LedgerEntries",
                column: "PostingKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_ReversesEntryId",
                table: "LedgerEntries",
                column: "ReversesEntryId",
                unique: true,
                filter: "\"ReversesEntryId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Invoices_ReturnInvoiceId",
                table: "Payments",
                column: "ReturnInvoiceId",
                principalTable: "Invoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE FUNCTION "Accounting_RejectFixedAccountWrite"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'AccountingFixedAccountsImmutable' USING ERRCODE = '23514';
                END $$;
                CREATE TRIGGER "TR_Accounts_Fixed" BEFORE INSERT OR UPDATE OR DELETE ON "Accounts"
                    FOR EACH ROW EXECUTE FUNCTION "Accounting_RejectFixedAccountWrite"();

                CREATE FUNCTION "Accounting_ProtectEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514';
                    END IF;
                    IF OLD."PostedAt" IS NOT NULL OR NEW."PostedAt" IS NULL OR
                        (to_jsonb(NEW) - ARRAY['PostedAt', 'UpdatedAt', 'UpdatedBy']) IS DISTINCT FROM
                        (to_jsonb(OLD) - ARRAY['PostedAt', 'UpdatedAt', 'UpdatedBy']) THEN
                        RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_LedgerEntries_Immutable" BEFORE UPDATE OR DELETE ON "LedgerEntries"
                    FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectEntry"();

                CREATE FUNCTION "Accounting_ProtectLine"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER "TR_AccountTransactions_Immutable" BEFORE INSERT OR UPDATE OR DELETE ON "AccountTransactions"
                    FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectLine"();

                CREATE FUNCTION "Accounting_ValidateEntry"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE CONSTRAINT TRIGGER "TR_LedgerEntries_Balanced" AFTER INSERT OR UPDATE ON "LedgerEntries"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateEntry"();

                CREATE FUNCTION "Accounting_ValidateDebtBalance"() RETURNS trigger LANGUAGE plpgsql AS $$
                DECLARE balance numeric;
                BEGIN
                    IF NEW."DebtItemId" IS NOT NULL THEN
                        SELECT coalesce(sum(t."Debit" - t."Credit"), 0) INTO balance FROM "AccountTransactions" t
                            JOIN "LedgerEntries" e ON e."Id" = t."LedgerEntryId"
                            WHERE t."DebtItemId" = NEW."DebtItemId" AND e."PostedAt" IS NOT NULL;
                        IF balance < 0 OR balance > 9999999999999.99 THEN
                            RAISE EXCEPTION 'AccountingOverpayment' USING ERRCODE = '23514';
                        END IF;
                    END IF;
                    RETURN NULL;
                END $$;
                CREATE CONSTRAINT TRIGGER "TR_AccountTransactions_DebtBalance" AFTER INSERT ON "AccountTransactions"
                    DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION "Accounting_ValidateDebtBalance"();

                CREATE FUNCTION "Accounting_ProtectDebtItem"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'AccountingEntryImmutable' USING ERRCODE = '23514';
                END $$;
                CREATE TRIGGER "TR_DebtItems_Immutable" BEFORE UPDATE OR DELETE ON "DebtItems"
                    FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectDebtItem"();

                CREATE FUNCTION "Accounting_ProtectDebtAccount"() RETURNS trigger LANGUAGE plpgsql AS $$
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
                CREATE TRIGGER "TR_CustomerDebtAccounts_Owner" BEFORE UPDATE OR DELETE ON "CustomerDebtAccounts"
                    FOR EACH ROW EXECUTE FUNCTION "Accounting_ProtectDebtAccount"();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_Accounts_Fixed" ON "Accounts";
                DROP TRIGGER "TR_LedgerEntries_Immutable" ON "LedgerEntries";
                DROP TRIGGER "TR_LedgerEntries_Balanced" ON "LedgerEntries";
                DROP TRIGGER "TR_AccountTransactions_Immutable" ON "AccountTransactions";
                DROP TRIGGER "TR_AccountTransactions_DebtBalance" ON "AccountTransactions";
                DROP TRIGGER "TR_DebtItems_Immutable" ON "DebtItems";
                DROP TRIGGER "TR_CustomerDebtAccounts_Owner" ON "CustomerDebtAccounts";
                DROP FUNCTION "Accounting_RejectFixedAccountWrite"();
                DROP FUNCTION "Accounting_ProtectEntry"();
                DROP FUNCTION "Accounting_ProtectLine"();
                DROP FUNCTION "Accounting_ValidateEntry"();
                DROP FUNCTION "Accounting_ValidateDebtBalance"();
                DROP FUNCTION "Accounting_ProtectDebtItem"();
                DROP FUNCTION "Accounting_ProtectDebtAccount"();
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Invoices_ReturnInvoiceId",
                table: "Payments");

            migrationBuilder.DropTable(
                name: "AccountTransactions");

            migrationBuilder.DropTable(
                name: "Accounts");

            migrationBuilder.DropTable(
                name: "DebtItems");

            migrationBuilder.DropTable(
                name: "LedgerEntries");

            migrationBuilder.DropTable(
                name: "CustomerDebtAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReturnInvoiceId",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Purpose",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ReturnInvoiceId",
                table: "Payments");
        }
    }
}
