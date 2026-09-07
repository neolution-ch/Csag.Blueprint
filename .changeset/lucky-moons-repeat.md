---
"@neolution-ch/csag-blueprint-web": minor
---

Restrict culture fallback to bare two-letter language codes

`CultureNormalizationHelper` previously fell back on the two-letter language for **any** request,
so `fr-FR` resolved to a supported `fr-CH`. A full IETF tag now only ever matches exactly; the
language-only fallback applies to bare codes such as `fr`.

This is a behaviour change: requests for a regional variant you do not support now fall through to
the default language instead of silently being served a different region's content.
