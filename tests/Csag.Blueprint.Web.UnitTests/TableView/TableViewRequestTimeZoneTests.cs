namespace Csag.Blueprint.Web.UnitTests.TableView;

using Csag.Blueprint.Web.TableView;
using Microsoft.Extensions.Logging;

/// <summary>
/// Unit tests for <see cref="TableViewRequestTimeZone"/>: a malformed request zone is refused, while a
/// well-formed one this host's time zone data does not know falls back to the tenant zone with a warning,
/// since browsers can know zones the server does not yet.
/// </summary>
public sealed class TableViewRequestTimeZoneTests
{
    [Fact]
    public void TryFindRequested_NoZone_FindsNoneWithoutWarning()
    {
        var logger = new RecordingLogger();

        TableViewRequestTimeZone.TryFindRequested(null, logger, out var zone).ShouldBeTrue();

        zone.ShouldBeNull();
        logger.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void TryFindRequested_KnownZone_FindsIt()
    {
        var logger = new RecordingLogger();

        TableViewRequestTimeZone.TryFindRequested("America/New_York", logger, out var zone).ShouldBeTrue();

        zone.ShouldNotBeNull().Id.ShouldBe("America/New_York");
        logger.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void TryFindRequested_WellFormedZoneUnknownToHost_FallsBackWithWarning()
    {
        var logger = new RecordingLogger();
        var id = UnknownId();

        TableViewRequestTimeZone.TryFindRequested(id, logger, out var zone).ShouldBeTrue();

        zone.ShouldBeNull("the caller falls back to the tenant zone");
        logger.Warnings.ShouldHaveSingleItem().ShouldContain(id);
    }

    [Fact]
    public void TryFindRequested_SameUnknownZoneAgain_WarnsOnlyOnce()
    {
        var first = new RecordingLogger();
        var second = new RecordingLogger();
        var id = UnknownId();

        TableViewRequestTimeZone.TryFindRequested(id, first, out _).ShouldBeTrue();
        TableViewRequestTimeZone.TryFindRequested(id, second, out var zone).ShouldBeTrue();

        zone.ShouldBeNull();
        first.Warnings.ShouldHaveSingleItem();
        second.Warnings.ShouldBeEmpty("a browser sends its zone on every request; one warning per id is enough");
        second.Debugs.ShouldHaveSingleItem().ShouldContain(id);
    }

    [Theory]
    [InlineData("W. Europe Standard Time")]
    [InlineData("Europe//Zurich")]
    [InlineData("")]
    public void TryFindRequested_MalformedZone_IsRefused(string timeZone)
    {
        var logger = new RecordingLogger();

        TableViewRequestTimeZone.TryFindRequested(timeZone, logger, out var zone).ShouldBeFalse();

        zone.ShouldBeNull();
        logger.Warnings.ShouldBeEmpty();
    }

    /// <summary>
    /// The set of ids already warned about lives for the process, so each test uses an id of its own.
    /// </summary>
    private static string UnknownId() => $"Mars/Test_{Guid.NewGuid():N}";

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Warnings { get; } = [];

        public List<string> Debugs { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                this.Warnings.Add(formatter(state, exception));
            }
            else if (logLevel == LogLevel.Debug)
            {
                this.Debugs.Add(formatter(state, exception));
            }
        }
    }
}
