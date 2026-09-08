// #Misfits Add - Marks a mind as being per-round recruited by the Enclave.
// The mind receives the literal EnclaveRecruit job while this marker stores
// the job that should be restored when recruitment ends.

using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Misfits.Enclave;

/// <summary>
/// Added to a mind entity when a player is per-round recruited into the
/// Enclave. The EnclaveRecruitSystem handles lifecycle (add on recruit
/// verb, remove on death/round restart) and assigns the actual EnclaveRecruit
/// job so its normal playtime tracker is used.
/// </summary>
[RegisterComponent]
public sealed partial class EnclaveRecruitMindComponent : Component
{
    /// <summary>
    /// Job held before recruitment, restored when recruitment ends on death.
    /// </summary>
    [DataField]
    public ProtoId<JobPrototype>? PreviousJob;

    /// <summary>
    /// True once the Enclave entry jobs have been whitelisted for this recruit,
    /// so the unlock system does not re-process the mind on every poll.
    /// </summary>
    [DataField]
    public bool JobsGranted; // #Cythisiax Added - tracks auto-whitelist at 4h unlock
}
