// #Misfits Add - Weapon durability.
//
// Data model:
//   * Every fitted part (grip/bolt/barrel/stock/drive/optics) carries a
//     WeaponPartDurabilityComponent whose Current survives assembly and
//     disassembly, because construction moves the part entity itself.
//   * The receiver/frame is the weapon body, so its health lives on the
//     weapon's WeaponDurabilityComponent and is copied across the entity swap
//     construction performs (see WeaponDurabilityConstructionSystem).
//   * Non-modular weapons (map-spawned guns, melee) carry all of their
//     durability on the body and use the monolithic wear/feature fields.
//   * Total/TotalMax are cached sums of body + fitted parts.
//
// Wear is server-authoritative: the shared shoot/melee/throw events also run on
// the client for prediction, so mutation is guarded with IsServer. What the
// client needs in order to predict consistently (spread and melee damage) is
// derived from damaged parts and pushed out as networked multipliers.
using System.Linq;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Misfits.Weapons.Durability;

public sealed partial class SharedWeaponDurabilitySystem : EntitySystem
{
    private const string StockContainer = "craft_stock";

    // Fraction of the barrel's per-shot wear that is recoverable.
    private const float BarrelDeferredFraction = 0.9f;

    // Drawback thresholds, keyed to a part's (or the body's) remaining ratio.
    private const float LowThreshold = 0.5f;
    private const float CriticalThreshold = 0.25f;

    // Spread can only be seen as a multiplier of the base gun angles.
    private const float LowSpreadMultiplier = 1.5f;
    private const float CriticalSpreadMultiplier = 2f;

    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private readonly List<(EntityUid Uid, WeaponPartDurabilityComponent Comp)> _parts = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WeaponDurabilityComponent, ComponentInit>(OnWeaponInit);
        SubscribeLocalEvent<WeaponDurabilityComponent, MapInitEvent>(OnWeaponMapInit);
        SubscribeLocalEvent<WeaponPartDurabilityComponent, ComponentInit>(OnPartInit);

        SubscribeLocalEvent<WeaponDurabilityComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<WeaponDurabilityComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<WeaponDurabilityComponent, AmmoShotEvent>(OnAmmoShot);
        SubscribeLocalEvent<WeaponDurabilityComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<WeaponDurabilityComponent, ActivateInWorldEvent>(OnRack);
        SubscribeLocalEvent<WeaponDurabilityComponent, UseInHandEvent>(OnRack);

        SubscribeLocalEvent<WeaponDurabilityComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<WeaponDurabilityComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<WeaponDurabilityComponent, ThrowDoHitEvent>(OnThrowDoHit);

        SubscribeLocalEvent<WeaponDurabilityComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<WeaponPartDurabilityComponent, ExaminedEvent>(OnPartExamine);

