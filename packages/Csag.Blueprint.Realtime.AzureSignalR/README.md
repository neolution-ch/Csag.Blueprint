# Csag.Blueprint.Realtime.AzureSignalR

## Overview

The Azure SignalR Service provider for the CSAG Blueprint real-time channel. It implements
`IRealtimeNotifier` and `IRealtimeTicketIssuer` from `Csag.Blueprint.Application` on Azure SignalR
Service in **serverless mode**.

The API never holds a connection. It grants a user membership of the tenant group and mints a client
token when the browser asks for a ticket, and it publishes over the service's REST API while it serves
the request that changed something. The browser keeps the only long-lived connection, to Azure SignalR.
That is what lets the API scale to zero.

## Registration

```csharp
services.AddBlueprintAzureSignalRRealtime(options =>
    configuration.GetSection("RealtimeSettings").Bind(options));
```

A host without real-time registers `NoOpRealtimeService` (from `Csag.Blueprint.Application`) for both
interfaces instead.

| Option | Default | Meaning |
| --- | --- | --- |
| `ConnectionString` | none | Must carry `Endpoint` and `AccessKey`. Against the emulator, add `ClientEndpoint` when the API and the browser reach it under different hostnames. |
| `HubName` | `app` | 1-64 letters, digits or underscores. |
| `TicketTokenLifetimeMinutes` | `60` | How long a client token stays valid. Must be shorter than the membership. |
| `GroupMembershipTtlHours` | `12` | How long a granted tenant membership lasts, 1 to 8760. |

The options are validated at startup. The connection string goes through the SDK's own parser, so a
malformed value fails the start instead of the first request.

## Addressing

| Audience | Address on the service |
| --- | --- |
| Everyone in a tenant | group `t:{tenantId:N}` |
| One user in a tenant | user `{tenantId:N}:{userId:N}` |

The user address carries the tenant, so a person in two tenants receives a message only on the
connections opened while in that tenant. `RealtimeGroupNames` builds both.

## Delivery

- Messages go to the client method `Notify`, serialized in camelCase: `{ eventName, entityId, occurredAt }`.
- The transient transport sends each message as its own HTTPS request, with the SDK's retries on.
- A failed publish is logged as a warning and never thrown: the change that caused it is already committed.
