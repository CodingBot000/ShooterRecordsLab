using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShooterRecordsLab.Application.Abstractions;
using ShooterRecordsLab.Application.Rankings;

namespace ShooterRecordsLab.IntegrationTests;

public sealed class RankingsApiTests
{
    [Theory]
    [InlineData("", 200, 20)]
    [InlineData("?top=1", 200, 1)]
    [InlineData("?top=100", 200, 100)]
    [InlineData("?top=0", 400, 0)]
    [InlineData("?top=101", 400, 0)]
    [InlineData("?top=1;DROP", 400, 0)]
    public async Task Rankings_contract_validates_top_and_maps_rows(string query, int expected, int top)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.WithoutExternalDatabase().UseEnvironment("Development").ConfigureServices(services =>
            {
                services.RemoveAll<IRankingRepository>();
                services.AddScoped<IRankingRepository, StubRankings>();
            }));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/rankings" + query, CancellationToken.None);
        Assert.Equal(expected, (int)response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        if (expected == 200)
        {
            Assert.Equal(top, json.RootElement.GetProperty("top").GetInt32());
            var row = json.RootElement.GetProperty("items")[0];
            Assert.Equal(1, row.GetProperty("rank").GetInt64());
            Assert.Equal("PracticePlayer", row.GetProperty("displayName").GetString());
            Assert.Equal(37, row.GetProperty("kills").GetInt64());
        }
        else
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    private sealed class StubRankings : IRankingRepository
    {
        public Task<IReadOnlyList<RankingRow>> GetTopAsync(int top, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RankingRow>>([new RankingRow
            {
                Rank = 1, PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111"), DisplayName = "PracticePlayer", Matches = 5, Wins = 4, Kills = 37
            }]);
        }
    }
}
