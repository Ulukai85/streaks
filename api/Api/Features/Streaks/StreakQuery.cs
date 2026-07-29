using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Streaks;

public class StreakQuery(AppDbContext db)
{
    public async Task<StreakResult> ForChallenge(
        Guid challengeId, Cadence cadence, DateOnly startsOn,
        DateOnly? archivedAtLocalDateOnly, DateOnly today, CancellationToken ct)
    {
        var referenceOrdinal = PeriodOrdinal.For(archivedAtLocalDateOnly ?? today, cadence);
        var startsOnOrdinal = PeriodOrdinal.For(startsOn, cadence);

        FormattableString sql = CreateSqlQuery(cadence, challengeId, startsOn);

        var row = await db.Database.SqlQuery<StreakRow>(sql)
            .FirstOrDefaultAsync(ct);

        if (row is null) return new StreakResult(0, false, null);

        var isAlive = row.EndsAt == referenceOrdinal
                      || (row.EndsAt == referenceOrdinal - 1 && referenceOrdinal - 1 >= startsOnOrdinal);

        return new StreakResult(row.Length, isAlive, row.LastPeriodStart);
    }

    private static FormattableString CreateSqlQuery(Cadence cadence, Guid challengeId, DateOnly startsOn)
    {
        //language=sql
        return $"""
          WITH ordinals AS (
              SELECT "PeriodStart",
                  CASE {cadence.ToString()}
                      WHEN 'Daily' THEN ("PeriodStart" - DATE '1970-01-01')
                      WHEN 'Weekly' THEN ("PeriodStart" - DATE '1970-01-01') / 7
                      WHEN 'Monthly'
                          THEN EXTRACT(YEAR FROM "PeriodStart")::int * 12 + EXTRACT(MONTH FROM "PeriodStart")::int
                  END AS ord
              FROM "Completions"
              WHERE "ChallengeId" = {challengeId} AND "PeriodStart" >= {startsOn}
          ),
          grouped AS (
              SELECT "PeriodStart", ord, ord - ROW_NUMBER() OVER (ORDER BY ord) AS grp
              FROM ordinals
          )
          SELECT COUNT(*) AS "Length", MAX(ord) AS "EndsAt", MAX("PeriodStart") AS "LastPeriodStart"
          FROM grouped
          GROUP BY grp
          ORDER BY MAX(ord) DESC
          LIMIT 1
          """;
    }
}
