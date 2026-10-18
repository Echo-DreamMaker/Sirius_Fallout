// #Misfits Add - Server half of the weapon durability system.
//
// Construction swaps the weapon entity for a different prototype when it is
// built up or stripped down, so the body's durability has to be carried across
// by hand. The construction event is server-only, hence this little system
// living on the server while the rest of the logic is shared.
using Content.Server.Construction;
using Content.Shared._Misfits.Weapons.Durability;

namespace Content.Server._Misfits.Weapons.Durability;

public sealed class WeaponDurabilityConstructionSystem : EntitySystem
{
    [Dependency] private readonly SharedWeaponDurabilitySystem _durability = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ConstructionChangeEntityEvent>(OnConstructionChangeEntity);
    }

    private void OnConstructionChangeEntity(ConstructionChangeEntityEvent args)
    {
        _durability.TransferFrame(args.Old, args.New);
    }
}
