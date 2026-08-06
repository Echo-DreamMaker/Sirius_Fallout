using Content.Server.EUI;
using Content.Shared._Misfits.Enclave;
using Content.Shared.Eui;

namespace Content.Server._Misfits.Enclave;

/// <summary>
/// Consent UI for the Enclave contract and personalized oath.
/// </summary>
public sealed class EnclaveRecruitEui : BaseEui
{
    private readonly string _characterName;
    private readonly Action _onAccept;
    private readonly Action _onDecline;
    private bool _resolved;

    public EnclaveRecruitEui(string characterName, Action onAccept, Action onDecline)
    {
        _characterName = characterName;
        _onAccept = onAccept;
        _onDecline = onDecline;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new EnclaveRecruitEuiState(_characterName);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (_resolved || msg is not EnclaveRecruitDecisionMessage decision)
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
