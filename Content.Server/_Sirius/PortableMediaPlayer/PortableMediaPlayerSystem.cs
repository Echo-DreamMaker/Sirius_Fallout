using System.Linq;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared._Sirius.PortableMediaPlayer;
using Content.Shared.Audio.Jukebox;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sirius.PortableMediaPlayer;

public sealed class PortableMediaPlayerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PortableMediaPlayerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PortableMediaPlayerComponent, PortableMediaPlayerMessage>(OnUiMessage);
        SubscribeNetworkEvent<PipBoyMusicTrackFinishedEvent>(OnTrackFinished);
        SubscribeNetworkEvent<PipBoyMusicSyncRequestEvent>(OnSyncRequest);
        SubscribeNetworkEvent<PipBoyMusicPositionSyncEvent>(OnPositionSync);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnUiOpened(EntityUid uid, PortableMediaPlayerComponent component, BoundUIOpenedEvent args)
    {
        UpdateUiState((uid, component));
    }

    private void OnUiMessage(EntityUid uid, PortableMediaPlayerComponent component, PortableMediaPlayerMessage args)
    {
        var actor = args.Actor;
        component.OwnerEntity = actor;
        component.AudibleToOthers = args.AudibleToOthers;

        var netSource = GetNetEntity(uid);

        switch (args.Action)
        {
            case PipBoyMusicUiAction.Select:
                if (args.TrackId == null || !_proto.HasIndex<JukeboxPrototype>(args.TrackId))
                    return;
                component.SelectedTrackId = args.TrackId;
                component.PlaybackOffset = 0f;
                if (component.IsPlaying)
                    PlayCurrent((uid, component), actor);
                break;

            case PipBoyMusicUiAction.Play:
                if (component.SelectedTrackId == null)
                    return;
                component.IsPlaying = true;
                PlayCurrent((uid, component), actor);
                break;

            case PipBoyMusicUiAction.Pause:
                component.IsPlaying = false;
                BroadcastPause(component, actor, netSource);
                break;

            case PipBoyMusicUiAction.Stop:
                component.IsPlaying = false;
                component.PlaybackOffset = 0f;
                BroadcastStop(component, actor, netSource);
                break;

            case PipBoyMusicUiAction.Seek:
                component.PlaybackOffset = args.SeekPosition;
                BroadcastSeek(component, actor, netSource, args.SeekPosition);
                break;

            case PipBoyMusicUiAction.ToggleRepeat:
                component.RepeatOne = !component.RepeatOne;
                break;

            case PipBoyMusicUiAction.ToggleAutoNext:
                component.AutoNext = !component.AutoNext;
                break;

            default:
                return;
        }

        UpdateUiState((uid, component));
    }

    private void OnPositionSync(PipBoyMusicPositionSyncEvent ev, EntitySessionEventArgs args)
    {
        if (ev.FromPipBoy)
            return;

        var source = GetEntity(ev.LoaderUid);
        if (!TryComp<PortableMediaPlayerComponent>(source, out var comp))
            return;

        comp.PlaybackOffset = ev.Position;
    }

    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        var session = args.Player;
        var detachedEntity = args.Entity;

        var query = EntityQueryEnumerator<PortableMediaPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.OwnerEntity != detachedEntity)
                continue;

            comp.IsPlaying = false;
            comp.PlaybackOffset = 0f;
            RaiseNetworkEvent(new PipBoyMusicStopEvent(GetNetEntity(uid)), session);
            UpdateUiState((uid, comp));
        }
    }

    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var session = args.Player;
        var newEntity = args.Entity;

        var query = EntityQueryEnumerator<PortableMediaPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.OwnerEntity != newEntity)
                continue;

            comp.IsPlaying = false;
            comp.PlaybackOffset = 0f;
            RaiseNetworkEvent(new PipBoyMusicStopEvent(GetNetEntity(uid)), session);
            UpdateUiState((uid, comp));
        }
    }

    private void OnTrackFinished(PipBoyMusicTrackFinishedEvent ev, EntitySessionEventArgs args)
    {
        var source = GetEntity(ev.LoaderUid);

        if (!TryComp<PortableMediaPlayerComponent>(source, out var comp))
            return;

        if (comp.OwnerEntity == null)
            return;

        comp.PlaybackOffset = 0f;

        if (comp.RepeatOne)
            PlayCurrent((source, comp), comp.OwnerEntity.Value);
        else if (comp.AutoNext)
            AdvanceToNext((source, comp), comp.OwnerEntity.Value);
        else
            comp.IsPlaying = false;

        UpdateUiState((source, comp));
    }

    private void OnSyncRequest(PipBoyMusicSyncRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        var query = EntityQueryEnumerator<PortableMediaPlayerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsPlaying || comp.SelectedTrackId == null)
                continue;
            if (comp.OwnerEntity == session.AttachedEntity)
                continue;
            if (!_proto.TryIndex<JukeboxPrototype>(comp.SelectedTrackId, out var proto))
                continue;

            var playEv = new PipBoyMusicPlayEvent(
                proto.Path.Path.ToString(),
                comp.Volume,
                GetNetEntity(uid),
                comp.AudibleToOthers,
                startPosition: comp.PlaybackOffset,
                isOwn: false,
                fromPipBoy: false);
            RaiseNetworkEvent(playEv, session);
        }
    }

    private void AdvanceToNext(Entity<PortableMediaPlayerComponent> ent, EntityUid actor)
    {
        var tracks = GetSortedTracks();
        if (tracks.Count == 0)
            return;

        var currentIdx = -1;
        if (ent.Comp.SelectedTrackId != null)
            currentIdx = tracks.FindIndex(t => t.Id == ent.Comp.SelectedTrackId);

        var nextIdx = (currentIdx + 1) % tracks.Count;
        ent.Comp.SelectedTrackId = tracks[nextIdx].Id;
        ent.Comp.IsPlaying = true;
        ent.Comp.PlaybackOffset = 0f;

        PlayCurrent(ent, actor);
    }

    private void PlayCurrent(Entity<PortableMediaPlayerComponent> ent, EntityUid actor)
    {
        if (ent.Comp.SelectedTrackId == null ||
            !_proto.TryIndex<JukeboxPrototype>(ent.Comp.SelectedTrackId, out var proto))
            return;

        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        var netSource = GetNetEntity(ent.Owner);
        var startPos = ent.Comp.PlaybackOffset;

        var ownEv = new PipBoyMusicPlayEvent(
            proto.Path.Path.ToString(),
            ent.Comp.Volume,
            netSource,
            ent.Comp.AudibleToOthers,
            startPosition: startPos,
            isOwn: true,
            fromPipBoy: false);
        RaiseNetworkEvent(ownEv, actorComp.PlayerSession);

        if (ent.Comp.AudibleToOthers)
        {
            var filter = Filter.PvsExcept(actor);
            foreach (var session in filter.Recipients)
            {
                var ev = new PipBoyMusicPlayEvent(
                    proto.Path.Path.ToString(),
                    ent.Comp.Volume,
                    netSource,
                    ent.Comp.AudibleToOthers,
                    startPosition: startPos,
                    isOwn: false,
                    fromPipBoy: false);
                RaiseNetworkEvent(ev, session);
            }
        }
    }

    private void BroadcastPause(PortableMediaPlayerComponent comp, EntityUid actor, NetEntity netSource)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        RaiseNetworkEvent(new PipBoyMusicPauseEvent(netSource), actorComp.PlayerSession);

        if (!comp.AudibleToOthers)
            return;

        var filter = Filter.PvsExcept(actor);
        foreach (var session in filter.Recipients)
            RaiseNetworkEvent(new PipBoyMusicPauseEvent(netSource), session);
    }

    private void BroadcastStop(PortableMediaPlayerComponent comp, EntityUid actor, NetEntity netSource)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        RaiseNetworkEvent(new PipBoyMusicStopEvent(netSource), actorComp.PlayerSession);

        if (!comp.AudibleToOthers)
            return;

        var filter = Filter.PvsExcept(actor);
        foreach (var session in filter.Recipients)
            RaiseNetworkEvent(new PipBoyMusicStopEvent(netSource), session);
    }

    private void BroadcastSeek(PortableMediaPlayerComponent comp, EntityUid actor, NetEntity netSource, float position)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        RaiseNetworkEvent(new PipBoyMusicSeekEvent(netSource, position), actorComp.PlayerSession);

        if (!comp.AudibleToOthers)
            return;

        var filter = Filter.PvsExcept(actor);
        foreach (var session in filter.Recipients)
            RaiseNetworkEvent(new PipBoyMusicSeekEvent(netSource, position), session);
    }

    private void UpdateUiState(Entity<PortableMediaPlayerComponent> ent)
    {
        if (!_ui.HasUi(ent.Owner, PortableMediaPlayerUiKey.Key))
            return;

        var state = new PipBoyMusicUiState(
            GetSortedTracks(),
            ent.Comp.SelectedTrackId,
            ent.Comp.IsPlaying,
            ent.Comp.RepeatOne,
            ent.Comp.AutoNext,
            GetNetEntity(ent.Owner));

        _ui.SetUiState(ent.Owner, PortableMediaPlayerUiKey.Key, state);
    }

    private List<PipBoyMusicTrack> GetSortedTracks()
    {
        var tracks = new List<PipBoyMusicTrack>();
        foreach (var proto in _proto.EnumeratePrototypes<JukeboxPrototype>())
            tracks.Add(new PipBoyMusicTrack(proto.ID, proto.Name));
        tracks.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return tracks;
    }
}
