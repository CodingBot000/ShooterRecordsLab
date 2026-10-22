using System.Data.SqlTypes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShooterRecordsLab.Application;
using ShooterRecordsLab.Application.Matches;
using ShooterRecordsLab.Domain.Entities;
using ShooterRecordsLab.Domain.Enums;
using ShooterRecordsLab.Infrastructure.Repositories;

namespace ShooterRecordsLab.IntegrationTests;

[Trait("Category", "Database")]
public sealed class SqlServerTests
{
    [Fact]
    public async Task SqlServer_concurrency_atomicity_constraints_and_rankings()
    {
        var settings = DatabaseTestSettings.LoadApproved();
        await using var db = settings.CreateContext();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(CancellationToken.None));
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var prefix = "TEST-" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        var now = DateTimeOffset.UtcNow;
        db.Players.AddRange(new Player(a, "FixtureA", now), new Player(b, "FixtureB", now));
        await db.SaveChangesAsync(CancellationToken.None);
        try
        {
            RegisterMatchInput Input(string suffix, int kills = 8, Guid? player = null) => new(prefix + suffix, now.AddMinutes(-1),
                [new ParticipantInput(a, kills, 3, Outcome.Win), new ParticipantInput(player ?? b, 3, 8, Outcome.Loss)]);
            async Task<RegistrationResult> Register(RegisterMatchInput input)
            {
                await using var context = settings.CreateContext();
                return await new MatchService(new SqlRecordsRepository(context), TimeProvider.System).RegisterAsync(input, CancellationToken.None);
            }

            var tasks = Enumerable.Range(0, 10).Select(_ => Register(Input("-RACE"))).ToArray();
            var registrations = await Task.WhenAll(tasks);
            Assert.Single(registrations, r => r.Created);
            Assert.Single(registrations.Select(r => r.MatchId).Distinct());
            Assert.Equal(1, await db.Matches.CountAsync(m => m.ExternalMatchId == prefix + "-RACE", CancellationToken.None));
            var matchId = registrations[0].MatchId;
            Assert.Equal(2, await db.MatchResults.CountAsync(r => r.MatchId == matchId, CancellationToken.None));
            var conflict = await Assert.ThrowsAsync<RecordsException>(() => Register(Input("-RACE", 9)));
            Assert.Equal("MATCH_ID_CONFLICT", conflict.Code);
            var invalid = await Assert.ThrowsAsync<RecordsException>(() => Register(Input("-MISSING", player: Guid.NewGuid())));
            Assert.Equal("INVALID_PARTICIPANTS", invalid.Code);
            Assert.False(await db.Matches.AnyAsync(m => m.ExternalMatchId == prefix + "-MISSING", CancellationToken.None));

            var repository = new SqlRecordsRepository(db);
            var stats = await repository.GetPlayerStatsAsync(a, CancellationToken.None);
            Assert.Equal(1, stats.TotalMatches);
            Assert.Equal(8, stats.Kills);
            Assert.Equal(1m, stats.WinRate);
            var rows = await repository.ListPlayerMatchesAsync(a, 1, 20, CancellationToken.None);
            Assert.Equal(matchId, Assert.Single(rows.Items).MatchId);
            var detail = await repository.FindMatchAsync(matchId, CancellationToken.None);
            Assert.Equal(2, detail!.Participants.Count);

            var rankings = await new SqlRankingRepository(db).GetTopAsync(100, CancellationToken.None);
            Assert.InRange(rankings.Count, 1, 100);
            Assert.All(rankings, row => Assert.True(row.Matches > 0));
            Assert.Equal(Enumerable.Range(1, rankings.Count).Select(i => (long)i), rankings.Select(r => r.Rank));

            var sortedRankings = rankings.OrderByDescending(r => r.Wins).ThenByDescending(r => r.Kills)
                .ThenBy(r => new SqlGuid(r.PlayerId)).Select(r => r.PlayerId);
            Assert.Equal(sortedRankings, rankings.Select(r => r.PlayerId));
            var top = await new SqlRankingRepository(db).GetTopAsync(1, CancellationToken.None);
            Assert.Equal(rankings[0].PlayerId, Assert.Single(top).PlayerId);
            var invalidTop = await Assert.ThrowsAsync<SqlException>(() => new SqlRankingRepository(db).GetTopAsync(101, CancellationToken.None));
            Assert.Equal(50001, invalidTop.Number);

            var check = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.MatchResults SET Kills = {-1} WHERE MatchId = {matchId} AND PlayerId = {a}", CancellationToken.None));
            Assert.Equal(547, check.Number);
            var rollbackId = Guid.NewGuid();
            await using (var rollback = settings.CreateContext())
            {
                rollback.Matches.Add(new Match(rollbackId, prefix + "-ROLLBACK", now, now, new string('A', 64),
                    [new MatchResult(rollbackId, a, 1, 1, Outcome.Win), new MatchResult(rollbackId, Guid.NewGuid(), 1, 1, Outcome.Loss)]));
                await Assert.ThrowsAsync<DbUpdateException>(() => rollback.SaveChangesAsync(CancellationToken.None));
            }

            Assert.False(await db.Matches.AnyAsync(m => m.Id == rollbackId, CancellationToken.None));
            Assert.False(await db.MatchResults.AnyAsync(r => r.MatchId == rollbackId, CancellationToken.None));

            // Direct writes verify the real database's UNIQUE and FK enforcement.
            await using (var duplicate = settings.CreateContext())
            {
                duplicate.MatchResults.Add(new MatchResult(matchId, a, 0, 0, Outcome.Draw));
                await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync(CancellationToken.None));
            }

            await using (var missingPlayer = settings.CreateContext())
            {
                missingPlayer.MatchResults.Add(new MatchResult(matchId, Guid.NewGuid(), 0, 0, Outcome.Draw));
                await Assert.ThrowsAsync<DbUpdateException>(() => missingPlayer.SaveChangesAsync(CancellationToken.None));
            }
        }
        finally
        {
            // Delete only this fixture's own IDs; never reset/drop a database or truncate tables.
            await using var cleanup = settings.CreateContext();
            await cleanup.Matches.Where(m => m.ExternalMatchId.StartsWith(prefix)).ExecuteDeleteAsync(CancellationToken.None);
            await cleanup.Players.Where(p => p.Id == a || p.Id == b).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
}
