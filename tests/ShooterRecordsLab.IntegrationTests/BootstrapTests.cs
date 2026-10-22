using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ShooterRecordsLab.IntegrationTests;

public sealed class BootstrapTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Liveness_does_not_depend_on_database(string environment)
    {
        await using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live", CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Readiness_fails_without_database_without_leaking_details()
    {
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/ready", CancellationToken.None);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task OpenApi_is_only_exposed_in_development(string environment, HttpStatusCode expected)
    {
        await using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/openapi/v1.json", CancellationToken.None);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
            Assert.StartsWith("3.", json.RootElement.GetProperty("openapi").GetString());
        }
    }

    [Fact]
    public async Task Missing_route_uses_safe_problem_details()
    {
        await using var factory = CreateFactory("Production");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/missing", CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(404, json.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.WithoutExternalDatabase().UseEnvironment(environment));
}
