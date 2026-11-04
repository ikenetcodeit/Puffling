namespace Puffling.Core.Behavior;

/// <summary>
/// What the user appears to be doing, as inferred by the host app from running processes and
/// the foreground window. Core never guesses this itself — it only reacts to whatever
/// <see cref="Brain.SystemActivity"/> is set to, so detection heuristics stay out of the
/// platform-independent simulation.
///
/// The <c>Agent*</c> values distinguish real coding-agent lifecycle states (see
/// <see cref="Puffling.Core.Behavior.AgentLifecycle"/> and docs/agent-lifecycle.md for the
/// tool-agnostic event file any script or CLI wrapper can report them through) from the coarse
/// "some agent process exists" guess, which still only ever yields <see cref="AgentRunning"/>.
/// </summary>
public enum SystemActivity { None, Music, Movie, Coding, AgentRunning, AgentIdle, AgentWaitingForInput, AgentSucceeded, AgentFailed }
