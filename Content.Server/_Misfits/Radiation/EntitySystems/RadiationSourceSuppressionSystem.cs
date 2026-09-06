// #Misfits Add - Glowing ghouls can safely work around non-ghouls while wearing a
// radiation suit: the suit suppresses their innate RadiationSource emission.
using Content.Server._Misfits.Radiation.Components;
using Content.Server.Radiation.Systems;
using Content.Shared.Inventory.Events;
using Content.Shared.Radiation.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._Misfits.Radiation.EntitySystems;

/// <summary>
///     Disables a mob's <see cref="RadiationSourceComponent"/> while a
///     <see cref="RadiationSourceSuppressionComponent"/> item (e.g. a radiation suit)
///     is equipped in an allowed slot, and re-enables it on unequip. Non-emitting
///     wearers are unaffected. A mob can only wear one outerClothing, so a single
///     suppressor is always the whole story.
/// </summary>
public sealed class RadiationSourceSuppressionSystem : EntitySystem
{
    [Dependency] private readonly RadiationSystem _radiation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadiationSourceSuppressionComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<RadiationSourceSuppressionComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<RadiationSourceSuppressionComponent> ent, ref GotEquippedEvent args)
    {
        // Only apply when worn in an allowed slot (e.g. outerClothing) and the
        // wearer actually emits radiation (glowing ghouls).
        if ((args.SlotFlags & ent.Comp.AllowedSlots) == 0)
            return;

        if (TryComp<RadiationSourceComponent>(args.Equipee, out _))
            _radiation.SetSourceEnabled(args.Equipee, false);
    }

    private void OnUnequipped(Entity<RadiationSourceSuppressionComponent> ent, ref GotUnequippedEvent args)
    {
        if (TryComp<RadiationSourceComponent>(args.Equipee, out _))
            _radiation.SetSourceEnabled(args.Equipee, true);
    }
}
