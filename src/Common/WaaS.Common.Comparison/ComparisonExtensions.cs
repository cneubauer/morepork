namespace WaaS.Common.Comparison;

/// <summary>
/// Fluent entry points for <see cref="Changes"/>.
/// </summary>
public static class ComparisonExtensions
{
    /// <summary>
    /// Reports what would change when moving from this state to <paramref name="proposed"/>.
    /// </summary>
    /// <remarks>
    /// The receiver is the current state and the argument is the proposed one, so
    /// <c>current.Compare(proposed)</c> reads in the same direction as the reported
    /// <see cref="Change.Current"/> and <see cref="Change.New"/> values. Neither object is modified.
    /// </remarks>
    public static ChangeSet Compare<T>(this T? current, T? proposed, ComparisonOptions? options = null) =>
        Changes.Between(current, proposed, options);
}
