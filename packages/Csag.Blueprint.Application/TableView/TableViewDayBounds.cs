namespace Csag.Blueprint.Application.TableView;

/// <summary>
/// The bounds a range of calendar dates puts on a temporal column. A null bound is open.
/// </summary>
/// <param name="Lower">The inclusive lower bound, or null.</param>
/// <param name="Upper">The upper bound, or null.</param>
/// <param name="IsUpperInclusive">Whether <paramref name="Upper"/> is inclusive.</param>
internal readonly record struct TableViewDayBounds(object? Lower, object? Upper, bool IsUpperInclusive);
