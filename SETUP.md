# LGYM APP - Konfiguracja deweloperska

## Przygotowanie œrodowiska

### 1. Pliki konfiguracyjne

Po sklonowaniu repozytorium, utwórz nastêpuj¹ce pliki z w³asnymi danymi:

#### `LgymApi.Api/appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=LGYM-APP;Username=postgres;Password=TWOJE_HASLO;TimeZone=Europe/Warsaw"
  },
  "Jwt": {
    "Secret": "TWOJ_JWT_SECRET_MIN_64_ZNAKI"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

#### `LgymApi.Migrator/appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=LGYM-APP;Username=postgres;Password=TWOJE_HASLO;TimeZone=Europe/Warsaw"
  },
  "Mongo": {
    "ConnectionString": "TWOJ_MONGODB_CONNECTION_STRING",
    "Database": "test"
  },
  "Migrator": {
    "BatchSize": "1000"
  }
}
```

### 2. Wymagania

- .NET 8 SDK
- PostgreSQL (domyœlnie port 5433)
- MongoDB (dla migratora)

### 3. Uruchomienie

```bash
# API
cd LgymApi.Api
dotnet run

# Migrator
cd LgymApi.Migrator
dotnet run
```

## Uwagi bezpieczeñstwa

?? **NIGDY nie commituj plików `appsettings.Development.json` ani innych plików z has³ami!**

Te pliki s¹ ignorowane przez `.gitignore` i powinny pozostaæ tylko lokalnie.
