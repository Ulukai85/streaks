using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Infrastructure;

public sealed class UniqueConstraintExceptionHandler : IExceptionHandler
{
    // The only unique constraint a normal request can hit today - Users.NormalizedUserName and
    // RefreshTokens.TokenHash are both effectively unreachable in practice (single seeded user,
    // no registration endpoint; a SHA-256 collision). Named explicitly so a future constraint
    // violation gets a generic conflict instead of this Completions-specific message.
    private const string CompletionPeriodConstraintName = "IX_Completions_ChallengeId_PeriodStart";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pgException })
        {
            return false;
        }

        var detail = pgException.ConstraintName == CompletionPeriodConstraintName
            ? "This period has already been completed."
            : "This record already exists.";

        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = detail,
            },
        });
    }
}
