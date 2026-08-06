// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Misfits.Paper;

[RegisterComponent, NetworkedComponent]
public sealed partial class BlockWritingComponent : Component
{
    [DataField]
    public LocId FailWriteMessage = "paper-component-illiterate";
}
