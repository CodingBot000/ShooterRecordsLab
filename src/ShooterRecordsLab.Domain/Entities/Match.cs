namespace ShooterRecordsLab.Domain.Entities;

public sealed class Match
{
    private Match() { }

    public Match(Guid id, string externalMatchId, DateTimeOffset playedAtUtc, DateTimeOffset createdAtUtc,
        string payloadFingerprint, IEnumerable<MatchResult> results)
    {
        Id = id;
        ExternalMatchId = externalMatchId;
        PlayedAtUtc = playedAtUtc;
        CreatedAtUtc = createdAtUtc;
        PayloadFingerprint = payloadFingerprint;
        Results = results.ToList();
    }

    public Guid Id { get; private set; }
    public string ExternalMatchId { get; private set; } = "";
    public DateTimeOffset PlayedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string PayloadFingerprint { get; private set; } = "";
    public IReadOnlyCollection<MatchResult> Results { get; private set; } = new List<MatchResult>();
}
