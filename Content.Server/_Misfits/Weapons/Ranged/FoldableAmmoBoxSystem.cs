// #Misfits Change /Add/ - Empty cardboard ammo boxes can be folded flat to refund their cardboard sheet.
using Content.Server.Materials;
using Content.Shared._Misfits.Weapons.Ranged;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.Weapons.Ranged;

public sealed class FoldableAmmoBoxSystem : EntitySystem
{
    [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FoldableAmmoBoxComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<FoldableAmmoBoxComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        if (!TryComp<BallisticAmmoProviderComponent>(ent, out var ammoProvider) || ammoProvider.AmmoCount > 0)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("misfits-foldable-ammo-box-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
            Act = () => Fold(ent),
            Priority = 1,
        });
    }

    private void Fold(Entity<FoldableAmmoBoxComponent> ent)
    {
        if (Deleted(ent))
            return;

        if (TryComp<BallisticAmmoProviderComponent>(ent, out var ammoProvider) && ammoProvider.AmmoCount > 0)
            return;

        _materialStorage.SpawnMultipleFromMaterial(ent.Comp.RefundAmount, ent.Comp.RefundMaterial, Transform(ent).Coordinates);
        QueueDel(ent);
    }
}