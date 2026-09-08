using System.Numerics;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared._Misfits.Special;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server.Standing;

public sealed class StandingStateSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;

    // A character with high Agility keeps their held weapon when falling:
    // > 4 when lying down deliberately, > 6 when knocked/forced down.
    private const int VoluntaryLieKeepAgility = 4;
    private const int ForcedFallKeepAgility = 6;

    private void FallOver(EntityUid uid, StandingStateComponent component, DropHandItemsEvent args)
    {
        var direction = EntityManager.TryGetComponent(uid, out PhysicsComponent? comp) ? comp.LinearVelocity / 50 : Vector2.Zero;
        var dropAngle = _random.NextFloat(0.8f, 1.2f);

        var fellEvent = new FellDownEvent(uid);
        RaiseLocalEvent(uid, fellEvent, false);

        if (!TryComp(uid, out HandsComponent? handsComp))
            return;

        // #Misfits Change /Add/: agile characters keep their grip and don't drop held items.
        var keepAgility = args.Voluntary ? VoluntaryLieKeepAgility : ForcedFallKeepAgility;
        if (_special.UsesSpecialStats(uid) && _special.GetEffective(uid, SpecialStat.Agility) > keepAgility)
            return;

        var worldRotation = EntityManager.GetComponent<TransformComponent>(uid).WorldRotation.ToVec();
        foreach (var hand in handsComp.Hands.Values)
        {
            if (hand.HeldEntity is not EntityUid held)
                continue;

            if (!_handsSystem.TryDrop(uid, hand, null, checkActionBlocker: false, handsComp: handsComp))
                continue;

            _throwingSystem.TryThrow(held,
                _random.NextAngle().RotateVec(direction / dropAngle + worldRotation / 50),
                0.5f * dropAngle * _random.NextFloat(-0.9f, 1.1f),
                uid, 0);
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StandingStateComponent, DropHandItemsEvent>(FallOver);
    }

}

    /// <summary>
    /// Raised after an entity falls down.
    /// </summary>
    public sealed class FellDownEvent : EntityEventArgs
    {
        public EntityUid Uid { get; }
        public FellDownEvent(EntityUid uid)
        {
            Uid = uid;
        }
    }
