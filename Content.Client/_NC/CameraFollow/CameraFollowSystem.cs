// License granted by JerryImMouse to Corvax Forge.
// Non-exclusive, non-transferable, perpetual license to use, distribute, and modify.
// All other rights reserved by JerryImMouse.
using System.Numerics;
using Content.Shared._NC.CameraFollow.Components;
using Content.Shared._NC.CameraFollow.Events;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared._Misfits.Special;
using Content.Shared._Misfits.Special.Prototypes;
using Content.Shared._Misfits.SpecialStats;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;

namespace Content.Client._NC.CameraFollow;

/// <summary>
/// Use to make camera follow for player's mouse, uses Lerp() func. to follow player's mouse
/// </summary>
public sealed class CameraFollowSystem : EntitySystem
{
    /// <summary>
    /// Nominal cruise speed of the aim camera (tiles per second). The camera accelerates
    /// up to this speed after entering aim mode, then tapers to a stop as it nears the
    /// aim target.
    /// </summary>
    private const float AimMaxMoveSpeed = 5f;

    /// <summary>
    /// Distance from the aim target (world units) below which the camera is considered
    /// to have arrived and snaps exactly onto it.
    /// </summary>
    private const float AimArrivalEpsilon = 1e-3f;

    /// <summary>
    /// Psycho reagent (DamageModifyingMixture). While its timeline is active the
    /// aim shake is suppressed entirely.
    /// </summary>
    private const string PsychoReagentId = "DamageModifyingMixture";

    private readonly Dictionary<EntityUid, Vector2> _velocities = new();

    /// <summary>
    /// Shake vector applied to the rendered eye on the previous frame. It is folded
    /// back out of the cursor-to-character aim line so the camera keeps tracking the
    /// real mouse position instead of chasing its own tremor.
    /// </summary>
    private Vector2 _lastAimShake;

    /// <summary>
    /// Accumulated render time driving the pseudo-random shake wobble. Accumulating
    /// instead of sampling wall time keeps the phase frozen while the game is paused.
    /// </summary>
    private float _shakeElapsed;

    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IEyeManager _manager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedSpecialSystem _special = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // Check if mouse position is valid
        if (!_timing.IsFirstTimePredicted || !_input.MouseScreenPosition.IsValid)
        {
            _lastAimShake = Vector2.Zero;
            return;
        }

        var player = _player.LocalSession?.AttachedEntity;

        if (player == null)
        {
            _velocities.Clear();
            _lastAimShake = Vector2.Zero;
            return;
        }

        if (!TryComp<CameraFollowComponent>(player.Value, out var followComponent))
        {
            _velocities.Remove(player.Value);
            _lastAimShake = Vector2.Zero;
            return;
        }
        if (!followComponent.Enabled || !TryComp<EyeComponent>(player.Value, out var eyeComponent))
        {
            _velocities.Remove(player.Value);
            _lastAimShake = Vector2.Zero;
            return;
        }

        // Get player map position
        var xform = Transform(player.Value);
        var playerPos = _transform.GetMapCoordinates(xform).Position;

        // Get cursor position on the map. The view matrix already folds the current eye
        // offset into this result, so subtract it back out: the aim vector then measures
        // the cursor relative to the character independently of where the camera drifted,
        // which keeps the aim point fixed while the player holds the mouse still.
        var cursorWorld = _manager.PixelToMap(_input.MouseScreenPosition).Position;
        if (cursorWorld == Vector2.Zero)
        {
            _lastAimShake = Vector2.Zero;
            return;
        }

        // The rendered eye still carries last frame's visual shake, so PixelToMap above
        // returns the cursor shifted by exactly that shake. Undo it before anchoring the
        // aim line, otherwise the shake would feed back into the chase target.
        cursorWorld -= _lastAimShake;

        // Aim shake is a cosmetic Eye-only offset: damage+Strength drive its magnitude
        // and Psycho zeroes it. It never touches the networked eye/camera offsets.
        var shake = ComputeAimShake(player.Value, frameTime);

        var aimLine = cursorWorld - playerPos - eyeComponent.Offset;

        // Desired offset: stretch the view along the cursor->character line, scaled by the
        // aim range (weapon profile / Perception / scopes, networked in MaxDistance). The
        // camera never jumps past the character: short cursor distances yield small offsets.
        var max = followComponent.MaxDistance.X;
        var length = aimLine.Length();
        var desired = length < 0.01f ? Vector2.Zero : aimLine / length * MathF.Min(max, length);

        var remaining = desired - followComponent.Offset;
        var dist = remaining.Length();

        // Arrived: land exactly on the aim target and stop. The tremor is still applied
        // on top of the settled view so the shake keeps working while holding aim.
        if (dist < AimArrivalEpsilon)
        {
            _velocities.Remove(player.Value);
            var arrivedEye = _manager.CurrentEye;
            if (arrivedEye != null)
                arrivedEye.Offset = eyeComponent.Offset - followComponent.Offset + desired + shake;
            if (dist > 0f && (desired - followComponent.Offset).LengthSquared() > 1e-6f)
                RaisePredictiveEvent(new ChangeCamOffsetEvent { Offset = desired });
            return;
        }

