using System.Text.Json;

namespace Puffling.Core.Behavior;

/// <summary>
/// Parses the generic, tool-agnostic "agent lifecycle" event file that an external script or CLI
/// wrapper can write to report real agent state — idle, running, waiting for input, succeeded, or
/// failed — instead of Puffling only ever guessing "running" from a process being present. See
/// docs/agent-lifecycle.md for the full protocol. Parsing lives in Core, alongside everything else
/// that doesn't need a real file system watcher, so it stays unit-testable; the host app owns
/// finding and reading the actual file (its well-known path is a Windows convention, not a
/// platform-independent concern).
/// </summary>
public static class AgentLifecycle
{
    /// <summary>How long a reported state stays valid before Puffling falls back to the coarse
    /// process-presence heuristic — guards against a crashed or forgotten script leaving a stale
    /// "succeeded" or "failed" showing forever.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Parses one JSON object shaped like <c>{"state":"running","updatedAt":"2026-11-04T12:00:00Z"}</c>.
    /// Both fields are required: a missing/unparsable <c>updatedAt</c> is treated the same as a
    /// stale one, so a script that forgets to stamp its writes can never get stuck showing a
    /// one-off reaction forever. Returns null for malformed JSON, a missing/unrecognized
    /// <c>state</c>, a missing/invalid <c>updatedAt</c>, or one older than <see cref="MaxAge"/>
    /// relative to <paramref name="now"/> (defaults to <see cref="DateTimeOffset.UtcNow"/>).
    /// </summary>
    public static SystemActivity? TryParse(string json, DateTimeOffset? now = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("state", out var stateEl) || stateEl.ValueKind != JsonValueKind.String)
                return null;

            SystemActivity? activity = stateEl.GetString() switch
            {
                "idle" => SystemActivity.AgentIdle,
                "running" => SystemActivity.AgentRunning,
                "waitingForInput" => SystemActivity.AgentWaitingForInput,
                "succeeded" => SystemActivity.AgentSucceeded,
                "failed" => SystemActivity.AgentFailed,
                _ => null,
            };
            if (activity is null) return null;

            if (!root.TryGetProperty("updatedAt", out var tsEl) || tsEl.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParse(tsEl.GetString(), out var updatedAt))
                return null;
            if ((now ?? DateTimeOffset.UtcNow) - updatedAt > MaxAge) return null;

            return activity;
        }
        catch (JsonException) { return null; }
    }
}
