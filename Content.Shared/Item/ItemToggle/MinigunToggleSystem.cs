using Content.Shared.Hands;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;

namespace Content.Shared.Item.ItemToggle;
// TODO: rework
/// Credit to BurgerMoth for original code
/// it has been heavily revised to get around issue that the toggle blocked gun racking
/// Suprisingly more complicated than I originally thought, but it did show me how messy interaction code is
/// gave me more ideas on how I should refactor guns in future.
/// Also please dont add more until I can refactor things to work neatly and be way less hardcoded
/// I already overthink alot when needing to make new additions that needs to somehow work with older content
/// and still be scalable and easy to add onto for the future

/// <summary>
/// This handles toggling guns on and off for the purposes of changing their stats during different active states
/// </summary>
public sealed partial class MinigunToggleSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private MovementSpeedModifierSystem _move = default!;
    public override void Initialize()
    {

        SubscribeLocalEvent<MinigunToggleComponent, MapInitEvent>(OnMapInitRefresh);
        SubscribeLocalEvent<MinigunToggleComponent, GunRefreshModifiersEvent>(ActiveFireRate);
        SubscribeLocalEvent<MinigunToggleComponent, ItemToggledEvent>(OnItemToggled);
        SubscribeLocalEvent<MinigunToggleComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(ActiveSpeedModifier);
    }


    // Firing is controlled solely by the power button on E (ActivateInWorldEvent).
    // Z (UseInHandEvent) is left free so the weapon can be wielded in both hands
    // without turning it on or off.

    private void OnMapInitRefresh(EntityUid uid, MinigunToggleComponent comp, ref MapInitEvent args)
    {
        // Weapons that spawn turned off must still apply their gated fire rate
        // before the first toggle, otherwise they would fire at the base rate.
        _gun.RefreshModifiers(uid);
    }

    private void OnItemToggled(EntityUid uid, MinigunToggleComponent comp, ItemToggledEvent args)
    {
        _gun.RefreshModifiers(uid);
        if (args.User != null)
            _move.RefreshMovementSpeedModifiers(args.User.Value);
    }

    // TODO: should be affected by Special
    /// <summary>
    /// Handles changing the fire rate when the gun is active and inactive.
    /// The power button (E) is a hard on/off switch for every toggle weapon:
    /// off means the weapon cannot fire at all, on means it fires at its
    /// prototype fire rate (or ActivatedFireRate when set).
    /// </summary>
    public void ActiveFireRate(Entity<MinigunToggleComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var comp = Comp<ItemToggleComponent>(ent.Owner);
        if (!comp.Activated)
        {
            args.FireRate = 0f;
            return;
        }

        if (ent.Comp.ActivatedFireRate > 0f)
            args.FireRate = ent.Comp.ActivatedFireRate;
    }

    // TODO: should be affected by Special
    /// <summary>
    /// Handles changing user movement speed when the gun is held and active (defaults to base speed when in active)
    /// </summary>
    public void ActiveSpeedModifier(EntityUid uid, MinigunToggleComponent comp, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        // Ballistic chamber miniguns always take the movement penalty while active.
        // Energy (BypassChamber) weapons opt in via ApplyActiveSpeedModifier.
        if (comp.BypassChamber && !comp.ApplyActiveSpeedModifier)
            return;

        var active = Comp<ItemToggleComponent>(uid).Activated;
        float speedMod = active ? comp.ActivatedSpeedModifier : 1f;
        args.Args.ModifySpeed(speedMod, speedMod, true);
    }
}


