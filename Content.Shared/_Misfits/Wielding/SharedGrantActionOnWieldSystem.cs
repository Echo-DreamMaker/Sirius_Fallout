using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Interaction.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;


namespace Content.Shared._Misfits.Wielding;

/// <summary>
/// Grants specific actions and takes them away when wielding and unwielding a weapon that grants actions on wield.
///
/// TODO: Currently possible to game cooldowns for the same action by swapping between two weapons that grant it
/// </summary>
public sealed partial class SharedGrantActionOnWieldSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrantActionOnWieldComponent, MisfitsItemWieldedEvent>(OnWield);
        SubscribeLocalEvent<GrantActionOnWieldComponent, ItemUnwieldedEvent>(OnItemUnwielded);
    }

    /// <summary>
    /// All possible forms of unwielding are caught in <see cref="WieldableSystem"/>,
    /// so we only need to catch the ItemUnwieldedEvent that the system will send out.
    /// </summary>
    /// <param name="ent">the item unwielded</param>
    /// <param name="args"></param>
    private void OnItemUnwielded(Entity<GrantActionOnWieldComponent> ent, ref ItemUnwieldedEvent args)
    {
        // I think a null User only happens if the User has been gibbed or otherwise deleted from existence?
        if (args.User is not { } wielder)
            return;

        foreach (var action in ent.Comp.ActionIds)
        {
            _actions.RemoveAction(wielder, action);
        }
        // ent.Comp.ActionIds.Clear();
    }

    private void OnWield(Entity<GrantActionOnWieldComponent> ent, ref MisfitsItemWieldedEvent args)
    {
        if (ent.Comp.ActionIds.Count > 0)
        {
            foreach (var actionId in ent.Comp.ActionIds)
            {
                _actions.AddActionDirect(args.User, actionId);
            }
        }
        else
        {
            foreach (var action in ent.Comp.Actions)
            {
                if (_actions.AddAction(args.User, action) is {} actionId)
                    ent.Comp.ActionIds.Add(actionId);
            }
        }
    }
}
