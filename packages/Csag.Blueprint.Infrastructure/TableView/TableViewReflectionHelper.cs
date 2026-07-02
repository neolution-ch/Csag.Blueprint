namespace Csag.Blueprint.Infrastructure.TableView;

using Csag.Blueprint.Application.TableView;

/// <summary>
/// Provides reflection helpers for table view definition discovery and metadata extraction.
/// </summary>
internal static class TableViewReflectionHelper
{
    /// <summary>
    /// Walks the type hierarchy to find a base type matching the given open generic type definition.
    /// </summary>
    /// <param name="type">The type to search from.</param>
    /// <param name="openGenericBase">The open generic type definition to match.</param>
    /// <returns>The matching generic base type, or null if not found.</returns>
    public static Type? FindGenericBaseType(Type type, Type openGenericBase)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openGenericBase)
            {
                return current;
            }

            current = current.BaseType;
        }

        return null;
    }

    /// <summary>
    /// Extracts catalog registration metadata from a type implementing <see cref="ITableViewDefinitionInfo"/>.
    /// </summary>
    /// <param name="definitionType">The definition type to extract metadata from.</param>
    /// <param name="definitionBaseType">The generic TableViewDefinition base type to extract entity type from.</param>
    /// <returns>A <see cref="TableViewCatalogRegistration"/> with extracted metadata, or null if extraction fails.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required static properties are missing or null.</exception>
    public static TableViewCatalogRegistration? ExtractCatalogRegistration(Type definitionType, Type definitionBaseType)
    {
        // Read static properties via reflection from the definition type
        var viewIdProperty = definitionType.GetProperty("ViewId");
        var displayNameProperty = definitionType.GetProperty("DisplayName");
        var descriptionProperty = definitionType.GetProperty("Description");
        var requiredPermissionProperty = definitionType.GetProperty("RequiredPermission");

        if (viewIdProperty == null || displayNameProperty == null ||
            descriptionProperty == null || requiredPermissionProperty == null)
        {
            return null;
        }

        var viewId = (string?)viewIdProperty.GetValue(null);
        var displayName = (string?)displayNameProperty.GetValue(null);
        var description = (string?)descriptionProperty.GetValue(null);
        var requiredPermission = (string?)requiredPermissionProperty.GetValue(null);

        if (viewId == null || displayName == null || description == null || requiredPermission == null)
        {
            return null;
        }

        var entityType = definitionBaseType.GetGenericArguments()[0].Name;

        return new TableViewCatalogRegistration(viewId, displayName, description, requiredPermission, entityType);
    }
}
