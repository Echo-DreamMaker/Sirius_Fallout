using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared.Audio.Jukebox;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sirius.CartridgeLoader.Cartridges;

public sealed class PipBoyMusicCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PipBoyMusicCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<PipBoyMusicCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeNetworkEvent<PipBoyMusicTrackFinishedEvent>(OnTrackFinished);
        SubscribeNetworkEvent<PipBoyMusicSyncRequestEvent>(OnSyncRequest);
        SubscribeNetworkEvent<PipBoyMusicPositionSyncEvent>(OnPositionSync);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnUiReady(Entity<PipBoyMusicCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUiState(ent, args.Loader);
    }

    private void OnUiMessage(Entity<PipBoyMusicCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not PipBoyMusicUiMessageEvent msg)
            return;

        var loader = GetEntity(args.LoaderUid);
        var actor = args.Actor;

        ent.Comp.OwnerEntity = actor;
        ent.Comp.AudibleToOthers = msg.AudibleToOthers;

        var netLoader = GetNetEntity(loader);

        switch (msg.Action)
        {
            case PipBoyMusicUiAction.Select:
                if (msg.TrackId == null || !_proto.HasIndex<JukeboxPrototype>(msg.TrackId))
                    return;
                ent.Comp.SelectedTrackId = msg.TrackId;
                ent.Comp.PlaybackOffset = 0f;
                if (ent.Comp.IsPlaying)
                    PlayCurrent(ent, actor, loader);
                break;

            case PipBoyMusicUiAction.Play:
                if (ent.Comp.SelectedTrackId == null)
                    return;
                ent.Comp.IsPlaying = true;
                PlayCurrent(ent, actor, loader);
                break;

            case PipBoyMusicUiAction.Pause:
                ent.Comp.IsPlaying = false;
                BroadcastPause(ent, actor, netLoader);
                break;

            case PipBoyMusicUiAction.Stop:
                ent.Comp.IsPlaying = false;
                ent.Comp.PlaybackOffset = 0f;
                BroadcastStop(ent, actor, netLoader);
                break;

            case PipBoyMusicUiAction.Seek:
                ent.Comp.PlaybackOffset = msg.SeekPosition;
                BroadcastSeek(ent, actor, netLoader, msg.SeekPosition);
                break;

            case PipBoyMusicUiAction.ToggleRepeat:
                ent.Comp.RepeatOne = !ent.Comp.RepeatOne;
                break;

            case PipBoyMusicUiAction.ToggleAutoNext:
                ent.Comp.AutoNext = !ent.Comp.AutoNext;
                break;

            default:
                return;
        }

        UpdateUiState(ent, loader);
    }

    private void OnPositionSync(PipBoyMusicPositionSyncEvent ev, EntitySessionEventArgs args)
    {
        if (!ev.FromPipBoy)
            return;
        var loader = GetEntity(ev.LoaderUid);
        if (!TryComp<CartridgeComponent>(loader, out var cart) || cart.LoaderUid is not { } owner)
            return;
        var query = EntityQueryEnumerator<PipBoyMusicCartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryComp<CartridgeComponent>(uid, out var c) || c.LoaderUid != owner)
                continue;

            comp.PlaybackOffset = ev.Position;
            return;
        }
    }
    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        var session = args.Player;
        var detachedEntity = args.Entity;

        var query = EntityQueryEnumerator<PipBoyMusicCartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.OwnerEntity != detachedEntity)
                continue;

            comp.IsPlaying = false;
            comp.PlaybackOffset = 0f;

            if (TryComp<CartridgeComponent>(uid, out var cart) && cart.LoaderUid != null)
            {
                var netLoader = GetNetEntity(cart.LoaderUid.Value);
                RaiseNetworkEvent(new PipBoyMusicStopEvent(netLoader), session);
                UpdateUiState((uid, comp), cart.LoaderUid.Value);
            }
        }
    }
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        var session = args.Player;
        var newEntity = args.Entity;

        var query = EntityQueryEnumerator<PipBoyMusicCartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.OwnerEntity != newEntity)
                continue;

            comp.IsPlaying = false;
            comp.PlaybackOffset = 0f;

            if (TryComp<CartridgeComponent>(uid, out var cart) && cart.LoaderUid != null)
            {
                var netLoader = GetNetEntity(cart.LoaderUid.Value);
                RaiseNetworkEvent(new PipBoyMusicStopEvent(netLoader), session);
                UpdateUiState((uid, comp), cart.LoaderUid.Value);
            }
        }
    }

    private void OnTrackFinished(PipBoyMusicTrackFinishedEvent ev, EntitySessionEventArgs args)
    {
        var loader = GetEntity(ev.LoaderUid);

        var query = EntityQueryEnumerator<PipBoyMusicCartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!TryComp<CartridgeComponent>(uid, out var cart) || cart.LoaderUid != loader)
                continue;

            if (comp.OwnerEntity == null)
                return;

            comp.PlaybackOffset = 0f;

            if (comp.RepeatOne)
            {
                PlayCurrent((uid, comp), comp.OwnerEntity.Value, loader);
                UpdateUiState((uid, comp), loader);
                return;
            }

            if (comp.AutoNext)
            {
                AdvanceToNext((uid, comp), comp.OwnerEntity.Value, loader);
                UpdateUiState((uid, comp), loader);
                return;
            }

            comp.IsPlaying = false;
            UpdateUiState((uid, comp), loader);
            return;
        }
    }

    private void OnSyncRequest(PipBoyMusicSyncRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        var query = EntityQueryEnumerator<PipBoyMusicCartridgeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsPlaying || comp.SelectedTrackId == null)
                continue;
            if (comp.OwnerEntity == session.AttachedEntity)
                continue;
            if (!_proto.TryIndex<JukeboxPrototype>(comp.SelectedTrackId, out var proto))
                continue;
            if (!TryComp<CartridgeComponent>(uid, out var cart) || cart.LoaderUid == null)
                continue;

            var playEv = new PipBoyMusicPlayEvent(
                proto.Path.Path.ToString(),
                comp.Volume,
                GetNetEntity(cart.LoaderUid.Value),
                comp.AudibleToOthers,
                startPosition: comp.PlaybackOffset,
                isOwn: false,
                fromPipBoy: true);
            RaiseNetworkEvent(playEv, session);
        }
    }

    private void AdvanceToNext(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid actor, EntityUid loader)
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

        PlayCurrent(ent, actor, loader);
    }

    private void PlayCurrent(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid actor, EntityUid loader)
    {
        if (ent.Comp.SelectedTrackId == null ||
            !_proto.TryIndex<JukeboxPrototype>(ent.Comp.SelectedTrackId, out var proto))
            return;

        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        var netLoader = GetNetEntity(loader);
        var startPos = ent.Comp.PlaybackOffset;

        var ownEv = new PipBoyMusicPlayEvent(
            proto.Path.Path.ToString(),
            ent.Comp.Volume,
            netLoader,
            ent.Comp.AudibleToOthers,
            startPosition: startPos,
            isOwn: true,
            fromPipBoy: true);
        RaiseNetworkEvent(ownEv, actorComp.PlayerSession);

        if (ent.Comp.AudibleToOthers)
        {
            var filter = Filter.PvsExcept(actor);
            foreach (var session in filter.Recipients)
            {
                var ev = new PipBoyMusicPlayEvent(
                    proto.Path.Path.ToString(),
                    ent.Comp.Volume,
                    netLoader,
                    ent.Comp.AudibleToOthers,
                    startPosition: startPos,
                    isOwn: false,
                    fromPipBoy: true);
                RaiseNetworkEvent(ev, session);
            }
        }
    }

    private void BroadcastPause(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid actor, NetEntity netLoader)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        RaiseNetworkEvent(new PipBoyMusicPauseEvent(netLoader), actorComp.PlayerSession);

        if (!ent.Comp.AudibleToOthers)
            return;

        var filter = Filter.PvsExcept(actor);
        foreach (var session in filter.Recipients)
            RaiseNetworkEvent(new PipBoyMusicPauseEvent(netLoader), session);
    }

    private void BroadcastStop(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid actor, NetEntity netLoader)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        RaiseNetworkEvent(new PipBoyMusicStopEvent(netLoader), actorComp.PlayerSession);

        if (!ent.Comp.AudibleToOthers)
            return;

        var filter = Filter.PvsExcept(actor);
        foreach (var session in filter.Recipients)
            RaiseNetworkEvent(new PipBoyMusicStopEvent(netLoader), session);
    }

    private void BroadcastSeek(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid actor, NetEntity netLoader, float position)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        RaiseNetworkEvent(new PipBoyMusicSeekEvent(netLoader, position), actorComp.PlayerSession);

        if (!ent.Comp.AudibleToOthers)
            return;

        var filter = Filter.PvsExcept(actor);
        foreach (var session in filter.Recipients)
            RaiseNetworkEvent(new PipBoyMusicSeekEvent(netLoader, position), session);
    }

    private void UpdateUiState(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid loader)
    {
        var state = new PipBoyMusicUiState(
            GetSortedTracks(),
            ent.Comp.SelectedTrackId,
            ent.Comp.IsPlaying,
            ent.Comp.RepeatOne,
            ent.Comp.AutoNext,
            GetNetEntity(loader));
        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
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
