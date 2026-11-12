# Backend architecture

StorEase is organized as a layered .NET solution:

```text
StorEase.Api            HTTP endpoints, middleware, authentication setup
StorEase.Application    use cases, DTOs, validation, business services
StorEase.Domain         entities and domain enums
StorEase.Infrastructure  EF Core persistence, migrations, repositories, integrations
tests/                  application and API test projects
```

## Request flow

An API controller receives and validates an HTTP request, then delegates the use case to an
application service. The service applies authorization and business rules through the current
user and unit-of-work abstractions. Infrastructure provides the database implementation and
external service adapters. Controllers translate the application result into the HTTP response.

## Persistence boundaries

Entity Framework Core mappings live under `StorEase.Infrastructure/Persistence/Configurations`.
Schema changes are represented by migrations under `StorEase.Infrastructure/Migrations`. Keep
database access behind repositories or unit-of-work abstractions so application services remain
testable with the existing test database helpers.

## Feature layout

Application features are grouped by business area under `BE/StorEase.Application/Features`.
New endpoints should normally add the DTOs, validators, service logic, and controller changes
within the corresponding feature rather than placing business logic in controllers.
