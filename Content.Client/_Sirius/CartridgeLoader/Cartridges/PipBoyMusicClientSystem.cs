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
    private string? _streamPath;
    private bool _streamPaused;
    private NetEntity? _streamLoader;
    private bool _finishedSent;

    public override void Initialize()
    {
        base.Initialize();
        Instance = this;

        SubscribeNetworkEvent<PipBoyMusicPlayEvent>(OnPlay);
        SubscribeNetworkEvent<PipBoyMusicPauseEvent>(OnPause);
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
        if (_stream != null &&
            _streamPath == ev.Path &&
            _streamPaused &&
            TryComp<AudioComponent>(_stream, out var existing))
        {
            _audio.SetState(_stream, AudioState.Playing, force: true, existing);
            _streamPaused = false;
            _finishedSent = false;
            return;
        }

        Stop();

        var loader = GetEntity(ev.LoaderUid);
        if (!Exists(loader))
            return;

        var audioParams = AudioParams.Default
            .WithVolume(ev.Volume)
            .WithLoop(false);

        var stream = _audio.PlayEntity(ev.Path, Filter.Local(), loader, false, audioParams);
        _stream = stream?.Entity;
        _streamPath = ev.Path;
        _streamLoader = ev.LoaderUid;
        _streamPaused = false;
        _finishedSent = false;

        if (_stream != null && ev.StartPosition > 0f && TryComp<AudioComponent>(_stream, out var comp))
            _audio.SetPlaybackPosition(new Entity<AudioComponent?>(_stream.Value, comp), ev.StartPosition);
    }

    private void OnPause(PipBoyMusicPauseEvent ev)
    {
        if (_stream == null || _streamPaused || !TryComp<AudioComponent>(_stream, out var comp))
            return;

        _audio.SetState(_stream, AudioState.Paused, force: true, comp);
        _streamPaused = true;
    }

    private void OnStop(PipBoyMusicStopEvent ev)
    {
        Stop();
    }

    private void OnLocalDetached(LocalPlayerDetachedEvent ev) => Stop();
    private void OnLocalAttached(LocalPlayerAttachedEvent ev) => Stop();

    private void Stop()
    {
        if (_stream != null)
            _stream = _audio.Stop(_stream);

        _stream = null;
        _streamPath = null;
        _streamLoader = null;
        _streamPaused = false;
        _finishedSent = false;
    }
    public (float Position, float Length, bool HasStream, bool Playing) GetPlaybackInfo()
    {
        if (_stream == null || !TryComp<AudioComponent>(_stream, out var comp))
            return (0f, 0f, false, false);

        var length = _audio.GetAudioLength(new ResolvedPathSpecifier(comp.FileName));
        return (
            (float) comp.PlaybackPosition,
            (float) length.TotalSeconds,
            true,
            comp.State == AudioState.Playing);
    }

    public void SeekTo(float position)
    {
        if (_stream == null || !TryComp<AudioComponent>(_stream, out var comp))
            return;
        _audio.SetPlaybackPosition(new Entity<AudioComponent?>(_stream.Value, comp), position);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_stream == null || _finishedSent)
            return;
        if (!TryComp<AudioComponent>(_stream, out var comp))
        {
            _stream = null;
            _finishedSent = true;
            if (_streamLoader != null)
                RaiseNetworkEvent(new PipBoyMusicTrackFinishedEvent(_streamLoader.Value));
            return;
        }
        if (comp.State == AudioState.Stopped && !_streamPaused)
        {
            _finishedSent = true;
            if (_streamLoader != null)
                RaiseNetworkEvent(new PipBoyMusicTrackFinishedEvent(_streamLoader.Value));
        }
    }
}
