// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid;
using Content.Shared._Misfits.Genetics.Mutations;
using Robust.Shared.Network;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Force-adds the listed markings to the mob when this mutation is added.
/// Used by Felinized so the ears and tail actually show up on the mutated body.
/// Removal is handled by the polymorph reverting to the original body.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AddMarkingsMutationComponent : Component
{
    /// <summary>
    /// Marking prototype ids to force onto the mob.
    /// </summary>
    [DataField(required: true)]
    public List<string> Markings = new();
}

public sealed class AddMarkingsMutationSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _humanoid = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddMarkingsMutationComponent, MutationAddedEvent>(OnAdded);
    }

    private void OnAdded(Entity<AddMarkingsMutationComponent> ent, ref MutationAddedEvent args)
    {
        // server-authoritative, marking changes are networked through humanoid appearance.
        // this also runs when mutations transfer to a polymorphed body, so the felinid
        // body gets its ears and tail after the transformation.
        if (_net.IsClient)
            return;

        var target = args.Target.Owner;
        if (!TryComp<HumanoidAppearanceComponent>(target, out var humanoid))
            return;

        foreach (var marking in ent.Comp.Markings)
        {
            // this deliberately reruns after a polymorph transfer so the new body gets the
            // markings, which means it can fire more than once on the same body - without
            // this check you end up wearing two sets of ears.
            if (HasMarking(humanoid, marking))
                continue;

            _humanoid.AddMarking(target, marking, sync: true, forced: true, humanoid: humanoid);
        }
    }

    private static bool HasMarking(HumanoidAppearanceComponent humanoid, string id)
    {
        foreach (var (_, markings) in humanoid.MarkingSet.Markings)
        {
            foreach (var marking in markings)
            {
                if (marking.MarkingId == id)
                    return true;
            }
        }

        return false;
    }
}
