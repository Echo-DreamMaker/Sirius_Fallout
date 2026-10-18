// #Misfits Add - Weapon repair interactions.
//
// Repairs are applied per part so a weapon cannot be restored past its designed
// ceiling with cheap consumables, and each tool only fits the state the weapon
// is in:
//   * Duct tape     -> a loose stock or grip part (+50, capped at 75% of max)
//   * Wonderglue    -> a loose bolt part          (+30, capped at 60% of max)
//   * Welder        -> a fully stripped frame     (+50, capped at 75%, fuel);
//                      with parts fitted the welder belongs to construction.
//   * Cleaning rod  -> the barrel, fitted or loose (restores recoverable wear
//                      in repeated 2s strokes until the bore is clean); on a
//                      weapon with nothing to clean it is stored in the
//                      underbarrel slot.
//   * Whetstone     -> recoverable melee weapons  (restores recoverable wear).
// Tape/glue are consumed one unit at a time (a stack loses a single item).
//
// #Misfits Fix - furniture and the barrel are repaired with different tools:
// tape binds a loose stock or grip and glue mends a loose bolt, but neither
// ever touches a barrel - the fouling and dents a barrel picks up only come
// back out with the cleaning rod.
using System;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Content.Shared._Misfits.Special;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.Weapons.Durability;

public sealed partial class SharedWeaponDurabilitySystem
{
    private const string UnderbarrelSlot = "gun_bottom";

    private const string DuctTapeTag = "DuctTape";
    private const string WonderGlueTag = "WonderGlue";
    private const string CleaningRodTag = "N14GunCleaningRod";
    private const string WhetstoneTag = "N14Whetstone";

    private const float TapeRepair = 50f;
    private const float TapeCap = 0.75f;
    private const float GlueRepair = 30f;
    private const float GlueCap = 0.60f;
    private const float WeldRepair = 50f;
    private const float WeldCap = 0.75f;
    private const float RodRepair = 10f;
    private const float WhetstoneRepair = 10f;

    private static readonly FixedPoint2 WelderRepairFuel = FixedPoint2.New(5);
    private static readonly TimeSpan RodRepairDelay = TimeSpan.FromSeconds(2);

    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly TagSystem _tag = default!;

    private void OnRepairInteract(EntityUid uid, WeaponDurabilityComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !_net.IsServer || args.User == uid)
            return;

        var used = args.Used;

        // Tape and glue are for parts taken out of the weapon, so they never
        // apply to a weapon body; see OnPartRepairInteract.

        if (TryComp<WelderComponent>(used, out var welder))
        {
            args.Handled = TryFrameWeld(uid, comp, args.User, used, welder);
            return;
        }

        if (_tag.HasTag(used, CleaningRodTag))
        {
            HandleCleaningRod(uid, comp, args, used);
            return;
        }

