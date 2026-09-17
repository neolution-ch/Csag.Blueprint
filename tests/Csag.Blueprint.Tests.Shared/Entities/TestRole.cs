namespace Csag.Blueprint.Tests.Shared.Entities;

using Csag.Blueprint.Domain.Entities;

/// <summary>
/// Minimal concrete closure of <see cref="BlueprintRole"/> for unit tests.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("SonarAnalyzer.CSharp", "S2094:Classes should not be empty", Justification = "Intentionally the plainest possible closure of the generic base type; tests need no additional role members.")]
public sealed class TestRole : BlueprintRole
{
}
