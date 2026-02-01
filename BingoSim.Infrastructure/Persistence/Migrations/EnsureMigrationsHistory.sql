-- Run this once on an empty PostgreSQL database if "dotnet ef database update"
-- fails with: relation "__EFMigrationsHistory" does not exist
-- (Npgsql queries the history table before creating it on first run.)
-- Then run: dotnet ef database update --project BingoSim.Infrastructure --startup-project BingoSim.Web

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory__" PRIMARY KEY ("MigrationId")
);
