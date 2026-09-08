using System.Linq;
using System.Runtime.CompilerServices;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;

namespace Content.Shared.Weapons.Ranged.Systems;
/// <summary>
/// Helpers for that roughly do the same thing <see cref="BallisticAmmoProviderComponent"/>
/// Some methods were made to also be reusable with other comps(pending further refactor)
/// </summary>
public abstract partial class SharedGunSystem
{
    /// <summary>
    /// Take some or none amount of ammo from giverUID returning a list of that ammo
    /// How this is done is up to comps of giverUID that listen to <see cref="TakeAmmoEvent"/>
    /// </summary>
    /// <param name="ammoAmount">Ammo we TRY to take from giverUID. Though not guaranteed(ie. not enough ammo, or other mechanic ect)</param>
    /// <param name="giverUID">UID who we take ammo from(should have comps that listen to event)</param>
    /// <param name="user"> user that caused event (ie. player interacting with ammo box)</param>
    /// <returns>list of tuples with UID and Ishootable of spawned ammo</returns>
    /// <remarks>
    /// Gets rid of 3 lines of boiler plate, but also makes it clear that we just
    /// get and use the returned ammo for whatever
    /// <remarks/>
    public List<(EntityUid?, IShootable)> DoTakeAmmo(int ammoAmount, EntityUid giverUID, EntityUid? user = null, bool spreadRng = false)
    {
        List<(EntityUid? Entity, IShootable Shootable)> ammo = new(ammoAmount);
        var evTakeAmmo = new TakeAmmoEvent(ammoAmount, ammo, Transform(giverUID).Coordinates, user, spreadRng);
        RaiseLocalEvent(giverUID, evTakeAmmo);
        return ammo;
    }

    /// <summary>
    /// Location in ballistics system code where we actually put the ammo into the recieving comp
    /// also has boilerplate for updating
    /// </summary>
    /// <remarks>
    /// Seperated into its own method for clarity and is probably a likely point of failure so execption handling
    /// <remarks/>
    public virtual void DoAmmoInsert(List<(EntityUid? Entity, IShootable Shootable)> ammo, BallisticAmmoProviderComponent recieverComp, EntityUid recieverUid, EntityUid? User = null)
    {
        if (!Timing.IsFirstTimePredicted)
        {
            foreach (var (shotUID, _) in ammo)
            {
                _xform.DetachEntity(shotUID!.Value);
            }
            //NetworkCompState(recieverUid, User, recieverComp);
            UpdateBallisticAppearance(recieverUid, recieverComp);
            UpdateAmmoCount(recieverUid);
            return;
        }

        foreach (var (shotUID, _) in ammo)
        {
            Containers.Insert(shotUID!.Value, recieverComp.Container);
        }
        recieverComp.SpawnedCountPredict += ammo.Count;
        recieverComp.IndexPredict = recieverComp.IndexPredict + ammo.Count;

        NetworkCompState(recieverUid, User, recieverComp);
        UpdateBallisticAppearance(recieverUid, recieverComp);
        UpdateAmmoCount(recieverUid);
    }

    /// <summary>
    /// default cycle implementation
    /// </summary>
    /// <remarks>GunCycledEvent seems unused for now<remarks/>
    protected List<(EntityUid?, IShootable)> Cycle(EntityUid giverUid, BallisticAmmoProviderComponent comp, EntityUid user)
    {

        var giverXform = (giverUid, Transform(giverUid));
        var sequence = comp.AmmoCount;
        var netEnt = GetNetEntity(giverUid);

        var ammo = DoTakeAmmo(1, giverUid, user, true);

        // server
        if (_netManager.IsServer && ammo.TryFirstOrNull(out var enty))
        {
            var entity = enty.Value.Item1!.Value;
            PlaceNextToRot((entity, Transform(entity)), giverXform);
            EjectCartRNG(entity, sequence, netEnt.Id);
            return ammo;
        }

        // client
        if (Timing.IsFirstTimePredicted && ammo.TryFirstOrNull(out var ent))
        {
            var proto = MetaData(ent.Value.Item1!.Value)!.EntityPrototype!.ID;
            RaisePredictiveEvent(new GunEjectEvent(proto, netEnt.Id, sequence, netEnt));
            return ammo;
        }


        return ammo;
    }

    public void EjectCartRNG(EntityUid cart, int ammoCount, int seed)
    {
        if (Containers.IsEntityInContainer(cart)) return;
        var xform = Transform(cart);
        var (pRNG, rRNG) = GetRandVectAngle(seed, ammoCount);
        var pBase = _xform.GetWorldPosition(xform);
        DebugTools.Assert(DebugEjectCartRNG(seed, ammoCount, pRNG, pBase, rRNG));
        _xform.SetWorldPositionRotation(cart, pRNG + pBase, rRNG.Reduced(), xform);
    }

