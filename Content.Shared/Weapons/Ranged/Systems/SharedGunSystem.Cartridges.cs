using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Weapons.Ranged.Systems;


public abstract partial class SharedGunSystem
{
    protected virtual void InitializeCartridge()
    {
        // inserting just the cartridge itself into something
        SubscribeLocalEvent<CartridgeAmmoComponent, TakeAmmoEvent>(OnTakeAmmo);
    }

    /// <summary>
    /// "Taking ammo" from a single cartridge. Done just for compatability/standardization
    /// (not a container with cartridges like <see cref="BallisticAmmoProviderComponent"/>)
    /// </summary>
    private void OnTakeAmmo(EntityUid uid, CartridgeAmmoComponent giverComp, TakeAmmoEvent args)
    {

        args.Ammo.Add((uid, EnsureShootable(uid)));
        //Dirty(uid, giverComp);
    }
    /// <summary>
    /// clients running this should handle visual/send event to server
    /// server handling this should just validate(ie.. proto isnt null) and then network to relevent clients to run code
    /// </summary>
    /// <param name="baseCoord"> where the event/ejected casing originate from</param>
    /// <param name="baseAngle">usually angle 'shooter' was facing</param>
    /// <param name="cartProto">prototype of the shot cartridge(the bullet shot)</param>
    /// <param name="sender">client the event originated from. Null if server.
    ///                      Important for filtering clients who sent the event
    ///                      so they dont get it twice</param>
    public virtual void EjectSpentCart(SpentCartEvent ev) { }

    [Serializable, NetSerializable]
    public sealed class SpentCartEvent(MapCoordinates baseCoord, Angle baseAngle, string? cartProto, NetUserId? sender) : EntityEventArgs
    {
        public MapCoordinates Coords = baseCoord;
        public Angle Angle = baseAngle;
        public string? Proto = cartProto;
        public NetUserId? Sender = sender;
    }




}
