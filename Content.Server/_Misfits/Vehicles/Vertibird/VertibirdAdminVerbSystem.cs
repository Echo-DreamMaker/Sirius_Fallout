// #Misfits Add - Admin debug toggles for vertibirds, under the right-click Tricks menu.
using Content.Server.Administration.Managers;
using Content.Shared._Misfits.Vehicles.Vertibird;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.Vehicles.Vertibird;

/// <summary>
/// Adds the vertibird debug toggles to the Tricks verb category. Testing a craft
/// otherwise means waiting out a 55 second startup and hauling fuel and gun belts
/// to the pad every time.
/// </summary>
public sealed class VertibirdAdminVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VertibirdComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<VertibirdComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) ||
            !_admin.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
        {
            return;
        }

        var vertibird = ent.Comp;

        args.Verbs.Add(MakeToggle(
            "Vertibird Infinite Fuel",
            "infinite_battery.png",
            vertibird.DebugInfiniteFuel,
            () => vertibird.DebugInfiniteFuel = !vertibird.DebugInfiniteFuel));

        args.Verbs.Add(MakeToggle(
            "Vertibird Instant Takeoff/Landing",
            "super_speed.png",
            vertibird.DebugInstantFlight,
            () => vertibird.DebugInstantFlight = !vertibird.DebugInstantFlight));

        args.Verbs.Add(MakeToggle(
            "Vertibird Infinite Ammo",
            "fill-stack.png",
            vertibird.DebugInfiniteAmmo,
            () => vertibird.DebugInfiniteAmmo = !vertibird.DebugInfiniteAmmo));

        args.Verbs.Add(MakeToggle(
            "Vertibird Stay Up Without Pilot",
            "pause.png",
            vertibird.DebugIgnorePilotLoss,
            () => vertibird.DebugIgnorePilotLoss = !vertibird.DebugIgnorePilotLoss));
    }

    /// <summary>
    /// The Tricks category is icons-only, so the label is never drawn: the icon is the
    /// whole button and Message is what the hover tooltip reads from. A verb without
    /// both is an unlabelled blank square in the grid.
    /// </summary>
    private static Verb MakeToggle(string label, string icon, bool enabled, Action act)
    {
        var text = $"{label} ({(enabled ? "on" : "off")})";

        return new Verb
        {
            Text = text,
            Message = text,
            Icon = new SpriteSpecifier.Texture(new ResPath($"/Textures/Interface/AdminActions/{icon}")),
            Category = VerbCategory.Tricks,
            Act = act,
            Impact = LogImpact.Medium,
        };
    }
}
