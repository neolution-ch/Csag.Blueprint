---
"@neolution-ch/csag-blueprint-web": patch
---

Reject control characters in the return address of a failed OpenID Connect round trip

With `OAuth.FrontendBaseUrl` set, a failed provider round trip redirects to the challenge's `AuthenticationProperties.RedirectUri` when it is a local path. That check now uses `RedirectHttpResult.IsLocalUrl` (still requiring a leading `/`), so a return address carrying a control character such as CR or LF is treated as non-local and the user is sent to `FrontendBaseUrl` with `?error=external_auth_failed` instead.
