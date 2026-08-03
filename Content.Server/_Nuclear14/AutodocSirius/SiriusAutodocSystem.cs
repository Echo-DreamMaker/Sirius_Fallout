using Content.Shared._Nuclear14.AutodocSirius;
using Content.Shared.Power;
using Robust.Shared.Containers;

namespace Content.Server._Nuclear14.AutodocSirius;

public sealed partial class SiriusAutodocSystem : SharedSiriusAutodocSystem
{
    private SiriusAutodocSurgerySystem? _surgerySystem;
    public override void Initialize()
    {
        base.Initialize();
        _surgerySystem = EntityManager.System<SiriusAutodocSurgerySystem>();
        SubscribeLocalEvent<SiriusAutodocComponent, EntInsertedIntoContainerMessage>(OnContainerInserted);
        SubscribeLocalEvent<SiriusAutodocComponent, EntRemovedFromContainerMessage>(OnContainerRemoved);
        SubscribeLocalEvent<SiriusAutodocComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<SiriusAutodocComponent, BoundUIOpenedEvent>(OnBoundUIOpened);
        SubscribeLocalEvent<SiriusAutodocComponent, BoundUIClosedEvent>(OnBoundUIClosed);
        SubscribeLocalEvent<SiriusAutodocComponent, AutodocUiButtonPressedMessage>(OnUiButtonPressed);
        SubscribeLocalEvent<SiriusAutodocComponent, AutodocUiToggleOpenMessage>(OnToggleOpenMessage);
        SubscribeLocalEvent<SiriusAutodocComponent, AutodocSurgeryPartSelectedMessage>(OnSurgeryPartSelected);
        SubscribeLocalEvent<SiriusAutodocComponent, AutodocSurgeryOperationMessage>(OnSurgeryOperationSelected);
        SubscribeLocalEvent<AutodocSurgeryOperationDoAfterEvent>(OnSurgeryOperationDoAfter);
    }
}
