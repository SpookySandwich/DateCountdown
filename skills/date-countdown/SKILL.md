---
name: date-countdown
description: Create a countdown in the Date Countdown Windows app when the user asks to track the days or hours remaining until a date such as a deadline, exam, launch, trip, or event. Runs the datecountdown command installed with the Microsoft Store app.
---

# Date Countdown

Add countdowns with the `datecountdown` command. It is on PATH when the Date Countdown app is installed from the Microsoft Store, and it works whether or not the app is running: it talks to the running app, or updates shared storage directly when the app is closed.

## Add a countdown

```
datecountdown add --title "Submit report" --at "2026-08-01T17:00:00+08:00" --timezone "Asia/Shanghai" --request-id "97eabec38c7b43cbb7e121d0d4fbfdca" --json
```

- `--title`: up to 80 characters.
- `--at`: RFC 3339 timestamp with `Z` or an explicit UTC offset. Resolve relative dates like "next Friday" against the user's local time zone before building the timestamp.
- `--timezone`: optional IANA or Windows time-zone identifier. Set it when the deadline belongs to a specific zone, such as a conference submission in `AoE`.
- `--request-id`: optional 32-digit lowercase hexadecimal id (a GUID without dashes). Generate a fresh one per new countdown and reuse it on retries; repeating a request id never creates a duplicate.
- `--json`: write a machine-readable result to stdout.

## Result

With `--json` the command prints one object: `isSuccess`, `code`, and on success `countdownId`, `title`, `targetDate`, and `targetTimeZoneId`.

Exit codes: `0` success, `2` usage error, `3` validation error (`code` names the reason, such as `TargetDateInPast` or `InvalidTimeZone`), `4` runtime error (such as `PackageUnavailable` when the Store app is not installed).
