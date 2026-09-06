using Content.Shared._Misfits.Mech;
using Robust.Client.GameObjects;

namespace Content.Client._Misfits.Mech;

/// <summary>
/// Power armor prototypes inherit the stock mech's no-rotation sprite setting,
/// but their composite armor sprite must rotate toward the combat target.
/// </summary>
public sealed class PowerArmorMechVisualSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PowerArmorMechComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, PowerArmorMechComponent component, ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.NoRotation = false;
    }
}
