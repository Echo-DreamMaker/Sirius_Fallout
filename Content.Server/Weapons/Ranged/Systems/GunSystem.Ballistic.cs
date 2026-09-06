using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{


    protected override void CycleCartridge(EntityUid seeder, EntityUid cart, int sequence)
    {
        var seed = GetNetEntity(seeder).Id;
        var giverXform = (seeder, Transform(seeder));
        var xform = (cart, Transform(cart));

        PlaceNextToRot(xform, giverXform);
        EjectCartRNG(cart, sequence, seed);
    }
    /*
    public override void DoAmmoInsert(List<(EntityUid? Entity, IShootable Shootable)> ammo, BallisticAmmoProviderComponent recieverComp, EntityUid recieverUid, EntityUid? User = null)
    {
        foreach (var (shotUID, _) in ammo)
        {
            Containers.Insert(shotUID!.Value, recieverComp.Container);
        }

        // todo: change recieverComp update
        recieverComp.SpawnedCountPredict += ammo.Count;
        recieverComp.IndexPredict = recieverComp.IndexPredict + ammo.Count;
        Dirty(recieverUid, recieverComp);
        UpdateBallisticAppearance(recieverUid, recieverComp);
        UpdateAmmoCount(recieverUid);

    }
    */
}









/// Misfit Change: outdated. Client/Server Implementation in <see cref="SharedGunSystem.Ballistics"/>

/*
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{

    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates)
    {
        EntityUid? ent = null;

        // TODO: Combine with TakeAmmo
        if (component.Container.Count > 0)
        {
            var existing = component.Entities[^1];
            component.Entities.RemoveAt(component.Entities.Count - 1);

            Containers.Remove(existing, component.Container);
            EnsureShootable(existing);
        }
        else if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            ent = Spawn(component.Proto, coordinates);
            EnsureShootable(ent.Value);
        }

        if (ent != null)
            EjectCartridge(ent.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }
}


*/
