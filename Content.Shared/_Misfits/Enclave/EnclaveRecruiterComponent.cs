// #Misfits Add - Marker component that grants Enclave recruitment privileges.
// Add to any player entity via addcomp to let them use the Recruit verb,
// regardless of their job/department.

namespace Content.Shared._Misfits.Enclave;

/// <summary>
/// When present on a player entity, grants access to the Enclave "Recruit"
/// right-click verb. Intended for admin use via <c>addcomp</c> to extend
/// recruitment permissions to non-Enclave roles (e.g. Brotherhood Elder).
/// </summary>
[RegisterComponent]
public sealed partial class EnclaveRecruiterComponent : Component
{
}
