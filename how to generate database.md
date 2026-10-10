# How to generate the database

These steps create the StorEase database on your own machine from the EF Core migrations in this
repo. You never write SQL by hand, and you never create the first migration yourself — the
`InitialSchema` migration is already committed and builds all 30 tables.

---

## Before you start

You need:

- **.NET SDK 8 or later**: check with `dotnet --list-sdks`
- **SQL Server** (Express, Developer or LocalDB) running locally
- This repo cloned, **on the right branch**:
  - after PR #1 is merged: `git switch main` then `git pull`
  - before that: `git fetch` then `git switch db-initial-schema`

---

## 1. Install the EF Core tool (once per machine)

```powershell
dotnet tool install --global dotnet-ef
```

If it says the tool is already installed, that is fine. Check with `dotnet ef --version`.

## 2. Find your SQL Server instance name (once per machine)

```powershell
Get-Service | Where-Object {$_.Name -like "MSSQL*"}
```

| Service name you see | Server value to use |
|---|---|
| `MSSQL$SQLEXPRESS` | `localhost\\SQLEXPRESS` |
| `MSSQLSERVER` | `localhost` |
| none, but you have LocalDB | `(localdb)\\MSSQLLocalDB` |

Whatever follows `MSSQL$` goes after the backslash.

## 3. Create your connection string (once per machine)

Create the file **`BE/StorEase.Api/appsettings.Development.json`**:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=StorEase;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- Replace `localhost\\SQLEXPRESS` with your value from step 2.
- Keep the backslash **doubled** — JSON needs it.
- This file is gitignored. **Never commit it**: everyone's instance name is different.

## 4. Create the database

Always run EF commands from the **`BE`** folder:

```powershell
cd <your clone>\BE
dotnet ef database update -p StorEase.Infrastructure -s StorEase.Api
```

It finishes with `Done.` Running it again is safe — it only applies migrations you don't have yet.

## 5. Check it worked

```powershell
sqlcmd -S "localhost\SQLEXPRESS" -E -d StorEase -Q "SELECT COUNT(*) FROM sys.tables"
```

Expect **31** (30 tables + `__EFMigrationsHistory`). You can also open the `StorEase` database in
SSMS or Azure Data Studio.

---

## Everyday commands

All from `BE/`:

| What | Command |
|---|---|
| Get teammates' new migrations | `git pull` then `dotnet ef database update -p StorEase.Infrastructure -s StorEase.Api` |
| Add a migration after changing an entity or configuration | `dotnet ef migrations add <Name> -p StorEase.Infrastructure -s StorEase.Api` |
| Undo your last migration (**only if not yet pushed**) | `dotnet ef migrations remove -p StorEase.Infrastructure -s StorEase.Api` |
| Wipe your local database and rebuild it | `dotnet ef database drop -f -p StorEase.Infrastructure -s StorEase.Api` then `database update` |

### Rules for migrations

- Name it after what it does: `AddLockoutStageToOverdueCase`, not `Update1`.
- One feature per migration. Change only your own module's tables.
- **Never edit a migration file by hand**, and never edit one that is already pushed — add a new
  one instead.
- Pull and run `database update` **before** adding your migration, so it builds on the latest
  schema.
- Commit the migration files together with the entity/configuration change that produced them.

---

## Troubleshooting

| Error | Cause and fix |
|---|---|
| `Unable to retrieve project metadata. Ensure it's an SDK-style project.` | You are in the wrong folder, or a folder with no code. `cd` into `BE/` of the clone that has `StorEase.sln`. |
| `A network-related or instance-specific error occurred...` | Wrong server name, or a single backslash in the JSON. Re-check steps 2 and 3, and that the SQL Server service is running. |
| `Login failed for user...` | Your Windows account has no access to that instance. Use SQL Server Configuration Manager / SSMS to grant it, or use LocalDB. |
| `dotnet-ef` not recognized | Step 1 not done, or open a new terminal so `PATH` picks it up. |
| `The ConnectionString property has not been initialized` | `appsettings.Development.json` missing or in the wrong folder — it must be in `BE/StorEase.Api/`. |
| `Build failed.` | Run `dotnet build` from `BE/` to see the real error and fix it first. |
| `There is already an object named '...' in the database` | Your database was created some other way. Drop it (see Everyday commands) and run `database update` again. |

Still stuck? Ask Khang.
