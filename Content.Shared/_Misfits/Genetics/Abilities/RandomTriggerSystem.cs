// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Misfits.Genetics.Mutations;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared._Misfits.Genetics.Abilities;

public sealed class RandomTriggerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MutationTriggerSystem _triggers = default!;

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RandomTriggerComponent, EffectOnTriggerMutationComponent, MutationComponent>();
        while (query.MoveNext(out var uid, out var random, out _, out var mutation))
        {
            if (now < random.NextUpdate)
                continue;
            random.NextUpdate = now + random.UpdateDelay;
            if (mutation.Target is not {} target || !_random.Prob(random.Prob))
                continue;

            _triggers.Apply(uid);
        }
    }
}
