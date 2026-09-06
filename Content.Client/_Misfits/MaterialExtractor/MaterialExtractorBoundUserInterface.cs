using Content.Client.UserInterface.Controls;
using Content.Shared._Misfits.MaterialExtractor;
using Robust.Client.UserInterface;

namespace Content.Client._Misfits.MaterialExtractor;

public sealed class MaterialExtractorBoundUserInterface : BoundUserInterface
{
    private MaterialExtractorWindow? _window;

    public MaterialExtractorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MaterialExtractorWindow>();
        _window.OnStart += () => SendMessage(new MaterialExtractorStartMessage());
        _window.OnStop += () => SendMessage(new MaterialExtractorStopMessage());
        _window.OnEjectFuel += () => SendMessage(new MaterialExtractorEjectFuelMessage());
        _window.OpenCenteredLeft();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is MaterialExtractorUiState extractorState)
            _window?.Update(extractorState);
    }
}
