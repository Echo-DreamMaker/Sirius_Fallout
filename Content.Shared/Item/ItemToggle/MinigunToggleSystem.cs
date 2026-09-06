using Content.Shared.Hands;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Interaction.Events;

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
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private MovementSpeedModifierSystem _move = default!;
    public override void Initialize()
    {

        SubscribeLocalEvent<MinigunToggleComponent, UseInHandEvent>(OnUseTryActivate, before: [typeof(SharedGunSystem)]);
        SubscribeLocalEvent<MinigunToggleComponent, GunRefreshModifiersEvent>(ActiveFireRate);
        SubscribeLocalEvent<MinigunToggleComponent, HeldRelayedEvent<RefreshMovementSpeedModifiersEvent>>(ActiveSpeedModifier);
    }


    private void OnUseTryActBallistic(EntityUid uid, ChamberMagazineAmmoProviderComponent compBallistic, UseInHandEvent args)
    {

        var closedBolt = compBallistic.BoltClosed;
        if (closedBolt != true || _gun.GetChamberEntity(uid) is null)
        {
            _toggle.TryDeactivate(uid);
        }
        else
        {
            _toggle.Toggle(uid);
            args.Handled = true;
        }
    }

    // I want this to be handled in a generic way inside gunsystem. I dont like hardcoding interactions like this
    public void OnUseTryActivate(EntityUid uid, MinigunToggleComponent comp, UseInHandEvent args)
    {

        if (TryComp<ChamberMagazineAmmoProviderComponent>(uid, out var compBallistic))
        {
            OnUseTryActBallistic(uid, compBallistic, args);
        }
        else
        {
            _toggle.Toggle(uid);
            args.Handled = true;
        }

        _gun.RefreshModifiers(uid);
        _move.RefreshMovementSpeedModifiers(args.User);
    }

    // TODO: should be affected by Special
    /// <summary>
    /// Handles changing the fire rate when the gun is active and inactive
    /// </summary>
    public void ActiveFireRate(Entity<MinigunToggleComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var comp = Comp<ItemToggleComponent>(ent.Owner);
        args.FireRate = comp.Activated ? ent.Comp.ActivatedFireRate : ent.Comp.InactiveWeaponFireRate;

    }

    // TODO: should be affected by Special
    /// <summary>
    /// Handles changing user movement speed when the gun is held and active (defaults to base speed when in active)
    /// </summary>
    public void ActiveSpeedModifier(EntityUid uid, MinigunToggleComponent comp, ref HeldRelayedEvent<RefreshMovementSpeedModifiersEvent> args)
    {
        var active = Comp<ItemToggleComponent>(uid).Activated;
        float speedMod = active ? comp.ActivatedSpeedModifier : 1f;
        args.Args.ModifySpeed(speedMod, speedMod, true);
    }
}


