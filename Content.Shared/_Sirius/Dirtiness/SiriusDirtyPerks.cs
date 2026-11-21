namespace Content.Shared._Sirius.Dirtiness;

// #Sirius Add: marker components attached by the cleanliness perk prototypes.
// TraitAddComponent skips a component that already exists, so every perk gets its own type.

/// <summary>
/// #Sirius Add: doubles the mood received from smells (both own and surrounding).
/// </summary>
[RegisterComponent]
public sealed partial class SiriusPerkKeenSmellComponent : Component
{
}

/// <summary>
/// #Sirius Add: blocks smell mood entirely, except for the Animal state.
/// </summary>
[RegisterComponent]
public sealed partial class SiriusPerkChronicRhinitisComponent : Component
{
}

/// <summary>
/// #Sirius Add: cleanliness decays twice as slowly.
/// </summary>
[RegisterComponent]
public sealed partial class SiriusPerkCleanFreakComponent : Component
{
}

/// <summary>
/// #Sirius Add: cleanliness decays three times as fast.
/// </summary>
[RegisterComponent]
public sealed partial class SiriusPerkStinkyComponent : Component
{
}
