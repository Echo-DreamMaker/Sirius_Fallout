// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Forensics;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Content.Shared._Misfits.Genetics.Abilities;

namespace Content.Server._Misfits.Genetics.Abilities;

/// <summary>
/// "Sniff the Air": once you've tracked someone's scent, this points you at them.
/// Without it the mutation could only tell you whether a thing you were already
/// examining smelled right, which was useless for actually following a trail.
/// </summary>
public sealed class ScentSenseActionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScentSenseActionComponent, ScentSenseActionEvent>(OnSniff);
    }

    private void OnSniff(Entity<ScentSenseActionComponent> ent, ref ScentSenseActionEvent args)
    {
        args.Handled = true;
        var user = args.Performer;

        if (!TryComp<ScentTrackerComponent>(user, out var tracker) || tracker.Scent == string.Empty)
        {
            _popup.PopupEntity(Loc.GetString("scent-sense-no-target"), user, user);
            return;
        }

        var userPos = _transform.GetMapCoordinates(user).Position;
        EntityUid? closest = null;
        var closestDist = float.MaxValue;

        foreach (var found in _lookup.GetEntitiesInRange(user, ent.Comp.Range))
        {
            if (found == user ||
                !TryComp<ForensicsComponent>(found, out var forensics) ||
                forensics.Scent != tracker.Scent)
                continue;

            var dist = (_transform.GetMapCoordinates(found).Position - userPos).Length();
            if (dist >= closestDist)
                continue;

            closest = found;
            closestDist = dist;
        }

        if (closest is not {} scented)
        {
            _popup.PopupEntity(Loc.GetString("scent-sense-cold"), user, user);
            return;
        }

        var direction = Direction(_transform.GetMapCoordinates(scented).Position - userPos);
        var strength = closestDist switch
        {
            < 3f => "scent-sense-overwhelming",
            < 8f => "scent-sense-strong",
            < 16f => "scent-sense-faint",
            _ => "scent-sense-distant"
        };

        _popup.PopupEntity(
            Loc.GetString(strength, ("direction", Loc.GetString(direction)), ("target", Identity.Name(scented, EntityManager))),
            user,
            user,
            PopupType.Medium);
    }

    /// <summary>
    /// Turns an offset into one of the eight compass directions.
    /// </summary>
    private static string Direction(Vector2 offset)
    {
        var angle = MathF.Atan2(offset.Y, offset.X) * (180f / MathF.PI);
        if (angle < 0)
            angle += 360f;

        return angle switch
        {
            < 22.5f or >= 337.5f => "scent-direction-east",
            < 67.5f => "scent-direction-northeast",
            < 112.5f => "scent-direction-north",
            < 157.5f => "scent-direction-northwest",
            < 202.5f => "scent-direction-west",
            < 247.5f => "scent-direction-southwest",
            < 292.5f => "scent-direction-south",
            _ => "scent-direction-southeast"
        };
    }
}
