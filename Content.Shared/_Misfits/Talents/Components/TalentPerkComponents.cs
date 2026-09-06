// #Misfits Add - Talent tree perk marker components.
// One tiny marker component per talent-tree perk, grouped in a single file to avoid a
// file-per-perk. Each is granted on spawn by the matching trait in
// Resources/Prototypes/_Misfits/Traits/talents.yml via the existing TraitAddComponent
// function (same pattern as RidingPerkComponent). Gameplay systems check
// HasComp<TraitXComponent>() to apply each perk's effect. Networked so client-side
// systems (e.g. gun spread prediction) see the markers too.

using Robust.Shared.GameStates;

namespace Content.Shared._Misfits.Talents.Components;

// --- PHYSICAL / Combat ---
[RegisterComponent, NetworkedComponent] public sealed partial class TraitSwiftLearnerComponent : Component { }
[RegisterComponent, NetworkedComponent] public sealed partial class TraitEducatedComponent : Component { }
[RegisterComponent, NetworkedComponent] public sealed partial class TraitScroungerComponent : Component { }
[RegisterComponent, NetworkedComponent] public sealed partial class TraitJunkerFinderComponent : Component { }
[RegisterComponent, NetworkedComponent] public sealed partial class TraitNerdRageComponent : Component { }
[RegisterComponent, NetworkedComponent] public sealed partial class TraitFortunesFavorComponent : Component { }
