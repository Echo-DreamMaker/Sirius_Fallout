using Content.Shared._Misfits.Overwatch;
using Robust.Client.Audio;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;

namespace Content.Client._Misfits.Overwatch;

public sealed class OverwatchConsoleSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly List<(Entity<AudioComponent, OverwatchRelayedSoundComponent> Audio, EntityCoordinates Position)> _toRelay = new();
    private OverwatchTargetOverlay? _targetOverlay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OverwatchRelayedSoundComponent, ComponentRemove>(OnRelayedRemove);
        SubscribeLocalEvent<OverwatchRelayedSoundComponent, EntityTerminatingEvent>(OnRelayedRemove);
    }

    private void OnRelayedRemove<T>(Entity<OverwatchRelayedSoundComponent> ent, ref T args)
    {
        TryDeleteRelayed(ent.Comp.Relay);
    }

    private void TryDeleteRelayed(EntityUid? relay)
    {
        if (relay == null)
            return;

        if (IsClientSide(relay.Value))
            QueueDel(relay.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateTargetOverlay();

        if (_player.LocalEntity is not { } player ||
            !TryComp<OverwatchWatchingComponent>(player, out var watching) ||
            watching.Watching is not { } watchedTarget ||
            !TryComp(watchedTarget, out TransformComponent? watchedTransform))
        {
            ClearRelayedAudio();
            return;
        }

        _toRelay.Clear();

        var listenerEye = _eye.CurrentEye.Position;
        var listenerCoords = _transform.ToCoordinates(listenerEye);
        var watchedCoords = watchedTransform.Coordinates;
        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            var audioCoords = xform.Coordinates;
            if (!audioCoords.TryDelta(EntityManager, _transform, watchedCoords, out var watchedDelta))
            {
                RemCompDeferred<OverwatchRelayedSoundComponent>(uid);
                continue;
            }

            if (watchedDelta.LengthSquared() > audio.MaxDistance * audio.MaxDistance)
            {
                RemCompDeferred<OverwatchRelayedSoundComponent>(uid);
                continue;
            }

            if (audioCoords.TryDelta(EntityManager, _transform, listenerCoords, out var listenerDelta) &&
                listenerDelta.LengthSquared() <= audio.MaxDistance * audio.MaxDistance)
            {
                RemCompDeferred<OverwatchRelayedSoundComponent>(uid);
                continue;
            }

            var position = listenerEye.Offset(watchedDelta);
            var relayed = EnsureComp<OverwatchRelayedSoundComponent>(uid);
            if (relayed.Relay != null && !TerminatingOrDeleted(relayed.Relay))
            {
                _transform.SetMapCoordinates(relayed.Relay.Value, position);
                continue;
            }

            _toRelay.Add(((uid, audio, relayed), _transform.ToCoordinates(position)));
        }

        foreach (var (audio, coordinates) in _toRelay)
        {
            var relayedAudio = _audio.PlayStatic(
                new SoundPathSpecifier(audio.Comp1.FileName),
                player,
                coordinates,
                audio.Comp1.Params);

            if (relayedAudio is not { Entity: var relayedAudioEnt })
                continue;

            _audio.SetPlaybackPosition(relayedAudioEnt, audio.Comp1.PlaybackPosition);
            audio.Comp2.Relay = relayedAudioEnt;
        }
    }

    /// <summary>
    /// Returns the local player's current watch session, if any. Used by the
    /// tactical-map and holotape windows to resolve their own feed and roster highlight.
    /// </summary>
    public OverwatchWatchingComponent? GetLocalWatch()
    {
        if (_player.LocalEntity is not { } player ||
            !TryComp<OverwatchWatchingComponent>(player, out var watching))
        {
            return null;
        }

        return watching;
    }

    private void UpdateTargetOverlay()
    {
        var isWatched = _player.LocalEntity is { } player &&
                        TryComp<OverwatchTargetComponent>(player, out var target) &&
                        target.WatcherNames.Count > 0;

        if (isWatched)
        {
            if (_targetOverlay == null)
            {
                _targetOverlay = new OverwatchTargetOverlay(EntityManager, _player, _resourceCache);
                _overlayManager.AddOverlay(_targetOverlay);
            }
        }
        else if (_targetOverlay != null)
        {
            _overlayManager.RemoveOverlay(_targetOverlay);
            _targetOverlay = null;
        }
    }

    private void ClearRelayedAudio()
    {
        var relayQuery = AllEntityQuery<OverwatchRelayedSoundComponent>();
        while (relayQuery.MoveNext(out var uid, out var relay))
        {
            TryDeleteRelayed(relay.Relay);
            RemCompDeferred<OverwatchRelayedSoundComponent>(uid);
        }
    }
}
