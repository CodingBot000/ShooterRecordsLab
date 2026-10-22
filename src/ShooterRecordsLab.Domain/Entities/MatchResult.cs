using ShooterRecordsLab.Domain.Enums;

namespace ShooterRecordsLab.Domain.Entities;

public sealed class MatchResult
{
    private MatchResult() { }

    public MatchResult(Guid matchId, Guid playerId, int kills, int deaths, Outcome outcome)
    {
        if (matchId == Guid.Empty || playerId == Guid.Empty || kills is < 0 or > 10000 || deaths is < 0 or > 10000 || !Enum.IsDefined(outcome))
        {
            throw new ArgumentException("Invalid participant result.");
        }

        Id = Guid.NewGuid();
        MatchId = matchId;
        PlayerId = playerId;
        Kills = kills;
        Deaths = deaths;
        Outcome = outcome;
    }

    public Guid Id { get; private set; }
    public Guid MatchId { get; private set; }
    public Guid PlayerId { get; private set; }
    public int Kills { get; private set; }
    public int Deaths { get; private set; }
    public Outcome Outcome { get; private set; }
}
