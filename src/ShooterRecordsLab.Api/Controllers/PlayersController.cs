using Microsoft.AspNetCore.Mvc;
using ShooterRecordsLab.Api.Contracts;
using ShooterRecordsLab.Application.Players;

namespace ShooterRecordsLab.Api.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController(PlayerService service) : ControllerBase
{
    [HttpGet("{playerId:guid}")]
    public async Task<ActionResult<PlayerResponse>> Get(Guid playerId, CancellationToken cancellationToken)
    {
        ValidateId(playerId);
        var player = await service.GetAsync(playerId, cancellationToken);
        return Ok(new PlayerResponse(player.PlayerId, player.DisplayName, player.CreatedAtUtc));
    }

    [HttpGet("{playerId:guid}/matches")]
    public async Task<ActionResult<PageResponse<PlayerMatchResponse>>> Matches(Guid playerId, CancellationToken cancellationToken,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        ValidateId(playerId);
        var result = await service.MatchesAsync(playerId, page, pageSize, cancellationToken);
        return Ok(new PageResponse<PlayerMatchResponse>(result.Items.Select(r =>
            new PlayerMatchResponse(r.MatchId, r.PlayedAtUtc, r.Kills, r.Deaths, r.Outcome)).ToArray(), result.Page, result.PageSize, result.TotalCount));
    }

    [HttpGet("{playerId:guid}/stats")]
    public async Task<ActionResult<PlayerStatsResponse>> Stats(Guid playerId, CancellationToken cancellationToken)
    {
        ValidateId(playerId);
        var s = await service.StatsAsync(playerId, cancellationToken);
        return Ok(new PlayerStatsResponse(s.PlayerId, s.TotalMatches, s.Wins, s.Losses, s.Draws, s.Kills, s.Deaths, s.WinRate, s.KdRatio));
    }

    private static void ValidateId(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ShooterRecordsLab.Application.RecordsException("INVALID_ID", "An empty UUID is not valid.");
        }
    }
}
