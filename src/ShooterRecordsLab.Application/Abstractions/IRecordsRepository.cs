using ShooterRecordsLab.Application.Matches;
using ShooterRecordsLab.Application.Players;
using ShooterRecordsLab.Domain.Entities;

namespace ShooterRecordsLab.Application.Abstractions;

public interface IRecordsRepository
{
    Task<PlayerView?> FindPlayerAsync(Guid id, CancellationToken cancellationToken);
    Task<PageView<PlayerMatchView>> ListPlayerMatchesAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken);
    Task<PlayerStatsView> GetPlayerStatsAsync(Guid id, CancellationToken cancellationToken);
    Task<MatchView?> FindMatchAsync(Guid id, CancellationToken cancellationToken);
    Task<RegistrationResult> RegisterMatchAsync(Match match, CancellationToken cancellationToken);
}