    /// <summary>
    /// Corrects comp values from bad yaml to prevent errors
    /// unspawned = capacity - containedEnts if prototype isnt null else 0
    /// Containers cant go over capacity else they get cleared
    /// </summary>
    private void EnsureCorrect(EntityUid uid, BallisticAmmoProviderComponent comp)
    {
        // this isnt only "make container if null". Each comp with a container needs its owning entity
        // to also have a containerManagerComp which handles stuff like initializing containers
        // so this ensures container AND containerManagerComp for ent
        // I dont know why this couldn't be done earlier like during serialization seems like an inefficency
        comp.Container = Containers.EnsureContainer<Container>(uid, "ballistic-ammo");

        if (comp.Proto is null && comp.UnspawnedCount > 0)
        {
            comp.UnspawnedCount = 0;
        }
        //  default value
        else if (comp.UnspawnedCount == DEFAULT_AMMO)
        {
            comp.UnspawnedCount = Math.Clamp(Math.Min(comp.Capacity, comp.Capacity - comp.Container.ContainedEntities.Count), 0, comp.Capacity);
        }

        if (comp.Container.ContainedEntities.Count > comp.Capacity)
        {
            Containers.CleanContainer(comp.Container);
        }
        comp.SpawnedCountPredict = comp.Container.ContainedEntities.Count;
    }
    /// <summary>
    /// Is this valid entity allowed to give more than 1 ammo at once?
    /// </summary>
    private bool CanInstantFill(EntityUid giver) => HasComp<SpeedLoaderComponent>(giver);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PlaceNextToRot(Entity<TransformComponent?> freshSpawn, Entity<TransformComponent?> originEnt)
    {
        _xform.PlaceNextTo(freshSpawn, originEnt);
        // _xform.SetLocalRotation(freshSpawn.Owner, rot);
        FlagPredicted(freshSpawn.Owner);
    }
    /// <summary>
    /// Big method of ingame popups
    /// </summary>
    private bool PopupCancelsWhitelist(BallisticAmmoProviderComponent recieverComp, EntityUid recieverUid,
                                BallisticAmmoProviderComponent giverComp, EntityUid giverUid,
                                EntityUid user, List<ProtoId<TagPrototype>>? recieverTags = null,
                                                List<ProtoId<TagPrototype>>? giverTags = null)
    {

        if (recieverComp.AmmoCount == recieverComp.Capacity)
        {
            _popup.PopupPredicted(
                Loc.GetString("gun-ballistic-transfer-target-full",
                    ("entity", MetaData(recieverUid).EntityName)),
                recieverUid,
                user);
            return true;
        }

        if (giverComp.AmmoCount == 0)
        {
            _popup.PopupPredicted(
                Loc.GetString("gun-ballistic-transfer-empty",
                    ("entity", MetaData(giverUid).EntityName)),
                giverUid,
                user);
            return true;
        }

        if (recieverTags is null || giverTags is null ||
            !recieverTags.Any(giverTags.Contains))
        {
            _popup.PopupPredicted(
                        Loc.GetString("gun-ballistic-transfer-invalid",
                            ("ammoEntity", MetaData(giverUid).EntityName),
                            ("targetEntity", MetaData(recieverUid).EntityName)),
                        giverUid,
                        user);
            return true;
        }
        return false;
    }
    private bool PopupCancels(BallisticAmmoProviderComponent recieverComp, EntityUid recieverUid,
                                BallisticAmmoProviderComponent giverComp, EntityUid giverUid,
                                EntityUid user)
    {

        if (recieverComp.AmmoCount == recieverComp.Capacity)
        {
            _popup.PopupPredicted(
                Loc.GetString("gun-ballistic-transfer-target-full",
                    ("entity", MetaData(recieverUid).EntityName)),
                recieverUid,
                user);

            return true;
        }

        if (giverComp.AmmoCount == 0)
        {
            _popup.PopupPredicted(
                Loc.GetString("gun-ballistic-transfer-empty",
                    ("entity", MetaData(giverUid).EntityName)),
                giverUid,
                user);

            return true;
        }
        DebugTools.Assert(recieverComp.AmmoCount < recieverComp.Capacity && recieverComp.AmmoCount > -1);
        return false;
    }

    [Serializable, NetSerializable]
    public sealed class GunEjectEvent(string proto, int seed, int sequence, NetEntity netEnt) : EntityEventArgs
    {
        public string Proto = proto;
        public int Seed = seed;
        public int Sequence = sequence;
        public NetEntity NetEnt = netEnt;
    }

}
