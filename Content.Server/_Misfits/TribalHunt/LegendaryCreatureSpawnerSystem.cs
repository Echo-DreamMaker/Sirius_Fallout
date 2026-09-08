using System.Collections.Generic;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._Misfits.TribalHunt;
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Misfits.TribalHunt;

/// <summary>
/// Handles spawning, tracking, and loot drops for tribal hunt targets.
/// Legendary hunts use a single scaled creature; minor hunts use a nearby pack.
/// </summary>
public sealed partial class LegendaryCreatureSpawnerSystem : EntitySystem
{
    private const int SpawnAttempts = 30;
    private const float MinSpawnDistance = 100f;
    private const float MaxSpawnDistance = 500f;
    private const float MinorPackDistanceMin = 35f;
    private const float MinorPackDistanceMax = 120f;
    private const float MinorPackMemberDistanceMin = 2f;
    private const float MinorPackMemberDistanceMax = 12f;
    private const int LegendaryHealthMultiplier = 3;

    private static readonly (string PrototypeId, string CreatureName, int MinCount, int MaxCount)[] MinorHuntPool =
    {
        ("N14MobDogFeral", "feral dog", 3, 6),
        ("N14MobDogFeral", "feral dog", 3, 6),
        ("N14MobMolerat", "molerat", 4, 6),
        ("N14MobMolerat", "molerat", 4, 6),
        ("N14MobGecko", "gecko", 3, 5),
        ("N14MobGecko", "gecko", 3, 5),
        ("N14MobGeckoFire", "fire gecko", 2, 4),
        ("N14MobGeckoGolden", "golden gecko", 2, 3),
        ("N14MobRadscorpion", "radscorpion", 2, 4),
        ("N14MobRadscorpion", "radscorpion", 2, 4),
        ("N14MobMirelurk", "mirelurk", 2, 3),
        // Minor hunts can roll yao guai, but they are capped to pairs so they stay minor.
        ("N14MobYaoguai", "yao guai", 2, 2),
    };

    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedMapSystem _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Subscribe to MobStateChanged instead of DestructionEventArgs so the entity
        // still has a valid TransformComponent when we spawn loot and raise events.
        SubscribeLocalEvent<LegendaryCreatureComponent, MobStateChangedEvent>(OnCreatureMobStateChanged);
    }

    /// <summary>
    /// Spawns a legendary creature near the hunt leader on a valid, unobstructed tile.
    /// </summary>
    public EntityUid? TrySpawnLegendaryCreature(string creatureProto, EntityUid huntSessionId, MapCoordinates leaderMapCoords)
    {
        EntityCoordinates spawnCoords = default;
        var foundSpawn = false;

        for (var i = 0; i < SpawnAttempts; i++)
        {
            var offset = _random.NextVector2(MinSpawnDistance, MaxSpawnDistance);
            var candidate = new MapCoordinates(leaderMapCoords.Position + offset, leaderMapCoords.MapId);

            if (!TryGetValidSpawnCoordinates(candidate, out spawnCoords))
                continue;

            foundSpawn = true;
            break;
        }

        if (!foundSpawn)
            return null;

        var creature = Spawn(creatureProto, spawnCoords);
        ApplyLegendaryHealthMultiplier(creature);

        var legComp = EnsureComp<LegendaryCreatureComponent>(creature);
        legComp.HuntSessionId = huntSessionId;
        legComp.CreatureName = "Deathclaw";
        legComp.LeatherDropCount = 3;
        legComp.RevealLocation = true;
        Dirty(creature, legComp);

        return creature;
    }

    /// <summary>
    /// Spawns a randomized minor-hunt pack near the hunt caller.
    /// The pack center is intentionally much closer than a legendary hunt target.
    /// </summary>
    public List<EntityUid>? TrySpawnMinorHuntPack(EntityUid huntSessionId, MapCoordinates leaderMapCoords, out string creatureName)
    {
        creatureName = string.Empty;
        var availablePool = new List<(string PrototypeId, string CreatureName, int MinCount, int MaxCount)>();

        foreach (var entry in MinorHuntPool)
        {
            if (_prototypeManager.TryIndex<EntityPrototype>(entry.PrototypeId, out _))
                availablePool.Add(entry);
        }

        if (availablePool.Count == 0)
            return null;

        var definition = availablePool[_random.Next(availablePool.Count)];
        creatureName = definition.CreatureName;
        var desiredCount = _random.Next(definition.MinCount, definition.MaxCount + 1);

        EntityCoordinates packCenterCoords = default;
        MapCoordinates packCenterMapCoords = default;
        var centerFound = false;

        for (var i = 0; i < SpawnAttempts; i++)
        {
            var offset = _random.NextVector2(MinorPackDistanceMin, MinorPackDistanceMax);
            var candidate = new MapCoordinates(leaderMapCoords.Position + offset, leaderMapCoords.MapId);

            if (!TryGetValidSpawnCoordinates(candidate, out packCenterCoords))
                continue;

            packCenterMapCoords = candidate;
            centerFound = true;
            break;
        }

        if (!centerFound)
            return null;

        var pack = new List<EntityUid>(desiredCount);
        for (var i = 0; i < desiredCount; i++)
        {
            var spawnCoords = packCenterCoords;
            var foundSpawn = false;

            for (var attempt = 0; attempt < SpawnAttempts; attempt++)
            {
                var offset = _random.NextVector2(MinorPackMemberDistanceMin, MinorPackMemberDistanceMax);
                var candidate = new MapCoordinates(packCenterMapCoords.Position + offset, packCenterMapCoords.MapId);

                if (!TryGetValidSpawnCoordinates(candidate, out spawnCoords))
                    continue;

                foundSpawn = true;
                break;
            }

            if (!foundSpawn)
                continue;

            var creature = Spawn(definition.PrototypeId, spawnCoords);
            var minorComp = EnsureComp<MinorHuntCreatureComponent>(creature);
            minorComp.HuntSessionId = huntSessionId;
            minorComp.CreatureName = definition.CreatureName;
            minorComp.RevealLocation = true;
            pack.Add(creature);
        }

        if (pack.Count < definition.MinCount)
        {
            foreach (var creature in pack)
            {
                if (Exists(creature))
                    QueueDel(creature);
            }

            return null;
        }

        return pack;
    }

    private void ApplyLegendaryHealthMultiplier(EntityUid creature)
    {
        if (!TryComp(creature, out MobThresholdsComponent? thresholds))
            return;

        ScaleThreshold(creature, MobState.SoftCritical, thresholds);
        ScaleThreshold(creature, MobState.Critical, thresholds);
        ScaleThreshold(creature, MobState.Dead, thresholds);
    }

    private void ScaleThreshold(EntityUid creature, MobState state, MobThresholdsComponent thresholds)
    {
        var threshold = _mobThreshold.GetThresholdForState(creature, state, thresholds);
        if (threshold <= 0)
            return;

        _mobThreshold.SetMobStateThreshold(creature, threshold * LegendaryHealthMultiplier, state, thresholds);
    }

    private bool TryGetValidSpawnCoordinates(MapCoordinates mapCoords, out EntityCoordinates spawnCoords)
    {
        spawnCoords = default;

        if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var gridComp))
            return false;

        var tileIndices = _mapSystem.CoordinatesToTile(gridUid, gridComp, mapCoords);
        var gridCoords = _mapSystem.GridTileToLocal(gridUid, gridComp, tileIndices);
        var tileRef = _mapSystem.GetTileRef(gridUid, gridComp, gridCoords);

        // #Misfits Fix - Also reject air-blocked tiles (inside walls, sealed rooms) and
        // space tiles. Using only IsTileBlocked with MobMask missed fully enclosed areas
        // where no mob-collidable fixture overlaps the exact tile center.
        if (tileRef.Tile.IsSpace()
            || _turf.IsTileBlocked(tileRef, CollisionGroup.MobMask)
            || _atmosphere.IsTileAirBlocked(gridUid, tileIndices, mapGridComp: gridComp))
            return false;

        spawnCoords = gridCoords;
        return true;
    }

    /// <summary>
    /// Fires when the creature transitions to Dead. The entity still has a valid
    /// TransformComponent here, so physics queries and loot spawns are safe.
    /// </summary>
    private void OnCreatureMobStateChanged(EntityUid uid, LegendaryCreatureComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        // Cache coordinates while the entity is still fully intact.
        var coords = _transformSystem.GetMoverCoordinates(uid);

        RaiseLocalEvent(uid, new LegendaryCreatureKilledEvent());

        // Spawn loot at the cached position instead of querying the dying entity's physics.
        for (var i = 0; i < comp.LeatherDropCount; i++)
        {
            Spawn("TribalLegendaryLeather", coords);
        }
    }
}
