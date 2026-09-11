using System.Net;
using System.Text;
using TfsEd.Core;
using TfsEd.Server;

namespace TfsEd.Tests;

public class AdoClientTests
{
    private static readonly CollectionUrl Collection = CollectionUrl.Parse("https://tfs.example.com/DefaultCollection");

    private const string ConnectionDataJson =
        """{"authenticatedUser":{"id":"u1","providerDisplayName":"Jane Doe"},"instanceId":"6b0d3a1e-0000-0000-0000-000000000001","deploymentType":"onPremises"}""";

    [Fact]
    public async Task Sends_pat_as_basic_auth_and_parses_connection_data()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json(ConnectionDataJson));
        var client = new AdoClient(new HttpClient(handler), Collection, "my-pat");

        var data = await client.GetConnectionDataAsync(CancellationToken.None);

        Assert.Equal("Jane Doe", data.AuthenticatedUser?.ProviderDisplayName);
        Assert.Equal("onPremises", data.DeploymentType);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("Basic", request.Headers.Authorization?.Scheme);
        Assert.Equal(":my-pat", Encoding.ASCII.GetString(Convert.FromBase64String(request.Headers.Authorization!.Parameter!)));
        Assert.StartsWith("https://tfs.example.com/DefaultCollection/_apis/connectionData", request.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task Parses_project_list()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json("""{"count":2,"value":[{"name":"A"},{"name":"B"}]}"""));
        var client = new AdoClient(new HttpClient(handler), Collection, "pat");

        var projects = await client.GetProjectsAsync(CancellationToken.None);

        Assert.Equal(["A", "B"], projects.Select(p => p.Name));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.NonAuthoritativeInformation)]
    public async Task Rejected_pat_throws_authentication_exception(HttpStatusCode status)
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(status) { Content = new StringContent("<html/>", Encoding.UTF8, "text/html") });
        var client = new AdoClient(new HttpClient(handler), Collection, "bad");

        await Assert.ThrowsAsync<TfsEdAuthenticationException>(() => client.GetConnectionDataAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Server_error_message_is_surfaced()
    {
        var handler = new StubHttpHandler(_ => StubHttpHandler.Json("""{"message":"TF14045: item not found"}""", HttpStatusCode.NotFound));
        var client = new AdoClient(new HttpClient(handler), Collection, "pat");

        var ex = await Assert.ThrowsAsync<TfsEdServerException>(() => client.GetProjectsAsync(CancellationToken.None));

        Assert.Contains("TF14045", ex.Message);
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }
}
