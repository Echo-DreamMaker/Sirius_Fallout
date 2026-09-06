
namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised directed on a gun when it cycles.
/// Generic EntityEventArgs to make this easier to use with existing verbs/interact events
/// </summary>
[ByRefEvent]
public struct GunCycledEvent<T>(EntityUid used, EntityUid user, T interactEvent) where T : EntityEventArgs
{
    public EntityUid Used = used;
    public EntityUid User = user;
    //public BallisticAmmoProviderComponent BallisticComp = ballisticComp;
    public T InteractEv = interactEvent;
}
