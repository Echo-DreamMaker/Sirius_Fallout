using Content.Client.Eui;
using Content.Shared._Misfits.Enclave;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Misfits.Enclave;

[UsedImplicitly]
public sealed class EnclaveRecruitEui : BaseEui
{
    private readonly EnclaveRecruitWindow _window;
    private bool _responded;

    public EnclaveRecruitEui()
    {
        _window = new EnclaveRecruitWindow();
        _window.OnAccepted += () => Respond(true);
        _window.OnDeclined += () => Respond(false);
        _window.OnClose += () => Respond(false);
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is EnclaveRecruitEuiState recruitState)
            _window.SetCharacterName(recruitState.CharacterName);
    }

    public override void Opened()
    {
        base.Opened();
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _responded = true;
        _window.Close();
    }

    private void Respond(bool accepted)
    {
        if (_responded)
            return;

        _responded = true;
        SendMessage(new EnclaveRecruitDecisionMessage(accepted));
        _window.Close();
    }
}
