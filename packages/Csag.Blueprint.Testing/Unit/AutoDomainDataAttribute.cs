namespace Csag.Blueprint.Testing.Unit;

using AutoFixture;
using AutoFixture.Xunit3;

/// <summary>
/// AutoData attribute configured to handle circular references in domain entities.
/// Replaces ThrowingRecursionBehavior with OmitOnRecursionBehavior to allow
/// AutoFixture to create entity graphs with navigation properties.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AutoDomainDataAttribute : AutoDataAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoDomainDataAttribute"/> class.
    /// </summary>
    public AutoDomainDataAttribute()
        : base(() => CreateFixture())
    {
    }

    private static Fixture CreateFixture()
    {
        var fixture = new Fixture();

        // Remove the default ThrowingRecursionBehavior
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));

        // Add OmitOnRecursionBehavior to handle circular references in entity navigation properties
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        return fixture;
    }
}
