# LGYM APP - Konfiguracja deweloperska

## Przygotowanie �rodowiska

### 1. Pliki konfiguracyjne

Po sklonowaniu repozytorium, utw�rz nast�puj�ce pliki z w�asnymi danymi:

#### `LgymApi.Api/appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=LGYM-APP;Username=postgres;Password=TWOJE_HASLO;TimeZone=Europe/Warsaw"
  },
  "Jwt": {
    "SigningKey": "TWOJ_JWT_SIGNING_KEY_MIN_64_ZNAKI"
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
- PostgreSQL (domy�lnie port 5433)
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

## Uwagi bezpiecze�stwa

?? **NIGDY nie commituj plik�w `appsettings.Development.json` ani innych plik�w z has�ami!**

Te pliki s� ignorowane przez `.gitignore` i powinny pozosta� tylko lokalnie.
