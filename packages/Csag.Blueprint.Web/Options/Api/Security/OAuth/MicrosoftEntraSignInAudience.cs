namespace Csag.Blueprint.Web.Options.Api.Security.OAuth
{
    /// <summary>
    /// Defines which accounts are allowed to sign in via Microsoft Entra ID (Azure AD).
    /// This determines the OIDC Authority endpoint and issuer validation behavior.
    /// Must match the app registration's "Supported account types" (--sign-in-audience) in Entra.
    /// Only relevant when <see cref="OidcProviderSettings.Profile"/> is <see cref="OidcProviderProfile.Entra"/>.
    /// </summary>
    public enum MicrosoftEntraSignInAudience
    {
        /// <summary>
        /// Only accounts in the configured tenant directory can sign in.
        /// Authority: https://login.microsoftonline.com/{TenantId}/v2.0.
        /// Requires <see cref="OidcProviderSettings.TenantId"/> to be set.
        /// Matches az --sign-in-audience AzureADMyOrg.
        /// </summary>
        SingleTenant,

        /// <summary>
        /// Accounts from any organization's Entra ID directory can sign in.
        /// Authority: https://login.microsoftonline.com/organizations/v2.0.
        /// Matches az --sign-in-audience AzureADMultipleOrgs.
        /// </summary>
        MultiTenant,

        /// <summary>
        /// Any organizational Entra ID account plus personal Microsoft accounts (outlook.com, xbox, etc.).
        /// Authority: https://login.microsoftonline.com/common/v2.0.
        /// Matches az --sign-in-audience AzureADandPersonalMicrosoftAccount.
        /// </summary>
        MultiTenantAndPersonal,
    }
}
