using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ShooterRecordsLab.Application;
using ShooterRecordsLab.Application.Abstractions;
using ShooterRecordsLab.Application.Matches;
using ShooterRecordsLab.Application.Players;
using ShooterRecordsLab.Domain.Entities;
using ShooterRecordsLab.Domain.Enums;
using ShooterRecordsLab.Infrastructure.Persistence;

namespace ShooterRecordsLab.Infrastructure.Repositories;

public sealed class SqlRecordsRepository(ShooterRecordsDbContext db) : IRecordsRepository
{
    public Task<PlayerView?> FindPlayerAsync(Guid id, CancellationToken cancellationToken) => db.Players.AsNoTracking()
        .Where(p => p.Id == id).Select(p => new PlayerView(p.Id, p.DisplayName, p.CreatedAtUtc)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PageView<PlayerMatchView>> ListPlayerMatchesAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = from result in db.MatchResults.AsNoTracking()
                    join match in db.Matches.AsNoTracking() on result.MatchId equals match.Id
                    where result.PlayerId == id
                    select new PlayerMatchView(match.Id, match.PlayedAtUtc, result.Kills, result.Deaths, result.Outcome);
        var count = await query.LongCountAsync(cancellationToken);
        var offset = (long)(page - 1) * pageSize;
        if (offset >= count)
        {
            return new PageView<PlayerMatchView>([], page, pageSize, count);
        }

        // OFFSET is bigint in SQL Server; a valid int page can exceed LINQ Skip(int).
        var rows = await db.Database.SqlQuery<PlayerMatchSqlRow>($"""
            SELECT m.Id AS MatchId, m.PlayedAtUtc, r.Kills, r.Deaths, r.Outcome
            FROM dbo.MatchResults AS r INNER JOIN dbo.Matches AS m ON m.Id = r.MatchId
            WHERE r.PlayerId = {id}
            ORDER BY m.PlayedAtUtc DESC, m.Id DESC
            OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY
            """).ToListAsync(cancellationToken);
        return new PageView<PlayerMatchView>(rows.Select(r => new PlayerMatchView(r.MatchId, r.PlayedAtUtc, r.Kills, r.Deaths, r.Outcome)).ToArray(), page, pageSize, count);
    }

    public async Task<PlayerStatsView> GetPlayerStatsAsync(Guid id, CancellationToken cancellationToken)
    {
        var stats = await db.MatchResults.AsNoTracking().Where(r => r.PlayerId == id).GroupBy(r => r.PlayerId)
            .Select(rows => new PlayerStatsView(rows.Key, rows.LongCount(),
                rows.Sum(r => r.Outcome == Outcome.Win ? 1L : 0L), rows.Sum(r => r.Outcome == Outcome.Loss ? 1L : 0L),
                rows.Sum(r => r.Outcome == Outcome.Draw ? 1L : 0L), rows.Sum(r => (long)r.Kills), rows.Sum(r => (long)r.Deaths)))
            .SingleOrDefaultAsync(cancellationToken);
        return stats ?? new PlayerStatsView(id, 0, 0, 0, 0, 0, 0);
    }

    public async Task<MatchView?> FindMatchAsync(Guid id, CancellationToken cancellationToken)
    {
        var match = await db.Matches.AsNoTracking().Where(m => m.Id == id)
            .Select(m => new { m.Id, m.ExternalMatchId, m.PlayedAtUtc }).SingleOrDefaultAsync(cancellationToken);
        if (match is null)
        {
            return null;
        }

        var results = await db.MatchResults.AsNoTracking().Where(r => r.MatchId == id)
            .Select(r => new ParticipantInput(r.PlayerId, r.Kills, r.Deaths, r.Outcome)).ToListAsync(cancellationToken);
        return new MatchView(match.Id, match.ExternalMatchId, match.PlayedAtUtc, results.OrderBy(r => r.PlayerId).ToArray());
    }

    public async Task<RegistrationResult> RegisterMatchAsync(Match match, CancellationToken cancellationToken)
    {
        var existing = await FindExistingAsync(match.ExternalMatchId, cancellationToken);
        if (existing is not null)
        {
            return Compare(existing, match);
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var playerIds = match.Results.Select(r => r.PlayerId).ToArray();
            var count = await db.Players.AsNoTracking().CountAsync(p => playerIds.Contains(p.Id), cancellationToken);
            if (count != playerIds.Length)
            {
                throw new RecordsException("INVALID_PARTICIPANTS", "All participants must be existing players.");
            }

            db.Matches.Add(match);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RegistrationResult(match.Id, match.ExternalMatchId, true);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Transaction disposal rolls back before querying the winning committed request.
            db.ChangeTracker.Clear();
            existing = await FindExistingAsync(match.ExternalMatchId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return Compare(existing, match);
        }
    }

    private Task<ExistingMatch?> FindExistingAsync(string externalId, CancellationToken cancellationToken) =>
        db.Matches.AsNoTracking().Where(m => m.ExternalMatchId == externalId)
            .Select(m => new ExistingMatch(m.Id, m.ExternalMatchId, m.PayloadFingerprint)).SingleOrDefaultAsync(cancellationToken);

    private static RegistrationResult Compare(ExistingMatch existing, Match match) => existing.PayloadFingerprint == match.PayloadFingerprint
        ? new RegistrationResult(existing.Id, existing.ExternalMatchId, false)
        : throw new RecordsException("MATCH_ID_CONFLICT", "externalMatchId already exists with different content.");

    private sealed record ExistingMatch(Guid Id, string ExternalMatchId, string PayloadFingerprint);
}

public sealed class PlayerMatchSqlRow
{
    public Guid MatchId { get; set; }
    public DateTimeOffset PlayedAtUtc { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public Outcome Outcome { get; set; }
}
