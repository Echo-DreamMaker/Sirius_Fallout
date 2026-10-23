using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Misfits.WeaponAssemblyStation;

[Serializable, NetSerializable]
public sealed class WeaponAssemblyUiButtonPressedMessage : BoundUserInterfaceMessage
{
    public WeaponAssemblyUiButton Button;

    public WeaponAssemblyUiButtonPressedMessage(WeaponAssemblyUiButton button)
    {
        Button = button;
    }
}