        var dir = remaining / dist;

        // Ease-out: once within a fraction of the range the target speed is tapered off
        // with a smoothstep, so the camera glides to a stop instead of slamming into the
        // aim position.
        var stopDist = MathF.Max(0.75f, max * 0.35f);
        var targetSpeed = AimMaxMoveSpeed * SmoothStep(0f, stopDist, dist);

        // Ease-in: the velocity itself ramps exponentially toward the target speed, so
        // entering aim mode accelerates smoothly from a standstill and the deceleration
        // above is smooth as well. The BackStrength tuning (aimCameraChaseRate) controls
        // how quickly the speed ramps.
        var blend = 1f - MathF.Exp(-frameTime * followComponent.BackStrength);
        var velocity = Vector2.Lerp(GetVelocity(player.Value), dir * targetSpeed, blend);
        _velocities[player.Value] = velocity;

        var offset = followComponent.Offset + velocity * frameTime;

        // Guard against crossing the aim target: snap onto it instead.
        if (Vector2.Dot(offset - desired, remaining) >= 0f)
            offset = desired;

        // Apply the aim offset to the rendered eye every frame so the view glides
        // smoothly at render rate instead of stepping at the 30Hz tick rate of the
        // shared recoil system - stepping reads as jitter while the character moves.
        // Only the visual Eye is touched (via the render manager, which the engine lets
        // any code write without dirtying the networked component): the shared tick
        // system keeps re-applying the networked Offset, this override is cosmetic.
        var currentEye = _manager.CurrentEye;
        if (currentEye != null)
            currentEye.Offset = eyeComponent.Offset - followComponent.Offset + offset + shake;

        if ((offset - followComponent.Offset).LengthSquared() < 1e-6f)
            return;

        RaisePredictiveEvent(new ChangeCamOffsetEvent
        {
            Offset = offset
        });
    }

    /// <summary>
    /// Computes the aim-mode camera shake for this frame. The shake ramps in with
    /// damage (starts at <see cref="SpecialTuningPrototype.AimCameraShakeDamageLowRatio"/>
    /// of the death threshold, saturates at the high ratio), is damped linearly by
    /// Strength (STR 10 = none, STR 1 = full) and is suppressed entirely while Psycho's
    /// dose timeline is active.
    /// </summary>
    private Vector2 ComputeAimShake(EntityUid player, float frameTime)
    {
        // Psycho's timeline lives for the whole dose and cancels the tremor.
        if (TryComp<DrugSpecialTimelineComponent>(player, out var timeline)
            && timeline.ReagentId == PsychoReagentId)
        {
            _lastAimShake = Vector2.Zero;
            return Vector2.Zero;
        }

        var tuning = _special.GetTuning();

        var damageRatio = 0f;
        if (TryComp<DamageableComponent>(player, out var damageable)
            && _mobThreshold.TryGetDeadThreshold(player, out var deadThreshold))
        {
            var dead = deadThreshold.Value;
            if (dead > FixedPoint2.Zero && damageable.TotalDamage > FixedPoint2.Zero)
                damageRatio = (float)(damageable.TotalDamage / dead);
        }

        var damageFactor = SmoothStep(
            tuning.AimCameraShakeDamageLowRatio,
            tuning.AimCameraShakeDamageHighRatio,
            damageRatio);

        // GetEffective is already clamped to 1..10 (SpecialProfile).
        var strength = _special.GetEffective(player, SpecialStat.Strength);
        var strengthDampening = (SpecialProfile.Maximum - strength) / 9f;

        var shake = ShakeWobble(frameTime)
                    * tuning.AimCameraShakeMaxAmplitude
                    * damageFactor
                    * strengthDampening;

        _lastAimShake = shake;
        return shake;
    }

    /// <summary>
    /// Organic pseudo-noise direction vector inside the unit square, built from a sum of
    /// incommensurate sines so the jitter reads as uneven, non-repeating tremor.
    /// </summary>
    private Vector2 ShakeWobble(float frameTime)
    {
        _shakeElapsed += frameTime;
        var t = _shakeElapsed;
        return new Vector2(
            MathF.Sin(t * 12f) * 0.55f + MathF.Sin(t * 23f) * 0.25f + MathF.Sin(t * 5.3f) * 0.2f,
            MathF.Sin(t * 9.7f) * 0.55f + MathF.Sin(t * 17.3f) * 0.25f + MathF.Sin(t * 7.1f) * 0.2f);
    }

    private Vector2 GetVelocity(EntityUid uid)
    {
        return _velocities.TryGetValue(uid, out var velocity) ? velocity : Vector2.Zero;
    }

    /// <summary>
    /// Smoothstep (hermite) interpolation of <paramref name="x"/> between
    /// <paramref name="edge0"/> and <paramref name="edge1"/>.
    /// </summary>
    private static float SmoothStep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}