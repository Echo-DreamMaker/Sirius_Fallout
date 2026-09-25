using System.Linq;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared._Sirius.PortableMediaPlayer;
using Content.Shared.Audio.Jukebox;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sirius.PortableMediaPlayer;

public sealed class PortableMediaPlayerSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PortableMediaPlayerComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<PortableMediaPlayerComponent, PortableMediaPlayerMessage>(OnUiMessage);
        SubscribeLocalEvent<PortableMediaPlayerComponent, ComponentShutdown>(OnShutdown);
        SubscribeNetworkEvent<PipBoyMusicTrackFinishedEvent>(OnTrackFinished);
    }

    private void OnShutdown(EntityUid uid, PortableMediaPlayerComponent component, ComponentShutdown args)
    {
        StopServerStream(component);
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

        switch (args.Action)
        {
            case PipBoyMusicUiAction.Select:
                if (args.TrackId == null || !_proto.HasIndex<JukeboxPrototype>(args.TrackId))
                    return;
                component.SelectedTrackId = args.TrackId;
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
                StopServerStream(component);
                SendPauseToOwner(actor);
                break;

            case PipBoyMusicUiAction.Stop:
                component.IsPlaying = false;
                StopServerStream(component);
                SendStopToOwner(actor);
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

    private void OnTrackFinished(PipBoyMusicTrackFinishedEvent ev, EntitySessionEventArgs args)
    {
        var source = GetEntity(ev.LoaderUid);

        if (!TryComp<PortableMediaPlayerComponent>(source, out var comp))
            return;

        if (comp.OwnerEntity == null)
            return;

        if (comp.RepeatOne)
        {
            PlayCurrent((source, comp), comp.OwnerEntity.Value);
        }
        else if (comp.AutoNext)
        {
            AdvanceToNext((source, comp), comp.OwnerEntity.Value);
        }
        else
        {
            comp.IsPlaying = false;
        }

        UpdateUiState((source, comp));
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

        PlayCurrent(ent, actor);
    }

    private void PlayCurrent(Entity<PortableMediaPlayerComponent> ent, EntityUid actor)
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
            GetNetEntity(ent.Owner),
            ent.Comp.AudibleToOthers);
        RaiseNetworkEvent(ev, actorComp.PlayerSession);

        if (ent.Comp.AudibleToOthers)
        {
            var filter = Filter.PvsExcept(actor);
            var stream = _audio.PlayEntity(
                proto.Path.Path.ToString(), filter, ent.Owner, false,
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

    private void StopServerStream(PortableMediaPlayerComponent comp)
    {
        if (comp.ServerStream == null)
            return;
        _audio.Stop(comp.ServerStream);
        comp.ServerStream = null;
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
            ent.Comp.AutoNext);

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
