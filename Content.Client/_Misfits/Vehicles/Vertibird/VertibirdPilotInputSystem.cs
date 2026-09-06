using System.Numerics;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared.Input;
using Content.Shared.Movement.Systems;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Misfits.Vehicles.Vertibird;

public sealed class VertibirdPilotInputSystem : EntitySystem
{
    private const float CameraPanFactor = 0.55f;
    private const float CameraMaxOffset = 10f;
    private const float CameraLerpSpeed = 8f;
    private const float CameraSyncThreshold = 0.1f;
    private static readonly TimeSpan CameraSyncInterval = TimeSpan.FromSeconds(0.1);

    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityUid? _cameraPilot;
    private Vector2 _cameraOffset;
    private Vector2 _lastSentCameraOffset;
    private TimeSpan _nextCameraSync;

    public override void Initialize()
    {
        base.Initialize();

        var binds = CommandBinds.Builder;
        Bind(binds, EngineKeyFunctions.MoveUp, VertibirdControlInput.Forward);
        Bind(binds, EngineKeyFunctions.MoveDown, VertibirdControlInput.Back);
        Bind(binds, EngineKeyFunctions.MoveLeft, VertibirdControlInput.Left);
        Bind(binds, EngineKeyFunctions.MoveRight, VertibirdControlInput.Right);
        binds.Register<VertibirdPilotInputSystem>();
    }

    public override void Shutdown()
    {
        ResetCamera();
        base.Shutdown();
        CommandBinds.Unregister<VertibirdPilotInputSystem>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } pilot ||
            !_input.MouseScreenPosition.IsValid ||
            !TryGetActiveVertibird(pilot, out _) ||
            !TryComp<EyeComponent>(pilot, out var eye) ||
            !TryComp<TransformComponent>(pilot, out var xform))
        {
            ResetCamera();
            return;
        }

        var pilotChanged = _cameraPilot != pilot;
        _cameraPilot = pilot;
        var mouseMap = _eyeManager.PixelToMap(_input.MouseScreenPosition);
        if (mouseMap.MapId != xform.MapID)
            return;

        // Remove our existing eye displacement from the unprojected mouse
        // position so moving the camera does not feed back into a larger pan.
        var pilotPosition = _transform.GetWorldPosition(xform);
        var target = (mouseMap.Position - pilotPosition - _cameraOffset) * CameraPanFactor;
        var length = target.Length();
        if (length > CameraMaxOffset)
            target = target / length * CameraMaxOffset;

        var blend = 1f - MathF.Exp(-CameraLerpSpeed * MathF.Max(frameTime, 0f));
        _cameraOffset = Vector2.Lerp(_cameraOffset, target, blend);
        _eye.SetOffset(pilot, _cameraOffset, eye);
        _eye.SetPvsScale((pilot, eye), 1.75f);

        // #Misfits Add - Pilot zoom follows how hard the camera is panned, mirroring CMU's
        // gunship camera. Scales from 1x at rest up to 1.5x at the pan limit.
        var panFraction = Math.Clamp(_cameraOffset.Length() / CameraMaxOffset, 0f, 1f);
        var pilotZoom = 1f + panFraction * 0.5f;
        _eye.SetZoom(pilot, new Vector2(pilotZoom, pilotZoom), eye);

        SyncCameraOffset(pilotChanged);
    }

    private void Bind(CommandBinds.BindingsBuilder binds, BoundKeyFunction key, VertibirdControlInput input)
    {
        binds.BindBefore(key, new VertibirdMovementHandler(this, input), typeof(SharedMoverController));
    }

    private void SendInput(EntityUid? pilot, VertibirdControlInput input, bool pressed)
    {
        if (pilot is not { } uid || _player.LocalEntity != uid ||
            !IsCruisingPilot(uid))
            return;

        RaiseNetworkEvent(new VertibirdControlInputMessage(input, pressed));
    }

    private bool IsCruisingPilot(EntityUid pilot)
    {
        var query = EntityQueryEnumerator<VertibirdComponent>();
        while (query.MoveNext(out _, out var vertibird))
        {
            if (vertibird.Pilot == pilot && vertibird.State == VertibirdFlightState.Cruising)
                return true;
        }

        return false;
    }

    private bool TryGetActiveVertibird(EntityUid pilot, out Entity<VertibirdComponent> result)
    {
        var query = EntityQueryEnumerator<VertibirdComponent>();
        while (query.MoveNext(out var uid, out var vertibird))
        {
            if (vertibird.Pilot != pilot || vertibird.State == VertibirdFlightState.Grounded)
                continue;

            result = (uid, vertibird);
            return true;
        }

        result = default;
        return false;
    }

    private void ResetCamera()
    {
        if (_cameraPilot is { } pilot)
        {
            if (TryComp<EyeComponent>(pilot, out var eye))
            {
                _eye.SetOffset(pilot, Vector2.Zero, eye);
                _eye.SetPvsScale((pilot, eye), 1f);
                _eye.SetZoom(pilot, Vector2.One, eye);
            }

            RaiseNetworkEvent(new VertibirdCameraOffsetMessage(Vector2.Zero));
        }

        _cameraPilot = null;
        _cameraOffset = Vector2.Zero;
        _lastSentCameraOffset = Vector2.Zero;
        _nextCameraSync = TimeSpan.Zero;
    }

    private void SyncCameraOffset(bool force)
    {
        if (!force && (_timing.CurTime < _nextCameraSync ||
            Vector2.DistanceSquared(_cameraOffset, _lastSentCameraOffset) <
            CameraSyncThreshold * CameraSyncThreshold))
        {
            return;
        }

        RaiseNetworkEvent(new VertibirdCameraOffsetMessage(_cameraOffset));
        _lastSentCameraOffset = _cameraOffset;
        _nextCameraSync = _timing.CurTime + CameraSyncInterval;
    }

    private sealed class VertibirdMovementHandler(VertibirdPilotInputSystem system, VertibirdControlInput input)
        : InputCmdHandler
    {
        private bool _passedDown;

        public override bool HandleCmdMessage(
            IEntityManager entManager,
            ICommonSession? session,
            IFullInputCmdMessage message)
        {
            if (message.State == BoundKeyState.Down)
            {
                var block = session?.AttachedEntity is { } pilot && system.IsCruisingPilot(pilot);
                _passedDown = !block;

                if (block)
                    system.SendInput(session!.AttachedEntity, input, true);

                return block;
            }

            if (message.State == BoundKeyState.Up && _passedDown)
            {
                _passedDown = false;
                return false;
            }

            if (session?.AttachedEntity is not { } releasePilot || !system.IsCruisingPilot(releasePilot))
                return false;

            system.SendInput(releasePilot, input, false);
            return true;
        }
    }
}
