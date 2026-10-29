---
"@neolution-ch/csag-blueprint-domain": patch
"@neolution-ch/csag-blueprint-application": patch
"@neolution-ch/csag-blueprint-infrastructure": patch
"@neolution-ch/csag-blueprint-web": patch
"@neolution-ch/csag-blueprint-testing": patch
"@neolution-ch/csag-blueprint-source-generators": patch
---

Raise vulnerable dependencies to patched versions

The ASP.NET Core and EF Core packages move to the 10.0.13 servicing release. It brings
`System.Security.Cryptography.Xml` 10.0.13 (GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj,
GHSA-r955-8q5j-3h83) and `System.Security.Cryptography.Pkcs` 10.0.13 (GHSA-846f-f764-76mx,
GHSA-2fmv-rv2c-59x7).

`Csag.Blueprint.Infrastructure`, `Csag.Blueprint.Web` and `Csag.Blueprint.Testing` now also depend
on `System.Security.Cryptography.Pkcs` 10.0.13. The package that pulls it in,
`Microsoft.Data.SqlClient`, still asks for a vulnerable version. Upgrading the blueprint packages is
enough to get the patched version, without a pin of your own.

**When upgrading:**

- Your own `Microsoft.AspNetCore.*` and `Microsoft.EntityFrameworkCore.*` references must be 10.0.13
  or later, or restore fails with a package downgrade error (NU1605, or NU1109 under central package
  management).
- Run your applications on the .NET 10.0.13 runtime or later. Microsoft fixes these advisories in
  the runtime as well as in the packages, and the Xml advisory also covers `System.Private.Xml`,
  which only ships with the runtime. Update the installed runtime or the container base image, and
  rebuild and redeploy self-contained applications.
