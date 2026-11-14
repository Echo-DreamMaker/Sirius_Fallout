using Content.Server.Destructible;
using Content.Server.Gatherable.Components;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Whitelist;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private TagSystem _tagSystem = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GatherableComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<GatherableComponent, AttackedEvent>(OnAttacked);
        InitializeProjectile();
    }

    private void OnAttacked(Entity<GatherableComponent> gatherable, ref AttackedEvent args)
    {
        if (_whitelistSystem.IsWhitelistFailOrNull(gatherable.Comp.ToolWhitelist, args.Used))
            return;

        Gather(gatherable, args.User);
    }

    private void OnActivate(Entity<GatherableComponent> gatherable, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (_whitelistSystem.IsWhitelistFailOrNull(gatherable.Comp.ToolWhitelist, args.User))
            return;

        // ActivateInWorldInteraction raises a contact event after this handler. Defer destruction until
        // that interaction has finished so the normal contact/forensics path sees a valid entity.
        Gather(args.Target, args.User, gatherable.Comp, deferDestruction: true);
        args.Handled = true;
    }
    public void Gather(EntityUid gatheredUid, EntityUid? gatherer = null, GatherableComponent? component = null,
        bool deferDestruction = false)
    {
        if (!Resolve(gatheredUid, ref component))
            return;

        if (TryComp<SoundOnGatherComponent>(gatheredUid, out var soundComp))
            _audio.PlayPvs(soundComp.Sound, Transform(gatheredUid).Coordinates);
        // Complete the gathering process
        if (deferDestruction)
        {
            Timer.Spawn(0, () =>
            {
                if (Exists(gatheredUid))
                    _destructible.DestroyEntity(gatheredUid);
            });
        }
        else
        {
            _destructible.DestroyEntity(gatheredUid);
        }

        // Spawn the loot!
        if (component.Loot == null)
            return;

        var pos = _transform.GetMapCoordinates(gatheredUid);

        foreach (var (tag, table) in component.Loot)
        {
            if (tag != "All")
            {
                if (gatherer != null && !_tagSystem.HasTag(gatherer.Value, tag))
                    continue;
            }
            var getLoot = _proto.Index(table);
            var spawnLoot = getLoot.GetSpawns(_random);

            // #Misfits Fix - Prevent ArgumentOutOfRangeException when spawnLoot is empty
            if (spawnLoot.Count == 0)
                continue;

            var spawnPos = pos.Offset(_random.NextVector2(component.GatherOffset));
            Spawn(spawnLoot[0], spawnPos);
        }
    }
}
