using System;
using Content.Shared._N14.Vehicles;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._N14.Vehicles;

// #Sirius Add
/// <summary>
/// Hybrid car sprite rotation: smooth rotation with 4-directional sprite states.
/// NW/NE use North state, SW/SE use South state; sprite rotation offset keeps facing continuous.
/// </summary>
public sealed class CarSpriteRotationSystem : EntitySystem
{
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CarComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var sprite, out var xform))
        {
            if (xform.MapID == MapId.Nullspace)
                continue;

            var worldRot = xform.WorldRotation;
            var dir8 = worldRot.GetDir();

            Direction stateDir;
            Angle canonical;

            switch (dir8)
            {
                case Direction.North:
                case Direction.NorthEast:
                case Direction.NorthWest:
                    stateDir = Direction.North;
                    canonical = Direction.North.ToAngle();
                    break;
                case Direction.South:
                case Direction.SouthEast:
                case Direction.SouthWest:
                    stateDir = Direction.South;
                    canonical = Direction.South.ToAngle();
                    break;
                case Direction.East:
                    stateDir = Direction.East;
                    canonical = Direction.East.ToAngle();
                    break;
                default:
                    stateDir = Direction.West;
                    canonical = Direction.West.ToAngle();
                    break;
            }

            var offset = (worldRot - canonical).Reduced();

            sprite.EnableDirectionOverride = true;
            sprite.DirectionOverride = stateDir;
            sprite.Rotation = offset;
        }
    }
}
