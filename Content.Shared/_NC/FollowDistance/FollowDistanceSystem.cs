// License granted by JerryImMouse to Corvax Forge.
// Non-exclusive, non-transferable, perpetual license to use, distribute, and modify.
// All other rights reserved by JerryImMouse.
using Content.Shared._NC.CameraFollow.Components;
using Content.Shared._NC.CameraFollow.Events;
using Content.Shared.Camera;
using Robust.Shared.Network;

namespace Content.Shared._NC.FollowDistance;
/// <summary>
/// System to set new max distance and back strength for <see cref="CameraFollowComponent"/>
/// </summary>
public sealed class FollowDistanceSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly Actions.SharedActionsSystem _actionsSystem = default!;
    private EntityQuery<CameraRecoilComponent> _activeRecoil;
    private EntityQuery<CameraFollowComponent> _activeCamera;

    public override void Initialize()
    {
        SubscribeLocalEvent<CameraFollowComponent, ComponentRemove>(OnCameraFollowRemove);
        SubscribeLocalEvent<CameraFollowComponent, MapInitEvent>(OnCameraFollowInit);

        SubscribeLocalEvent<CameraFollowComponent, GetEyeOffsetEvent>(OnCameraRecoilGetEyeOffset);

        SubscribeAllEvent<ChangeCamOffsetEvent>(OnChangeOffset);

        _activeRecoil = GetEntityQuery<CameraRecoilComponent>();
        _activeCamera = GetEntityQuery<CameraFollowComponent>();
    }

    private void OnChangeOffset(ChangeCamOffsetEvent msg, EntitySessionEventArgs args)
    {
        var plr = args.SenderSession.AttachedEntity;
        if(plr == null || !_activeCamera.TryComp(plr, out var cameraFollowComponent))
            return;
        cameraFollowComponent.Offset = msg.Offset;
        Dirty(plr.Value, cameraFollowComponent);
    }

    private void OnCameraRecoilGetEyeOffset(Entity<CameraFollowComponent> ent, ref GetEyeOffsetEvent arg)
    {
        if (!ent.Comp.Enabled || !_activeRecoil.TryComp(ent, out var recoil))
            return;

        arg.Offset = recoil.BaseOffset + recoil.CurrentKick + ent.Comp.Offset;
    }

    private void OnCameraFollowInit(EntityUid uid, CameraFollowComponent component, MapInitEvent args)
    {
        if (component.Action == null)
            return;

        _actionsSystem.AddAction(uid, ref component.ActionEntity, component.Action);
    }

    private void OnCameraFollowRemove(EntityUid uid, CameraFollowComponent component, ComponentRemove args)
    {
        if(component.ActionEntity == null || TerminatingOrDeleted(component.ActionEntity.Value))
            return;
        _actionsSystem.RemoveAction(uid, component.ActionEntity);
    }

}
