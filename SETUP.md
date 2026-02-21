# LGYM APP - Konfiguracja deweloperska

## Przygotowanie środowiska

### 1. Pliki konfiguracyjne

Po sklonowaniu repozytorium utwórz następujące pliki z własnymi danymi:

#### `LgymApi.Api/appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=LGYM-APP;Username=postgres;Password=TWOJE_HASLO;TimeZone=Europe/Warsaw"
  },
  "Jwt": {
    "SigningKey": "TWOJ_JWT_SIGNING_KEY_MIN_32_ZNAKI"
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
- PostgreSQL (domyślnie port 5433)
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

## Uwagi bezpieczeństwa

**NIGDY nie commituj plików `appsettings.Development.json` ani innych plików z hasłami.**

Te pliki są ignorowane przez `.gitignore` i powinny pozostać tylko lokalnie.
