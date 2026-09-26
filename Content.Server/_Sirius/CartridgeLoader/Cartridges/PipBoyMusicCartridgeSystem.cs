using System.Linq;
using Content.Server.CartridgeLoader;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared.Audio.Jukebox;
using Content.Shared.CartridgeLoader;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sirius.CartridgeLoader.Cartridges;

public sealed class PipBoyMusicCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PipBoyMusicCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<PipBoyMusicCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<PipBoyMusicCartridgeComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<PipBoyMusicTrackFinishedEvent>(OnTrackFinished);
    }

    private void OnShutdown(Entity<PipBoyMusicCartridgeComponent> ent, ref ComponentShutdown args)
    {
        StopServerStream(ent.Comp);
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

        switch (msg.Action)
        {
            case PipBoyMusicUiAction.Select:
                if (msg.TrackId == null || !_proto.HasIndex<JukeboxPrototype>(msg.TrackId))
                    return;
                ent.Comp.SelectedTrackId = msg.TrackId;
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
                StopServerStream(ent.Comp);
                SendPauseToOwner(actor);
                break;

            case PipBoyMusicUiAction.Stop:
                ent.Comp.IsPlaying = false;
                StopServerStream(ent.Comp);
                SendStopToOwner(actor);
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

        PlayCurrent(ent, actor, loader);
    }

    private void PlayCurrent(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid actor, EntityUid loader)
    {
        if (ent.Comp.SelectedTrackId == null ||
            !_proto.TryIndex<JukeboxPrototype>(ent.Comp.SelectedTrackId, out var proto))
            return;

        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        StopServerStream(ent.Comp);

        var ev = new PipBoyMusicPlayEvent(
            proto.Path.Path.ToString(),
            ent.Comp.Volume,
            GetNetEntity(loader),
            ent.Comp.AudibleToOthers);
        RaiseNetworkEvent(ev, actorComp.PlayerSession);

        if (ent.Comp.AudibleToOthers)
        {
            var filter = Filter.PvsExcept(actor);
            var stream = _audio.PlayEntity(
                proto.Path.Path.ToString(), filter, loader, false,
                AudioParams.Default.WithVolume(ent.Comp.Volume).WithLoop(false));
            ent.Comp.ServerStream = stream?.Entity;
        }
    }

    private void SendPauseToOwner(EntityUid actor)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;
        RaiseNetworkEvent(new PipBoyMusicPauseEvent(), actorComp.PlayerSession);
    }

    private void SendStopToOwner(EntityUid actor)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;
        RaiseNetworkEvent(new PipBoyMusicStopEvent(), actorComp.PlayerSession);
    }

    private void StopServerStream(PipBoyMusicCartridgeComponent comp)
    {
        if (comp.ServerStream == null)
            return;
        _audio.Stop(comp.ServerStream);
        comp.ServerStream = null;
    }

    private void UpdateUiState(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid loader)
    {
        var state = new PipBoyMusicUiState(
            GetSortedTracks(),
            ent.Comp.SelectedTrackId,
            ent.Comp.IsPlaying,
            ent.Comp.RepeatOne,
            ent.Comp.AutoNext);
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
