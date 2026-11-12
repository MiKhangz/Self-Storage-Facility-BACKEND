# Development guide

This repository contains the StorEase backend solution under `BE/`.

## Prerequisites

- .NET 8 SDK
- SQL Server for local API runs
- A local `appsettings.Development.json` with the connection string and other secrets

Keep local secrets out of Git. The repository ignores `appsettings.Development.json` and
`.env` files.

## Build and test

Run these commands from the repository root:

```powershell
dotnet restore BE/StorEase.sln
dotnet build BE/StorEase.sln --no-restore
dotnet test BE/StorEase.sln --no-build
```

To run the API locally:

```powershell
dotnet run --project BE/StorEase.Api
```

Swagger is available at the URL printed by the application. The default launch profiles use
the Swagger route as their launch URL.

## Database setup

Use [DB_SETUP.md](DB_SETUP.md) for SQL Server and EF Core migration instructions. Do not run
database migrations against a shared environment without reviewing the generated migration
and confirming the target connection string.

## Before opening a pull request

1. Run the build and test commands above.
2. Check that no local configuration or generated build output is staged.
3. Summarize schema changes and required migration steps in the pull request.