        // #Misfits Add - Repair interactions (tape/glue/welder/cleaning rod/whetstone).
        SubscribeLocalEvent<WeaponDurabilityComponent, InteractUsingEvent>(OnRepairInteract);
        SubscribeLocalEvent<WeaponPartDurabilityComponent, InteractUsingEvent>(OnPartRepairInteract);
        SubscribeLocalEvent<WeaponDurabilityComponent, WeaponCleaningRodRepairDoAfterEvent>(OnRodRepairFinished);
        // #Misfits Fix - the barrel can be swabbed while it is out of the weapon.
        SubscribeLocalEvent<WeaponPartDurabilityComponent, WeaponCleaningRodRepairDoAfterEvent>(OnPartRodRepairFinished);
    }

    private void OnWeaponInit(EntityUid uid, WeaponDurabilityComponent comp, ComponentInit args)
    {
        if (float.IsNaN(comp.FrameCurrent))
            comp.FrameCurrent = comp.FrameMax;
    }

    private void OnPartInit(EntityUid uid, WeaponPartDurabilityComponent comp, ComponentInit args)
    {
        if (float.IsNaN(comp.Current))
            comp.Current = comp.Max;
    }

    private void OnWeaponMapInit(EntityUid uid, WeaponDurabilityComponent comp, MapInitEvent args)
    {
        Recompute(uid, comp);

        if (_net.IsServer)
            UpdateModifiers(uid, comp);
    }

    #region Damage intake

    private void OnBeforeDamage(EntityUid uid, WeaponDurabilityComponent comp, ref BeforeDamageChangedEvent args)
    {
        var amount = args.Damage.GetTotal().Float();
        if (amount <= 0f)
            return;

        GetParts(uid, _parts);

        var share = amount / (_parts.Count + 1);

        comp.FrameCurrent = MathF.Max(0f, comp.FrameCurrent - share);
        foreach (var (partUid, part) in _parts)
            part.Current = MathF.Max(0f, part.Current - share);

        AfterWear(uid, comp);

        // The weapon never accumulates ordinary damage; it only wears parts.
        args.Cancelled = true;
    }

    #endregion

    #region Shooting

    private void OnAttemptShoot(EntityUid uid, WeaponDurabilityComponent comp, ref AttemptShootEvent args)
    {
        // A jam already known to the client cancels deterministically on both sides.
        if (comp.Jammed)
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("misfits-weapon-jammed");
            return;
        }

        if (!_net.IsServer)
            return;

        GetParts(uid, _parts);

        if (comp.Modular)
        {
            if (!HasPart(_parts, WeaponPartRole.Barrel))
            {
                args.Cancelled = true;
                args.Message = Loc.GetString("misfits-weapon-no-barrel");
                return;
            }

            if (!HasPart(_parts, WeaponPartRole.Bolt))
            {
                args.Cancelled = true;
                args.Message = Loc.GetString("misfits-weapon-no-bolt");
                return;
            }
        }

        var jamRatio = comp.Modular
            ? PartRatio(_parts, WeaponPartRole.Bolt)
            : HasFeature(comp, WeaponFeatures.Jam) ? Ratio(comp) : 1f;

        var chance = FeatureChance(args.User, JamChance(jamRatio));
        if (chance > 0f && _random.Prob(chance))
        {
            comp.Jammed = true;
            Dirty(uid, comp);
            args.Cancelled = true;
            args.Message = Loc.GetString("misfits-weapon-jammed");
        }
    }

    private void OnAmmoShot(EntityUid uid, WeaponDurabilityComponent comp, AmmoShotEvent args)
    {
        if (!_net.IsServer)
            return;

        GetParts(uid, _parts);

        if (comp.Modular)
        {
            var noStock = HasStockSlot(uid) && !HasPart(_parts, WeaponPartRole.Stock);
            comp.FrameCurrent = MathF.Max(0f, comp.FrameCurrent - (noStock ? 1.5f : 0.5f));

            foreach (var (_, part) in _parts)
            {
                var wear = PartShotWear(part.Role, noStock);
                part.Current = MathF.Max(0f, part.Current - wear);

                if (part.Role is WeaponPartRole.Barrel or WeaponPartRole.Optics)
                    part.Deferred += wear * BarrelDeferredFraction;
            }
        }
        else
        {
            comp.FrameCurrent = MathF.Max(0f, comp.FrameCurrent - comp.ShotWear);
        }

        // A worn/replaced grip can slip out of the user's hands.
        var dropRatio = comp.Modular
            ? PartRatio(_parts, WeaponPartRole.Grip)
            : HasFeature(comp, WeaponFeatures.Drop) ? Ratio(comp) : 1f;

        var dropChance = FeatureChance(GetHolder(uid), DropChance(dropRatio));
        if (dropChance > 0f && _random.Prob(dropChance))
            Drop(uid);

        AfterWear(uid, comp);
    }

    private void OnRack(EntityUid uid, WeaponDurabilityComponent comp, ActivateInWorldEvent args)
    {
        ClearJam(uid, comp);
    }

    private void OnRack(EntityUid uid, WeaponDurabilityComponent comp, UseInHandEvent args)
    {
        ClearJam(uid, comp);
    }

    private void ClearJam(EntityUid uid, WeaponDurabilityComponent comp)
    {
        if (!comp.Jammed || !_net.IsServer)
            return;

        comp.Jammed = false;
        Dirty(uid, comp);
    }

    #endregion

    #region Spread

    private void OnGunRefreshModifiers(EntityUid uid, WeaponDurabilityComponent comp, ref GunRefreshModifiersEvent args)
    {
        if (comp.SpreadMultiplier <= 1f)
            return;

        args.MinAngle *= comp.SpreadMultiplier;
        args.MaxAngle *= comp.SpreadMultiplier;
        args.AngleIncrease *= comp.SpreadMultiplier;
    }

    #endregion

    #region Melee / throwing

    private void OnMeleeHit(EntityUid uid, WeaponDurabilityComponent comp, MeleeHitEvent args)
    {
        if (!_net.IsServer || !args.IsHit)
            return;

        GetParts(uid, _parts);

        if (comp.Modular)
        {
            comp.FrameCurrent = MathF.Max(0f, comp.FrameCurrent - 1f);
            foreach (var (_, part) in _parts)
            {
                var wear = part.Role == WeaponPartRole.Stock ? 2f : 1f;
                part.Current = MathF.Max(0f, part.Current - wear);
            }
        }
        else
        {
            comp.FrameCurrent = MathF.Max(0f, comp.FrameCurrent - comp.MeleeWear);
            if (comp.Recoverable)
                comp.Deferred += comp.MeleeWear * BarrelDeferredFraction;
        }

        var dropRatio = comp.Modular
            ? PartRatio(_parts, WeaponPartRole.Grip)
            : HasFeature(comp, WeaponFeatures.Drop) ? Ratio(comp) : 1f;

        var dropChance = FeatureChance(args.User, DropChance(dropRatio));
        if (dropChance > 0f && _random.Prob(dropChance))
            Drop(uid);

        AfterWear(uid, comp);
    }

    private void OnGetMeleeDamage(EntityUid uid, WeaponDurabilityComponent comp, ref GetMeleeDamageEvent args)
    {
        if (comp.DamageMultiplier < 1f)
            args.Damage *= comp.DamageMultiplier;
    }

    private void OnThrowDoHit(EntityUid uid, WeaponDurabilityComponent comp, ThrowDoHitEvent args)
    {
        if (!_net.IsServer)
            return;

        GetParts(uid, _parts);

        if (comp.Modular)
        {
            var noStock = HasStockSlot(uid) && !HasPart(_parts, WeaponPartRole.Stock);
            comp.FrameCurrent = MathF.Max(0f, comp.FrameCurrent - (noStock ? 2f : 1f));

            foreach (var (_, part) in _parts)
            {
                var wear = part.Role == WeaponPartRole.Stock ? 2f : (noStock ? 2f : 1f);
                part.Current = MathF.Max(0f, part.Current - wear);
            }
        }
        else
        {
            comp.FrameCurrent = MathF.Max(0f, comp.FrameCurrent - comp.ThrowWear);
        }

        AfterWear(uid, comp);
    }

    #endregion

    #region Examine

    private void OnExamine(EntityUid uid, WeaponDurabilityComponent comp, ExaminedEvent args)
    {
        if (!_net.IsServer || comp.TotalMax <= 0f)
            return;

        var ratio = comp.Total / comp.TotalMax;
        args.PushMarkup(Loc.GetString("misfits-weapon-durability-examine",
            ("state", Loc.GetString(StateKey(ratio))),
            ("color", StateColor(ratio))));
    }

    private void OnPartExamine(EntityUid uid, WeaponPartDurabilityComponent comp, ExaminedEvent args)
    {
        if (!_net.IsServer || comp.Max <= 0f)
            return;

        var ratio = MathHelper.Clamp01(comp.Current / comp.Max);
        args.PushMarkup(Loc.GetString("misfits-weapon-part-examine",
            ("state", Loc.GetString(StateKey(ratio))),
            ("color", StateColor(ratio))));

        // Only the barrel takes recoverable fouling, and a cleaning rod is the
        // only thing that puts it back.
        if (comp.Role == WeaponPartRole.Barrel && comp.Deferred > 0f)
            args.PushMarkup(Loc.GetString("misfits-weapon-part-examine-fouled"));
    }

    #endregion

    #region Core

    /// <summary>
    ///     Carries body health across the entity swap construction performs when
    ///     a weapon is built up or stripped down. Called by
    ///     WeaponDurabilityConstructionSystem (construction events are
    ///     server-only).
    /// </summary>
    public void TransferFrame(EntityUid old, EntityUid @new)
    {
        if (!TryComp<WeaponDurabilityComponent>(old, out var oldComp))
            return;

        var @newComp = EnsureComp<WeaponDurabilityComponent>(@new);
        @newComp.FrameMax = oldComp.FrameMax;
        @newComp.FrameCurrent = oldComp.FrameCurrent;
        @newComp.Jammed = oldComp.Jammed;

        // The containers have already moved to the new entity by the time this
        // is called, so the totals can be refreshed right away.
        Recompute(@new, @newComp);
        UpdateModifiers(@new, @newComp);
    }

    private void AfterWear(EntityUid uid, WeaponDurabilityComponent comp)
    {
        DestroyWornParts(uid);
        Recompute(uid, comp);
        UpdateModifiers(uid, comp);
        TryCollapse(uid, comp);
    }

    /// <summary>
    ///     Removes any fitted part that has been worn down to zero. A destroyed
    ///     grip drops the weapon; a destroyed barrel or bolt simply stops it
    ///     firing (checked on the next shot attempt).
    /// </summary>
    private void DestroyWornParts(EntityUid uid)
    {
        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        var destroyedGrip = false;

        foreach (var container in manager.Containers.Values.ToArray())
        {
            foreach (var contained in container.ContainedEntities.ToArray())
            {
                if (!TryComp<WeaponPartDurabilityComponent>(contained, out var part) || part.Current > 0f)
                    continue;

                if (part.Role == WeaponPartRole.Grip)
                    destroyedGrip = true;

                _container.Remove(contained, container, reparent: true);
                QueueDel(contained);
            }
        }

        if (destroyedGrip)
            Drop(uid);
    }

    private void Drop(EntityUid uid)
    {
        if (!_net.IsServer)
            return;

        var holder = Transform(uid).ParentUid;
        if (holder.IsValid())
            _hands.TryDrop(holder, uid, checkActionBlocker: false);
    }

    private void UpdateModifiers(EntityUid uid, WeaponDurabilityComponent comp)
    {
        float spread;
        float damage;

        if (comp.Modular)
        {
            GetParts(uid, _parts);
            spread = SpreadMultiplier(PartRatio(_parts, WeaponPartRole.Barrel));
            damage = 1f;
        }
        else
        {
            spread = HasFeature(comp, WeaponFeatures.Spread) ? SpreadMultiplier(Ratio(comp)) : 1f;
            damage = HasFeature(comp, WeaponFeatures.DamageDown) ? DamageMultiplier(Ratio(comp)) : 1f;
        }

        if (MathHelper.CloseTo(comp.SpreadMultiplier, spread) && MathHelper.CloseTo(comp.DamageMultiplier, damage))
            return;

        comp.SpreadMultiplier = spread;
        comp.DamageMultiplier = damage;
        Dirty(uid, comp);

        if (TryComp<GunComponent>(uid, out _))
            _gun.RefreshModifiers(uid);
    }

    private void Recompute(EntityUid uid, WeaponDurabilityComponent comp)
    {
        GetParts(uid, _parts);

        var total = comp.FrameCurrent;
        var max = comp.FrameMax;
        foreach (var (_, part) in _parts)
        {
            total += part.Current;
            max += part.Max;
        }

        comp.Total = MathF.Max(0f, total);
        comp.TotalMax = max;
    }

    private void TryCollapse(EntityUid uid, WeaponDurabilityComponent comp)
    {
        if (comp.FrameCurrent > 0f && comp.Total > 0f)
            return;

        if (TryComp<ContainerManagerComponent>(uid, out var manager))
        {
            foreach (var container in manager.Containers.Values.ToArray())
                _container.EmptyContainer(container);
        }

        QueueDel(uid);
    }

    private void GetParts(EntityUid uid, List<(EntityUid Uid, WeaponPartDurabilityComponent Comp)> buffer)
    {
        buffer.Clear();

        if (!TryComp<ContainerManagerComponent>(uid, out var manager))
            return;

        foreach (var container in manager.Containers.Values)
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (TryComp<WeaponPartDurabilityComponent>(contained, out var part))
                    buffer.Add((contained, part));
            }
        }
    }

    private bool HasStockSlot(EntityUid uid)
    {
        return _container.TryGetContainer(uid, StockContainer, out _);
    }

    private static bool HasPart(List<(EntityUid Uid, WeaponPartDurabilityComponent Comp)> parts, WeaponPartRole role)
    {
        foreach (var (_, part) in parts)
        {
            if (part.Role == role)
                return true;
        }

        return false;
    }

    private static float PartRatio(List<(EntityUid Uid, WeaponPartDurabilityComponent Comp)> parts, WeaponPartRole role)
    {
        foreach (var (_, part) in parts)
        {
            if (part.Role == role && part.Max > 0f)
                return MathHelper.Clamp01(part.Current / part.Max);
        }

        // No part of this role fitted: treat it as entirely worn.
        return 0f;
    }

    private static float Ratio(WeaponDurabilityComponent comp)
    {
        return comp.FrameMax <= 0f ? 0f : MathHelper.Clamp01(comp.FrameCurrent / comp.FrameMax);
    }

    private static bool HasFeature(WeaponDurabilityComponent comp, WeaponFeatures feature)
    {
        return (comp.Features & feature) != 0;
    }

    private static float DropChance(float ratio)
    {
        if (ratio < CriticalThreshold)
            return 0.5f;
        if (ratio < LowThreshold)
            return 0.25f;
        return 0f;
    }

    private static float JamChance(float ratio)
    {
        if (ratio < CriticalThreshold)
            return 0.4f;
        if (ratio < LowThreshold)
            return 0.2f;
        return 0f;
    }

    private static float SpreadMultiplier(float ratio)
    {
        if (ratio < CriticalThreshold)
            return CriticalSpreadMultiplier;
        if (ratio < LowThreshold)
            return LowSpreadMultiplier;
        return 1f;
    }

    private static float DamageMultiplier(float ratio)
    {
        if (ratio < CriticalThreshold)
            return 0.5f;
        if (ratio < LowThreshold)
            return 0.75f;
        return 1f;
    }

    private static float PartShotWear(WeaponPartRole role, bool noStock)
    {
        return role switch
        {
            WeaponPartRole.Barrel or WeaponPartRole.Optics => 2f,
            WeaponPartRole.Stock => 1f,
            _ => noStock ? 1.5f : 0.5f,
        };
    }

    private static string StateKey(float ratio)
    {
        if (ratio >= 0.999f)
            return "misfits-weapon-durability-state-1";
        if (ratio >= 0.90f)
            return "misfits-weapon-durability-state-2";
        if (ratio >= 0.75f)
            return "misfits-weapon-durability-state-3";
        if (ratio >= 0.50f)
            return "misfits-weapon-durability-state-4";
        if (ratio >= 0.30f)
            return "misfits-weapon-durability-state-5";
        if (ratio >= 0.15f)
            return "misfits-weapon-durability-state-6";
        return "misfits-weapon-durability-state-7";
    }

    /// <summary>
    ///     #Misfits Add - colours the durability/condition readout by level, from
    ///     dark red (worst) up through red, orange, yellow, light green and green
    ///     to azure (best). Mirrors the thresholds of <see cref="StateKey"/>.
    /// </summary>
    private static string StateColor(float ratio)
    {
        if (ratio >= 0.999f)
            return "#33A9FF"; // azure
        if (ratio >= 0.90f)
            return "#32CD32"; // green
        if (ratio >= 0.75f)
            return "#90EE90"; // light green
        if (ratio >= 0.50f)
            return "#FFD700"; // yellow
        if (ratio >= 0.30f)
            return "#FF8C00"; // orange
        if (ratio >= 0.15f)
            return "#E53935"; // red
        return "#8B0000";     // dark red
    }

    #endregion
}
