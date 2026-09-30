namespace AspireWebAppTemplate.Web.Utilities;

/// <summary>
/// Builds a query string from a base path and an ordered set of parameters, matching the
/// escaping and omission rules the typed API clients apply by hand: string values are escaped
/// with <see cref="Uri.EscapeDataString(string)"/>, non-string values use their default string
/// form, optional parameters whose value is unset are omitted, parts are joined with '&amp;', and
/// the '?' separator is emitted only when at least one parameter is present.
/// </summary>
public sealed class QueryStringBuilder
{
    #region Fields

    /// <summary>
    /// The ordered query-string parts already added, each in "key=value" form.
    /// </summary>
    private readonly List<string> _parts = new();

    #endregion

    #region Building

    /// <summary>
    /// Builds a full URL for <paramref name="path"/> by applying <paramref name="configure"/> to a
    /// new builder and appending the resulting query string.
    /// </summary>
    /// <param name="path">The base path (for example "/api/users").</param>
    /// <param name="configure">A callback that adds the parameters in order.</param>
    /// <returns>The path with a query string, or the bare path when no parameters were added.</returns>
    public static string Build(string path, Action<QueryStringBuilder> configure)
    {
        var builder = new QueryStringBuilder();
        configure(builder);
        return builder._parts.Count > 0 ? $"{path}?{string.Join("&", builder._parts)}" : path;
    }

    /// <summary>
    /// Adds a required parameter whose value is always emitted (used for parameters the current
    /// code always includes, such as page and pageSize on the notifications and announcements lists).
    /// </summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The parameter value; formatted with its default string form.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public QueryStringBuilder Add(string key, object value)
    {
        _parts.Add($"{key}={value}");
        return this;
    }

    /// <summary>
    /// Adds a parameter only when <paramref name="value"/> has a value (nullable value types).
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The optional value; omitted when null.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public QueryStringBuilder AddIfHasValue<T>(string key, T? value) where T : struct
    {
        if (value.HasValue)
            _parts.Add($"{key}={value.Value}");
        return this;
    }

    /// <summary>
    /// Adds a string parameter only when it is not null, empty, or whitespace, escaping the value
    /// with <see cref="Uri.EscapeDataString(string)"/> to match the current hand-built escaping.
    /// </summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The optional string value; omitted when null/empty/whitespace.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public QueryStringBuilder AddIfNotWhiteSpace(string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            _parts.Add($"{key}={Uri.EscapeDataString(value)}");
        return this;
    }

    #endregion
}
