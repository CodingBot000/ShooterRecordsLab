using ShooterRecordsLab.Application;
using ShooterRecordsLab.Application.Abstractions;
using ShooterRecordsLab.Application.Matches;
using ShooterRecordsLab.Application.Players;
using ShooterRecordsLab.Domain.Entities;
using ShooterRecordsLab.Domain.Enums;

namespace ShooterRecordsLab.IntegrationTests.Fakes;

public sealed class InMemoryRecordsRepository : IRecordsRepository
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, Player> players = [];
    private readonly Dictionary<Guid, Match> matches = [];

    public void Seed(IEnumerable<Player> values)
    {
        lock (gate)
        {
            foreach (var player in values)
            {
                players.TryAdd(player.Id, player);
            }
        }
    }

    public Task<PlayerView?> FindPlayerAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(players.TryGetValue(id, out var player)
                ? new PlayerView(player.Id, player.DisplayName, player.CreatedAtUtc) : null);
        }
    }

    public Task<PageView<PlayerMatchView>> ListPlayerMatchesAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var rows = matches.Values.SelectMany(m => m.Results.Where(r => r.PlayerId == id)
                .Select(r => new PlayerMatchView(m.Id, m.PlayedAtUtc, r.Kills, r.Deaths, r.Outcome)))
                .OrderByDescending(r => r.PlayedAtUtc).ThenByDescending(r => r.MatchId).ToArray();
            var offset = (long)(page - 1) * pageSize;
            IReadOnlyList<PlayerMatchView> items = offset >= rows.Length ? [] : rows.Skip((int)offset).Take(pageSize).ToArray();
            return Task.FromResult(new PageView<PlayerMatchView>(items, page, pageSize, rows.LongLength));
        }
    }

    public Task<PlayerStatsView> GetPlayerStatsAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var rows = matches.Values.SelectMany(m => m.Results).Where(r => r.PlayerId == id).ToArray();
            return Task.FromResult(new PlayerStatsView(id, rows.LongLength, rows.LongCount(r => r.Outcome == Outcome.Win),
                rows.LongCount(r => r.Outcome == Outcome.Loss), rows.LongCount(r => r.Outcome == Outcome.Draw),
                rows.Sum(r => (long)r.Kills), rows.Sum(r => (long)r.Deaths)));
        }
    }

    public Task<MatchView?> FindMatchAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return Task.FromResult(matches.TryGetValue(id, out var match) ? ToView(match) : null);
        }
    }

    public Task<RegistrationResult> RegisterMatchAsync(Match match, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            var existing = matches.Values.SingleOrDefault(m => m.ExternalMatchId == match.ExternalMatchId);
            if (existing is not null)
            {
                if (existing.PayloadFingerprint != match.PayloadFingerprint)
                {
                    throw new RecordsException("MATCH_ID_CONFLICT", "externalMatchId already exists with different content.");
                }

                return Task.FromResult(new RegistrationResult(existing.Id, existing.ExternalMatchId, false));
            }

            if (match.Results.Any(r => !players.ContainsKey(r.PlayerId)))
            {
                throw new RecordsException("INVALID_PARTICIPANTS", "All participants must be existing players.");
            }

            matches.Add(match.Id, match);
            return Task.FromResult(new RegistrationResult(match.Id, match.ExternalMatchId, true));
        }
    }

    private static MatchView ToView(Match match) => new(match.Id, match.ExternalMatchId, match.PlayedAtUtc,
        match.Results.OrderBy(r => r.PlayerId).Select(r => new ParticipantInput(r.PlayerId, r.Kills, r.Deaths, r.Outcome)).ToArray());
}
