using System.Diagnostics.CodeAnalysis;
using Content.Server.SurveillanceCamera;
using Content.Shared._Misfits.Silicon;
using Content.Shared.DoAfter;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server._Misfits.Silicon;

/// <summary>
/// [Changed by MisfitsCrew/Operator] Lets Z.A.X/Station AI remotely compromise visible cameras
/// that are not currently available for AI interaction.
/// </summary>
public sealed class StationAiCameraHackSystem : EntitySystem
{
    private static readonly TimeSpan HackDelay = TimeSpan.FromSeconds(30);
    private static readonly SoundSpecifier HackSound = new SoundCollectionSpecifier("sparks");

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StationAiVisionSystem _vision = default!;

    private EntityQuery<BroadphaseComponent> _broadphaseQuery;
    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();

        _broadphaseQuery = GetEntityQuery<BroadphaseComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<StationAiHeldComponent, StationAiHackCameraActionEvent>(OnHackCamera);
        SubscribeLocalEvent<SurveillanceCameraComponent, StationAiHackCameraDoAfterEvent>(OnHackDoAfter);
    }

    private void OnHackCamera(Entity<StationAiHeldComponent> ent, ref StationAiHackCameraActionEvent args)
    {
        if (args.Handled ||
            !ValidateAi(ent) ||
            !TryComp(args.Target, out SurveillanceCameraComponent? _) ||
            HasCameraAccess(args.Target) ||
            !CanSee(ent.Owner, Transform(args.Target).Coordinates))
        {
            return;
        }

        args.Handled = true;

        var doAfterComp = EnsureComp<DoAfterComponent>(args.Target);
        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            args.Target,
            HackDelay,
            new StationAiHackCameraDoAfterEvent(),
            args.Target,
            target: args.Target)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
            NeedHand = false,
            RequireCanInteract = false
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, doAfterComp))
            return;

        _audio.PlayPvs(HackSound, args.Target, AudioParams.Default.WithVolume(-10f).WithMaxDistance(6f));
    }

    private void OnHackDoAfter(Entity<SurveillanceCameraComponent> ent, ref StationAiHackCameraDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var vision = EnsureComp<StationAiVisionComponent>(ent.Owner);
        if (!vision.Enabled)
            _stationAi.SetVisionEnabled((ent.Owner, vision), true);

        var whitelist = EnsureComp<StationAiWhitelistComponent>(ent.Owner);
        if (!whitelist.Enabled)
            _stationAi.SetWhitelistEnabled((ent.Owner, whitelist), true);
    }

    private bool HasCameraAccess(EntityUid uid)
    {
        return TryComp(uid, out StationAiWhitelistComponent? whitelist) && whitelist.Enabled;
    }

    private bool ValidateAi(Entity<StationAiHeldComponent> ai)
    {
        if (!TryGetCore(ai, out var core))
            return false;

        SharedApcPowerReceiverComponent? receiver = null;
        return _power.IsPowered((core.Value.Owner, receiver));
    }

    private bool TryGetCore(
        Entity<StationAiHeldComponent> ai,
        [NotNullWhen(true)] out Entity<StationAiCoreComponent>? core)
    {
        core = null;

        if (!_container.TryGetContainingContainer((ai.Owner, null, null), out var container) ||
            container.ID != StationAiCoreComponent.Container ||
            !TryComp(container.Owner, out StationAiCoreComponent? coreComp))
        {
            return false;
        }

        core = (container.Owner, coreComp);
        return true;
    }

    private bool CanSee(EntityUid ai, EntityCoordinates coordinates)
    {
        if (!TryComp(ai, out StationAiHeldComponent? held) ||
            !TryGetCore((ai, held), out var core))
        {
            return false;
        }

        var targetMap = coordinates.ToMap(EntityManager, _transform);
        if (!TryGetGrid(coordinates, targetMap, out var gridUid, out var grid) ||
            !_broadphaseQuery.TryComp(gridUid, out var broadphase) ||
            Transform(core.Value.Owner).GridUid != gridUid)
        {
            return false;
        }

        var targetTile = _map.LocalToTile(gridUid, grid, coordinates);
        lock (_vision)
        {
            return _vision.IsAccessible((gridUid, broadphase, grid), targetTile);
        }
    }

    private bool TryGetGrid(
        EntityCoordinates coordinates,
        MapCoordinates mapCoordinates,
        out EntityUid gridUid,
        [NotNullWhen(true)] out MapGridComponent? grid)
    {
        gridUid = EntityUid.Invalid;
        grid = null;

        if (_gridQuery.TryComp(coordinates.EntityId, out grid))
        {
            gridUid = coordinates.EntityId;
            return true;
        }

        var resolvedGrid = _transform.GetGrid(coordinates);
        if (resolvedGrid != null && _gridQuery.TryComp(resolvedGrid.Value, out grid))
        {
            gridUid = resolvedGrid.Value;
            return true;
        }

        return _mapManager.TryFindGridAt(mapCoordinates, out gridUid, out grid);
    }
}
