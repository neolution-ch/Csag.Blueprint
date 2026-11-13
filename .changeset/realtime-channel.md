---
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-testing": minor
"@neolution-ch/csag-blueprint-realtime-azure-signalr": minor
---

Add the real-time channel: tenant-scoped signals from the API to the browser, with Azure SignalR Service as the first provider

- **Csag.Blueprint.Application** (`Csag.Blueprint.Application.Abstractions.Realtime`): the transport-neutral contracts. `IRealtimeNotifier` publishes to a tenant, one user or several users of a tenant, and has no way to reach every tenant. `IRealtimeTicketIssuer` hands a browser a `RealtimeTicket` (client URL, access token, expiry). `RealtimeEvent` is the envelope: event name, entity id, and the time from an injected `TimeProvider`, nothing else. `NoOpRealtimeService` implements both interfaces for a host without real-time.
- **Csag.Blueprint.Realtime.AzureSignalR** (new package): `AddBlueprintAzureSignalRRealtime(options => ...)` registers both interfaces on Azure SignalR Service in serverless mode. The API holds no connection: it grants group membership, mints client tokens and publishes over the REST API while it serves a request, so it can scale to zero. `BlueprintAzureSignalROptions` (connection string, hub name, ticket lifetime, group membership TTL) is validated at startup, including the connection string through the SDK's own parser.
- **Csag.Blueprint.Testing** (`Csag.Blueprint.Testing.Realtime`): `RecordingRealtimeNotifier` keeps every notification in memory, so an integration test can assert what an endpoint published.
