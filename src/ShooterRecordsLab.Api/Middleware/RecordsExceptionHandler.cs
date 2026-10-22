using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShooterRecordsLab.Application;

namespace ShooterRecordsLab.Api.Middleware;

internal sealed class RecordsExceptionHandler(IProblemDetailsService problems, ILogger<RecordsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && context.RequestAborted.IsCancellationRequested)
        {
            return false;
        }

        var records = exception as RecordsException;
        var status = records?.Code switch
        {
            "PLAYER_NOT_FOUND" or "MATCH_NOT_FOUND" => 404,
            "MATCH_ID_CONFLICT" => 409,
            not null => 400,
            _ => 500
        };
        if (status == 500)
        {
            logger.LogError("Unhandled request failure of type {ExceptionType}; trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        }

        context.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = status == 500 ? "Request failed" : "Request could not be completed",
            Detail = records?.Message ?? "An unexpected error occurred."
        };
        if (records is not null)
        {
            problem.Extensions["code"] = records.Code;
        }

        await problems.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        return true;
    }
}
