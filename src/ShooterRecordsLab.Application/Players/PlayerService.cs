using ShooterRecordsLab.Application.Abstractions;

namespace ShooterRecordsLab.Application.Players;

public sealed class PlayerService(IRecordsRepository repository)
{
    public async Task<PlayerView> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindPlayerAsync(id, cancellationToken)
        ?? throw new RecordsException("PLAYER_NOT_FOUND", "Player was not found.");

    public async Task<PageView<PlayerMatchView>> MatchesAsync(Guid id, int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            throw new RecordsException("INVALID_PAGINATION", "page must be positive and pageSize must be between 1 and 100.");
        }

        await GetAsync(id, cancellationToken);
        return await repository.ListPlayerMatchesAsync(id, page, pageSize, cancellationToken);
    }

    public async Task<PlayerStatsView> StatsAsync(Guid id, CancellationToken cancellationToken)
    {
        await GetAsync(id, cancellationToken);
        return await repository.GetPlayerStatsAsync(id, cancellationToken);
    }
}
