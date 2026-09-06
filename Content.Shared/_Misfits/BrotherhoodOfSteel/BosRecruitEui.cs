// #Misfits Add - Shared EUI types for the fluff-only Brotherhood of Steel
// "Ad Viktoriya" oath, a tribute to Founder Viktoriya Plum (killed in the
// schism; Her loyalists remain). Pure roleplay: no job, no department, no
// time gate, no binding mechanic. The player carries the oath in-character.

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Misfits.BrotherhoodOfSteel;

[Serializable, NetSerializable]
public sealed class BosRecruitEuiState : EuiStateBase
{
    public readonly string RecruiterName;
    public readonly string TargetName;

    public BosRecruitEuiState(string recruiterName, string targetName)
    {
        RecruiterName = recruiterName;
        TargetName = targetName;
    }
}

[Serializable, NetSerializable]
public sealed class BosRecruitDecisionMessage : EuiMessageBase
{
    public readonly bool Accepted;

    public BosRecruitDecisionMessage(bool accepted)
    {
        Accepted = accepted;
    }
}
