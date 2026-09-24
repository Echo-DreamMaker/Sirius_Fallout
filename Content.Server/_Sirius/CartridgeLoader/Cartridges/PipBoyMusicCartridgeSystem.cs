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

        switch (msg.Action)
        {
            case PipBoyMusicUiAction.Select:
                if (msg.TrackId == null || !_proto.HasIndex<JukeboxPrototype>(msg.TrackId))
                    return;
                ent.Comp.SelectedTrackId = msg.TrackId;
                if (ent.Comp.IsPlaying)
                    PlayTrack(ent, actor, loader);
                break;

            case PipBoyMusicUiAction.Play:
                if (ent.Comp.SelectedTrackId == null)
                    return;
                ent.Comp.IsPlaying = true;
                PlayTrack(ent, actor, loader);
                break;

            case PipBoyMusicUiAction.Stop:
                ent.Comp.IsPlaying = false;
                StopTrack(actor);
                break;

            default:
                return;
        }

        UpdateUiState(ent, loader);
    }

    private void PlayTrack(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid actor, EntityUid loader)
    {
        if (ent.Comp.SelectedTrackId == null ||
            !_proto.TryIndex<JukeboxPrototype>(ent.Comp.SelectedTrackId, out var proto))
            return;

        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        var ev = new PipBoyMusicPlayEvent(proto.Path.Path.ToString(), ent.Comp.Volume, GetNetEntity(loader));
        RaiseNetworkEvent(ev, actorComp.PlayerSession);
    }

    private void StopTrack(EntityUid actor)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return;

        RaiseNetworkEvent(new PipBoyMusicStopEvent(), actorComp.PlayerSession);
    }

    private void UpdateUiState(Entity<PipBoyMusicCartridgeComponent> ent, EntityUid loader)
    {
        var tracks = new List<PipBoyMusicTrack>();
        foreach (var proto in _proto.EnumeratePrototypes<JukeboxPrototype>())
        {
            tracks.Add(new PipBoyMusicTrack(proto.ID, proto.Name));
        }

        tracks.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        var state = new PipBoyMusicUiState(tracks, ent.Comp.SelectedTrackId, ent.Comp.IsPlaying);
        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
    }
}
