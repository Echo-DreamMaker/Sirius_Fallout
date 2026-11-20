using System.Numerics;
using Content.Shared._N14.Vehicles;
using Content.Shared.Actions;
using Content.Shared.Light;
using Content.Shared.Light.Components;
using Content.Shared.Mech.Components; // #Sirius Add
using Content.Shared.Toggleable;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Server._N14.Vehicles;

// #Sirius Change - two cone headlights spawned in front of the car, toggled by the light action.
public sealed class CarHeadlightsSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarHeadlightsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CarHeadlightsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CarHeadlightsComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<CarHeadlightsComponent, ToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<CarHeadlightsComponent, LightToggleEvent>(OnLightToggle);
    }

    private void OnMapInit(EntityUid uid, CarHeadlightsComponent comp, MapInitEvent args)
    {
        // #Sirius Change - inherited mech flashlight would toggle the car's own PointLight as a sphere.
        RemComp<UnpoweredFlashlightComponent>(uid);

        if (comp.LeftLight != null)
        {
            QueueDel(comp.LeftLight.Value);
            comp.LeftLight = null;
        }
        if (comp.RightLight != null)
        {
            QueueDel(comp.RightLight.Value);
            comp.RightLight = null;
        }

        var coords = Transform(uid).Coordinates;
        comp.LeftLight = Spawn(comp.LightPrototype, coords);
        comp.RightLight = Spawn(comp.LightPrototype, coords);

        // #Sirius Change - the car's own PointLight (from BaseMech) must stay invisible.
        DisableControllerLight(uid);

        comp.HeadlightsOn = false;
        SetLightEnabled(comp.LeftLight, false);
        SetLightEnabled(comp.RightLight, false);
        UpdateLightPositions(uid, comp);
    }

    private void OnShutdown(EntityUid uid, CarHeadlightsComponent comp, ComponentShutdown args)
    {
        if (comp.LeftLight != null)
            QueueDel(comp.LeftLight.Value);
        if (comp.RightLight != null)
            QueueDel(comp.RightLight.Value);
    }

    private void OnMove(EntityUid uid, CarHeadlightsComponent comp, ref MoveEvent args)
    {
        UpdateLightPositions(uid, comp);
    }

    // #Sirius Change - light action is raised on the car (action container), not the pilot.
    private void OnToggleAction(EntityUid uid, CarHeadlightsComponent comp, ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        SetHeadlights(uid, comp, !comp.HeadlightsOn);
        _actions.SetToggled(args.Action, comp.HeadlightsOn);
        RaiseLocalEvent(uid, new LightToggleEvent(comp.HeadlightsOn));
        args.Handled = true;
    }

    private void OnLightToggle(EntityUid uid, CarHeadlightsComponent comp, ref LightToggleEvent args)
    {
        SetHeadlights(uid, comp, args.IsOn);
    }

    /// <summary>
    /// #Sirius Add - toggles both cone sources and keeps the action icon in sync.
    /// </summary>
    public void SetHeadlights(EntityUid uid, CarHeadlightsComponent comp, bool enabled)
    {
        comp.HeadlightsOn = enabled;
        DisableControllerLight(uid);
        SetLightEnabled(comp.LeftLight, enabled);
        SetLightEnabled(comp.RightLight, enabled);
        UpdateLightPositions(uid, comp);

        // #Sirius Add - keep the action icon in sync when lights are killed by key/battery removal.
        if (TryComp<MechComponent>(uid, out var mech) && mech.ToggleActionEntity != null)
            _actions.SetToggled(mech.ToggleActionEntity, enabled);
    }

    private void DisableControllerLight(EntityUid uid)
    {
        if (!TryComp<PointLightComponent>(uid, out var light))
            return;

        _light.SetEnabled(uid, false, light);
        _light.SetRadius(uid, 0f, light);
        _light.SetEnergy(uid, 0f, light);
    }

    private void SetLightEnabled(EntityUid? lightUid, bool enabled)
    {
        if (lightUid == null || !TryComp<PointLightComponent>(lightUid.Value, out var light))
            return;

        _light.SetEnabled(lightUid.Value, enabled, light);
    }

    private void UpdateLightPositions(EntityUid uid, CarHeadlightsComponent comp)
    {
        if (comp.LeftLight == null || comp.RightLight == null)
            return;

        var xform = Transform(uid);
        if (xform.MapID == MapId.Nullspace)
            return;

        var worldPos = _transform.GetWorldPosition(xform);
        var rot = _transform.GetWorldRotation(xform);

        // #Sirius Change - forward is car "south" at rotation 0; perpendicular is east.
        var forward = rot.ToWorldVec();
        var perp = new Vector2(-forward.Y, forward.X);

        var forwardPoint = worldPos + forward * comp.ForwardOffset;
        var rightPos = forwardPoint + perp * comp.SideOffset;
        var leftPos = forwardPoint - perp * comp.SideOffset;

        _transform.SetWorldPosition(comp.RightLight.Value, rightPos);
        _transform.SetWorldRotation(comp.RightLight.Value, rot);
        _transform.SetWorldPosition(comp.LeftLight.Value, leftPos);
        _transform.SetWorldRotation(comp.LeftLight.Value, rot);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // #Sirius Change - keep beams glued to the car while turning in place.
        var query = EntityQueryEnumerator<CarHeadlightsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID == MapId.Nullspace)
                continue;
            if (comp.LeftLight == null || comp.RightLight == null)
                continue;

            UpdateLightPositions(uid, comp);
        }
    }
}
