// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Damage;
using Content.Shared.Item;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared._Misfits.Genetics.Abilities;

/// <summary>
/// Lets the holder devour any item at all - rocks, ore, tools, whatever fits in a hand -
/// digesting it for nutrition. Rock Absorber additionally takes on the material's
/// properties, mending the body as it digests.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DevourMutationComponent : Component
{
    /// <summary>
    /// How long devouring an item takes.
    /// </summary>
    [DataField]
    public TimeSpan DevourTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Hunger restored per item devoured.
    /// </summary>
    [DataField]
    public float Nutrition = 30f;

    /// <summary>
    /// Thirst restored per item devoured.
    /// </summary>
    [DataField]
    public float Hydration = 10f;

    /// <summary>
    /// Damage healed per item devoured, for absorbing the material's properties.
    /// Negative values heal.
    /// </summary>
    [DataField]
    public DamageSpecifier? Absorb;

    /// <summary>
    /// Items matching this are never offered to the verb and can't be devoured.
    /// Devouring deletes the item outright, so round-critical things (the nuke disk,
    /// command IDs, antag steal objectives) have to stay off the menu.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}

[Serializable, NetSerializable]
public sealed partial class DevourMutationDoAfterEvent : SimpleDoAfterEvent;

public sealed class DevourMutationSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly HungerSystem _hunger = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ThirstSystem _thirst = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DevourMutationComponent, GetVerbsEvent<InnateVerb>>(OnGetVerbs);
        SubscribeLocalEvent<DevourMutationComponent, DevourMutationDoAfterEvent>(OnDoAfter);
    }

    private void OnGetVerbs(Entity<DevourMutationComponent> ent, ref GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.User == args.Target)
            return;

        // anything you could pick up, you can eat, short of the round-critical stuff
        if (!HasComp<ItemComponent>(args.Target) || !CanDevour(ent, args.Target))
            return;

        var target = args.Target;
        args.Verbs.Add(new InnateVerb
        {
            Act = () => StartDevour(ent, target),
            Text = Loc.GetString("devour-mutation-verb"),
            Icon = new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/smite.svg.192dpi.png")),
            Priority = 1
        });
    }

    /// <summary>
    /// Whether this item is allowed to be eaten. Checked both when offering the verb and
    /// again on completion, since the item can be swapped during the do-after.
    /// </summary>
    private bool CanDevour(Entity<DevourMutationComponent> ent, EntityUid target)
        => _whitelist.IsBlacklistFailOrNull(ent.Comp.Blacklist, target);

    private void StartDevour(Entity<DevourMutationComponent> ent, EntityUid target)
    {
        var args = new DoAfterArgs(EntityManager, ent.Owner, ent.Comp.DevourTime, new DevourMutationDoAfterEvent(), ent.Owner, target: target)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupEntity(Loc.GetString("devour-mutation-start", ("item", target)), ent, ent);
    }

    private void OnDoAfter(Entity<DevourMutationComponent> ent, ref DevourMutationDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target is not {} target)
            return;

        // recheck: the do-after gives a window to swap in something protected
        if (!CanDevour(ent, target))
            return;

        args.Handled = true;

        // deleting the item and feeding is server business
        if (_net.IsClient)
            return;

        _popup.PopupEntity(Loc.GetString("devour-mutation-finish", ("item", target)), ent, ent);

        _hunger.ModifyHunger(ent.Owner, ent.Comp.Nutrition);
        if (TryComp<ThirstComponent>(ent, out var thirst))
            _thirst.ModifyThirst(ent.Owner, thirst, ent.Comp.Hydration);

        // Rock Absorber: take on the properties of what you ate
        if (ent.Comp.Absorb is {} absorb)
            _damageable.TryChangeDamage(ent.Owner, absorb, ignoreResistances: true, interruptsDoAfters: false);

        QueueDel(target);
    }
}
