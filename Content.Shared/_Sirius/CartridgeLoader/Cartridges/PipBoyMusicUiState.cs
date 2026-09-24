using Robust.Shared.Serialization;
using Content.Shared.CartridgeLoader;

namespace Content.Shared._Sirius.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class PipBoyMusicUiState : BoundUserInterfaceState
{
    public List<PipBoyMusicTrack> Tracks;
    public string? SelectedTrackId;
    public bool IsPlaying;

    public PipBoyMusicUiState(List<PipBoyMusicTrack> tracks, string? selectedTrackId, bool isPlaying)
    {
        Tracks = tracks;
        SelectedTrackId = selectedTrackId;
        IsPlaying = isPlaying;
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

    public PipBoyMusicUiMessageEvent(PipBoyMusicUiAction action, string? trackId = null)
    {
        Action = action;
        TrackId = trackId;
    }
}

[Serializable, NetSerializable]
public enum PipBoyMusicUiAction : byte
{
    Select,
    Play,
    Stop,
}
[Serializable, NetSerializable]
public sealed class PipBoyMusicPlayEvent : EntityEventArgs
{
    public string Path;
    public float Volume;
    public NetEntity LoaderUid;

    public PipBoyMusicPlayEvent(string path, float volume, NetEntity loaderUid)
    {
        Path = path;
        Volume = volume;
        LoaderUid = loaderUid;
    }
}

[Serializable, NetSerializable]
public sealed class PipBoyMusicStopEvent : EntityEventArgs;
