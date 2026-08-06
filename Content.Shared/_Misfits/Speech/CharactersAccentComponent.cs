// SPDX-License-Identifier: AGPL-3.0-or-later


namespace Content.Shared._Misfits.Speech;

/// <summary>
/// Replaces individual characters with random strings, ignoring case etc.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CharactersAccentComponent : Component
{
    [DataField(required: true)]
    public Dictionary<char, List<string>> Chars = new();
}
