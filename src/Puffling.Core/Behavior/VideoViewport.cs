using Puffling.Core.Movement;

namespace Puffling.Core.Behavior;

/// <summary>
/// A detected video-playback region, set by the host app from <see cref="Brain.Video"/>.
/// <see cref="Fullscreen"/> means <see cref="Bounds"/> is the whole monitor the video occupies
/// (interested companions may gather on another display and face it); otherwise it's the playing
/// window's rectangle, which wandering/travel steer clear of and movement near slows down for.
/// </summary>
public readonly record struct VideoViewport(RectI Bounds, bool Fullscreen);
