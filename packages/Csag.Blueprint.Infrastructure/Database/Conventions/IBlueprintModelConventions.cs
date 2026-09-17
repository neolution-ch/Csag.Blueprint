namespace Csag.Blueprint.Infrastructure.Database.Conventions;

using Microsoft.EntityFrameworkCore;

/// <summary>
/// Implemented by contexts whose blueprint conventions are applied after the model is complete.
/// </summary>
/// <remarks>
/// Keeps <see cref="BlueprintModelCustomizer"/> free of the tenant, user and role generic parameters:
/// the customizer is resolved from the service provider and cannot be generic over them.
/// </remarks>
public interface IBlueprintModelConventions
{
    /// <summary>
    /// Applies the contract-driven blueprint conventions to the finished model.
    /// </summary>
    /// <param name="modelBuilder">The model builder holding the completed model.</param>
    void ApplyBlueprintConventions(ModelBuilder modelBuilder);
}
