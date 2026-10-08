using Content.Shared._Sirius.Books;
using Content.Shared.Interaction;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;

namespace Content.Client._Sirius.Books;

public sealed class HyperlinkBookSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IUriOpener _uriOpener = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HyperlinkBookComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnActivateInWorld(EntityUid uid, HyperlinkBookComponent component, ActivateInWorldEvent args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (string.IsNullOrWhiteSpace(component.Url))
            return;

        _uriOpener.OpenUri(component.Url);
        args.Handled = true;
    }
}
