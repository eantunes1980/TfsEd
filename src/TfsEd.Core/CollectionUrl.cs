namespace TfsEd.Core;

/// <summary>
/// Normalized URL of an Azure DevOps Server / TFS project collection,
/// e.g. <c>https://tfs.example.com/DefaultCollection</c>.
/// </summary>
public sealed class CollectionUrl : IEquatable<CollectionUrl>
{
    private CollectionUrl(string value) => Value = value;

    /// <summary>Absolute URL without a trailing slash.</summary>
    public string Value { get; }

    public static CollectionUrl Parse(string value)
    {
        if (!TryParse(value, out var url))
        {
            throw new TfsEdException($"'{value}' is not a valid collection URL. Expected an absolute http(s) URL such as https://tfs.example.com/DefaultCollection.");
        }

        return url;
    }

    public static bool TryParse(string? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CollectionUrl? url)
    {
        url = null;
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            return false;
        }

        url = new CollectionUrl(uri.GetLeftPart(UriPartial.Path).TrimEnd('/'));
        return true;
    }

    /// <summary>Builds an absolute URI for a path relative to the collection (may include a query string).</summary>
    public Uri Combine(string relative) => new($"{Value}/{relative.TrimStart('/')}");

    public bool Equals(CollectionUrl? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as CollectionUrl);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;
}
