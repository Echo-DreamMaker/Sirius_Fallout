using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Robust.Shared.Serialization;

namespace Content.Shared._Sirius.PortableMediaPlayer;

[Serializable, NetSerializable]
public sealed class PortableMediaPlayerMessage : BoundUserInterfaceMessage
{
    public readonly PipBoyMusicUiAction Action;
    public readonly string? TrackId;
    public readonly bool AudibleToOthers;
    public readonly float SeekPosition;

    public PortableMediaPlayerMessage(
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
