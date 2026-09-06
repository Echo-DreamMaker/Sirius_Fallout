using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Supporter;

/// <summary>
///     Patreon supporter tier for Misfits. Mirrors the tiers shown in the credits window:
///     Silver, Gold, and Nuclear (Nuclear being the highest).
///     <see cref="None"/> means the player is not a supporter.
/// </summary>
[Serializable, NetSerializable]
public enum SupporterTier : byte
{
    None = 0,
    Silver = 1,
    Gold = 2,
    Nuclear = 3
}
