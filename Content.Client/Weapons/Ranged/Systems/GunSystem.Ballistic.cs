using Content.Client._Misfits.Movement;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameStates;
using Robust.Client.Timing;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{

    protected override void InitializeBallistic()
    {
        base.InitializeBallistic();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, UpdateAmmoCounterEvent>(OnBallisticAmmoCount);
        SubscribeLocalEvent<GunEjectEvent>(OnEject);
    }
    [Dependency] private IClientGameStateManager _stateMan = default!;
    protected override void CycleCartridge(EntityUid seeder, EntityUid cart, int sequence)
    {
        var netEnt = GetNetEntity(seeder);
        var seed = netEnt.Id;
        var proto = MetaData(cart).EntityPrototype!.ID;
        var ev = new GunEjectEvent(proto, seed, sequence, netEnt);
        // this is what RaisePredictiveEvent does but without some overhead
        // and stuff that gets called we dont use like networking
        // SystemMessageDispatched puts the event in a list that
        // the client game loop calls during PredictTicks
        // significant thing is that it only gets rid of said event
        // when the last acknowledged server sequence passes the
        // seqence the event was orignally dispacted at
        // said sequence is even what SystemMessageDispatched returns
        // normal prediction wouldnt remember the sequence or repeat the event way past its tick
        // tho predicted ents still cant run past a tick w/o being deleted/reset
        // doesnt matter here because visual of plopped cartridge is just for a tick
        // tho of course in an ideal case we'll just want to have the visual/thing spawned once and
        // only dissapear when its ack'd server state/sequence/tick/whatever arrives
        // and not repeatedly reset over and over per tick
        // maybe I am missing something obvious i dunno
        _stateMan.SystemMessageDispatched(ev);
        RaiseLocalEvent(ev);
    }
    // handles unspent cart ejection. Kept as its own seperate predicted event
    // since gun system goes by its own prediction
    private void OnEject(GunEjectEvent args)
    {
        var giverUid = GetEntity(args.NetEnt);
        var xform = (giverUid, Transform(giverUid));
        var ent = Spawn(args.Proto);
        FlagPredicted(ent);
        PlaceNextToRot((ent, Transform(ent)), xform);
        EjectCartRNG(ent, args.Sequence, args.Seed);
    }
    /// <summary>
    /// updates client ui on ammo change
    /// </summary>
    private void OnBallisticAmmoCount(EntityUid uid, BallisticAmmoProviderComponent component, UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
        {
            control.Update(component.AmmoCount, component.Capacity);
        }
    }



}
