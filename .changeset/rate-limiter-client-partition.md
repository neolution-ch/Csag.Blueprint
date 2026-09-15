---
"@neolution-ch/csag-blueprint-web": minor
---

Add rate-limiting primitives for hosts wiring up `AddRateLimiter`:

- `RateLimiterExtensions.TryGetClientPartitionKey` resolves the key identifying a caller, preferring an
  edge-stamped header and falling back to `Connection.RemoteIpAddress`. It reports failure rather than
  inventing a key, so a host can route an unidentifiable caller to `RateLimitPartition.GetNoLimiter`
  instead of collapsing every such caller into one shared bucket. The key is normalized: an IPv4-mapped
  IPv6 address collapses to IPv4, and a native IPv6 address is truncated to its `/64` prefix
  (`2001:db8:85a3:8d3::/64`), because a caller varying the source address inside one routed /64 would
  otherwise get a bucket of their own on each of 2^64 addresses.
- `RateLimiterExtensions.UseBlueprintRejectionResponse` shapes the rejection: `429 Too Many Requests`
  (the framework default is `503 Service Unavailable`, which an upstream load balancer reads as a
  backend fault), an RFC 9457 ProblemDetails body carrying the correlation ID, and a `Retry-After`
  header when the limiter reports one. The delay is serialized as a `long`, and a lease reporting a
  negative delay writes no header at all: RFC 9110 `delay-seconds` is unsigned, and `0` would tell a
  client to retry immediately against the limiter that just rejected it. `TokenBucketRateLimiter` reaches
  both edges with ordinary options — it derives the value from an unchecked `ReplenishmentPeriod.Ticks`
  multiply, which exceeds `Int32` seconds well before it overflows `Int64` and goes negative.

The `UseBlueprintSecurityHeaders` forwarded-headers documentation is corrected: `RemoteIpAddress` is the
nearest hop, not the caller. Behind an edge that appends its own `X-Forwarded-For` entry — a Google
external load balancer sends `<client>,<balancer>` — the rightmost entry that `ForwardLimit 1` reads is
the balancer, and behind a further reverse proxy it is that proxy's egress address, one constant shared
by every caller. Hosts partitioning a rate limiter, or recording a caller's address, must resolve the
client from an edge-stamped header. Behavior is unchanged.

That method's remarks now also state the deployment requirement it has always carried: it registers
`ForwardedHeadersMiddleware` with `KnownProxies` and `KnownIPNetworks` empty, which turns off the
known-proxy check, so the app must be reachable only through an edge that rewrites or appends
`X-Forwarded-For` and `X-Forwarded-Proto`. A caller that can connect directly otherwise supplies both
itself, and a forged `X-Forwarded-Proto: https` satisfies the HTTPS redirection and HSTS middleware.
