using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Sirius.WeaponAssemblyStation;

[Serializable, NetSerializable]
public sealed class WeaponAssemblyUiButtonPressedMessage : BoundUserInterfaceMessage
{
    public WeaponAssemblyUiButton Button;

    public WeaponAssemblyUiButtonPressedMessage(WeaponAssemblyUiButton button)
    {
        Button = button;
    }
}