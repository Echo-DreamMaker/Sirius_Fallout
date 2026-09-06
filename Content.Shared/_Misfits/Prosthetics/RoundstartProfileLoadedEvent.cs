// #Misfits Add - Distinct event type so roundstart prosthetics can react to profile load without
// duplicating the Shitmed (BodyComponent, ProfileLoadFinishedEvent) directed subscription, which
// crashes the event bus with "Duplicate Subscriptions".

namespace Content.Shared._Misfits.Prosthetics;

/// <summary>
/// Raised on a humanoid body after its profile has finished loading and its body parts have been
/// finalized. Mirrors <c>ProfileLoadFinishedEvent</c> but is a distinct event type so a server
/// system can subscribe without tripping the event bus duplicate-subscription check.
/// </summary>
public sealed class RoundstartProfileLoadedEvent : EntityEventArgs { }
