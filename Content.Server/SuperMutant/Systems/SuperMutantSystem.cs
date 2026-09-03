using Content.Server.Tools;
using Content.Server._Misfits.Special;
using Content.Shared._N14.SuperMutant;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Hands;
using Content.Shared.Humanoid;
using Content.Shared.Tools.Components;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Server.SuperMutant.Systems;

/// <summary>
/// Server-side half of the super mutant ability. Handles the melee damage bonus (log curve +
/// hard cap for real super mutants, flat modifiers for other holders such as the Oni race),
/// auto-wielding two-handed firearms in one hand, stamina handling and the held-item marker.
///
/// NOTE: the firearm firing rules (one-handed block + spread penalty) live in the standalone
/// shared <see cref="SharedSuperMutantSystem"/>, NOT here — that system must run on both client
/// and server (prediction), and must not be a base class of this one to avoid being replaced
/// by the subtype during entity-system discovery on the server.
/// </summary>
public sealed class SuperMutantSystem : EntitySystem
{
    private const float MutantMeleeDamageCeiling = 160f;

    [Dependency] private readonly ToolSystem _toolSystem = default!;
    [Dependency] private readonly WieldableSystem _wieldable = default!;
    [Dependency] private readonly INetManager _net = default!;

    // Track weapons whose health contest we disabled for super mutants.
    private readonly HashSet<EntityUid> _healthContestDisabled = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SuperMutantComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<SuperMutantComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
        SubscribeLocalEvent<SuperMutantComponent, GotEquippedHandEvent>(OnGotHand);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeDamageEvent>(OnGetMeleeDamage,
            after: [typeof(WieldableSystem), typeof(SpecialCombatSystem)]);
        SubscribeLocalEvent<HeldBySuperMutantComponent, TakeStaminaDamageEvent>(OnStamHit);
    }

    private void OnEntInserted(EntityUid uid, SuperMutantComponent component, EntInsertedIntoContainerMessage args)
    {
        var heldComp = EnsureComp<HeldBySuperMutantComponent>(args.Entity);
        heldComp.Holder = uid;

        if (TryComp<ToolComponent>(args.Entity, out var tool) && _toolSystem.HasQuality(args.Entity, "Prying", tool))
            _toolSystem.SetSpeedModifier((args.Entity, tool), tool.SpeedModifier * 1.66f);
    }

    private void OnEntRemoved(EntityUid uid, SuperMutantComponent component, EntRemovedFromContainerMessage args)
    {
        if (TryComp<ToolComponent>(args.Entity, out var tool) && _toolSystem.HasQuality(args.Entity, "Prying", tool))
            _toolSystem.SetSpeedModifier((args.Entity, tool), tool.SpeedModifier / 1.66f);

        // Restore health contest on the weapon when dropped from a super mutant.
        if (_healthContestDisabled.Remove(args.Entity)
            && TryComp<MeleeWeaponComponent>(args.Entity, out var melee)
            && melee.ContestArgs is not null)
        {
            melee.ContestArgs.DoHealthInteraction = true;
        }

        RemComp<HeldBySuperMutantComponent>(args.Entity);
    }

    /// <summary>
    /// Auto-wield fitting/non-fitting two-handed firearms in one hand when grabbed by a super mutant.
    /// </summary>
    private void OnGotHand(EntityUid uid, SuperMutantComponent component, GotEquippedHandEvent args)
    {
        if (!_net.IsServer)
            return;

        var equipped = args.Equipped;

        if (!HasComp<GunComponent>(equipped))
            return;

        if (!TryComp<WieldableComponent>(equipped, out var wield) || wield.Wielded)
            return;

        _wieldable.TryWield(equipped, wield, uid);
    }

    private void OnGetMeleeDamage(EntityUid uid, MeleeWeaponComponent component, ref GetMeleeDamageEvent args)
    {
        if (!TryComp<SuperMutantComponent>(args.User, out var sm))
            return;

        // Super Mutants and Nightkin: log curve for wielded weapons, hard 160 cap on everything.
        bool isWielded = TryComp<WieldableComponent>(uid, out var wield) && wield.Wielded;
        bool isSuperMutant = TryComp<HumanoidAppearanceComponent>(args.User, out var appearance) &&
            (appearance.Species == "SuperMutant" || appearance.Species == "Nightkin");

        if (isSuperMutant)
        {
            // Disable health contest so the hard cap isn't bypassed by HP-scaling.
            if (component.ContestArgs is { DoHealthInteraction: true })
            {
                component.ContestArgs.DoHealthInteraction = false;
                _healthContestDisabled.Add(uid);
            }

            // Apply Oni/SuperMutant modifier coefficients directly to args.Damage so the hard cap
            // can clamp the final number. Do NOT add to args.Modifiers (applied after this returns).
            args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, sm.MeleeModifiers);

            // Log curve: shape wielded weapon damage so low-damage tools aren't trivial
            // and high-damage weapons are compressed toward the ceiling.
            if (isWielded)
            {
                var baseDamage = args.Damage.GetTotal().Float();
                if (baseDamage > 0f)
                {
                    var logCurveDamage = MutantMeleeDamageCeiling * MathF.Log(baseDamage + 1f) /
                                          MathF.Log(MutantMeleeDamageCeiling + 1f);
                    var targetDamage = MathF.Min(MathF.Max(logCurveDamage, baseDamage), MutantMeleeDamageCeiling);
                    args.Damage *= targetDamage / baseDamage;
                }
            }

            // Hard cap: super mutant damage never exceeds 160 total, period.
            var total = args.Damage.GetTotal().Float();
            if (total > MutantMeleeDamageCeiling)
                args.Damage *= MutantMeleeDamageCeiling / total;

            return; // Super mutant path done — skip generic modifier path below.
        }

        // Non-super-mutant holders (e.g. Oni race): flat modifiers via args.Modifiers (no log curve, no cap).
        args.Modifiers.Add(sm.MeleeModifiers);
    }

    private void OnStamHit(EntityUid uid, HeldBySuperMutantComponent component, TakeStaminaDamageEvent args)
    {
        if (!TryComp<SuperMutantComponent>(component.Holder, out var sm))
            return;

        args.Multiplier *= sm.StamMultiplier;
    }
}
