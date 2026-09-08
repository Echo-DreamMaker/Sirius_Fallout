namespace Content.Server.SuperMutant;

/// <summary>
/// Server-side marker attached to an item while it is held by a creature with
/// <see cref="Content.Shared._N14.SuperMutant.SuperMutantComponent"/>. Mirrors the old HeldByOni,
/// used to apply stamina handling while the item is held.
/// </summary>
[RegisterComponent]
public sealed partial class HeldBySuperMutantComponent : Component
{
    public EntityUid Holder = default!;
}
