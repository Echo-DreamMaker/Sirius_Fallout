using System.Collections.Generic;
using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Sirius.CartridgeLoader.Cartridges;

public sealed class PipBoyMusicClientSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public static PipBoyMusicClientSystem? Instance { get; private set; }

    private sealed class Stream
    {
        public EntityUid? Audio;
        public string Path = "";
        public float Volume = -6f;
        public float StartPosition = 0f;
        public bool Paused;
        public bool FromPipBoy;
        public bool FinishedSent;
        public NetEntity LoaderUid;
        public TimeSpan LastSync = TimeSpan.Zero;
    }
    private readonly Dictionary<NetEntity, Stream> _ownStreams = new();
    private readonly Dictionary<NetEntity, Stream> _otherStreams = new();

    public override void Initialize()
    {
        base.Initialize();
        Instance = this;

        SubscribeNetworkEvent<PipBoyMusicPlayEvent>(OnPlay);
        SubscribeNetworkEvent<PipBoyMusicPauseEvent>(OnPause);
        SubscribeNetworkEvent<PipBoyMusicStopEvent>(OnStop);
        SubscribeNetworkEvent<PipBoyMusicSeekEvent>(OnSeek);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalDetached);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalAttached);

        _cfg.OnValueChanged(CCVars.MuteOthersPipBoyMusic, _ => OnMuteCvarChanged(true));
        _cfg.OnValueChanged(CCVars.MuteOthersPortableMediaPlayerMusic, _ => OnMuteCvarChanged(false));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        StopAll();
        Instance = null;
    }

    private void OnPlay(PipBoyMusicPlayEvent ev)
    {
        if (ev.IsOwn)
            PlayStream(_ownStreams, ev, isOwn: true);
        else
            PlayStream(_otherStreams, ev, isOwn: false);
    }

    private void PlayStream(Dictionary<NetEntity, Stream> dict, PipBoyMusicPlayEvent ev, bool isOwn)
    {
        if (!isOwn)
        {
            var mute = ev.FromPipBoy
                ? _cfg.GetCVar(CCVars.MuteOthersPipBoyMusic)
                : _cfg.GetCVar(CCVars.MuteOthersPortableMediaPlayerMusic);

            if (!dict.TryGetValue(ev.LoaderUid, out var s))
            {
                s = new Stream();
                dict[ev.LoaderUid] = s;
            }

            s.Path = ev.Path;
            s.Volume = ev.Volume;
            s.StartPosition = ev.StartPosition;
            s.Paused = false;
            s.FromPipBoy = ev.FromPipBoy;
            s.FinishedSent = false;
            s.LoaderUid = ev.LoaderUid;

            if (mute)
            {
                StopStreamAudioInternal(s);
                return;
            }

            StartStreamAudio(s);
            return;
        }
        if (dict.TryGetValue(ev.LoaderUid, out var own) &&
            own.Path == ev.Path &&
            own.Paused &&
            own.Audio != null &&
            TryComp<AudioComponent>(own.Audio, out var existing))
        {
            _audio.SetState(own.Audio, AudioState.Playing, force: true, existing);
            own.Paused = false;
            own.FinishedSent = false;
            return;
        }

        if (!dict.TryGetValue(ev.LoaderUid, out var os))
        {
            os = new Stream();
            dict[ev.LoaderUid] = os;
        }

        os.Path = ev.Path;
        os.Volume = ev.Volume;
        os.StartPosition = ev.StartPosition;
        os.Paused = false;
        os.FromPipBoy = ev.FromPipBoy;
        os.FinishedSent = false;
        os.LoaderUid = ev.LoaderUid;
        os.LastSync = _timing.CurTime;

        StartStreamAudio(os);
    }

    private void StartStreamAudio(Stream s)
    {
        StopStreamAudioInternal(s);

        var loader = GetEntity(s.LoaderUid);
        if (!Exists(loader))
            return;

        var audioParams = AudioParams.Default.WithVolume(s.Volume).WithLoop(false);
        var stream = _audio.PlayEntity(s.Path, Filter.Local(), loader, false, audioParams);
        s.Audio = stream?.Entity;

        if (s.Audio != null && s.StartPosition > 0f && TryComp<AudioComponent>(s.Audio, out var comp))
            _audio.SetPlaybackPosition(new Entity<AudioComponent?>(s.Audio.Value, comp), s.StartPosition);
    }

    private void StopStreamAudioInternal(Stream s)
    {
        if (s.Audio == null)
            return;

        if (TryComp<AudioComponent>(s.Audio, out var comp))
            _audio.SetState(s.Audio, AudioState.Stopped, force: true, comp);

        if (Exists(s.Audio.Value))
            QueueDel(s.Audio.Value);

        s.Audio = null;
    }

    private void OnPause(PipBoyMusicPauseEvent ev)
    {
        if (_ownStreams.TryGetValue(ev.LoaderUid, out var own))
        {
            if (own.Audio == null || own.Paused || !TryComp<AudioComponent>(own.Audio, out var comp))
                return;
            _audio.SetState(own.Audio, AudioState.Paused, force: true, comp);
            own.Paused = true;
            return;
        }

        if (_otherStreams.TryGetValue(ev.LoaderUid, out var other))
        {
            if (other.Audio != null && TryComp<AudioComponent>(other.Audio, out var comp))
                _audio.SetState(other.Audio, AudioState.Paused, force: true, comp);
            other.Paused = true;
        }
    }

    private void OnStop(PipBoyMusicStopEvent ev)
    {
        if (_ownStreams.TryGetValue(ev.LoaderUid, out var own))
        {
            StopStreamAudioInternal(own);
            _ownStreams.Remove(ev.LoaderUid);
            return;
        }

        if (_otherStreams.TryGetValue(ev.LoaderUid, out var other))
        {
            StopStreamAudioInternal(other);
            _otherStreams.Remove(ev.LoaderUid);
        }
    }

    private void OnSeek(PipBoyMusicSeekEvent ev)
    {
        if (_ownStreams.TryGetValue(ev.LoaderUid, out var own))
        {
            if (own.Audio != null && TryComp<AudioComponent>(own.Audio, out var comp))
                _audio.SetPlaybackPosition(new Entity<AudioComponent?>(own.Audio.Value, comp), ev.Position);
            own.StartPosition = ev.Position;
            return;
        }

        if (_otherStreams.TryGetValue(ev.LoaderUid, out var other))
        {
            if (other.Audio != null && TryComp<AudioComponent>(other.Audio, out var comp))
                _audio.SetPlaybackPosition(new Entity<AudioComponent?>(other.Audio.Value, comp), ev.Position);
            other.StartPosition = ev.Position;
        }
    }

    private void OnLocalDetached(LocalPlayerDetachedEvent ev) => StopAll();
    private void OnLocalAttached(LocalPlayerAttachedEvent ev) => StopAll();

    private void StopAll()
    {
        foreach (var s in _ownStreams.Values)
            StopStreamAudioInternal(s);
        foreach (var s in _otherStreams.Values)
            StopStreamAudioInternal(s);
        _ownStreams.Clear();
        _otherStreams.Clear();
    }

    private void OnMuteCvarChanged(bool pipBoy)
    {
        var mute = pipBoy
            ? _cfg.GetCVar(CCVars.MuteOthersPipBoyMusic)
            : _cfg.GetCVar(CCVars.MuteOthersPortableMediaPlayerMusic);

        if (mute)
        {
            foreach (var (uid, other) in _otherStreams)
            {
                if (other.FromPipBoy != pipBoy)
                    continue;
                StopStreamAudioInternal(other);
            }
        }
        else
        {
            RaiseNetworkEvent(new PipBoyMusicSyncRequestEvent());
        }
    }
    public (float Position, float Length, bool HasStream, bool Playing) GetPlaybackInfo(NetEntity source)
    {
        Stream? s = null;
        if (!_ownStreams.TryGetValue(source, out s))
            _otherStreams.TryGetValue(source, out s);

        if (s?.Audio == null || !TryComp<AudioComponent>(s.Audio, out var comp))
            return (0f, 0f, false, false);

        var length = _audio.GetAudioLength(new ResolvedPathSpecifier(comp.FileName));
        return (
            (float) comp.PlaybackPosition,
            (float) length.TotalSeconds,
            true,
            comp.State == AudioState.Playing);
    }
    public void SeekLocalOnly(NetEntity source, float position)
    {
        Stream? s = null;
        if (!_ownStreams.TryGetValue(source, out s))
            _otherStreams.TryGetValue(source, out s);

        if (s?.Audio != null && TryComp<AudioComponent>(s.Audio, out var comp))
            _audio.SetPlaybackPosition(new Entity<AudioComponent?>(s.Audio.Value, comp), position);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var (uid, s) in _ownStreams)
        {
            if (s.Audio == null || s.Paused || s.FinishedSent)
                continue;
            if (!TryComp<AudioComponent>(s.Audio, out var comp))
                continue;

            if (_timing.CurTime - s.LastSync < TimeSpan.FromSeconds(10))
                continue;

            s.LastSync = _timing.CurTime;
            RaiseNetworkEvent(new PipBoyMusicPositionSyncEvent(uid, (float) comp.PlaybackPosition, s.FromPipBoy));
        }
        foreach (var (uid, s) in _ownStreams)
        {
            if (s.Audio == null || s.FinishedSent || s.Paused)
                continue;

            if (!TryComp<AudioComponent>(s.Audio, out var comp))
            {
                s.FinishedSent = true;
                RaiseNetworkEvent(new PipBoyMusicTrackFinishedEvent(uid));
                continue;
            }

            if (comp.State == AudioState.Stopped)
            {
                s.FinishedSent = true;
                RaiseNetworkEvent(new PipBoyMusicTrackFinishedEvent(uid));
            }
        }
    }
}
