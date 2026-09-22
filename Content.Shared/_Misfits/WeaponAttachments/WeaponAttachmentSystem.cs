// #Misfits Add - Core weapon attachment system.
using Content.Shared._Misfits.Scope;
using Content.Shared._Misfits.WeaponAttachments.Components;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.Shared._Misfits.WeaponAttachments;

/// <summary>
/// Handles installing, removing and operating weapon attachments.
/// Attachments are stored in normal <see cref="ItemSlotsComponent"/> slots named
/// <c>gun_top</c>, <c>gun_muzzle</c>, <c>gun_bottom</c> and <c>gun_handle</c>.
/// </summary>
public sealed partial class WeaponAttachmentSystem : EntitySystem
{
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeaponAttachmentHostComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, EntInsertedIntoContainerMessage>(OnAttachmentInserted);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, EntRemovedFromContainerMessage>(OnAttachmentRemoved);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, ComponentStartup>(OnAttachmentHostStartup);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, ToggleActionEvent>(OnUnderbarrelToggleAction);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, GotEquippedHandEvent>(OnGunEquippedHand);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, GotUnequippedHandEvent>(OnGunUnequippedHand);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, GunMuzzleFlashAttemptEvent>(OnGunMuzzleFlashAttempt);
        SubscribeLocalEvent<WeaponAttachmentHostComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<WeaponAttachmentComponent, MapInitEvent>(OnAttachmentMapInit);
        SubscribeLocalEvent<WeaponAttachmentComponent, GetItemActionsEvent>(OnAttachmentGetActions);
        SubscribeLocalEvent<WeaponAttachmentComponent, ToggleActionEvent>(OnAttachmentToggleAction);

