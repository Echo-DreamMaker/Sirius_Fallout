using Content.Client.Eui;
using Content.Shared._Misfits.BrotherhoodOfSteel;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._Misfits.BrotherhoodOfSteel;

[UsedImplicitly]
public sealed class BosRecruitEui : BaseEui
{
    private readonly BosRecruitWindow _window;
    private bool _responded;

    public BosRecruitEui()
    {
        _window = new BosRecruitWindow();
        _window.OnAccepted += () => Respond(true);
        _window.OnDeclined += () => Respond(false);
        _window.OnClose += () => Respond(false);
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is BosRecruitEuiState recruitState)
            _window.SetNames(recruitState.RecruiterName, recruitState.TargetName);
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
        SendMessage(new BosRecruitDecisionMessage(accepted));
        _window.Close();
    }
}
