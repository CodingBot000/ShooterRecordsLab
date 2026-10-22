using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;

namespace ShooterRecordsLab.IntegrationTests;

public sealed class AuthenticationTests
{
    [Theory]
    [InlineData("missing", 401)]
    [InlineData("malformed", 401)]
    [InlineData("expired", 401)]
    [InlineData("future", 401)]
    [InlineData("signature", 401)]
    [InlineData("issuer", 401)]
    [InlineData("audience", 401)]
    [InlineData("algorithm", 401)]
    [InlineData("no-scope", 403)]
    [InlineData("substring-scope", 403)]
    [InlineData("writer", 400)]
    [InlineData("multiple-scopes", 400)]
    public async Task Bearer_validation_and_scope_are_enforced_before_model_validation(string scenario, int expected)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.WithoutExternalDatabase().UseEnvironment("Production"));
        using var client = factory.CreateClient();
        var token = scenario switch
        {
            "missing" => null,
            "malformed" => "invalid-token",
            "expired" => TestTokens.Create(expired: true),
            "future" => TestTokens.Create(future: true),
            "signature" => TestTokens.Create(wrongKey: true),
            "issuer" => TestTokens.Create(issuer: "wrong"),
            "audience" => TestTokens.Create(audience: "wrong"),
            "algorithm" => TestTokens.Create(algorithm: SecurityAlgorithms.HmacSha384),
            "no-scope" => TestTokens.Create(scope: "players.read"),
            "substring-scope" => TestTokens.Create(scope: "matches.write.other"),
            "multiple-scopes" => TestTokens.Create(scope: "players.read matches.write"),
            _ => TestTokens.Create()
        };
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.PostAsJsonAsync("/api/matches", new { }, CancellationToken.None);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        Assert.Equal(expected, json.RootElement.GetProperty("status").GetInt32());
        Assert.True(json.RootElement.TryGetProperty("traceId", out _));
        if (token is not null)
        {
            Assert.DoesNotContain(token, json.RootElement.ToString(), StringComparison.Ordinal);
        }
    }
}
