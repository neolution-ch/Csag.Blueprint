namespace Csag.Blueprint.Domain.Contracts;

/// <summary>
/// Interface for entities that have an internal name.
/// The internal name is typically used for system identification, reporting,
/// or administrative purposes and is distinct from user-facing display names.
/// </summary>
public interface IHasInternalName
{
    /// <summary>
    /// Gets or sets the internal name for system identification.
    /// <para>
    /// This name is intended for internal use, reporting, and system administration.
    /// It should be unique within its context and follow consistent naming conventions.
    /// Unlike display names, internal names are typically not localized.
    /// </para>
    /// </summary>
    string InternalName { get; set; }
}
