using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ShooterRecordsLab.Application.Abstractions;
using ShooterRecordsLab.Domain.Entities;
using ShooterRecordsLab.Domain.Enums;
using Match = ShooterRecordsLab.Domain.Entities.Match;

namespace ShooterRecordsLab.Application.Matches;

public sealed partial class MatchService(IRecordsRepository repository, TimeProvider clock)
{
    public async Task<MatchView> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindMatchAsync(id, cancellationToken)
        ?? throw new RecordsException("MATCH_NOT_FOUND", "Match was not found.");

    public async Task<RegistrationResult> RegisterAsync(RegisterMatchInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var externalId = input.ExternalMatchId?.Trim();
        if (externalId is null || !ExternalIdPattern().IsMatch(externalId))
        {
            throw new RecordsException("INVALID_EXTERNAL_MATCH_ID", "externalMatchId must contain 1..64 ASCII letters, digits, '-' or '_'.");
        }

        externalId = externalId.ToUpperInvariant();
        var now = clock.GetUtcNow();
        if (input.PlayedAtUtc.Offset != TimeSpan.Zero || input.PlayedAtUtc > now.AddMinutes(5))
        {
            throw new RecordsException("INVALID_PLAYED_AT", "playedAtUtc must be UTC and no more than five minutes in the future.");
        }

        var participants = input.Participants;
        if (participants is null || participants.Count is < 2 or > 20 ||
            participants.Any(p => p.PlayerId == Guid.Empty || p.Kills is < 0 or > 10000 || p.Deaths is < 0 or > 10000 || !Enum.IsDefined(p.Outcome)) ||
            participants.Select(p => p.PlayerId).Distinct().Count() != participants.Count)
        {
            throw new RecordsException("INVALID_PARTICIPANTS", "Provide 2..20 distinct valid players with kills and deaths in 0..10000.");
        }

        var allDraw = participants.All(p => p.Outcome == Outcome.Draw);
        var decisive = participants.All(p => p.Outcome != Outcome.Draw) &&
            participants.Any(p => p.Outcome == Outcome.Win) && participants.Any(p => p.Outcome == Outcome.Loss);
        if (!allDraw && !decisive)
        {
            throw new RecordsException("INVALID_PARTICIPANTS", "Outcomes must be all Draw, or include both Win and Loss without Draw.");
        }

        var sorted = participants.OrderBy(p => p.PlayerId).ToArray();
        var canonical = new StringBuilder().Append(externalId).Append('|')
            .Append(input.PlayedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture));
        foreach (var p in sorted)
        {
            canonical.Append('|').Append(p.PlayerId.ToString("D")).Append(':')
                .Append(p.Kills.ToString(CultureInfo.InvariantCulture)).Append(':')
                .Append(p.Deaths.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.Outcome);
        }

        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        var matchId = Guid.NewGuid();
        var match = new Match(matchId, externalId, input.PlayedAtUtc, now, fingerprint,
            sorted.Select(p => new MatchResult(matchId, p.PlayerId, p.Kills, p.Deaths, p.Outcome)));
        return await repository.RegisterMatchAsync(match, cancellationToken);
    }

    [GeneratedRegex("\\A[A-Za-z0-9_-]{1,64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex ExternalIdPattern();
}
