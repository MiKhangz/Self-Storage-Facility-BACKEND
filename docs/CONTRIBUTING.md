# Contributing to StorEase

## Branches and commits

- Create a focused branch from the current integration branch.
- Keep each commit limited to one logical change.
- Use an imperative commit subject with a scope when useful, for example:
  `test: cover overdue renewal decision`.
- Do not commit local configuration, credentials, build output, or database files.

## Change checklist

Before opening a pull request:

1. Review the diff for unrelated changes.
2. Run `powershell -File .\scripts\verify.ps1` from the repository root.
3. Explain behavior changes, migrations, and configuration requirements.
4. Include or update tests for changed business rules.

## Backend conventions

- Keep business rules in `StorEase.Application` services.
- Keep persistence configuration and infrastructure services in `StorEase.Infrastructure`.
- Keep HTTP concerns in `StorEase.Api` controllers and middleware.
- Store secrets in local development configuration or user-secrets, never in committed files.
