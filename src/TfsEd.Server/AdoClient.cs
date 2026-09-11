using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using TfsEd.Core;

namespace TfsEd.Server;

/// <summary>Thin REST client for one Azure DevOps Server / TFS collection, authenticated with a PAT.</summary>
public sealed class AdoClient
{
    private const string ApiVersion = "7.0";

    private readonly HttpClient _http;
    private readonly AuthenticationHeaderValue _authorization;

    public AdoClient(HttpClient http, CollectionUrl collection, string personalAccessToken)
    {
        _http = http;
        Collection = collection;
        _authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{personalAccessToken}")));
    }

    public CollectionUrl Collection { get; }

    public Task<ConnectionData> GetConnectionDataAsync(CancellationToken cancellationToken) =>
        GetAsync("_apis/connectionData?connectOptions=none", ServerJsonContext.Default.ConnectionData, cancellationToken);

    public async Task<IReadOnlyList<TeamProject>> GetProjectsAsync(CancellationToken cancellationToken)
    {
        var response = await GetAsync(
            $"_apis/projects?$top=1000&api-version={ApiVersion}", ServerJsonContext.Default.ListResponseTeamProject, cancellationToken);
        return response.Value;
    }

    private async Task<T> GetAsync<T>(string relativeUrl, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Collection.Combine(relativeUrl));
        request.Headers.Authorization = _authorization;
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken)
            ?? throw new TfsEdServerException($"Empty response from {request.RequestUri}.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        // Azure DevOps answers a rejected PAT with 401, or with 203 + an HTML sign-in page.
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NonAuthoritativeInformation
            || (response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType == "text/html"))
        {
            throw new TfsEdAuthenticationException(
                "Authentication failed: the personal access token is invalid, expired or lacks the required scopes.",
                response.StatusCode);
        }

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var message = TryReadErrorMessage(body) ?? response.ReasonPhrase;
        throw new TfsEdServerException($"Server returned {(int)response.StatusCode}: {message}", response.StatusCode);
    }

    private static string? TryReadErrorMessage(string body)
    {
        try
        {
            return JsonSerializer.Deserialize(body, ServerJsonContext.Default.ServerError)?.Message;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
