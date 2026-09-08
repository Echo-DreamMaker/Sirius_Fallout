using Robust.Shared.GameObjects;

namespace Content.Shared._Misfits.MaterialExtractor;

/// <summary>
/// Marks the seismic extractor and its destroyed casing as a permanent
/// tactical-map landmark for the duration of the round.
/// </summary>
[RegisterComponent]
public sealed partial class MaterialExtractorLandmarkComponent : Component;
