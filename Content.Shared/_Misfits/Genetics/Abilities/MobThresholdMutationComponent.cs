// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.FixedPoint;
using Content.Shared.Mobs;

namespace Content.Shared._Misfits.Genetics.Abilties;

/// <summary>
/// Removes a mob state from the target mob's thresholds and allowed states.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(MobThresholdMutationSystem))]
[AutoGenerateComponentState]
public sealed partial class MobThresholdMutationComponent : Component
{
    /// <summary>
    /// Removes this mob state from the thresholds and allowed states.
    /// </summary>
    [DataField(required: true)]
    public MobState Removed = MobState.Invalid;

    /// <summary>
    /// The threshold it had, if it was present.
    /// Used when removing the mutation.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2? OldThreshold;

    /// <summary>
    /// Whether the state was actually taken out of the mob's allowed states.
    /// Tracked separately from <see cref="OldThreshold"/>, a mob can allow a state
    /// without having a threshold for it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool RemovedState;
}
