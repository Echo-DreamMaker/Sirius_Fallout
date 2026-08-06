// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Speech;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpeechFontOverrideComponent : Component
{
    [DataField(required: true)]
    public string Font = string.Empty;

    [DataField]
    public bool SourceOnly = true;
}
