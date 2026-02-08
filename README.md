# LGYM .NET Backend

This backend replaces the Node/Mongo implementation with .NET 10, EF Core, and PostgreSQL.
All API routes and payloads remain 1:1 compatible with the existing frontend.

## Requirements

- .NET SDK 10.x
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

## Plan implementacji Unit of Work

1. **Definicja kontraktu**  
   - W projekcie `LgymApi.Application` dodaj interfejs `IUnitOfWork` z metodami `Task<int> SaveChangesAsync(CancellationToken)` oraz `IDbContextTransaction BeginTransactionAsync(...)` lub prostym wrapperem `Task<IDisposable> BeginTransactionAsync(...)` (w zależności od aktualnego wzorca w EF Core).  
   - Ekspozycja `DbContext` nie jest potrzebna – UoW ma zarządzać wyłącznie zapisem.

2. **Implementacja**  
   - W projekcie `LgymApi.Infrastructure` utwórz klasę `UnitOfWork` korzystającą z istniejącego `LgymDbContext`.  
   - Zaimplementuj metody `SaveChangesAsync` oraz obsługę transakcji (opcjonalnie `CommitAsync`/`RollbackAsync` jeżeli decydujemy się na jawne transakcje).

3. **Rejestracja w DI**  
   - W miejscu konfiguracji usług (prawdopodobnie `LgymApi.Api/Program.cs`) zarejestruj `IUnitOfWork` jako `Scoped`.  
   - Repozytoria pozostają `Scoped` i współdzielą ten sam `DbContext`.

4. **Użycie w serwisach/aplikacji**  
   - W warstwie `Services` wstrzykuj `IUnitOfWork` tam, gdzie wykonywane są operacje na wielu repozytoriach w ramach jednej operacji biznesowej (np. zapisy treningów, planów, pomiarów).  
   - Używaj `SaveChangesAsync` zamiast wywołań `DbContext.SaveChangesAsync` (jeśli występują) lub zamiast rozproszonych zapisów w repozytoriach.

5. **Migracja istniejącego kodu**  
   - W repozytoriach usuń samodzielne wywołania `SaveChangesAsync`; odpowiedzialność za commit przenieś do warstwy aplikacji/serwisów.  
   - W miejscach wymagających transakcji (np. tworzenie planu wraz z dniami i ćwiczeniami) otwórz transakcję UoW i zatwierdź po poprawnym zakończeniu.

6. **Testy**  
   - Dodaj testy integracyjne w stylu obecnych testów (jeśli istnieją) sprawdzające, że wiele zmian w ramach jednej transakcji zapisuje się razem i że rollback działa (np. rzucenie wyjątku po zapisaniu pierwszej encji nie zapisuje drugiej).
