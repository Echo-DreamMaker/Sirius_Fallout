// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Action component for the Transcendent Olfaction sniff, which points the user toward
/// whoever's scent they are currently tracking.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ScentSenseActionComponent : Component
{
    /// <summary>
    /// How far the nose reaches.
    /// </summary>
    [DataField]
    public float Range = 30f;
}

public sealed partial class ScentSenseActionEvent : InstantActionEvent;