        if (_tag.HasTag(used, WhetstoneTag))
            args.Handled = TryWhetstone(uid, comp, args.User);
    }

    #region Consumables

    /// <summary>
    ///     Tape and glue only mend a part that is out of the weapon: a fitted
    ///     part is put back through construction, which is what restores the
    ///     weapon as a whole.
    /// </summary>
    private void OnPartRepairInteract(EntityUid uid, WeaponPartDurabilityComponent comp, InteractUsingEvent args)
    {
        if (args.Handled || !_net.IsServer || args.User == uid)
            return;

        var used = args.Used;

        // #Misfits Fix - tape binds furniture (stock, grip) and glue mends the
        // bolt group; a barrel takes neither. It is cleaned instead.
        if (_tag.HasTag(used, DuctTapeTag) && IsTapePart(comp.Role))
        {
            args.Handled = TryLoosePartRepair(uid, comp, args.User, used,
                TapeRepair, TapeCap, "misfits-weapon-repair-tape");
            return;
        }

        if (_tag.HasTag(used, WonderGlueTag) && comp.Role == WeaponPartRole.Bolt)
        {
            args.Handled = TryLoosePartRepair(uid, comp, args.User, used,
                GlueRepair, GlueCap, "misfits-weapon-repair-glue");
            return;
        }

        if (_tag.HasTag(used, CleaningRodTag))
            args.Handled = TryStartBarrelRodClean(uid, comp, args.User, used);
    }

    private static bool IsTapePart(WeaponPartRole role)
    {
        return role is WeaponPartRole.Stock or WeaponPartRole.Grip;
    }

    private bool TryLoosePartRepair(EntityUid uid, WeaponPartDurabilityComponent comp, EntityUid user, EntityUid used,
        float amount, float cap, string successLoc)
    {
        var ceiling = comp.Max * cap;
        if (comp.Current >= ceiling)
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-nothing"), uid, user);
            return true;
        }

        comp.Current = MathF.Min(ceiling, comp.Current + amount);
        ConsumeOne(used);
        _popup.PopupEntity(Loc.GetString(successLoc), uid, user);
        return true;
    }

    private void ConsumeOne(EntityUid used)
    {
        if (TryComp<StackComponent>(used, out var stack) && stack.Count > 1)
        {
            _stack.SetCount(used, stack.Count - 1, stack);
            return;
        }

        QueueDel(used);
    }

    #endregion

    #region Welder

    private bool TryFrameWeld(EntityUid uid, WeaponDurabilityComponent comp, EntityUid user, EntityUid used,
        WelderComponent welder)
    {
        // Only modular weapons carry a repairable receiver; monolithic guns and
        // non-recoverable melee cannot be welded back together.
        if (!comp.Modular || comp.FrameMax <= 0f)
            return false;

        // #Misfits Fix - A welder is also an assembly tool: the weapon graphs
        // weld the barrel on and cut those seams back off, and
        // ConstructionSystem handles the interaction after this system. Claiming
        // it while parts are still fitted starved those steps, which is why a
        // weld on a built weapon used to print "nothing to repair" instead of
        // removing the seam. Welding only applies to a fully stripped frame,
        // which is also the one state where no graph node can be waiting on it.
        GetParts(uid, _parts);
        if (_parts.Count > 0)
            return false;

        var ceiling = comp.FrameMax * WeldCap;
        if (comp.FrameCurrent >= ceiling)
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-nothing"), uid, user);
            return true;
        }

        if (!TryConsumeWelderFuel(used, welder))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-nofuel"), uid, user);
            return true;
        }

        comp.FrameCurrent = MathF.Min(ceiling, comp.FrameCurrent + WeldRepair);
        AfterWear(uid, comp);
        _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-weld"), uid, user);
        return true;
    }

    private bool TryConsumeWelderFuel(EntityUid used, WelderComponent welder)
    {
        if (!TryComp<SolutionContainerManagerComponent>(used, out var manager))
            return false;

        Entity<SolutionComponent>? sol = null;
        if (!_solution.ResolveSolution((used, manager), welder.FuelSolutionName, ref sol, out var solution))
            return false;

        if (solution.GetTotalPrototypeQuantity(welder.FuelReagent.Id) < WelderRepairFuel)
            return false;

        return _solution.RemoveReagent(sol!.Value, welder.FuelReagent.Id, WelderRepairFuel);
    }

    #endregion

    #region Cleaning rod

    private void HandleCleaningRod(EntityUid uid, WeaponDurabilityComponent comp, InteractUsingEvent args,
        EntityUid used)
    {
        GetParts(uid, _parts);
        if (comp.Modular && TryGetPart(_parts, WeaponPartRole.Barrel, out var barrel) && IsSwabbable(barrel))
        {
            if (StartRodDoAfter(uid, args.User, used))
            {
                _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-rod-start"), uid, args.User);
                args.Handled = true;
            }

            return;
        }

        // No barrel to clean: store the rod in the weapon's underbarrel slot.
        if (_container.TryGetContainer(uid, UnderbarrelSlot, out var slot) && slot.ContainedEntities.Count == 0)
        {
            if (_container.Insert(used, slot))
            {
                _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-rod-store"), uid, args.User);
                args.Handled = true;
            }

            return;
        }

        _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-rod-noslot"), uid, args.User);
        args.Handled = true;
    }

    private void OnRodRepairFinished(EntityUid uid, WeaponDurabilityComponent comp,
        WeaponCleaningRodRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        GetParts(uid, _parts);
        if (!TryGetPart(_parts, WeaponPartRole.Barrel, out var barrel) || !ApplyRodHeal(barrel))
            return;

        AfterWear(uid, comp);
        _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-rod"), uid, args.User);

        // Keep going while there is anything left to clean.
        if (IsSwabbable(barrel))
            ContinueRodClean(args.User, uid, args.Used);
    }

    /// <summary>
    ///     #Misfits Fix - a barrel out of the weapon can be swabbed too: the
    ///     wear it accumulated travels with the part, so there is no reason the
    ///     cleaning rod only works once it is back in a receiver.
    /// </summary>
    private bool TryStartBarrelRodClean(EntityUid uid, WeaponPartDurabilityComponent comp, EntityUid user,
        EntityUid rod)
    {
        if (comp.Role != WeaponPartRole.Barrel)
            return false;

        if (!IsSwabbable(comp))
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-nothing"), uid, user);
            return true;
        }

        if (!StartRodDoAfter(uid, user, rod))
            return false;

        _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-rod-start"), uid, user);
        return true;
    }

    private void OnPartRodRepairFinished(EntityUid uid, WeaponPartDurabilityComponent comp,
        WeaponCleaningRodRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || comp.Role != WeaponPartRole.Barrel)
            return;

        if (!ApplyRodHeal(comp))
            return;

        _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-rod"), uid, args.User);

        if (IsSwabbable(comp))
            ContinueRodClean(args.User, uid, args.Used);
    }

    /// <summary>
    ///     #Misfits Fix - swabbing is not a single stroke: the rod keeps working
    ///     the bore until the recoverable wear is gone, stopping the moment the
    ///     player moves or lets go of the rod.
    /// </summary>
    private void ContinueRodClean(EntityUid user, EntityUid target, EntityUid? rod)
    {
        if (rod is not { } rodUid || Deleted(rodUid) || !_hands.IsHolding(user, rodUid, out _))
            return;

        StartRodDoAfter(target, user, rodUid);
    }

    private bool StartRodDoAfter(EntityUid target, EntityUid user, EntityUid rod)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, RodRepairDelay,
            new WeaponCleaningRodRepairDoAfterEvent(), target, target: target, used: rod)
        {
            BreakOnMove = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private static bool IsSwabbable(WeaponPartDurabilityComponent barrel)
    {
        return barrel.Deferred > 0f && barrel.Current < barrel.Max;
    }

    private static bool ApplyRodHeal(WeaponPartDurabilityComponent barrel)
    {
        var heal = MathF.Min(RodRepair, MathF.Min(barrel.Deferred, barrel.Max - barrel.Current));
        if (heal <= 0f)
            return false;

        barrel.Deferred -= heal;
        barrel.Current += heal;
        return true;
    }

    #endregion

    #region Whetstone

    private bool TryWhetstone(EntityUid uid, WeaponDurabilityComponent comp, EntityUid user)
    {
        if (comp.Modular || !comp.Recoverable || comp.Deferred <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-nothing"), uid, user);
            return true;
        }

        var heal = MathF.Min(WhetstoneRepair, MathF.Min(comp.Deferred, comp.FrameMax - comp.FrameCurrent));
        if (heal <= 0f)
        {
            _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-nothing"), uid, user);
            return true;
        }

        comp.Deferred -= heal;
        comp.FrameCurrent = MathF.Min(comp.FrameMax, comp.FrameCurrent + heal);
        AfterWear(uid, comp);
        _popup.PopupEntity(Loc.GetString("misfits-weapon-repair-whetstone"), uid, user);
        return true;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Scales a wear side-effect chance by the user's Agility: 5 is neutral,
    ///     each point below raises the odds and each point above lowers them.
    /// </summary>
    private float FeatureChance(EntityUid? user, float baseChance)
    {
        if (baseChance <= 0f || user is not { } uid)
            return baseChance;

        var agility = _special.GetEffective(uid, SpecialStat.Agility);
        var multiplier = Math.Clamp(1f - 0.05f * (agility - 5), 0f, 2f);
        return Math.Clamp(baseChance * multiplier, 0f, 1f);
    }

    private EntityUid GetHolder(EntityUid uid)
    {
        return Transform(uid).ParentUid;
    }

    private static bool TryGetPart(List<(EntityUid Uid, WeaponPartDurabilityComponent Comp)> parts,
        WeaponPartRole role, out WeaponPartDurabilityComponent part)
    {
        foreach (var (_, candidate) in parts)
        {
            if (candidate.Role != role)
                continue;

            part = candidate;
            return true;
        }

        part = null!;
        return false;
    }

    #endregion
}

[Serializable, NetSerializable]
public sealed partial class WeaponCleaningRodRepairDoAfterEvent : DoAfterEvent
{
    public override DoAfterEvent Clone() => this;
}
