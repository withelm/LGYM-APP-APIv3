# LGYM .NET Backend

This backend replaces the Node/Mongo implementation with .NET 8, EF Core, and PostgreSQL.
All API routes and payloads remain 1:1 compatible with the existing frontend.

## Requirements

- .NET SDK 8.x
- PostgreSQL
- MongoDB (only needed for offline migration)

## Configuration

Update the following files or provide environment variables:

- API: `LgymApi.Api/appsettings.json`
- Migrator: `LgymApi.Migrator/appsettings.json`

Supported environment variable overrides:

- `ConnectionStrings__Postgres`
- `Mongo__ConnectionString`
- `Mongo__Database`
- `Jwt__Secret`

## Scripts

All scripts live in `scripts`.

### 1) Apply EF Core migrations

PowerShell:

```powershell
scripts\migrate-db.ps1 -ConnectionString "Host=...;Database=...;Username=...;Password=..."
```

CMD:

```cmd
scripts\migrate-db.cmd "Host=...;Database=...;Username=...;Password=..."
```

### 2) Run offline Mongo → Postgres migration

PowerShell:

```powershell
scripts\run-migrator.ps1 -MongoConnection "mongodb://..." -MongoDatabase "db" -PostgresConnection "Host=...;Database=...;Username=...;Password=..."
```

CMD:

```cmd
scripts\run-migrator.cmd "mongodb://..." "db" "Host=...;Database=...;Username=...;Password=..."
```

### 3) Run everything (migrate DB + data)

PowerShell:

```powershell
scripts\setup-all.ps1 -MongoConnection "mongodb://..." -MongoDatabase "db" -PostgresConnection "Host=...;Database=...;Username=...;Password=..."
```

CMD:

```cmd
scripts\setup-all.cmd "Host=...;Database=...;Username=...;Password=..." "mongodb://..." "db"
```

### 4) Generate SQL migration script

PowerShell:

```powershell
scripts\generate-migration-sql.ps1 -OutputPath "C:\temp\migration.sql"
scripts\generate-migration-sql.ps1 -FromMigration "InitialCreate" -ToMigration "InitialCreate" -OutputPath "C:\temp\migration.sql"
```

CMD:

```cmd
scripts\generate-migration-sql.cmd "C:\temp\migration.sql"
scripts\generate-migration-sql.cmd "C:\temp\migration.sql" InitialCreate InitialCreate
```

## Running the API

```bash
dotnet run --project LgymApi.Api
```

## Notes

- Password verification uses legacy `passport-local-mongoose` PBKDF2 settings (sha256, 25000 iterations, keylen 512, hex).
- All IDs are GUIDs in Postgres; responses return `_id` as a string GUID.
