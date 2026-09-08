using Content.Server.EUI;
using Content.Shared._Misfits.BrotherhoodOfSteel;
using Content.Shared.Eui;

namespace Content.Server._Misfits.BrotherhoodOfSteel;

/// <summary>
/// Consent UI for the fluff "Ad Viktoriya" oath. Nothing mechanical happens —
/// the oath binds only in-character, for as long as the player chooses to
/// honor it. Tribute to Founder Viktoriya Plum, killed in the schism.
/// </summary>
public sealed class BosRecruitEui : BaseEui
{
    private readonly string _recruiterName;
    private readonly string _targetName;
    private readonly Action _onAccept;
    private readonly Action _onDecline;
    private bool _resolved;

    public BosRecruitEui(string recruiterName, string targetName, Action onAccept, Action onDecline)
    {
        _recruiterName = recruiterName;
        _targetName = targetName;
        _onAccept = onAccept;
        _onDecline = onDecline;
    }

    public override void Opened()
    {
        // Send the recruiter and target names as soon as the client opens the
        // EUI. Without this, the themed window opens but never calls SetNames,
        // leaving its localized document body and status line blank.
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new BosRecruitEuiState(_recruiterName, _targetName);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (_resolved || msg is not BosRecruitDecisionMessage decision)
            return;

        _resolved = true;
        if (decision.Accepted)
            _onAccept();
        else
            _onDecline();

        Close();
    }

    public override void Closed()
    {
        if (_resolved)
            return;

        _resolved = true;
        _onDecline();
    }
}
