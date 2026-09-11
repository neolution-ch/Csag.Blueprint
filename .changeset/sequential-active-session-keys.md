---
"@neolution-ch/csag-blueprint-infrastructure": patch
---

Let `BlueprintActiveSessions` use its sequential primary-key default

`BlueprintActiveSessionConfiguration` has always given `Id` a `NEWSEQUENTIALID()` store default, but
`SessionManager.TrackSessionAsync` assigned a random `Guid` before every insert, so the default never
applied and the clustered key fragmented on this insert-heavy table. The key is now left to the database.
No migration is required — the column default is unchanged, and existing rows keep their keys.
