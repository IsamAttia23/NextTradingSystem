using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NEXT.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoreCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE SCHEMA IF NOT EXISTS platform;
                CREATE TABLE platform."Stores" (
                  "Id" uuid PRIMARY KEY, "Name" varchar(200) NOT NULL, "Slug" varchar(40) NOT NULL UNIQUE,
                  "DatabaseName" varchar(64) NOT NULL UNIQUE, "State" integer NOT NULL,
                  "OwnerName" varchar(200) NOT NULL, "OwnerUserName" varchar(100) NOT NULL, "OwnerEmail" varchar(256) NOT NULL,
                  "RequestHash" varchar(64) NOT NULL, "CreatedBy" uuid NOT NULL REFERENCES security."Users"("Id") ON DELETE RESTRICT,
                  "CreatedAt" timestamptz NOT NULL, "Version" integer NOT NULL DEFAULT 1,
                  CONSTRAINT "CK_Store_Registration" CHECK (
                    "State" IN (1,2,3,4) AND "Version">0 AND length(btrim("Name"))>0 AND length(btrim("OwnerName"))>0
                    AND length(btrim("OwnerUserName"))>0 AND length(btrim("OwnerEmail"))>0 AND length("RequestHash")=64
                    AND "Slug" ~ '^[a-z0-9][a-z0-9-]{1,39}$' AND "DatabaseName"='nts_trial_'||replace("Id"::text,'-',''))
                );
                CREATE FUNCTION platform."ProtectStoreRegistration"() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                  IF TG_OP='DELETE' THEN RAISE EXCEPTION 'StoreRegistrationRetained' USING ERRCODE='23514'; END IF;
                  IF (to_jsonb(NEW)-ARRAY['State','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['State','Version'])
                    OR NEW."Version"<>OLD."Version"+1 OR NOT (
                      (OLD."State"=1 AND NEW."State" IN (2,3)) OR (OLD."State"=3 AND NEW."State"=1)
                      OR (OLD."State"=2 AND NEW."State"=4) OR (OLD."State"=4 AND NEW."State"=2))
                  THEN RAISE EXCEPTION 'StoreChanged' USING ERRCODE='23514'; END IF;
                  RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Stores_Registration" BEFORE UPDATE OR DELETE ON platform."Stores" FOR EACH ROW EXECUTE FUNCTION platform."ProtectStoreRegistration"();
                """);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN IF EXISTS(SELECT 1 FROM platform."Stores") THEN RAISE EXCEPTION 'Cannot downgrade while registered stores exist.'; END IF; END $$;
                DROP TABLE platform."Stores";
                DROP FUNCTION platform."ProtectStoreRegistration"();
                """);

        }
    }
}
