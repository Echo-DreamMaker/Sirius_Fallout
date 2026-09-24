using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client._Sirius.CartridgeLoader.Cartridges;

public sealed class PipBoyMusicClientSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public static PipBoyMusicClientSystem? Instance { get; private set; }

    private EntityUid? _stream;

    public override void Initialize()
    {
        base.Initialize();

        Instance = this;

        SubscribeNetworkEvent<PipBoyMusicPlayEvent>(OnPlay);
        SubscribeNetworkEvent<PipBoyMusicStopEvent>(OnStop);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalDetached);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalAttached);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Stop();
        Instance = null;
    }

    private void OnPlay(PipBoyMusicPlayEvent ev)
    {
        _stream = _audio.Stop(_stream);

        var loader = GetEntity(ev.LoaderUid);
        if (!Exists(loader))
            return;

        var audioParams = AudioParams.Default
            .WithVolume(ev.Volume)
            .WithLoop(false);
        var stream = _audio.PlayEntity(ev.Path, Filter.Local(), loader, false, audioParams);
        _stream = stream?.Entity;
    }

    private void OnStop(PipBoyMusicStopEvent ev)
    {
        Stop();
    }

    private void OnLocalDetached(LocalPlayerDetachedEvent ev)
    {
        Stop();
    }

    private void OnLocalAttached(LocalPlayerAttachedEvent ev)
    {
        Stop();
    }

    private void Stop()
    {
        if (_stream == null)
            return;

        _stream = _audio.Stop(_stream);
        _stream = null;
    }
    public (float Position, float Length, bool Playing) GetPlaybackInfo()
    {
        if (_stream == null || !TryComp<AudioComponent>(_stream, out var comp))
            return (0f, 0f, false);

        var length = _audio.GetAudioLength(new ResolvedPathSpecifier(comp.FileName));
        return ((float) comp.PlaybackPosition, (float) length.TotalSeconds, comp.Playing);
    }
    public void SeekTo(float position)
    {
        if (_stream == null || !TryComp<AudioComponent>(_stream, out var comp))
            return;

        _audio.SetPlaybackPosition(new Entity<AudioComponent?>(_stream.Value, comp), position);
    }
}