        SubscribeLocalEvent<StandingStateComponent, StoodEvent>(OnStood);
        SubscribeLocalEvent<StandingStateComponent, DownedEvent>(OnDowned);
    }

    #region Modifiers

    private void OnGunRefreshModifiers(EntityUid uid, WeaponAttachmentHostComponent host, ref GunRefreshModifiersEvent args)
    {
        foreach (var slotId in SlotIds(host))
        {
            if (!_itemSlots.TryGetSlot(uid, slotId, out var slot) || slot.Item is not { } item)
                continue;

            if (!TryComp<WeaponAttachmentComponent>(item, out var attachment))
                continue;

            args.MinAngle += attachment.MinAngleModifier;
            args.MaxAngle += attachment.MaxAngleModifier;
            args.AngleIncrease += attachment.AngleIncreaseModifier;
            args.AngleDecay += attachment.AngleDecayModifier;
            args.CameraRecoilScalar *= attachment.CameraRecoilScalarModifier;
            args.FireRate *= attachment.FireRateModifier;

            if (attachment.SoundGunshotOverride != null)
                args.SoundGunshot = attachment.SoundGunshotOverride;
            else if (attachment.SuppressGunshot)
                args.SoundGunshot = null;

            if (TryComp<WeaponLaserSightComponent>(item, out var laser) && laser.Enabled)
            {
                args.MinAngle += laser.MinAngleBonus;
                args.MaxAngle += laser.MaxAngleBonus;
                args.AngleIncrease += laser.AngleIncreaseBonus;
            }

            if (TryComp<WeaponBipodComponent>(item, out var bipod) && bipod.Deployed)
            {
                if (IsHolderProne(uid))
                {
                    args.MinAngle += bipod.ProneMinAngleBonus;
                    args.MaxAngle += bipod.ProneMaxAngleBonus;
                    args.AngleIncrease += bipod.ProneAngleIncreaseBonus;
                    args.CameraRecoilScalar *= bipod.ProneCameraRecoilModifier;
                }
                else
                {
                    args.MinAngle += bipod.StandingMinAnglePenalty;
                    args.MaxAngle += bipod.StandingMaxAnglePenalty;
                    args.AngleIncrease += bipod.StandingAngleIncreasePenalty;
                }
            }
        }
    }

    private bool IsHolderProne(EntityUid gun)
    {
        var holder = Transform(gun).ParentUid;
        return holder.IsValid()
               && TryComp<StandingStateComponent>(holder, out var standing)
               && standing.CurrentState == StandingState.Lying;
    }

    #endregion

    #region Installation

    private void OnAttachmentInserted(EntityUid uid, WeaponAttachmentHostComponent host, EntInsertedIntoContainerMessage args)
    {
        if (!IsAttachmentSlot(args.Container.ID))
            return;

        DisableAttachmentSlotOcclusion(uid, host);
        UpdateUnderbarrelAction(uid, host);
        UpdateBayonet(uid, host);
        _gun.RefreshModifiers(uid);
        GrantAttachmentActionsToHolder(uid, args.Entity);
    }

    private void OnAttachmentRemoved(EntityUid uid, WeaponAttachmentHostComponent host, EntRemovedFromContainerMessage args)
    {
        if (!IsAttachmentSlot(args.Container.ID))
            return;

        if (host.UnderbarrelActive && !TryGetUnderbarrel(uid, out _))
        {
            host.UnderbarrelActive = false;
            Dirty(uid, host);
        }

        UpdateUnderbarrelAction(uid, host);
        DisableAttachmentSlotOcclusion(uid, host);
        UpdateBayonet(uid, host);
        _gun.RefreshModifiers(uid);
        RevokeAttachmentActionsFromHolder(uid, args.Entity);
    }

    /// <summary>
    /// Attachment slots hold items such as flashlights and laser sights whose lights would
    /// otherwise be hidden behind the container's default light occlusion. Weapon slot
    /// containers default to <see cref="BaseContainer.OccludesLight"/> = true, so flip them
    /// off to let a mounted light shine through the weapon.
    /// </summary>
    private void OnAttachmentHostStartup(EntityUid uid, WeaponAttachmentHostComponent host, ref ComponentStartup args)
    {
        DisableAttachmentSlotOcclusion(uid, host);
    }

    private void DisableAttachmentSlotOcclusion(EntityUid uid, WeaponAttachmentHostComponent host)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager) || manager.Containers.Count == 0)
            return;

        var dirty = false;
        foreach (var slotId in SlotIds(host))
        {
            if (manager.Containers.TryGetValue(slotId, out var container) && container.OccludesLight)
            {
                container.OccludesLight = false;
                dirty = true;
            }
        }

        if (dirty)
            Dirty(uid, manager);
    }

    private static bool IsAttachmentSlot(string slotId)
    {
        return slotId == WeaponAttachmentSlots.Top
               || slotId == WeaponAttachmentSlots.Muzzle
               || slotId == WeaponAttachmentSlots.Bottom
               || slotId == WeaponAttachmentSlots.Handle;
    }

    private void UpdateBayonet(EntityUid gun, WeaponAttachmentHostComponent host)
    {
        BayonetComponent? bayonet = null;

        if (_itemSlots.TryGetSlot(gun, WeaponAttachmentSlots.Bottom, out var slot) && slot.Item is { } item)
            TryComp(item, out bayonet);

        if (bayonet?.Damage == null)
        {
            if (!host.BayonetMeleeApplied)
                return;

            if (host.OriginalMeleeDamage != null && TryComp<MeleeWeaponComponent>(gun, out var existing))
                existing.Damage = host.OriginalMeleeDamage;
            else
                RemComp<MeleeWeaponComponent>(gun);

            host.BayonetMeleeApplied = false;
            host.OriginalMeleeDamage = null;
            return;
        }

        var hadMelee = TryComp<MeleeWeaponComponent>(gun, out var melee);
        melee ??= EnsureComp<MeleeWeaponComponent>(gun);

        if (!host.BayonetMeleeApplied)
        {
            host.OriginalMeleeDamage = hadMelee && melee.Damage != null
                ? new DamageSpecifier(melee.Damage)
                : null;
            host.BayonetMeleeApplied = true;
        }

        melee.Damage = new DamageSpecifier(bayonet.Damage);
        melee.Range = MathF.Max(melee.Range, bayonet.Range);
        melee.AttackRate = bayonet.AttackRate;
        melee.WideAnimationRotation = bayonet.WideAnimationRotation;
        if (bayonet.SoundHit != null)
            melee.SoundHit = bayonet.SoundHit;
        Dirty(gun, melee);
    }

    #endregion

    #region Verbs

    private void OnGetVerbs(EntityUid uid, WeaponAttachmentHostComponent host, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        foreach (var slotId in SlotIds(host))
        {
            if (!_itemSlots.TryGetSlot(uid, slotId, out var slot) || slot.Item is not { } item)
                continue;

            if (TryComp<WeaponBipodComponent>(item, out var bipod))
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString(bipod.Deployed
                        ? "weapon-attachment-verb-bipod-fold"
                        : "weapon-attachment-verb-bipod-deploy"),
                    Priority = -1,
                    Act = () => ToggleBipod(uid, item, bipod, args.User),
                });
            }

            if (TryComp<WeaponFlashlightComponent>(item, out var flashlight))
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString(flashlight.Enabled
                        ? "weapon-attachment-verb-flashlight-off"
                        : "weapon-attachment-verb-flashlight-on"),
                    Priority = -1,
                    Act = () => ToggleFlashlight(item, flashlight, args.User),
                });
            }

            if (TryComp<WeaponLaserSightComponent>(item, out var laser))
            {
                args.Verbs.Add(new AlternativeVerb
                {
                    Text = Loc.GetString(laser.Enabled
                        ? "weapon-attachment-verb-laser-off"
                        : "weapon-attachment-verb-laser-on"),
                    Priority = -1,
                    Act = () => ToggleLaser(item, laser, args.User),
                });
            }
        }

        if (TryGetUnderbarrel(uid, out _))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString(host.UnderbarrelActive
                    ? "weapon-attachment-verb-primary"
                    : "weapon-attachment-verb-underbarrel"),
                Priority = 3,
                Act = () => ToggleUnderbarrel(uid, host, args.User),
            });
        }
    }

    private void ToggleBipod(EntityUid gun, EntityUid attachment, WeaponBipodComponent bipod, EntityUid user)
    {
        bipod.Deployed = !bipod.Deployed;
        Dirty(attachment, bipod);
        _gun.RefreshModifiers(gun);
        _popup.PopupClient(
            Loc.GetString(bipod.Deployed
                ? "weapon-attachment-popup-bipod-deployed"
                : "weapon-attachment-popup-bipod-folded"),
            gun,
            user);
    }

    private void ToggleFlashlight(EntityUid attachment, WeaponFlashlightComponent flashlight, EntityUid user)
    {
        flashlight.Enabled = !flashlight.Enabled;
        Dirty(attachment, flashlight);
        _pointLight.SetEnabled(attachment, flashlight.Enabled);
        _popup.PopupClient(
            Loc.GetString(flashlight.Enabled
                ? "weapon-attachment-popup-flashlight-on"
                : "weapon-attachment-popup-flashlight-off"),
            attachment,
            user);
    }

    private void ToggleLaser(EntityUid attachment, WeaponLaserSightComponent laser, EntityUid user)
    {
        laser.Enabled = !laser.Enabled;
        Dirty(attachment, laser);
        _pointLight.SetEnabled(attachment, laser.Enabled);

        var gun = Transform(attachment).ParentUid;
        if (gun.IsValid())
            _gun.RefreshModifiers(gun);

        _popup.PopupClient(
            Loc.GetString(laser.Enabled
                ? "weapon-attachment-popup-laser-on"
                : "weapon-attachment-popup-laser-off"),
            attachment,
            user);
    }

    private void ToggleUnderbarrel(EntityUid gun, WeaponAttachmentHostComponent host, EntityUid user)
    {
        if (!TryGetUnderbarrel(gun, out _))
            return;

        host.UnderbarrelActive = !host.UnderbarrelActive;
        Dirty(gun, host);
        _popup.PopupClient(
            Loc.GetString(host.UnderbarrelActive
                ? "weapon-attachment-popup-underbarrel"
                : "weapon-attachment-popup-primary"),
            gun,
            user);
    }

    #endregion

    #region Actions

    private void OnAttachmentMapInit(EntityUid uid, WeaponAttachmentComponent attachment, MapInitEvent args)
    {
        if (attachment.ToggleAction != null)
            _actionContainer.EnsureAction(uid, ref attachment.ToggleActionEntity, attachment.ToggleAction);

        Dirty(uid, attachment);
    }

    /// <summary>
    /// Grants the attachment's toggle action when the item itself is held or equipped
    /// (it is not inside a weapon at that point).
    /// </summary>
    private void OnAttachmentGetActions(EntityUid uid, WeaponAttachmentComponent attachment, GetItemActionsEvent args)
    {
        if (attachment.ToggleAction != null)
            args.AddAction(ref attachment.ToggleActionEntity, attachment.ToggleAction);
    }

    /// <summary>
    /// Grants actions of mounted items when the weapon is picked up: scope actions from each
    /// slotted item, plus the underbarrel toggle action when an underbarrel weapon is attached.
    /// </summary>
    private void OnGunEquippedHand(EntityUid uid, WeaponAttachmentHostComponent host, ref GotEquippedHandEvent args)
    {
        foreach (var slotId in SlotIds(host))
        {
            if (!_itemSlots.TryGetSlot(uid, slotId, out var slot) || slot.Item is not { } item)
                continue;

            GrantAttachmentActionsFrom(args.User, item);
        }

        UpdateUnderbarrelAction(uid, host);
    }

    private void OnGunUnequippedHand(EntityUid uid, WeaponAttachmentHostComponent host, ref GotUnequippedHandEvent args)
    {
        foreach (var slotId in SlotIds(host))
        {
            if (!_itemSlots.TryGetSlot(uid, slotId, out var slot) || slot.Item is not { } item)
                continue;

            _actions.RemoveProvidedActions(args.User, item);
        }

        _actions.RemoveProvidedActions(args.User, uid);
    }

    private void GrantAttachmentActionsToHolder(EntityUid gun, EntityUid attachment)
    {
        if (!TryGetGunHolder(gun, out var user))
            return;

        GrantAttachmentActionsFrom(user, attachment);
    }

    private void RevokeAttachmentActionsFromHolder(EntityUid gun, EntityUid attachment)
    {
        if (!TryGetGunHolder(gun, out var user))
            return;

        _actions.RemoveProvidedActions(user, attachment);
    }

    private bool TryGetGunHolder(EntityUid gun, out EntityUid user)
    {
        var parent = Transform(gun).ParentUid;
        if (parent.IsValid() && HasComp<ActionsComponent>(parent))
        {
            user = parent;
            return true;
        }

        user = default;
        return false;
    }

    private void GrantAttachmentActionsFrom(EntityUid user, EntityUid item)
    {
        if (TryComp<ScopeComponent>(item, out var scope))
        {
            if (scope.ScopingToggleActionEntity != null)
                _actions.AddActionDirect(user, scope.ScopingToggleActionEntity.Value);

            if (scope.ZoomLevels.Count > 1 && scope.CycleZoomLevelActionEntity != null)
                _actions.AddActionDirect(user, scope.CycleZoomLevelActionEntity.Value);
        }

        // #Misfits: mounted toggleable attachments (flashlight / laser sight / bipod) get their
        // toggle action granted while the weapon is held, same as the scope actions above.
        if (TryComp<WeaponAttachmentComponent>(item, out var attachment) && attachment.ToggleActionEntity is { } toggle)
            _actions.AddActionDirect(user, toggle);
    }

    /// <summary>
    /// Toggles active attachments (flashlight / laser sight / bipod). When the item is not
    /// mounted on a weapon, shows a popup instead. Scope toggling is left to the scope system.
    /// </summary>
    private void OnAttachmentToggleAction(EntityUid uid, WeaponAttachmentComponent attachment, ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        var handled = false;

        if (TryGetAttachedGun(uid, out var gun))
        {
            if (TryComp<WeaponFlashlightComponent>(uid, out var flashlight))
            {
                ToggleFlashlight(uid, flashlight, args.Performer);
                handled = true;
            }
            else if (TryComp<WeaponLaserSightComponent>(uid, out var laser))
            {
                ToggleLaser(uid, laser, args.Performer);
                handled = true;
            }
            else if (TryComp<WeaponBipodComponent>(uid, out var bipod))
            {
                ToggleBipod(gun, uid, bipod, args.Performer);
                handled = true;
            }
        }
        else if (HasComp<WeaponFlashlightComponent>(uid) || HasComp<WeaponLaserSightComponent>(uid) || HasComp<WeaponBipodComponent>(uid))
        {
            _popup.PopupClient(
                Loc.GetString("weapon-attachment-popup-must-attach"),
                args.Performer,
                args.Performer);

            handled = true;
        }

        if (handled)
            args.Handled = true;
    }

    /// <summary>
    /// Toggles between the primary and underbarrel weapon when the host's underbarrel
    /// toggle action is used. Ignored while the host has no underbarrel attached.
    /// </summary>
    private void OnUnderbarrelToggleAction(EntityUid uid, WeaponAttachmentHostComponent host, ToggleActionEvent args)
    {
        if (args.Handled || args.Action.Owner != host.UnderbarrelToggleActionEntity)
            return;

        if (!TryGetUnderbarrel(uid, out _))
            return;

        ToggleUnderbarrel(uid, host, args.Performer);
        args.Handled = true;
    }

    private void UpdateUnderbarrelAction(EntityUid gun, WeaponAttachmentHostComponent host)
    {
        if (!TryGetGunHolder(gun, out var user))
            return;

        if (TryGetUnderbarrel(gun, out _))
            GrantUnderbarrelAction(gun, host, user);
        else if (host.UnderbarrelToggleActionEntity is { } action)
            _actions.RemoveAction(user, action);
    }

    private void GrantUnderbarrelAction(EntityUid gun, WeaponAttachmentHostComponent host, EntityUid user)
    {
        if (host.UnderbarrelToggleAction == null)
            return;

        if (host.UnderbarrelToggleActionEntity == null)
            _actionContainer.EnsureAction(gun, ref host.UnderbarrelToggleActionEntity, host.UnderbarrelToggleAction);

        if (host.UnderbarrelToggleActionEntity is { } action)
            _actions.AddActionDirect(user, action);
    }

    private bool TryGetAttachedGun(EntityUid attachment, out EntityUid gun)
    {
        var parent = Transform(attachment).ParentUid;
        if (parent.IsValid() && HasComp<WeaponAttachmentHostComponent>(parent))
        {
            gun = parent;
            return true;
        }

        gun = default;
        return false;
    }

    #endregion

    #region Underbarrel firing

    private void OnGunMuzzleFlashAttempt(EntityUid uid, WeaponAttachmentHostComponent host, ref GunMuzzleFlashAttemptEvent args)
    {
        // A muzzle device (suppressor / muzzle brake) suppresses the muzzle flash.
        if (!_itemSlots.TryGetSlot(uid, WeaponAttachmentSlots.Muzzle, out var slot) || slot.Item is not { } item)
            return;

        if (!HasComp<WeaponAttachmentComponent>(item))
            return;

        args.Cancelled = true;
    }

    private void OnShotAttempted(EntityUid uid, WeaponAttachmentHostComponent host, ref ShotAttemptedEvent args)
    {
        if (!host.UnderbarrelActive)
            return;

        if (!TryGetUnderbarrel(uid, out var inner) || !TryComp<GunComponent>(inner, out var innerGun))
            return;

        args.Cancel();

        if (args.Used.Comp.ShootCoordinates is not { } coordinates)
            return;

        _gun.AttemptShoot(args.User, inner, innerGun, coordinates);
    }

    private void OnInteractUsing(EntityUid uid, WeaponAttachmentHostComponent host, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryGetUnderbarrel(uid, out var inner) || !HasComp<BallisticAmmoProviderComponent>(inner))
            return;

        var ev = new InteractUsingEvent(args.User, args.Used, inner, args.ClickLocation);
        RaiseLocalEvent(inner, ev);

        if (ev.Handled)
            args.Handled = true;
    }

    private bool TryGetUnderbarrel(EntityUid gun, out EntityUid inner)
    {
        inner = default;

        if (!_itemSlots.TryGetSlot(gun, WeaponAttachmentSlots.Bottom, out var slot) || slot.Item is not { } item)
            return false;

        if (!HasComp<UnderslungWeaponComponent>(item))
            return false;

        inner = item;
        return true;
    }

    #endregion

    #region Stance

    private void OnStood(EntityUid uid, StandingStateComponent component, StoodEvent args)
    {
        RefreshHeldGun(uid);
    }

    private void OnDowned(EntityUid uid, StandingStateComponent component, DownedEvent args)
    {
        RefreshHeldGun(uid);
    }

    private void RefreshHeldGun(EntityUid user)
    {
        if (!_hands.TryGetActiveItem(user, out var held) || !HasComp<WeaponAttachmentHostComponent>(held.Value))
            return;

        _gun.RefreshModifiers(held.Value);
    }

    #endregion

    private static IEnumerable<string> SlotIds(WeaponAttachmentHostComponent host)
    {
        foreach (var slot in host.Slots)
            yield return WeaponAttachmentSlots.ToId(slot);
    }
}
