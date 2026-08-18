---
"@neolution-ch/csag-blueprint-domain": minor
"@neolution-ch/csag-blueprint-application": minor
"@neolution-ch/csag-blueprint-infrastructure": minor
"@neolution-ch/csag-blueprint-web": minor
"@neolution-ch/csag-blueprint-testing": minor
"@neolution-ch/csag-blueprint-source-generators": minor
---

Add the email address and the display name of the user to audit events

Audit events now contain `UserEmail` and `UserDisplayName` with `UserId`. The package reads these
values from the claims on the request when it writes the event. One helper reads the claims for the
Entity Framework path and for the HTTP path. Therefore an application can show the name of the user
without a query on the user table.

A service account has no email address. Therefore its email value is null, and its display name is
the account name from the token.

The package writes the two new values to the `JsonData` column, at `$.UserEmail` and
`$.UserDisplayName`. This release does not change the schema. An application does not need a
migration.
