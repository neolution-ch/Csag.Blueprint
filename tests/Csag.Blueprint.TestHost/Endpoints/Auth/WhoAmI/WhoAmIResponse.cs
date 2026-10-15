namespace Csag.Blueprint.TestHost.Endpoints.Auth.WhoAmI;

using System.Security.Claims;

/// <summary>
/// The caller's principal: each identity it carries and every claim.
/// </summary>
public sealed class WhoAmIResponse
{
    /// <summary>
    /// Gets or sets the authentication type of each authenticated identity on the principal, in order.
    /// </summary>
    public List<string> AuthenticationTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets every claim of the principal, as type and value.
    /// </summary>
    public List<WhoAmIClaim> Claims { get; set; } = [];

    /// <summary>
    /// Describes a principal.
    /// </summary>
    /// <param name="user">The principal.</param>
    /// <returns>The description.</returns>
    public static WhoAmIResponse From(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new WhoAmIResponse
        {
            AuthenticationTypes = user.Identities
                .Where(i => i.IsAuthenticated)
                .Select(i => i.AuthenticationType!)
                .ToList(),
            Claims = user.Claims.Select(c => new WhoAmIClaim { Type = c.Type, Value = c.Value }).ToList(),
        };
    }
}
