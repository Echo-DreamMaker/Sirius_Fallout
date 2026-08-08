using Robust.Shared.GameStates;

namespace Content.Shared._Sirius.NPC.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class SiriusFollowerComponent : Component
{
    [DataField]
    public EntityUid? Commander;

    [DataField]
    public bool IsFollowing = false;

    [DataField]
    public string OriginalRootTask = string.Empty;

    [DataField]
    public bool WasAutoHeld = false;

    [DataField]
    public float NoPathAccumulator = 0f;

    [DataField]
    public float NoPathTimeoutSeconds = 15f;

    [DataField]
    public bool IsTamed = false;

    [DataField]
    public EntityUid? Tamer;

    [DataField]
    public List<EntityUid> PetActionEntities = new()!;

    [DataField]
    public bool IsStaying = false;

    [DataField]
    public bool AttackMode = false;
}
