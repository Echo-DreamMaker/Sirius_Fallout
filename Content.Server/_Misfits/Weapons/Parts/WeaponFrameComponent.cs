// #Misfits Add - Marker for a modular weapon frame (and for a half-assembled
// weapon still sitting on one), with the node the frame should be assembled
// towards.
//
// WeaponFrameSystem needs to hook GetVerbsEvent<Verb> and ExaminedEvent for
// these entities, but it cannot subscribe against ConstructionComponent:
// RobustToolbox allows exactly one subscription per (component, event) pair
// and ConstructionSystem.Guided.cs already owns both of those for
// ConstructionComponent ("Duplicate Subscriptions for comp=..., event=...").
// Subscribing against a component of our own sidesteps that without having to
// touch the vanilla system.
namespace Content.Server._Misfits.Weapons.Parts;

[RegisterComponent]
public sealed partial class WeaponFrameComponent : Component
{
    /// <summary>
    ///     The construction graph node this frame is being assembled towards
    ///     ("done_&lt;weapon&gt;"). The "begin assembly" verb points the
    ///     pathfinding at it, which is what makes the per-step instructions
    ///     appear.
    /// </summary>
    [DataField]
    public string AssemblyTarget = string.Empty;
}