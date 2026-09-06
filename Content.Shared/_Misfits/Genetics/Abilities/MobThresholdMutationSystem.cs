// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared._Misfits.Genetics.Mutations;

namespace Content.Shared._Misfits.Genetics.Abilties;

public sealed partial class MobThresholdMutationSystem : EntitySystem
{
    [Dependency] private MobThresholdSystem _threshold = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobThresholdMutationComponent, MutationAddedEvent>(OnAdded);
        SubscribeLocalEvent<MobThresholdMutationComponent, MutationRemovedEvent>(OnRemoved);
    }

    private void OnAdded(Entity<MobThresholdMutationComponent> ent, ref MutationAddedEvent args)
    {
        var target = args.Target;
        if (!TryComp<MobStateComponent>(target, out var mob))
            return;

        var states = mob.AllowedStates;
        var state = ent.Comp.Removed;
        if (!states.Contains(state))
            return;

        states.Remove(state);
        Dirty(target, mob);

        ent.Comp.RemovedState = true;
        Dirty(ent);

        if (!TryComp<MobThresholdsComponent>(target, out var thresholds))
            return;

        var threshold = _threshold.GetThresholdForState(target, state, thresholds);
        if (threshold == FixedPoint2.Zero)
            return;

        var dict = thresholds.Thresholds;
        dict.Remove(threshold);
        Dirty(target, thresholds);

        ent.Comp.OldThreshold = threshold;
        Dirty(ent);

        // recheck now, a mob already sitting in the removed state has nothing mapping it anymore
        _threshold.VerifyThresholds(target, thresholds);
    }

    private void OnRemoved(Entity<MobThresholdMutationComponent> ent, ref MutationRemovedEvent args)
    {
        if (!ent.Comp.RemovedState)
            return;

        var target = args.Target;
        var state = ent.Comp.Removed;

        var old = ent.Comp.OldThreshold;
        ent.Comp.RemovedState = false;
        ent.Comp.OldThreshold = null;
        Dirty(ent);

        if (TryComp<MobStateComponent>(target, out var mob) && mob.AllowedStates.Add(state))
            Dirty(target, mob);

        // the threshold is restored separately, the mob may not have had one to take
        if (old is not {} threshold || !TryComp<MobThresholdsComponent>(target, out var thresholds))
            return;

        // re-verifies state afterwards, the mob may already be past the restored threshold
        _threshold.SetMobStateThreshold(target, threshold, state, thresholds);
    }
}
