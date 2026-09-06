using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Spawners;

namespace Content.Server._Misfits.Weapons.Guns;

public sealed partial class CasingPhysicsOptSystem : EntitySystem
{
    private const int MaxCasings = 3; // you only get to share 3 between the whole server

    // FIFO queue of tracked casing UIDs for cap enforcement.
    private readonly Queue<EntityUid> _casingQueue = new();
    [Dependency] private ILogManager _logManager = default!;
    private ISawmill _cartridgeAlarm = default!;
    public override void Initialize()
    {
        base.Initialize();
        /// none of these should be called btw. This is to catch left over code I missed

        // Track casings for the global cap when their despawn timer is attached.
        // This fires for ALL spent casings — both thrown and no-throw variants.
        SubscribeLocalEvent<CartridgeAmmoComponent, ComponentStartup>(OnCartridgeStartup);
        SubscribeLocalEvent<CartridgeAmmoComponent, LandEvent>(OnCasingLand);
        _cartridgeAlarm = _logManager.GetSawmill("server.gun.cartridge");

    }

    /// <summary>
    /// Raised by <c>ThrownItemSystem</c> on ent's throw timer finishing
    /// Landed cart is set to rest
    /// </summary>
    private void OnCasingLand(EntityUid uid, CartridgeAmmoComponent cartridge, ref LandEvent args)
    {
        if (!cartridge.Spent)
            return;
        _cartridgeAlarm.Error($"CARTRIDGE WASNT DELETED WHEN SPENT\nUID: {uid}\nPROTOTYPE: {MetaData(uid).EntityPrototype} ");
        if (_casingQueue.Count < MaxCasings)
        {
            _casingQueue.Enqueue(uid);
            return;
        }
        Del(uid);
    }

    /// <summary>
    /// Track spent casings for cap enforcement. We piggyback on ComponentStartup
    /// rather than adding a dedicated marker component.
    /// </summary>
    private void OnCartridgeStartup(EntityUid uid, CartridgeAmmoComponent cartridge, ComponentStartup args)
    {
        // Only track spent casings that have a despawn timer (i.e. ejected casings,
        // not cartridges sitting in a magazine).
        if (!cartridge.Spent || !HasComp<TimedDespawnComponent>(uid))
            return;
        _cartridgeAlarm.Error($"CARTRIDGE WASNT DELETED WHEN SPENT\nUID: {uid}\nPROTOTYPE: {MetaData(uid).EntityPrototype} ");
        if (_casingQueue.Count < MaxCasings)
        {
            _casingQueue.Enqueue(uid);
            return;
        }
        Del(uid);
    }
    /*
        /// <summary>
        /// Delete the oldest casings when the cap is exceeded.
        /// Skips already-deleted entities (natural despawn or manual cleanup).
        /// </summary>
        private void TrimCasings()
        {
            while (_casingQueue.Count > MaxCasings)
            {
                var oldest = _casingQueue.Dequeue();
                if (Exists(oldest) && !TerminatingOrDeleted(oldest))
                    QueueDel(oldest);
            }
        }
        */
}
