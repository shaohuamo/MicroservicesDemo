---
name: ef-migration
description: Create, inspect, and apply Entity Framework Core migrations in this .NET repository using dotnet ef; use when database schema changes are requested or a migration is missing or not being applied.
metadata:
  short-description: Manage EF Core migrations safely
---

# EF Core migration workflow

Use this skill for schema changes involving `ApplicationDbContext` or another EF Core `DbContext`.

## Mandatory rule

Never hand-write a migration class or its `.Designer.cs` file. Generate migrations with `dotnet ef migrations add`. The generated migration, generated designer metadata, and model snapshot must be kept together.

For this repository's Products database, use:

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/backend/Services/Products/ProductsMicroservice.Infrastructure/ProductsMicroservice.Infrastructure.csproj `
  --startup-project src/backend/Services/Products/ProductsMicroService.API/ProductsMicroService.API.csproj `
  --context ApplicationDbContext `
  --output-dir Migrations
```

## Workflow

1. Inspect the current `DbContext`, entity configuration, existing migrations, and model snapshot before generating anything.
2. Make the model/configuration change first.
3. Run the `dotnet ef migrations add` command above with a descriptive migration name.
4. Verify that EF generated all expected artifacts: the migration `.cs`, its `.Designer.cs`, and an updated `ApplicationDbContextModelSnapshot.cs`.
5. Inspect the generated `Up` and `Down` methods. Do not replace generated metadata with a manually authored migration.
6. Build the startup project and apply pending migrations through the application's normal `Database.MigrateAsync()` startup path or the repository's migration job.
7. Verify `__EFMigrationsHistory` and the changed database schema. A migration file existing on disk does not prove that it was applied.

## Existing or failed migrations

- If a hand-written migration has not been applied, remove it and regenerate it with `dotnet ef migrations add`.
- If a migration has already been applied in any environment, do not rewrite or delete it. Create a new corrective migration.
- If the migration is not detected, check that the generated class has EF metadata, the migration assembly is included in the deployed build, the correct `DbContext` and database are being used, and the process was restarted after rebuilding.
- Do not use `EnsureCreated` for a database managed by EF migrations.

## Validation

Use PowerShell commands only. For Products, a migration can be listed with:

```powershell
dotnet ef migrations list `
  --project src/backend/Services/Products/ProductsMicroservice.Infrastructure/ProductsMicroservice.Infrastructure.csproj `
  --startup-project src/backend/Services/Products/ProductsMicroService.API/ProductsMicroService.API.csproj `
  --context ApplicationDbContext
```

Then run the relevant build/tests and verify the target database contains the expected columns or constraints and that `__EFMigrationsHistory` contains the migration id.
