// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared._Misfits.Genetics.Mutations;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Toggleable chameleon invisibility for the Chameleon mutation. While active the body
/// strains to keep the skin transparent, dealing a little poison damage every second.
/// Turning it off puts the ability on a recharge cooldown.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ChameleonInvisibilityActionComponent : Component
{
    /// <summary>
    /// Whether the invisibility is currently active.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// The mob currently being kept invisible.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Target;

    /// <summary>
    /// Whether we were the ones who added the stealth. If the mob was already cloaked by
    /// something else we leave that alone instead of stripping it on our way out.
    /// </summary>
    [DataField]
    public bool AddedStealth;

    /// <summary>
    /// Poison dealt per second while invisible.
    /// </summary>
    [DataField]
    public FixedPoint2 PoisonPerSecond = 0.1;

    /// <summary>
    /// The damage type dealt while invisible.
    /// </summary>
    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Poison";

    /// <summary>
    /// Cooldown applied to the action after turning the invisibility off.
    /// </summary>
    [DataField]
    public TimeSpan Recharge = TimeSpan.FromSeconds(30);

    /// <summary>
    /// When the next poison tick happens.
    /// </summary>
    public TimeSpan NextTick;
}

public sealed partial class ChameleonInvisibilityActionEvent : InstantActionEvent;

public sealed class ChameleonInvisibilityActionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mob = default!;
    [Dependency] private readonly MutationSystem _mutation = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChameleonInvisibilityActionComponent, ChameleonInvisibilityActionEvent>(OnToggle);
        SubscribeLocalEvent<ChameleonInvisibilityActionComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnToggle(Entity<ChameleonInvisibilityActionComponent> ent, ref ChameleonInvisibilityActionEvent args)
    {
        args.Handled = true;

        if (ent.Comp.Active)
        {
            Deactivate(ent, recharge: true);
            return;
        }

        // find the mutated mob through the action's mutation container
        if (_mutation.GetActionMutation(ent.Owner) is not {} mutation ||
            _mutation.GetMutationTarget(mutation) is not {} mob)
            return;

        ent.Comp.Active = true;
        ent.Comp.Target = mob;
        ent.Comp.NextTick = _timing.CurTime + TimeSpan.FromSeconds(1);
        Dirty(ent);
        _actions.SetToggled(ent.Owner, true);

        if (_net.IsClient)
            return;

        // EnabledOnDeath isn't writable from here, but the Update loop already drops
        // the invisibility the moment the mob dies, so it doesn't matter.
        ent.Comp.AddedStealth = !HasComp<StealthComponent>(mob);
        var stealth = EnsureComp<StealthComponent>(mob);
        _stealth.SetVisibility(mob, stealth.MinVisibility, stealth);
    }

    private void OnShutdown(Entity<ChameleonInvisibilityActionComponent> ent, ref ComponentShutdown args)
    {
        // mutation removed or action deleted while invisible: drop the stealth
        Deactivate(ent, recharge: false);
    }

    private void Deactivate(Entity<ChameleonInvisibilityActionComponent> ent, bool recharge)
    {
        if (!ent.Comp.Active)
            return;

        var mob = ent.Comp.Target;
        ent.Comp.Active = false;
        ent.Comp.Target = null;
        Dirty(ent);
        _actions.SetToggled(ent.Owner, false);

        if (recharge)
            _actions.SetCooldown(ent.Owner, ent.Comp.Recharge);

        if (_net.IsClient || mob is not {} target)
            return;

        // only take the stealth away if it was ours to begin with, otherwise a cloak or
        // another source would get stripped when the mutation drops
        if (ent.Comp.AddedStealth)
            RemComp<StealthComponent>(target);
        else if (TryComp<StealthComponent>(target, out var stealth))
            _stealth.SetVisibility(target, stealth.MaxVisibility, stealth);

        ent.Comp.AddedStealth = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // the strain of staying transparent poisons the body a little every second
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ChameleonInvisibilityActionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Active || comp.Target is not {} mob || now < comp.NextTick)
                continue;

            comp.NextTick = now + TimeSpan.FromSeconds(1);

            if (_mob.IsDead(mob))
            {
                Deactivate((uid, comp), recharge: false);
                continue;
            }

            var damage = new DamageSpecifier(_proto.Index(comp.DamageType), comp.PoisonPerSecond);
            _damageable.TryChangeDamage(mob, damage, interruptsDoAfters: false);
        }
    }
}
