using Content.Shared.SprayPainter;
using Content.Shared.Decals;
using Content.Shared.SprayPainter.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using System.Linq;
using Robust.Shared.Graphics;

namespace Content.Client.SprayPainter;

public sealed class SprayPainterSystem : SharedSprayPainterSystem
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public List<SprayPainterEntry> Entries { get; private set; } = new();
    public List<SprayPainterDecalEntry> Decals { get; private set; } = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SprayPainterComponent, AfterAutoHandleStateEvent>(OnStateUpdated);
    }

    private void OnStateUpdated(Entity<SprayPainterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_ui.TryGetOpenUi(ent.Owner, SprayPainterUiKey.Key, out var bui))
            bui.Update();
    }

    protected override void CacheStyles()
    {
        base.CacheStyles();

        Entries.Clear();
        foreach (var style in Styles)
        {
            var name = style.Name;
            string? iconPath = Groups
              .FindAll(x => x.StylePaths.ContainsKey(name))?
              .MaxBy(x => x.IconPriority)?.StylePaths[name];
            if (iconPath == null)
            {
                Entries.Add(new SprayPainterEntry(name, null));
                continue;
            }

            RSIResource doorRsi = _resourceCache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / new ResPath(iconPath));
            if (!doorRsi.RSI.TryGetState("closed", out var icon))
            {
                Entries.Add(new SprayPainterEntry(name, null));
                continue;
            }

            Entries.Add(new SprayPainterEntry(name, icon.Frame0));
        }

        Decals.Clear();
        foreach (var decal in Proto.EnumeratePrototypes<DecalPrototype>().OrderBy(decal => decal.ID))
        {
            if ((!decal.Tags.Contains("station") && !decal.Tags.Contains("markings")) ||
                decal.Tags.Contains("dirty"))
                continue;

            Decals.Add(new SprayPainterDecalEntry(decal.ID, decal.Sprite));
        }
    }
}

public sealed record SprayPainterDecalEntry(string Name, SpriteSpecifier Sprite);

public sealed class SprayPainterEntry
{
    public string Name;
    public Texture? Icon;

    public SprayPainterEntry(string name, Texture? icon)
    {
        Name = name;
        Icon = icon;
    }
}
