namespace Csag.Blueprint.Application.TableView;

using System.Text.Json.Serialization;

/// <summary>
/// A value a table view column can be filtered by, paired with the text shown to the user for it.
/// Serialized to clients as <c>{ "value": "...", "label": "..." }</c>: <see cref="Value"/> is what a
/// client sends back as filter input and what filtering matches on, <see cref="Label"/> is display
/// text only.
/// </summary>
/// <remarks>
/// Instances are immutable, so the values owned by a table view definition can be shared by every
/// request. Request-time localization serves copies carrying the resolved <see cref="Label"/>.
/// </remarks>
public sealed record TableViewAllowedValue
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TableViewAllowedValue"/> class.
    /// </summary>
    /// <param name="value">The raw filter value.</param>
    /// <param name="label">The text shown to the user for <paramref name="value"/>.</param>
    public TableViewAllowedValue(string value, string label)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(label);

        this.Value = value;
        this.Label = label;
    }

    /// <summary>
    /// Gets the raw filter value. For enum columns this is the enum member name.
    /// </summary>
    public string Value { get; init; }

    /// <summary>
    /// Gets the text shown to the user for <see cref="Value"/>. When <see cref="LabelKey"/> is set,
    /// this is the fallback served when the key cannot be resolved at request time.
    /// </summary>
    public string Label { get; init; }

    /// <summary>
    /// Gets the translation key used to resolve <see cref="Label"/> at request time.
    /// Recorded during definition construction without touching any localizer, so definitions stay
    /// safe to construct during application startup. Not serialized to clients; when the key cannot
    /// be resolved, <see cref="Label"/> is served unchanged.
    /// </summary>
    [JsonIgnore]
    public string? LabelKey { get; init; }

    /// <summary>
    /// Gets a value indicating whether a missing translation for <see cref="LabelKey"/> is expected.
    /// When <c>true</c>, the localizer keeps <see cref="Label"/> without logging; when <c>false</c>,
    /// a missing translation is logged as a warning. Set for the keys enum columns derive by
    /// convention (<c>Enums.{EnumTypeName}.{MemberName}</c>), which applications translate only
    /// when they want to. Not serialized to clients.
    /// </summary>
    [JsonIgnore]
    public bool IsLabelKeyOptional { get; init; }
}
