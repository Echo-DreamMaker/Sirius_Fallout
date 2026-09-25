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

    public PipBoyMusicUiState(List<PipBoyMusicTrack> tracks, string? selectedTrackId, bool isPlaying, bool repeatOne, bool autoNext)
    {
        Tracks = tracks;
        SelectedTrackId = selectedTrackId;
        IsPlaying = isPlaying;
        RepeatOne = repeatOne;
        AutoNext = autoNext;
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

    public PipBoyMusicUiMessageEvent(PipBoyMusicUiAction action, string? trackId = null, bool audibleToOthers = false)
    {
        Action = action;
        TrackId = trackId;
        AudibleToOthers = audibleToOthers;
    }
}

[Serializable, NetSerializable]
public enum PipBoyMusicUiAction : byte
{
    Select,
    Play,
    Pause,
    Stop,
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

    public PipBoyMusicPlayEvent(string path, float volume, NetEntity loaderUid, bool audibleToOthers, float startPosition = 0f)
    {
        Path = path;
        Volume = volume;
        LoaderUid = loaderUid;
        AudibleToOthers = audibleToOthers;
        StartPosition = startPosition;
    }
}

[Serializable, NetSerializable]
public sealed class PipBoyMusicPauseEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class PipBoyMusicStopEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class PipBoyMusicTrackFinishedEvent : EntityEventArgs
{
    public NetEntity LoaderUid;

    public PipBoyMusicTrackFinishedEvent(NetEntity loaderUid)
    {
        LoaderUid = loaderUid;
    }
}
