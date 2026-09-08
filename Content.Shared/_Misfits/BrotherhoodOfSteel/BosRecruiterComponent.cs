// #Misfits Add - Marker component that grants the Ad Viktoriya "Recruit" verb.
// Add to any player entity via addcomp / View Variables to let them offer the
// oath regardless of their job/department — including aghosts. Intended for
// admin use so the Viktoriya remnant recruiter is whoever you designate, not a
// fixed job (unlike the Enclave verb, which is job-restricted).

namespace Content.Shared._Misfits.BrotherhoodOfSteel;

/// <summary>
/// When present on a player entity, grants access to the Ad Viktoriya "Recruit"
/// right-click verb. Bypasses the Brotherhood department requirement and the
/// normal access/interaction checks, so admins can addcomp it to their own
/// character or aghost and recruit from anywhere.
/// </summary>
[RegisterComponent]
public sealed partial class BosRecruiterComponent : Component
{
}
