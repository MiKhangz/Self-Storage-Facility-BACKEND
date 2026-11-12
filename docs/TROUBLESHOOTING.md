# Troubleshooting

## Restore or build errors

Confirm that the .NET 8 SDK is installed, then run:

```powershell
dotnet --info
dotnet restore BE/StorEase.sln
dotnet build BE/StorEase.sln --no-restore
```

If generated output is stale, close running .NET processes and remove only the ignored `bin`
and `obj` folders under `BE/` before rebuilding.

## Database connection errors

Check that `BE/StorEase.Api/appsettings.Development.json` exists locally and that its connection
string targets the intended SQL Server instance. This file is ignored by Git and must not be
committed. See [DB_SETUP.md](DB_SETUP.md) for the EF Core setup and migration commands.

## Tests fail after a schema change

Run the affected test project first to narrow the failure:

```powershell
dotnet test BE/tests/StorEase.Application.Tests/StorEase.Application.Tests.csproj
```

For migration-related failures, verify that the test database provider and the production EF
mapping agree on enum conversions, required fields, indexes, and date handling.

## API is running but Swagger is unavailable

Use the launch URL printed by `dotnet run --project BE/StorEase.Api`. Check that the API process
started successfully and that the selected port is not already in use. The launch profiles use
the Swagger route as their default launch URL.
