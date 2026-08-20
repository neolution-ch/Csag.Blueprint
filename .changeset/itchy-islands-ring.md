---
"@neolution-ch/csag-blueprint-domain": patch
"@neolution-ch/csag-blueprint-infrastructure": patch
"@neolution-ch/csag-blueprint-web": patch
---

Add a TenantId column to audit log entries

Consuming applications need their own EF Core migration to add the nullable TenantId column.
