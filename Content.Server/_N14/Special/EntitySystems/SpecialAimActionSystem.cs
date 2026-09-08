using Content.Server.Actions;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Components;
using Content.Shared._Misfits.SpecialStats;
using Content.Shared._N14.Special.Components;

namespace Content.Server._N14.Special.EntitySystems;

/// <summary>
/// Grants the SPECIAL aim-mode action to every character that participates in
/// SPECIAL stats. Lifecycle of the <see cref="SpecialAimableComponent"/> and its
/// toggle action is managed here; the shared <c>SpecialAimingSystem</c> handles
/// the actual zoom/accuracy behaviour.
/// </summary>
public sealed class SpecialAimActionSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;

    private const string AimToggleAction = "ActionToggleSpecialAim";

    public override void Initialize()
    {
        base.Initialize();

        // The directed (SpecialComponent, ComponentStartup/ComponentShutdown) pairs are
        // owned by SharedSpecialSystem, so drive the action lifecycle off these broadcast
        // events instead — same pattern as the misfits medical HUD action.
        SubscribeLocalEvent<SpecialChangedEvent>(OnSpecialChanged);
        SubscribeLocalEvent<SpecialStatsReadyEvent>(OnStatsReady);
        SubscribeLocalEvent<SpecialShutdownEvent>(OnSpecialShutdown);
        SubscribeLocalEvent<SpecialAimableComponent, ComponentShutdown>(OnAimableShutdown);
    }

    private void OnSpecialChanged(ref SpecialChangedEvent args)
    {
        EnsureAimable(args.ChangedEntity);
    }

    private void OnStatsReady(ref SpecialStatsReadyEvent args)
    {
        EnsureAimable(args.Entity);
    }

    private void OnSpecialShutdown(ref SpecialShutdownEvent args)
    {
        RemoveAimable(args.Entity);
    }

    private void OnAimableShutdown(EntityUid uid, SpecialAimableComponent component, ComponentShutdown args)
    {
        if (component.ToggleActionEntity is { } actionId)
            _actions.RemoveAction(uid, actionId);
    }

    private void EnsureAimable(EntityUid uid)
    {
        if (!_special.UsesSpecialStats(uid))
            return;

        var aimable = EnsureComp<SpecialAimableComponent>(uid);
        _actions.AddAction(uid, ref aimable.ToggleActionEntity, AimToggleAction);
    }

    private void RemoveAimable(EntityUid uid)
    {
        if (!TryComp<SpecialAimableComponent>(uid, out var aimable))
            return;

        if (aimable.ToggleActionEntity is { } actionId)
            _actions.RemoveAction(uid, actionId);

        RemComp<SpecialAimableComponent>(uid);
    }
}