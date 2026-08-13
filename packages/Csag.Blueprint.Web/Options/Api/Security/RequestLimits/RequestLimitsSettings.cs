namespace Csag.Blueprint.Web.Options.Api.Security.RequestLimits
{
    /// <summary>
    /// Request size limit configuration settings.
    /// Nested under Blueprint:Security:RequestLimits in appsettings.json.
    /// Centrally caps the request body size (Kestrel) and the multipart form body size
    /// so oversized requests are rejected at the transport level instead of relying on
    /// per-endpoint validation that only runs after the full body has been received.
    /// </summary>
    public sealed class RequestLimitsSettings
    {
        /// <summary>
        /// Gets or sets the maximum allowed request body size in megabytes (MiB),
        /// applied to all requests via Kestrel's <c>Limits.MaxRequestBodySize</c>.
        /// Oversized non-form bodies (e.g. JSON) are rejected with 413 Payload Too Large;
        /// on multipart/form-data endpoints the rejection surfaces during form binding and is
        /// returned as a 400 Problem Details response via the FastEndpoints FormExceptionTransformer.
        /// Choose a value with headroom above the largest expected upload to account
        /// for additional form fields and multipart encoding overhead.
        /// </summary>
        public int MaxRequestBodySizeMegabytes { get; set; }

        /// <summary>
        /// Gets or sets the maximum allowed length in megabytes (MiB) of each multipart section
        /// (e.g. each uploaded file) in a multipart/form-data request, applied via
        /// <c>FormOptions.MultipartBodyLengthLimit</c>. This is a per-section limit; the total
        /// request body is bounded by <see cref="MaxRequestBodySizeMegabytes"/>.
        /// Must not exceed <see cref="MaxRequestBodySizeMegabytes"/> — a per-section limit above
        /// the total body cap would be ineffective because Kestrel rejects the request first.
        /// </summary>
        public int MultipartBodyLengthLimitMegabytes { get; set; }
    }
}
