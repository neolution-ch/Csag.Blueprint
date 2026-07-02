namespace Csag.Blueprint.Web.Extensions
{
    /// <summary>
    /// Defines how an authentication scheme is applied to endpoints by default.
    /// </summary>
    public enum AuthMode
    {
        /// <summary>
        /// The scheme is not applied to endpoints by default.
        /// Individual endpoints must explicitly declare it via <c>AuthSchemes()</c>.
        /// </summary>
        OptIn,

        /// <summary>
        /// The scheme is applied to all authenticated endpoints by default.
        /// Individual endpoints can override this by explicitly declaring their own <c>AuthSchemes()</c>.
        /// </summary>
        OptOut,
    }
}
