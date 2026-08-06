using Content.Server._Misfits.Speech.EntitySystems;

namespace Content.Server._Misfits.Speech.Components;

/// <summary>
///     Ze German accent, ja? Word swaps plus phonetic replacements.
/// </summary>
[RegisterComponent]
[Access(typeof(GermanAccentSystem))]
public sealed partial class GermanAccentComponent : Component
{
}
