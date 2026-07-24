# 1. Calendar periods over rolling intervals

## Status

Accepted.

## Context

A weekly or monthly challenge needs a rule for what counts as "one period."
The obvious alternative to a fixed calendar period is a rolling interval —
e.g. "once every 7 days since the last completion," or "once every 30 days"
for monthly. Rolling intervals let a user shift their own schedule (do it
early, buy themselves a later deadline next time), and superficially feel
more forgiving.

## Decision

Periods are fixed calendar periods, not rolling intervals. Weekly cadence
uses the ISO week (Monday start); a challenge is satisfied once per ISO
week (e.g. 2026-W31), and Monday resets it regardless of when in the
previous week it was completed. Monthly cadence uses the calendar month.
`Completion.PeriodStart` stores the first day of that period and is the
unique key together with `ChallengeId`.

## Consequences

Calendar periods give every completion a stable, computable unique key
(`ChallengeId, PeriodStart`) with no dependency on completion order or
history, which keeps both the uniqueness constraint and the gaps-and-islands
streak query (§8) tractable. It also matches the mental model users already
have of "did I do it this week" rather than "am I still inside my personal
7-day window." The tradeoff is that completing early does not buy extra
slack — a challenge done on Monday still needs a separate completion the
following Monday — which is intentional: bounded retroactive completion
(§6.3) covers the legitimate "I forgot to log it" case without letting
rolling windows turn the tracker into a negotiable schedule.
