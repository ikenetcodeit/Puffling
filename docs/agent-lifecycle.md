# Agent lifecycle events (optional, external integration)

Puffling's default "a coding agent is running" reaction only knows that a known agent process
(`claude`, `codex`, `aider`, `ollama`, ...) exists somewhere on the machine — it can't tell idle
from running, or a session that's waiting on you from one that just finished successfully or
failed. That coarse guess is still the fallback used whenever nothing more specific is available.

For a richer reaction, any script, CLI wrapper, or editor integration can report real lifecycle
state by writing a small JSON file. Nothing in this repository writes this file today — it's a
documented, tool-agnostic protocol for external integrations to opt into.

## File location

```
%LOCALAPPDATA%\Puffling\agent-lifecycle.json
```

Same base folder Puffling already uses for its own log file and the Microsoft sign-in cache.
Puffling polls this file on the same cadence as its other system-activity detection (a few
seconds) — no need to signal it any other way.

## Format

```json
{ "state": "running", "updatedAt": "2026-11-04T12:00:00Z" }
```

- `state` — one of `idle`, `running`, `waitingForInput`, `succeeded`, `failed`. Any other value is
  ignored.
- `updatedAt` — an ISO-8601 timestamp (UTC recommended) of when the state was last reported. This
  field is **required**: a missing or unparsable timestamp is treated as invalid, and a timestamp
  older than 90 seconds is treated as stale. Both fall back to the coarse process-presence guess,
  so a crashed or forgotten script can never leave Puffling stuck celebrating or puzzled forever.
  In practice this means a long-running or waiting state needs to be re-written periodically (e.g.
  every 30-60 seconds) to stay current.

Write the whole file atomically (write to a temp file in the same folder, then rename over the
target) so Puffling never reads a half-written file.

## What each state does

| `state`           | Reaction                                                          |
| ------------------ | ------------------------------------------------------------------ |
| `idle`             | Nothing — same as no agent activity at all.                       |
| `running`          | The existing alert, sparkly "supervising" pose.                   |
| `waitingForInput`  | A restless, impatient pose; Green sometimes wanders off instead.  |
| `succeeded`        | A one-off, bouncy celebration.                                    |
| `failed`           | A one-off, puzzled head-tilt.                                     |

`succeeded`/`failed` are one-off events: write the file once when the event happens, with a fresh
`updatedAt`. Puffling plays the matching reaction once and then settles back to idle once the file
goes stale or is updated to a different state.

See `Puffling.Core.Behavior.AgentLifecycle.TryParse` for the exact parsing rules and
`Puffling.App.Platform.SystemActivityDetector` for how the file is read and merged with the coarse
process/window heuristic.
