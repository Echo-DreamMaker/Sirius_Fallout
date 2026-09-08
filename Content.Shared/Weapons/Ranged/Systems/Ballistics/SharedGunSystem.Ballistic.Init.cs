
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;
/// <summary>
/// Init logic
/// </summary>
public abstract partial class SharedGunSystem
{

    /// <summary>
    /// Corrects bad yaml and sets appearance data on Map init
    /// </summary>
    /// <remarks>
    /// visualizer handled by system described in <see cref="UpdateBallisticAppearance"/>
    /// So shouldnt have something like genericvisualizer in yaml
    /// </remarks>
    private void OnBallisticInit(EntityUid giverUid, BallisticAmmoProviderComponent comp, ComponentInit args)
    {
#if !RELEASE
        DebugInfo(giverUid, comp);
#endif
        EnsureCorrect(giverUid, comp);
        UpdateBallisticAppearance(giverUid, comp);
        Dirty(giverUid, comp);
    }
    /// Same as above ^^
    private void OnBallisticMapInit(EntityUid giverUid, BallisticAmmoProviderComponent comp, MapInitEvent args) => OnBallisticInit(giverUid, comp, new ComponentInit());


}
