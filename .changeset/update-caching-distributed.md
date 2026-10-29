---
"@neolution-ch/csag-blueprint-domain": patch
"@neolution-ch/csag-blueprint-application": patch
"@neolution-ch/csag-blueprint-infrastructure": patch
"@neolution-ch/csag-blueprint-web": patch
"@neolution-ch/csag-blueprint-testing": patch
"@neolution-ch/csag-blueprint-source-generators": patch
---

Update `Neolution.Extensions.Caching.Distributed` to 3.0.0

`Csag.Blueprint.Infrastructure`, `Csag.Blueprint.Web` and `Csag.Blueprint.Testing` now depend on
`Neolution.Extensions.Caching.Distributed` 3.0.0. This version depends on
`Foundatio.MessagePack` 13.0.5. `Foundatio.MessagePack` 13.0.5 requires `MessagePack` 3.1.12, which
fixes GHSA-qhrr-8q5h-9q3h. Thus, the blueprint packages do not pin `MessagePack` now.

**When you upgrade:**

- If your central package versions set `Neolution.Extensions.Caching.Distributed`, set it to
  3.0.0 or later. If you do not, restore fails with a package downgrade error (NU1605, or
  NU1109 with central package management).
