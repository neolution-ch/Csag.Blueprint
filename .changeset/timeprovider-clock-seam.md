---
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
---

Read all blueprint timestamps through an injected `TimeProvider`

Every blueprint type that stamped or compared a time read `DateTimeOffset.UtcNow` inline, so any
consumer behaviour that depends on the clock — audit stamps, session expiry, session cleanup, tenant
membership join times, table view preference timestamps — could only be tested by sleeping. The
clock is now a constructor dependency:

- `AuditableTimestampInterceptor` takes a `TimeProvider`.
- `SessionManager<TUser, TContext>` takes a `TimeProvider`. `GetUserSessionsAsync` and
  `CleanupExpiredSessionsAsync` read it once per call and compare against that instant rather than
  re-reading the clock inside the EF predicate.
- `TenantManager<TUser, TTenant, TContext>` takes a `TimeProvider`.
- `BlueprintTableViewPreferencesService<TContext, TUser>` takes a `TimeProvider`.
- The `CreatedAt` column on `BlueprintAuditLogs` is stamped from the `TimeProvider` resolved out of
  the application's services.

`AddBlueprintServices`, `AddBlueprintTenancyRuntime`, `AddBlueprintSessionInfrastructure` and
`AddBlueprintTableViewPreferences` each `TryAddSingleton(TimeProvider.System)`, so the default needs
no wiring and a `FakeTimeProvider` registered before any of them wins. The `TryAdd` matters for
consumers that use `Csag.Blueprint.Infrastructure` without the Web package — a seeding console app,
for example — which never call `AddBlueprintServices`.

**Breaking for consumers who construct the interceptor themselves.** `AuditableTimestampInterceptor`
no longer has a parameterless constructor, and `AddBlueprintTenancyRuntime` now registers it as a
singleton alongside `TenantSaveInterceptor`. Resolve it from the container when wiring the pooled
context factory:

```csharp
services.AddPooledDbContextFactory<ApplicationDbContext>((sp, options) =>
{
    var tenantInterceptor = sp.GetRequiredService<TenantSaveInterceptor>();
    var timestampInterceptor = sp.GetRequiredService<AuditableTimestampInterceptor>();
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
        .AddInterceptors(new AuditSaveChangesInterceptor(), timestampInterceptor, tenantInterceptor);
});
```

The singleton lifetime is unchanged and still safe across pooled `DbContext` instances: the
interceptor holds only the ambient `CurrentActorContext` and a singleton `TimeProvider`, neither of
which is scoped state.

No schema change, so consumers need no migration.
