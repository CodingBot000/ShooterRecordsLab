using Microsoft.AspNetCore.Mvc;
using ShooterRecordsLab.Application.Rankings;

namespace ShooterRecordsLab.Api.Controllers;

[ApiController]
[Route("api/rankings")]
public sealed class RankingsController(RankingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RankingsResponse>> Get(CancellationToken cancellationToken, [FromQuery] int top = 20)
    {
        var rows = await service.GetTopAsync(top, cancellationToken);
        return Ok(new RankingsResponse(rows.Select(row => new RankingResponse(row.Rank, row.PlayerId, row.DisplayName, row.Matches, row.Wins, row.Kills)).ToArray(), top));
    }
}

public sealed record RankingResponse(long Rank, Guid PlayerId, string DisplayName, long Matches, long Wins, long Kills);
public sealed record RankingsResponse(IReadOnlyList<RankingResponse> Items, int Top);
