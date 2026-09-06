// Origin: ColonialMarinesUniverse (AU-14) — Multi Z system
//   PR #1058 "Multi Z addition" & #1119 "Multi z fixes" by TheHellFireo
//   Based on Crystall Edge (crystallpunk-14) Multi-Z system
// Ported to misfits-14 _MultiZ/
// #Cythisiax Ported — Multi-Z level support for misfits-14

using System.Numerics;
using Content.Shared._MultiZ;
using Content.Shared._MultiZ.Core;
using Content.Shared._MultiZ.Core.Components;
using Content.Shared._MultiZ.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Maths;

namespace Content.Client._MultiZ.Core;

public sealed class MZVisibleEntityOverlay : Overlay
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public MZVisibleEntityOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_cfg.GetCVar(MZCVars.RenderEnabled))
            return;

        var player = _player.LocalSession?.AttachedEntity;
        if (player == null)
            return;

        var xform = _entMan.GetComponent<TransformComponent>(player.Value);
        if (xform.MapUid is not { } mapUid)
            return;

        if (!_entMan.TryGetComponent<MZMapComponent>(mapUid, out var zMap))
            return;

        var zSystem = _entMan.System<MZSharedSystem>();
        var handle = args.ScreenHandle;

        var hasAbove = zSystem.TryMapUp((mapUid, zMap), out _);
        var hasBelow = zSystem.TryMapDown((mapUid, zMap), out _);

        if (!hasAbove && !hasBelow)
            return;

        var screenSize = args.ViewportBounds.Size;
        var barHeight = 24f;

        if (hasAbove)
            handle.DrawRect(new UIBox2(0, 0, screenSize.X, barHeight), Color.Green.WithAlpha(0.4f));

        if (hasBelow)
            handle.DrawRect(new UIBox2(0, screenSize.Y - barHeight, screenSize.X, screenSize.Y), Color.Blue.WithAlpha(0.4f));
    }
}
