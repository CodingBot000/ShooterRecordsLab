using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShooterRecordsLab.Application.Abstractions;
using ShooterRecordsLab.Domain.Entities;
using ShooterRecordsLab.IntegrationTests.Fakes;

namespace ShooterRecordsLab.IntegrationTests;

public sealed class RecordsApiTests
{
    private const string PlayerA = "11111111-1111-1111-1111-111111111111";
    private const string PlayerB = "22222222-2222-2222-2222-222222222222";
    private const string PlayerC = "33333333-3333-3333-3333-333333333333";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-22T12:00:00Z");

    [Fact]
    public async Task Player_and_empty_collections_follow_contract()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var player = await GetJson(client, $"/api/players/{PlayerA}");
        Assert.Equal(PlayerA, player.GetProperty("playerId").GetString());
        Assert.Equal("PracticePlayerA", player.GetProperty("displayName").GetString());
        var page = await GetJson(client, $"/api/players/{PlayerA}/matches");
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        Assert.Equal(20, page.GetProperty("pageSize").GetInt32());
        Assert.Equal(0, page.GetProperty("totalCount").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        var stats = await GetJson(client, $"/api/players/{PlayerA}/stats");
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("winRate").ValueKind);
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("kdRatio").ValueKind);
    }

    [Theory]
    [InlineData("/api/players/not-a-uuid", 404)]
    [InlineData("/api/players/00000000-0000-0000-0000-000000000000", 400)]
    [InlineData("/api/players/99999999-9999-9999-9999-999999999999", 404)]
    [InlineData("/api/players/99999999-9999-9999-9999-999999999999/matches", 404)]
    [InlineData("/api/players/99999999-9999-9999-9999-999999999999/stats", 404)]
    [InlineData("/api/matches/99999999-9999-9999-9999-999999999999", 404)]
    [InlineData("/api/matches/00000000-0000-0000-0000-000000000000", 400)]
    [InlineData("/api/players/11111111-1111-1111-1111-111111111111/matches?page=0", 400)]
    [InlineData("/api/players/11111111-1111-1111-1111-111111111111/matches?page=-1", 400)]
    [InlineData("/api/players/11111111-1111-1111-1111-111111111111/matches?pageSize=0", 400)]
    [InlineData("/api/players/11111111-1111-1111-1111-111111111111/matches?pageSize=101", 400)]
    [InlineData("/api/players/11111111-1111-1111-1111-111111111111/matches?page=no", 400)]
    public async Task Invalid_and_missing_resources_return_problem_details(string path, int status)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path, CancellationToken.None);
        await AssertProblem(response, status);
    }

    [Fact]
    public async Task Registration_location_replay_conflict_and_statistics_follow_contract()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var created = await Post(client, Payload(" demo-one "));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var result = await Read(created);
        Assert.True(result.GetProperty("created").GetBoolean());
        Assert.Equal("DEMO-ONE", result.GetProperty("externalMatchId").GetString());
        Assert.EndsWith("/api/matches/" + result.GetProperty("matchId").GetString(), created.Headers.Location?.ToString());
        var detail = await GetJson(client, created.Headers.Location!.ToString());
        Assert.Equal(new[] { PlayerA, PlayerB }, detail.GetProperty("participants").EnumerateArray().Select(p => p.GetProperty("playerId").GetString()));
        Assert.Equal("Win", detail.GetProperty("participants")[0].GetProperty("outcome").GetString());
        using var replay = await Post(client, Payload("DEMO-ONE", reverse: true));
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayJson = await Read(replay);
        Assert.False(replayJson.GetProperty("created").GetBoolean());
        Assert.Equal(result.GetProperty("matchId").GetString(), replayJson.GetProperty("matchId").GetString());
        using var conflict = await Post(client, Payload("demo-one", kills: 9));
        var problem = await AssertProblem(conflict, 409);
        Assert.Equal("MATCH_ID_CONFLICT", problem.GetProperty("code").GetString());
        using var second = await Post(client, Payload("second", kills: 7, outcomeA: "Loss", outcomeB: "Win"));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var stats = await GetJson(client, $"/api/players/{PlayerA}/stats");
        Assert.Equal(2, stats.GetProperty("totalMatches").GetInt32());
        Assert.Equal(1, stats.GetProperty("wins").GetInt32());
        Assert.Equal(1, stats.GetProperty("losses").GetInt32());
        Assert.Equal(15, stats.GetProperty("kills").GetInt32());
        Assert.Equal(0.5m, stats.GetProperty("winRate").GetDecimal());
        Assert.Equal(JsonValueKind.Null, stats.GetProperty("kdRatio").ValueKind);
        var page = await GetJson(client, $"/api/players/{PlayerA}/matches?pageSize=1");
        var page2 = await GetJson(client, $"/api/players/{PlayerA}/matches?page=2&pageSize=1");
        var id1 = page.GetProperty("items")[0].GetProperty("matchId").GetGuid();
        var id2 = page2.GetProperty("items")[0].GetProperty("matchId").GetGuid();
        Assert.True(id1.CompareTo(id2) > 0);
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        var far = await GetJson(client, $"/api/players/{PlayerA}/matches?page=2147483647&pageSize=100");
        Assert.Empty(far.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("bad id", "2026-10-21T12:30:00Z", 8, "Win", "Loss", PlayerB)]
    [InlineData("ſ", "2026-10-21T12:30:00Z", 8, "Win", "Loss", PlayerB)]
    [InlineData("é", "2026-10-21T12:30:00Z", 8, "Win", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00+09:00", 8, "Win", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00", 8, "Win", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-22T12:05:01Z", 8, "Win", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00Z", -1, "Win", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 10001, "Win", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 8, "Win", "Win", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 8, "Win", "Draw", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 8, "win", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 8, "Other", "Loss", PlayerB)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 8, "Win", "Loss", PlayerA)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 8, "Win", "Loss", PlayerC)]
    [InlineData("valid", "2026-10-21T12:30:00Z", 8, "Win", "Loss", "00000000-0000-0000-0000-000000000000")]
    public async Task Invalid_registration_does_not_write_partial_match(string externalId, string time, int kills, string outcomeA, string outcomeB, string otherPlayer)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await Post(client, Payload(externalId, kills, time: time, outcomeA: outcomeA, outcomeB: outcomeB, otherPlayer: otherPlayer));
        await AssertProblem(response, 400);
        var stats = await GetJson(client, $"/api/players/{PlayerA}/stats");
        Assert.Equal(0, stats.GetProperty("totalMatches").GetInt32());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{broken")]
    [InlineData("{\"externalMatchId\":\"X\",\"playedAtUtc\":\"2026-10-21T12:00:00Z\",\"participants\":null}")]
    [InlineData("{\"externalMatchId\":\"X\",\"playedAtUtc\":\"2026-10-21T12:00:00Z\",\"participants\":[null,null]}")]
    [InlineData("{\"externalMatchId\":\"X\",\"playedAtUtc\":\"2026-10-21T12:00:00Z\",\"participants\":[]}")]
    public async Task Malformed_missing_and_null_fields_are_400(string json)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        TestTokens.Authorize(client);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/api/matches", content, CancellationToken.None);
        await AssertProblem(response, 400);
    }

    [Fact]
    public async Task All_draw_and_five_minute_boundary_are_valid()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await Post(client, Payload("DRAW", time: "2026-10-22T12:05:00+00:00", outcomeA: "Draw", outcomeB: "Draw"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stats = await GetJson(client, $"/api/players/{PlayerA}/stats");
        Assert.Equal(1, stats.GetProperty("draws").GetInt32());
        Assert.Equal(0, stats.GetProperty("winRate").GetDecimal());
    }

    [Fact]
    public async Task Cancellation_is_propagated_to_application()
    {
        await using var factory = CreateFactory();
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = scope.ServiceProvider.GetRequiredService<ShooterRecordsLab.Application.Players.PlayerService>();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(Guid.Parse(PlayerA), cancellation.Token));
    }

    [Fact]
    public async Task Unknown_failure_returns_safe_500()
    {
        using var logs = new CapturedLogs();
        await using var factory = CreateFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRecordsRepository>();
            services.AddScoped<IRecordsRepository, BrokenRepository>();
            services.AddSingleton<ILoggerProvider>(logs);
        }));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/players/{PlayerA}", CancellationToken.None);
        var json = await AssertProblem(response, 500);
        Assert.DoesNotContain("private-db-password", json.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(logs.Messages, message => message.Contains("private-db-password", StringComparison.Ordinal));
        Assert.Contains(logs.Messages, message => message.Contains("status 500", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Production_rejects_unauthenticated_writes()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.WithoutExternalDatabase().UseEnvironment("Production"));
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/matches", Payload("production"), CancellationToken.None);
        await AssertProblem(response, 401);
    }

    [Theory]
    [InlineData(1, 400)]
    [InlineData(2, 201)]
    [InlineData(20, 201)]
    [InlineData(21, 400)]
    public async Task Participant_count_limits_and_numeric_boundaries(int count, int expectedStatus)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ids = Enumerable.Range(1, count).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();
        factory.Services.GetRequiredService<InMemoryRecordsRepository>().Seed(ids.Select(id => new Player(id, "FixturePlayer", Now)));
        var body = new
        {
            externalMatchId = "LIMITS",
            playedAtUtc = "2026-10-21T12:30:00Z",
            participants = ids.Select(id => new { playerId = id, kills = 10000, deaths = 10000, outcome = "Draw" }).ToArray()
        };
        using var response = await Post(client, body);
        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    [Theory]
    [InlineData(64, 201)]
    [InlineData(65, 400)]
    public async Task External_id_length_is_enforced(int length, int expectedStatus)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await Post(client, Payload(new string('A', length)));
        Assert.Equal(expectedStatus, (int)response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder => builder.WithoutExternalDatabase().UseEnvironment("Development").ConfigureServices(services =>
        {
            var repository = new InMemoryRecordsRepository();
            repository.Seed([
                new Player(Guid.Parse(PlayerA), "PracticePlayerA", Now.AddDays(-1)),
                new Player(Guid.Parse(PlayerB), "PracticePlayerB", Now.AddDays(-1))
            ]);
            services.RemoveAll<IRecordsRepository>();
            services.AddScoped<IRecordsRepository>(services => services.GetRequiredService<InMemoryRecordsRepository>());
            services.RemoveAll<InMemoryRecordsRepository>();
            services.AddSingleton(repository);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock());
        }));

    private static object Payload(string externalId, int kills = 8, bool reverse = false, string time = "2026-10-21T12:30:00Z",
        string outcomeA = "Win", string outcomeB = "Loss", string otherPlayer = PlayerB)
    {
        var participants = new[]
        {
            new { playerId = PlayerA, kills, deaths = 0, outcome = outcomeA },
            new { playerId = otherPlayer, kills = 3, deaths = 8, outcome = outcomeB }
        };
        return new { externalMatchId = externalId, playedAtUtc = time, participants = reverse ? participants.Reverse().ToArray() : participants };
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, object payload)
    {
        TestTokens.Authorize(client);
        return client.PostAsJsonAsync("/api/matches", payload, CancellationToken.None);
    }

    private static async Task<JsonElement> GetJson(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await Read(response);
    }

    private static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None));
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> AssertProblem(HttpResponseMessage response, int expectedStatus)
    {
        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await Read(response);
        Assert.Equal(expectedStatus, json.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(json.GetProperty("detail").GetString()));
        return json;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class BrokenRepository : IRecordsRepository
    {
        public Task<ShooterRecordsLab.Application.Players.PlayerView?> FindPlayerAsync(Guid id, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("private-db-password");
        public Task<ShooterRecordsLab.Application.Players.PageView<ShooterRecordsLab.Application.Players.PlayerMatchView>> ListPlayerMatchesAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ShooterRecordsLab.Application.Players.PlayerStatsView> GetPlayerStatsAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ShooterRecordsLab.Application.Matches.MatchView?> FindMatchAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ShooterRecordsLab.Application.Matches.RegistrationResult> RegisterMatchAsync(Match match, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
