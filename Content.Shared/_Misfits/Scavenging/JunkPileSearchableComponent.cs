namespace Content.Shared._Misfits.Scavenging;

/// <summary>
/// Marks a junk pile as searchable for loot, then unavailable until its shared cooldown expires.
/// </summary>
[RegisterComponent]
public sealed partial class JunkPileSearchableComponent : Component
{
    [DataField("searchDuration")]
    public float SearchDuration = 3f;

    [DataField("cooldownSeconds")]
    public float CooldownSeconds = 3600f;

    public TimeSpan CooldownEnd = TimeSpan.Zero;

    /// <summary>
    /// Whether Junker Finder has already uncovered the one extra find available during this cooldown.
    /// </summary>
    public bool FinderSearchUsed;
}

[Serializable, NetSerializable]
public enum JunkPileVisuals : byte
{
    Depleted,
}
