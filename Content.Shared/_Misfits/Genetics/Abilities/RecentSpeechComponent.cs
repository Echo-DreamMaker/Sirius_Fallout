// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Records the last few things an entity said, so Mind Reader can pull them back out of
/// their head. Added automatically to anything that speaks.
/// </summary>
[RegisterComponent]
public sealed partial class RecentSpeechComponent : Component
{
    /// <summary>
    /// How many recent messages to remember.
    /// </summary>
    [DataField]
    public int MaxMessages = 5;

    /// <summary>
    /// The remembered messages, oldest first.
    /// </summary>
    [ViewVariables]
    public List<string> Messages = new();
}
