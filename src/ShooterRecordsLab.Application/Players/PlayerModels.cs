using ShooterRecordsLab.Domain.Enums;
using ShooterRecordsLab.Domain.Rules;

namespace ShooterRecordsLab.Application.Players;

public sealed record PlayerView(Guid PlayerId, string DisplayName, DateTimeOffset CreatedAtUtc);
public sealed record PlayerMatchView(Guid MatchId, DateTimeOffset PlayedAtUtc, int Kills, int Deaths, Outcome Outcome);
public sealed record PageView<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount);
public sealed record PlayerStatsView(Guid PlayerId, long TotalMatches, long Wins, long Losses, long Draws, long Kills, long Deaths)
{
    public decimal? WinRate => Statistics.Ratio(Wins, TotalMatches);
    public decimal? KdRatio => Statistics.Ratio(Kills, Deaths);
}
