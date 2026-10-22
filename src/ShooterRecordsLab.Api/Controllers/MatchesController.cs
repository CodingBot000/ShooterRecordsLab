using Microsoft.AspNetCore.Mvc;
using ShooterRecordsLab.Api.Contracts;
using ShooterRecordsLab.Application;
using ShooterRecordsLab.Application.Matches;

namespace ShooterRecordsLab.Api.Controllers;

[ApiController]
[Route("api/matches")]
public sealed class MatchesController(MatchService service, ILogger<MatchesController> logger, IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("{matchId:guid}")]
    public async Task<ActionResult<MatchResponse>> Get(Guid matchId, CancellationToken cancellationToken)
    {
        if (matchId == Guid.Empty)
        {
            throw new RecordsException("INVALID_ID", "An empty UUID is not valid.");
        }

        var match = await service.GetAsync(matchId, cancellationToken);
        return Ok(new MatchResponse(match.MatchId, match.ExternalMatchId, match.PlayedAtUtc,
            match.Participants.Select(p => new ParticipantResponse(p.PlayerId, p.Kills, p.Deaths, p.Outcome)).ToArray()));
    }

    [HttpPost]
    public async Task<ActionResult<RegisterMatchResponse>> Register(RegisterMatchRequest request, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        if (request.Participants?.Any(p => p is null) == true)
        {
            throw new RecordsException("INVALID_PARTICIPANTS", "Participants must not contain null entries.");
        }

        var input = new RegisterMatchInput(request.ExternalMatchId, request.PlayedAtUtc,
            request.Participants?.Select(p => new ParticipantInput(p!.PlayerId, p.Kills, p.Deaths, p.Outcome)).ToArray());
        var result = await service.RegisterAsync(input, cancellationToken);
        logger.LogInformation("Match {MatchId} registration completed; created {Created}", result.MatchId, result.Created);
        var response = new RegisterMatchResponse(result.MatchId, result.ExternalMatchId, result.Created);
        return result.Created ? CreatedAtAction(nameof(Get), new { matchId = result.MatchId }, response) : Ok(response);
    }
}
