# Csag.Blueprint.Testing

## Overview

This package provides **shared test infrastructure** for CSAG Blueprint-based applications: Testcontainer orchestrators for SQL Server and WireMock, a base class for integration tests with per-test database reset, and unit test helpers.

## Integration Testing

### `MsSqlTestContainerOrchestrator`

Manages the full lifecycle of a SQL Server Testcontainer (default image: `mcr.microsoft.com/mssql/server:2022-latest`). After seeding, it creates a database snapshot that is restored before each test for fast, isolated runs.

Typical usage in a fixture:

```csharp
// PreSetupAsync
await sqlOrchestrator.StartAsync();

// After migrations and seeding
await sqlOrchestrator.CreateSnapshotAsync(connectionString);

// Before each test (called by IntegrationTestBase)
await sqlOrchestrator.ResetDatabaseAsync();

// TearDownAsync
await sqlOrchestrator.DropSnapshotAsync();
await sqlOrchestrator.DisposeAsync();
```

### `WireMockTestContainerOrchestrator`

Manages a WireMock Testcontainer for stubbing external HTTP dependencies. Provides `GetPublicUri()` to override service base URIs in configuration and `CreateFreshAdminClientAsync()` to reset all stubs at the start of each test.

### `IntegrationTestBase<TFixture>`

Abstract base class for integration tests. Calls `TFixture.ResetDatabaseAsync()` in `SetupAsync()` so every test starts from the post-seeding snapshot. The fixture must implement `IResettableFixture`.

### `IResettableFixture`

Interface marking a test fixture as supporting per-test database reset. Implementations should delegate to `MsSqlTestContainerOrchestrator.ResetDatabaseAsync()`.

## Unit Testing

### `AutoDomainDataAttribute`

An `[AutoData]` attribute configured for domain entities. Replaces AutoFixture's default `ThrowingRecursionBehavior` with `OmitOnRecursionBehavior` so entity graphs with circular navigation properties can be auto-generated.

### `TestDbContextScope<TDbContext>`

Disposable wrapper around a `DbContext` instance that ensures proper cleanup when disposed. Useful for scoping a test-local context.

## Assertion Extensions

### `HttpResponseMessageExtensions`

Shouldly-style assertion for HTTP responses:

```csharp
await response.ShouldHaveStatusCodeAsync(HttpStatusCode.OK);
```

On failure, the assertion message includes the full response body for easier debugging.
