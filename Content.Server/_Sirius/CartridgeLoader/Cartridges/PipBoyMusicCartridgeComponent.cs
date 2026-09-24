namespace Content.Server._Sirius.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class PipBoyMusicCartridgeComponent : Component
{
    [DataField]
    public string? SelectedTrackId;
    [DataField]
    public bool IsPlaying;
    [DataField]
    public float Volume = -6f;
}
