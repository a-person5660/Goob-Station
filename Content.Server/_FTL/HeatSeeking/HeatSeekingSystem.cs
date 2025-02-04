using System.Linq;
using System.Numerics;
using Content.Shared.Interaction;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Random;

namespace Content.Server._FTL.HeatSeeking;

/// <summary>
/// This handles...
/// </summary>
public sealed class HeatSeekingSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    Angle oldAngle;
    Angle deltaAngle;
    float oldDistance;
    float deltaDistance;
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeatSeekingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.TargetEntity.HasValue)
            {

                var EntXform = Transform(comp.TargetEntity.Value);

                var distance = Vector2.Distance(
                    _transform.ToMapCoordinates(xform.Coordinates).Position,
                    _transform.ToMapCoordinates(EntXform.Coordinates).Position
                ); // current distance from target

                var angle = (
                    _transform.ToMapCoordinates(EntXform.Coordinates).Position -
                    _transform.ToMapCoordinates(xform.Coordinates).Position
                ).ToWorldAngle(); // current angle towards target

                deltaDistance = oldDistance - distance; // change in distance since last frame

                deltaAngle = angle - oldAngle; // change in angle since last frame

                Angle PN = comp.Gain * deltaDistance * deltaAngle; // calculate the optimal angle to face to hit target

                Console.WriteLine($"distance: {distance}");
                Console.WriteLine($"oldDistance: {oldDistance}");
                Console.WriteLine($"deltaDistance: {deltaDistance}");
                Console.WriteLine($"angle: {angle}");
                Console.WriteLine($"oldAngle: {oldAngle}");
                Console.WriteLine($"deltaAngle: {deltaAngle}");
                Console.WriteLine($"PN: {PN}");
                Console.WriteLine($"target: {comp.TargetEntity}");

                _transform.SetLocalRotationNoLerp(uid, angle + PN, xform);

                // if (!_rotate.TryRotateTo(uid, PN, frameTime, comp.WeaponArc, comp.RotationSpeed?.Theta ?? double.MaxValue, xform))
                // {
                //     //continue;
                // }

                _physics.SetLinearVelocity(uid, xform.worldRot * comp.Acceleration);
                //_physics.ApplyForce(uid, xform.LocalRotation.RotateVec(new Vector2(0, -1 * comp.Acceleration)));

                oldDistance = distance;
                oldAngle = angle;
                return;
            }
            else
            {
                GetNewTarget(uid, comp, xform);
            }
        }
    }

        public void GetNewTarget(EntityUid uid, HeatSeekingComponent component, TransformComponent transform)
    {
        var ray = new CollisionRay(_transform.GetMapCoordinates(uid, transform).Position,
            transform.LocalRotation.ToWorldVec(),
            (int) (CollisionGroup.Impassable | CollisionGroup.BulletImpassable));

        var results = _physics.IntersectRay(transform.MapID, ray, component.DefaultSeekingRange, uid).ToList();

        if (results.Count <= 0)
            return; // nothing to heatseek ykwim

        if (component is { LockedIn: true, TargetEntity: not null })
            return; // Don't reassign target entity if we have one AND we have the LockedIn property

        if (TryComp<ProjectileComponent>(uid, out var projectile)
            && TryComp<TransformComponent>(projectile.Shooter, out var shooterTransform))
        {
            var shooterGridUid = shooterTransform.GridUid;
            for (int i = 0; i < results.Count; i++)
            {
                var hitEntity = results[i].HitEntity;
                if (TryComp<TransformComponent>(hitEntity, out var hitTransform))
                {
                    if (shooterGridUid == hitTransform.GridUid)
                    {
                        continue;
                    }

                    if (hitEntity == uid)
                    {
                        continue;
                    }
                    component.TargetEntity = hitTransform.GridUid;
                    //if(component.TargetEntity is not null)
                    //    Log.Error($"Locked on {MetaData(component.TargetEntity.Value).EntityName}");
                    break;
                }
            }
        }
        else
        {
            component.TargetEntity = results[0].HitEntity;
        }
    }
}

//rotationSpeed
//acceleration
//seekRange
