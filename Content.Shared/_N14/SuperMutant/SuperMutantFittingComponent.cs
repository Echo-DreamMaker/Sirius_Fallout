namespace Content.Shared._N14.SuperMutant;

/// <summary>
/// Placed on a firearm to mark it as "fitting" for a super mutant. A fitting two-handed
/// firearm can be wielded by a super mutant in a single hand with no accuracy penalty.
/// Fitting firearms that are not two-handed (no WieldableComponent, e.g. a heavy minigun)
/// are simply usable as-is by a super mutant. Non-fitting firearms are handled as two-handed
/// (big spread penalty) or one-handed (cannot fire at all), see <see cref="SharedSuperMutantSystem"/>.
/// By default heavy and energy weapons carry this marker.
/// </summary>
[RegisterComponent]
public sealed partial class SuperMutantFittingComponent : Component
{
}
