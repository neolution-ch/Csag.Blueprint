---
"@neolution-ch/csag-blueprint-infrastructure": patch
---

Write session profile claims for an account without an email address

`UserClaimsHelper.SetUserProfileClaims` threw for a null or empty `IUserProfileClaimsSource.Email`, although the contract declares it nullable, so building or refreshing the claims of such an account failed. It now writes the identifier, name and preference claims and leaves the email claim out, removing one a ticket carried from before.
