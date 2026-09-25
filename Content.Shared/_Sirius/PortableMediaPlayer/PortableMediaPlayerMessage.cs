using Content.Shared._Sirius.CartridgeLoader.Cartridges;
using Robust.Shared.Serialization;

namespace Content.Shared._Sirius.PortableMediaPlayer;

[Serializable, NetSerializable]
public sealed class PortableMediaPlayerMessage : BoundUserInterfaceMessage
{
    public readonly PipBoyMusicUiAction Action;
    public readonly string? TrackId;
    public readonly bool AudibleToOthers;

    public PortableMediaPlayerMessage(PipBoyMusicUiAction action, string? trackId = null, bool audibleToOthers = false)
    {
        Action = action;
        TrackId = trackId;
        AudibleToOthers = audibleToOthers;
    }
}
