namespace Content.Server._Sirius.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class PipBoyMusicCartridgeComponent : Component
{
    [DataField]
    public string? SelectedTrackId;

    [DataField]
    public bool IsPlaying;

    [DataField]
    public bool RepeatOne;

    [DataField]
    public bool AutoNext;
    [DataField]
    public bool AudibleToOthers;

    [DataField]
    public float Volume = -6f;
    [ViewVariables]
    public EntityUid? ServerStream;

    [ViewVariables]
    public EntityUid? OwnerEntity;
}
