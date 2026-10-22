namespace ShooterRecordsLab.Application.Rankings;

public sealed class RankingRow
{
    public long Rank { get; set; }
    public Guid PlayerId { get; set; }
    public string DisplayName { get; set; } = "";
    public long Matches { get; set; }
    public long Wins { get; set; }
    public long Kills { get; set; }
}
