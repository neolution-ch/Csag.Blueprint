---
"@neolution-ch/csag-blueprint-web": minor
---

Let applications customize FastEndpoints discovery and OpenAPI document generation

`AddBlueprintServices` gains an optional `BlueprintServicesOptions` callback. `FastEndpointsAdditionalAssemblies` finds endpoints and command/event handlers outside the entry assembly. `ConfigureOpenApiDocument` sets extra OpenAPI document settings on top of the Blueprint defaults, for example registering a custom `IDocumentProcessor`.

Consumer migration: none. The callback is optional. `AddBlueprintServices()` with no argument behaves as before.
