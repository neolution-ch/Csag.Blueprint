namespace Csag.Blueprint.Web.Extensions
{
    using FastEndpoints;
    using Microsoft.AspNetCore.Builder;

    /// <summary>
    /// Extension methods for configuring FastEndpoints endpoint definitions.
    /// </summary>
    public static class EndpointDefinitionExtensions
    {
        private const string EndpointSuffix = "Endpoint";
        private const string NamespacePlaceholder = "[namespace]";

        /// <summary>
        /// Applies the shared naming and routing conventions to an endpoint definition: the
        /// "Endpoint" class-name suffix is dropped from the endpoint name, and the <c>[namespace]</c>
        /// route placeholder is replaced with the kebab-cased first namespace segment below
        /// <paramref name="baseNamespace"/>.
        /// </summary>
        /// <param name="ep">The endpoint definition to configure.</param>
        /// <param name="baseNamespace">
        /// The root namespace of the consuming application's endpoint classes (e.g.
        /// <c>"MyCompany.Api.Endpoints"</c>, with or without a trailing dot). Deliberately has no
        /// default: the package cannot know the host's namespace, so the application must pass its own.
        /// </param>
        public static void ApplyConventions(this EndpointDefinition ep, string baseNamespace)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(baseNamespace);

            ep.ApplyNamingConvention();
            ep.ApplyRoutingConvention(baseNamespace.EndsWith('.') ? baseNamespace : baseNamespace + ".");
        }

        private static void ApplyNamingConvention(this EndpointDefinition ep)
        {
            var cleanName = GetCleanEndpointName(ep.EndpointType.Name);
            if (cleanName != ep.EndpointType.Name)
            {
                ep.Options(o => o.WithName(cleanName));
            }
        }

        private static void ApplyRoutingConvention(this EndpointDefinition ep, string baseNamespace)
        {
            if (!HasNamespacePlaceholder(ep.Routes))
            {
                return;
            }

            var resourceName = GetResourceNameFromNamespace(ep.EndpointType.Namespace, baseNamespace);
            ReplaceNamespacePlaceholder(ep.Routes, resourceName);
        }

        private static string GetCleanEndpointName(string endpointName) =>
            endpointName.EndsWith(EndpointSuffix, StringComparison.Ordinal)
                ? endpointName[..^EndpointSuffix.Length]
                : endpointName;

        private static bool HasNamespacePlaceholder(string[] routes) =>
            routes.Any(r => r.Contains(NamespacePlaceholder, StringComparison.OrdinalIgnoreCase));

        private static string GetResourceNameFromNamespace(string? endpointNamespace, string baseNamespace)
        {
            if (string.IsNullOrEmpty(endpointNamespace))
            {
                return string.Empty;
            }

            var namespaceSuffix = endpointNamespace.StartsWith(baseNamespace, StringComparison.Ordinal)
                ? endpointNamespace[baseNamespace.Length..]
                : endpointNamespace;

            var resourceName = namespaceSuffix.Split('.')[0];

            return ToKebabCase(resourceName);
        }

        private static string ToKebabCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var result = new System.Text.StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                if (i > 0 && char.IsUpper(value[i]) && char.IsLower(value[i - 1]))
                {
                    result.Append('-');
                }

#pragma warning disable CA1308 // Normalize strings to uppercase
                result.Append(char.ToLowerInvariant(value[i]));
#pragma warning restore CA1308 // Normalize strings to uppercase
            }

            return result.ToString();
        }

        private static void ReplaceNamespacePlaceholder(string[] routes, string resourceName)
        {
            for (int i = 0; i < routes.Length; i++)
            {
                if (routes[i].Contains(NamespacePlaceholder, StringComparison.OrdinalIgnoreCase))
                {
                    routes[i] = routes[i].Replace(NamespacePlaceholder, resourceName, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
