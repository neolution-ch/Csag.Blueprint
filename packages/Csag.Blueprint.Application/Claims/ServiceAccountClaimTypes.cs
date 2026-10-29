namespace Csag.Blueprint.Application.Claims;

/// <summary>
/// The claim names of a service-account bearer token, and of the principal the package builds from it. They are raw
/// JWT claim names: the package's bearer scheme does not map inbound claims, so the principal carries exactly these
/// names and no <see cref="System.Security.Claims.ClaimTypes"/> alias of them.
/// </summary>
/// <remarks>
/// Changing a value invalidates every token already issued, so a change here is a deliberate, breaking decision.
/// </remarks>
public static class ServiceAccountClaimTypes
{
    /// <summary>
    /// Gets the claim type carrying the service account's client id ("sub").
    /// </summary>
    public static readonly string Subject = "sub";

    /// <summary>
    /// Gets the claim type carrying the service account's display name ("name").
    /// </summary>
    public static readonly string Name = "name";

    /// <summary>
    /// Gets the claim type naming the kind of principal ("type"). A service-account principal carries
    /// <see cref="ServiceAccountType"/> in it.
    /// </summary>
    public static readonly string Type = "type";

    /// <summary>
    /// Gets the claim type carrying the opaque service-account session key ("sid"). It is the only
    /// authorization-relevant claim a service-account token carries; roles, permissions and tenant are resolved
    /// server-side from this session on every request rather than trusted from the token.
    /// </summary>
    public static readonly string SessionId = "sid";

    /// <summary>
    /// Gets the value of the <see cref="Type"/> claim on a service-account principal.
    /// </summary>
    public static readonly string ServiceAccountType = "ServiceAccount";
}
