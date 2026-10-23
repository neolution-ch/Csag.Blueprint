---
"@neolution-ch/csag-blueprint-web": minor
---

Stop the culture fallback from swapping one region for another

`CultureNormalizationHelper` previously fell back on the two-letter language for **any** request,
so `fr-FR` resolved to a supported `fr-CH`. The language fallback now narrows but never swaps
regions: it still applies when either side is a bare language code — `de` resolves to a supported
`de-CH`, and `en-GB` resolves to a supported bare `en` — but two region-qualified tags must match
exactly.

This is a behaviour change: requests for a regional variant you do not support now fall through to
the default language instead of silently being served a different region's content.
