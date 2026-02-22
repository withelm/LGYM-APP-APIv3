# LGYM APP - Development Setup

## Environment setup

### 1. Configuration files

After cloning the repository, create the following files with your local values:

#### `LgymApi.Api/appsettings.Development.json`
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5433;Database=LGYM-APP;Username=postgres;Password=YOUR_PASSWORD;TimeZone=Europe/Warsaw"
  },
  "Jwt": {
    "SigningKey": "YOUR_JWT_SIGNING_KEY_MIN_32_CHARS"
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "http://localhost:5173"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 2. Requirements

- .NET 10 SDK
- PostgreSQL (default port 5433)

### 3. Running the apps

```bash
# API
cd LgymApi.Api
dotnet run
```

## Security notes

**Never commit `appsettings.Development.json` or any file with credentials.**

These files are ignored by `.gitignore` and should remain local only.

Set `Cors:AllowedOrigins` to your real frontend origins in each environment.
