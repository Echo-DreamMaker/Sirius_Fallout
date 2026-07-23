using Content.Server.Power.Components;
using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Server._Nuclear14.AutodocSirius;

public sealed partial class SiriusAutodocSystem : SharedSiriusAutodocSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiriusAutodocComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<SiriusAutodocComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<SiriusAutodocComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SiriusAutodocComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
        SubscribeLocalEvent<SiriusAutodocComponent, BoundUIClosedEvent>(OnBoundUIClosed);
        SubscribeLocalEvent<SiriusAutodocComponent, AutodocUiButtonPressedMessage>(OnUiButtonPressed);
        SubscribeLocalEvent<SiriusAutodocComponent, AutodocUiToggleOpenMessage>(OnToggleOpenMessage);
    }
}
