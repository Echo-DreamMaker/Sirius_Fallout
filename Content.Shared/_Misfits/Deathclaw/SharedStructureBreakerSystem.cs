// #Cythisiax Added - Allows Bwonsamdi to target explicitly indestructible structures
// before normal melee rejects them for lacking Damageable.
using Content.Shared.Tag;

namespace Content.Shared._Misfits.Deathclaw;

public sealed class SharedStructureBreakerSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StructureBreakerComponent, MeleeNonDamageableTargetAttemptEvent>(OnTargetAttempt);
    }

    private void OnTargetAttempt(
        Entity<StructureBreakerComponent> ent,
        ref MeleeNonDamageableTargetAttemptEvent args)
    {
        if (args.Allowed || TerminatingOrDeleted(args.Target))
            return;

        args.Allowed = IsSpecialTarget(args.Target);
    }

    public bool IsSpecialTarget(EntityUid target)
    {
        if (TerminatingOrDeleted(target) || !Transform(target).Anchored)
            return false;

        var prototypeId = MetaData(target).EntityPrototype?.ID;
        return prototypeId != null
            && (prototypeId.Contains("Indestructible", StringComparison.OrdinalIgnoreCase)
                || _tag.HasTag(target, "BwonsamdiBreakable"));
    }
}
