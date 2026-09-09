---
"@neolution-ch/csag-blueprint-web": minor
---

Add rate-limiting primitives for hosts wiring up `AddRateLimiter`:

- `RateLimiterExtensions.TryGetClientPartitionKey` resolves the key identifying a caller, preferring an
  edge-stamped header and falling back to `Connection.RemoteIpAddress`. It reports failure rather than
  inventing a key, so a host can route an unidentifiable caller to `RateLimitPartition.GetNoLimiter`
  instead of collapsing every such caller into one shared bucket.
- `RateLimiterExtensions.UseBlueprintRejectionResponse` shapes the rejection: `429 Too Many Requests`
  (the framework default is `503 Service Unavailable`, which an upstream load balancer reads as a
  backend fault), an RFC 9457 ProblemDetails body carrying the correlation ID, and a `Retry-After`
  header when the limiter reports one.

The `UseBlueprintSecurityHeaders` forwarded-headers comment is corrected: `RemoteIpAddress` is the
nearest hop, not the caller. Behind an edge that appends its own `X-Forwarded-For` entry — a Google
external load balancer sends `<client>,<balancer>` — the rightmost entry that `ForwardLimit 1` reads is
the balancer, and behind a further reverse proxy it is that proxy's egress address, one constant shared
by every caller. Hosts partitioning a rate limiter, or recording a caller's address, must resolve the
client from an edge-stamped header. Behavior is unchanged.
