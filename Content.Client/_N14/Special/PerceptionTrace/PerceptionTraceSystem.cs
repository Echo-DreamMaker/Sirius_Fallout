using System.Numerics;
using Content.Shared.Body.Components;
using Content.Shared._Misfits.Special;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._N14.Special.PerceptionTrace;

/// <summary>
/// Client-side motion-trace "infrared" vision. Characters with high Perception briefly see
/// lingering afterimages of living mobs that moved recently, drawn over walls like IR goggles.
/// The afterimage opacity grows with Perception.
/// </summary>
public sealed class PerceptionTraceSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    private PerceptionTraceOverlay _overlay = default!;

    public Color CurrentColor { get; private set; } = Color.TryFromHex("#ff5a2a") ?? new Color(1f, 0.35f, 0.16f);
    public float CurrentAlpha { get; private set; }

    public float RevealRadius { get; private set; } = 13f;

    private readonly Dictionary<EntityUid, Vector2> _lastPositions = new();
    private readonly Dictionary<EntityUid, TimeSpan> _activeTimers = new();
    private readonly List<EntityUid> _activeCache = new();

    private float _persistenceTime = 3f;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new PerceptionTraceOverlay(this);

        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_overlay == null || !_overlayMan.HasOverlay<PerceptionTraceOverlay>())
            return;

        var player = _player.LocalEntity;
        if (player == null)
        {
            CurrentAlpha = 0f;
            return;
        }

        var alpha = _special.GetPerceptionTraceAlpha(player.Value) ?? 0f;
        if (alpha <= 0f)
        {
            CurrentAlpha = 0f;
            return;
        }

        CurrentAlpha = alpha;

        var tuning = _special.GetTuning();
        var now = _timing.CurTime;
        var range = tuning.PerceptionTraceRange;
        var persistence = tuning.PerceptionTracePersistenceTime;

        // Track how far we can reveal (radius field is consumed by the overlay enumerator).
        // Update persisted settings only when they change to avoid needless churn.
        if (Math.Abs(RevealRadius - range) > 0.001f)
            RevealRadius = range;
        if (Math.Abs(_persistenceTime - persistence) > 0.001f)
            _persistenceTime = persistence;

        // Prune expired afterimages.
        _activeCache.Clear();
        foreach (var (uid, expire) in _activeTimers)
        {
            if (expire <= now)
                _activeCache.Add(uid);
        }

        foreach (var uid in _activeCache)
            _activeTimers.Remove(uid);

        // Refresh motion timers from live positions of living mobs.
        var playerPos = _transform.GetMapCoordinates(player.Value);
        var rangeSq = RevealRadius * RevealRadius;
        var mapId = playerPos.MapId;

        var query = EntityQueryEnumerator<BodyComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            if (_lastPositions.TryGetValue(uid, out var last))
            {
                if ((pos - last).LengthSquared() > 0.0001f)
                    _activeTimers[uid] = now + TimeSpan.FromSeconds(_persistenceTime);
            }

            _lastPositions[uid] = pos;
        }
    }

    /// <summary>
    /// Enumerates the currently-visible afterimage targets near the local player.
    /// Returns each target together with a fade factor (1 = fully visible, decreasing to 0
    /// as the afterimage nears expiry) so the overlay can fade the ghost out smoothly.
    /// </summary>
    public IEnumerable<(EntityUid Uid, float Fade)> EnumerateActive()
    {
        var player = _player.LocalEntity;
        if (player == null)
            yield break;

        var playerCoords = _transform.GetMapCoordinates(player.Value);
        var now = _timing.CurTime;
        var rangeSq = RevealRadius * RevealRadius;

        foreach (var (uid, expire) in _activeTimers)
        {
            if (expire <= now || uid == player.Value)
                continue;

            var coords = _transform.GetMapCoordinates(uid);
            if (coords.MapId != playerCoords.MapId)
                continue;

            if (Vector2.DistanceSquared(playerCoords.Position, coords.Position) > rangeSq)
                continue;

            var remaining = (expire - now).TotalSeconds;
            var fade = Math.Clamp((float)(remaining / Math.Max(_persistenceTime, 0.001f)), 0f, 1f);

            yield return (uid, fade);
        }
    }

    private void OnPlayerAttached(LocalPlayerAttachedEvent args)
    {
        if (!_overlayMan.HasOverlay<PerceptionTraceOverlay>())
            _overlayMan.AddOverlay(_overlay);
    }

    private void OnPlayerDetached(LocalPlayerDetachedEvent args)
    {
        CurrentAlpha = 0f;
        _activeTimers.Clear();
        _overlayMan.RemoveOverlay(_overlay);
    }
}
