// #Misfits Add - Tracks door destruction events and logs them
using System.Linq;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared._Misfits.Administration.DoorLogs;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Doors.Components;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Server._Misfits.Administration.DoorLogs;

/// <summary>
/// Listens for doors taking lethal damage and logs the destruction
/// with the door prototype and the player/mob responsible.
/// </summary>
public sealed class DoorLogSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// All recorded door destruction entries.
    /// </summary>
    public readonly List<DoorLogEntry> Entries = new();
    private readonly HashSet<DoorLogsEui> _openUis = new();

    /// <summary>
    /// Tracks the last attacker per door so we know who to blame at destruction time.
    /// </summary>
    private readonly Dictionary<EntityUid, EntityUid> _lastAttacker = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoorComponent, DamageChangedEvent>(OnDoorDamaged);
        SubscribeLocalEvent<DoorComponent, DamageThresholdReached>(OnDoorThresholdReached);
    }

    private void OnDoorDamaged(EntityUid uid, DoorComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.Origin == null)
            return;

        _lastAttacker[uid] = args.Origin.Value;
    }

    private void OnDoorThresholdReached(EntityUid uid, DoorComponent comp, ref DamageThresholdReached args)
    {
        if (!args.Threshold.Behaviors.OfType<DoActsBehavior>().Any(b =>
                b.HasAct(ThresholdActs.Destruction) || b.HasAct(ThresholdActs.Breakage)))
        {
            return;
        }

        var doorProto = MetaData(uid).EntityPrototype?.ID ?? "Unknown";

        string destroyerName;
        if (_lastAttacker.TryGetValue(uid, out var attacker))
        {
            _lastAttacker.Remove(uid);
            destroyerName = GetAttackerName(attacker);
        }
        else
        {
            destroyerName = "Unknown";
        }

        var entry = new DoorLogEntry(doorProto, destroyerName, _timing.CurTime);
        Entries.Add(entry);

        foreach (var ui in _openUis)
        {
            if (!ui.IsShutDown)
                ui.StateDirty();
        }
    }

    /// <summary>
    /// Resolves a human-readable name for the attacking entity.
    /// </summary>
    private string GetAttackerName(EntityUid attacker)
    {
        // Check if it's a player
        if (_playerManager.TryGetSessionByEntity(attacker, out var session))
            return session.Name;

        // Fall back to the entity's pretty-printed name
        return ToPrettyString(attacker);
    }

    /// <summary>
    /// Returns all log entries, newest first.
    /// </summary>
    public List<DoorLogEntry> GetEntries()
    {
        return Entries.OrderByDescending(e => e.Time).ToList();
    }

    public void RegisterUi(DoorLogsEui ui)
    {
        _openUis.Add(ui);
    }

    public void UnregisterUi(DoorLogsEui ui)
    {
        _openUis.Remove(ui);
    }
}
