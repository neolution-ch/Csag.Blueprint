namespace Csag.Blueprint.TestHost.Time;

/// <summary>
/// A <see cref="TimeProvider"/> that reads the system clock until a test pins it to a fixed instant.
/// </summary>
/// <remarks>
/// The host registers this as its <see cref="TimeProvider"/> so a test can drive the clock that a component
/// captured at startup. Audit.NET is the case that needs it: <c>ConfigureBlueprintAuditLogging</c> resolves
/// the provider once and the SQL data provider holds that instance for the life of the process, so the
/// <c>CreatedAt</c> column cannot be driven by constructing anything with a fake clock. Components that take
/// <see cref="TimeProvider"/> as a constructor dependency need none of this — a test builds those with a
/// <c>FakeTimeProvider</c> directly. Unpinned, this provider is the system clock, so it changes nothing for
/// the rest of the suite.
/// </remarks>
public sealed class PinnableTimeProvider : TimeProvider
{
    private DateTimeOffset? pinnedInstant;

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => this.pinnedInstant ?? TimeProvider.System.GetUtcNow();

    /// <summary>
    /// Pins every clock read to <paramref name="instant"/> until the returned scope is disposed.
    /// </summary>
    /// <param name="instant">The instant every read should return while the scope is held.</param>
    /// <returns>A scope that releases the pin when disposed.</returns>
    public IDisposable Pin(DateTimeOffset instant)
    {
        this.pinnedInstant = instant;
        return new PinScope(this);
    }

    private sealed class PinScope(PinnableTimeProvider owner) : IDisposable
    {
        public void Dispose() => owner.pinnedInstant = null;
    }
}
