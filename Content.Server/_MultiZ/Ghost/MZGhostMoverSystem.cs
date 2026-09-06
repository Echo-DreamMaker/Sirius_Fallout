// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
// Ported to misfits-14 _MultiZ/ — renamed &amp; adapted
// #Cythisiax Ported — Multi-Z level support for misfits-14

using Content.Server._MultiZ.Core;
using Content.Shared._MultiZ.Core.Components;
using Content.Shared._MultiZ.Ghost;
using Content.Shared.Actions;

namespace Content.Server._MultiZ.Ghost;

/// <summary>
/// Server-side ghost Z-level mover. Grants up/down actions to ghosts and handles the teleport.
/// </summary>
public sealed class MZGhostMoverSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    // #Cythisiax Fixed - inject concrete server MZSystem, not abstract MZSharedSystem.
    // MZSharedSystem has 2 server subtypes (MZSystem, MZPvsSystem) so Robust excludes the
    // abstract supertype from IoC, causing UnregisteredDependencyException at startup.
    [Dependency] private MZSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MZGhostMoverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MZGhostMoverComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<MZGhostMoverComponent, MZGhostActionUp>(OnZLevelUp);
        SubscribeLocalEvent<MZGhostMoverComponent, MZGhostActionDown>(OnZLevelDown);
    }

    private void OnMapInit(Entity<MZGhostMoverComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ZLevelUpActionEntity, ent.Comp.UpActionProto);
        _actions.AddAction(ent, ref ent.Comp.ZLevelDownActionEntity, ent.Comp.DownActionProto);
    }

    private void OnRemove(Entity<MZGhostMoverComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
    }

    private void OnZLevelDown(Entity<MZGhostMoverComponent> ent, ref MZGhostActionDown args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveDown(ent);
    }

    private void OnZLevelUp(Entity<MZGhostMoverComponent> ent, ref MZGhostActionUp args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveUp(ent);
    }
}
