using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Construction;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Electronics;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._NC.RandomAccessKey;

public sealed class RandomAccessKeySystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    private const string RandomAccessPrefix = "RandomAccess";
    private const string Key = "N14IDKeyIronEmpty";

    // #Cythisiax Added - lock/unlock feedback sounds (same paths as the generic LockComponent).
    private static readonly SoundSpecifier LockSound = new SoundPathSpecifier("/Audio/Machines/door_lock_on.ogg");
    private static readonly SoundSpecifier UnlockSound = new SoundPathSpecifier("/Audio/Machines/door_lock_off.ogg");

    public override void Initialize()
    {
        base.Initialize();

        // #Cythisiax Edited - generate a key for EVERY constructed door, not only the pre-marked
        // "locked" metal/wood door variants that carried RandomAccessKey in their YAML prototype.
        SubscribeLocalEvent<DoorComponent, ConstructionCompletedEvent>(OnConstructionCompleted);

        // #Cythisiax Edited - using the assigned key on the door now toggles its lock state instead of
        // opening/closing it. Empty-hand clicks still open an unlocked door and deny a locked one.
        SubscribeLocalEvent<RandomAccessKeyComponent, InteractUsingEvent>(OnKeyInteractUsing);
        SubscribeLocalEvent<RandomAccessKeyComponent, ExaminedEvent>(OnExamined);
    }

    // #Cythisiax Edited - generic key minting for any simple (non-airlock) door that finishes construction.
    private void OnConstructionCompleted(Entity<DoorComponent> ent, ref ConstructionCompletedEvent args)
    {
        if (args.UserUid == null)
            return;

        // #Cythisiax Added - skip airlock-style doors (bunker doors, windoors, blast doors, ...). Their
        // access is delegated to a door electronics board, so a per-door key would never be consulted.
        if (HasComp<AirlockComponent>(ent.Owner) || HasComp<DoorElectronicsComponent>(ent.Owner))
            return;

        var accessReader = EnsureComp<AccessReaderComponent>(ent.Owner);
        if (accessReader.ContainerAccessProvider != null)
            return;

        // #Cythisiax Added - idempotency guard so a door that already received a key (e.g. a locked
        // variant whose graph completed more than once) does not mint a second, conflicting key.
        if (HasComp<RandomAccessKeyComponent>(ent.Owner) && accessReader.AccessLists.Count > 0)
            return;

        var randomKey = _random.Next(1000, 9999);
        var prototypeId = $"{RandomAccessPrefix}{randomKey}";

        accessReader.AccessLists.Add(new HashSet<ProtoId<AccessLevelPrototype>> { prototypeId });
        // #Cythisiax Comment - dirty the access reader so the client sees the new access requirement;
        // without this the client predicted state has an empty AccessLists and diverges from the server.
        Dirty(ent.Owner, accessReader);
        // #Cythisiax Comment - notify NavMap/UI consumers (e.g. door electronics UI) of the change.
        RaiseLocalEvent(ent.Owner, new AccessReaderConfigurationChangedEvent());

        EnsureComp<RandomAccessKeyComponent>(ent.Owner);

        var userCord = _transform.GetMapCoordinates(args.UserUid.Value);
        var doorKey = Spawn(Key, userCord);
        var accessKey = EnsureComp<AccessComponent>(doorKey);

        _meta.SetEntityName(doorKey, $"Key #{randomKey}");
        accessKey.Tags.Clear();
        accessKey.Tags.Add(prototypeId);
        Dirty(doorKey, accessKey);
        _hands.PickupOrDrop(args.UserUid.Value, doorKey);

        var door = ent.Comp;
        door.CanPry = false;
        door.BumpOpen = false;
        Dirty(ent.Owner, door);
    }

    // #Cythisiax Edited - the assigned key (or any item carrying the same random access tag) toggles the
    // lock. This replaced the old "key opens/closes the door" bridge so the player can lock/unlock freely.
    private void OnKeyInteractUsing(Entity<RandomAccessKeyComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only react when the held item actually carries access tags, otherwise let other handlers run
        // (e.g. construction tools deconstructing the door, prying, welding, etc.).
        if (!HasComp<AccessComponent>(args.Used))
            return;

        if (!TryComp<AccessReaderComponent>(ent.Owner, out var reader))
            return;

        // #Cythisiax Added - ignore doors whose access is delegated to an electronics board.
        if (reader.ContainerAccessProvider != null)
            return;

        // #Cythisiax Added - only a key assigned to THIS door may toggle the lock. An unrelated ID card
        // must not be able to lock/unlock someone else's door.
        if (!HasMatchingAccess(args.Used, reader))
            return;

        args.Handled = true;

        // #Cythisiax Added - the lock state lives server-side in AccessReader.Enabled. Skip client
        // prediction to avoid double-toggling the lock.
        if (!_net.IsServer)
            return;

        ToggleLock(ent.Owner, reader, args.User);
    }

    private void ToggleLock(EntityUid door, AccessReaderComponent reader, EntityUid user)
    {
        if (!TryComp<DoorComponent>(door, out var doorComp))
            return;

        if (reader.Enabled)
        {
            // Unlock: anyone may now open the door.
            reader.Enabled = false;
            doorComp.CanPry = true;
            _popup.PopupClient(Loc.GetString("door-key-lock-unlocked"), door, user);
            _audio.PlayPredicted(UnlockSound, door, user);
        }
        else
        {
            // Lock: the door now requires the assigned key.
            reader.Enabled = true;
            doorComp.CanPry = false;
            _popup.PopupClient(Loc.GetString("door-key-lock-locked"), door, user);
            _audio.PlayPredicted(LockSound, door, user);
        }

        Dirty(door, reader);
        Dirty(door, doorComp);
        RaiseLocalEvent(door, new AccessReaderConfigurationChangedEvent());
    }

    private bool HasMatchingAccess(EntityUid key, AccessReaderComponent reader)
    {
        if (!TryComp<AccessComponent>(key, out var keyAccess))
            return false;

        foreach (var list in reader.AccessLists)
        {
            if (list.Overlaps(keyAccess.Tags))
                return true;
        }

        return false;
    }

    // #Cythisiax Added - surface the lock state when the door is examined.
    private void OnExamined(Entity<RandomAccessKeyComponent> ent, ref ExaminedEvent args)
    {
        if (TryComp<AccessReaderComponent>(ent.Owner, out var reader))
        {
            args.PushText(Loc.GetString(reader.Enabled
                ? "door-key-lock-examined-locked"
                : "door-key-lock-examined-unlocked"));
        }
    }
}
