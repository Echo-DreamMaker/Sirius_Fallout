using Robust.Shared.Serialization;
using Content.Shared.CartridgeLoader;

namespace Content.Shared._Sirius.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class PipBoyMusicUiState : BoundUserInterfaceState
{
    public List<PipBoyMusicTrack> Tracks;
    public string? SelectedTrackId;
    public bool IsPlaying;
    public bool RepeatOne;
    public bool AutoNext;
    public NetEntity? SourceEntity;

    public PipBoyMusicUiState(
        List<PipBoyMusicTrack> tracks,
        string? selectedTrackId,
        bool isPlaying,
        bool repeatOne,
        bool autoNext,
        NetEntity? sourceEntity = null)
    {
        Tracks = tracks;
        SelectedTrackId = selectedTrackId;
        IsPlaying = isPlaying;
        RepeatOne = repeatOne;
        AutoNext = autoNext;
        SourceEntity = sourceEntity;
    }
}

[Serializable, NetSerializable, DataRecord]
public sealed partial class PipBoyMusicTrack
{
    public string Id;
    public string Name;

    public PipBoyMusicTrack(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class PipBoyMusicUiMessageEvent : CartridgeMessageEvent
{
    public readonly PipBoyMusicUiAction Action;
    public readonly string? TrackId;
    public readonly bool AudibleToOthers;
    public readonly float SeekPosition;

    public PipBoyMusicUiMessageEvent(
        PipBoyMusicUiAction action,
        string? trackId = null,
        bool audibleToOthers = false,
        float seekPosition = 0f)
    {
        Action = action;
        TrackId = trackId;
        AudibleToOthers = audibleToOthers;
        SeekPosition = seekPosition;
    }
}

[Serializable, NetSerializable]
public enum PipBoyMusicUiAction : byte
{
    Select,
    Play,
    Pause,
    Stop,
    Seek,
    ToggleRepeat,
    ToggleAutoNext,
}

[Serializable, NetSerializable]
public sealed class PipBoyMusicPlayEvent : EntityEventArgs
{
    public string Path;
    public float Volume;
    public NetEntity LoaderUid;
    public bool AudibleToOthers;
    public float StartPosition;
    public bool IsOwn;
    public bool FromPipBoy;

    public PipBoyMusicPlayEvent(
        string path,
        float volume,
        NetEntity loaderUid,
        bool audibleToOthers,
        float startPosition = 0f,
        bool isOwn = true,
        bool fromPipBoy = true)
    {
        Path = path;
        Volume = volume;
        LoaderUid = loaderUid;
        AudibleToOthers = audibleToOthers;
        StartPosition = startPosition;
        IsOwn = isOwn;
        FromPipBoy = fromPipBoy;
    }
}

[Serializable, NetSerializable]
public sealed class PipBoyMusicPauseEvent : EntityEventArgs
{
    public NetEntity LoaderUid;

    public PipBoyMusicPauseEvent(NetEntity loaderUid)
    {
        LoaderUid = loaderUid;
    }
}

[Serializable, NetSerializable]
public sealed class PipBoyMusicStopEvent : EntityEventArgs
{
    public NetEntity LoaderUid;

    public PipBoyMusicStopEvent(NetEntity loaderUid)
    {
        LoaderUid = loaderUid;
    }
}
[Serializable, NetSerializable]
public sealed class PipBoyMusicSeekEvent : EntityEventArgs
{
    public NetEntity LoaderUid;
    public float Position;

    public PipBoyMusicSeekEvent(NetEntity loaderUid, float position)
    {
        LoaderUid = loaderUid;
        Position = position;
    }
}
[Serializable, NetSerializable]
public sealed class PipBoyMusicPositionSyncEvent : EntityEventArgs
{
    public NetEntity LoaderUid;
    public float Position;
    public bool FromPipBoy;

    public PipBoyMusicPositionSyncEvent(NetEntity loaderUid, float position, bool fromPipBoy)
    {
        LoaderUid = loaderUid;
        Position = position;
        FromPipBoy = fromPipBoy;
    }
}

[Serializable, NetSerializable]
public sealed class PipBoyMusicTrackFinishedEvent : EntityEventArgs
{
    public NetEntity LoaderUid;

    public PipBoyMusicTrackFinishedEvent(NetEntity loaderUid)
    {
        LoaderUid = loaderUid;
    }
}
[Serializable, NetSerializable]
public sealed class PipBoyMusicSyncRequestEvent : EntityEventArgs;
