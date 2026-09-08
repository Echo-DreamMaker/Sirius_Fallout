using Content.Shared._Misfits.Holotape;
using Content.Shared._Misfits.Overwatch;
using Content.Client.Eye;
using Content.Client._Misfits.Overwatch;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Graphics;

// #Misfits Add - Client BUI for the holotape/terminal viewer.
// Creates the green-on-black terminal window and receives content state from server.
// #Misfits Add - Wires up notes submit/delete/request events for the notebook tab.
// #Misfits Add - Wires up link port invoke events for the LINKS tab.

namespace Content.Client._Misfits.Holotape;

/// <summary>
/// Bound user interface that bridges server state to the HolotapeWindow display.
/// Sends note submit/delete/request messages and link port invoke messages to the server.
/// </summary>
[UsedImplicitly]
public sealed class HolotapeBoundUserInterface : BoundUserInterface
{
    private readonly EyeLerpingSystem _eyeLerpingSystem;
    private HolotapeWindow? _window;
    private EntityUid? _currentOverwatchTarget;

    public HolotapeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeLerpingSystem = EntMan.System<EyeLerpingSystem>();
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<HolotapeWindow>();

        // Wire up notebook events to send BUI messages to the server
        _window.OnSubmitNote += (author, text) =>
        {
            SendMessage(new SubmitTerminalNoteMessage(author, text));
        };

        _window.OnDeleteNote += noteId =>
        {
            SendMessage(new DeleteTerminalNoteMessage(noteId));
        };

        _window.OnRequestNotes += () =>
        {
            SendMessage(new RequestTerminalNotesMessage());
        };

        // #Misfits Add - Wire link port invocation to send BUI message to server
        _window.OnInvokeLinkPort += portId =>
        {
            SendMessage(new InvokeTerminalLinkPortMessage(portId));
        };

        _window.OnOverwatchAction += (type, targetNumber) =>
        {
            SendMessage(new OverwatchConsoleMessage(type, targetNumber));
        };

        // #Misfits Add - Wire database events to send BUI messages to server.
        _window.OnRequestDatabase += () =>
            SendMessage(new RequestDatabaseStateMessage());
        _window.OnOpenDatabaseDocument += docId =>
            SendMessage(new OpenDatabaseDocumentMessage(docId));
        _window.OnCreateDatabaseFolder += (parentId, name, markAdmin) =>
            SendMessage(new CreateDatabaseFolderMessage(parentId, name, markAdmin));
        _window.OnCreateDatabaseDocument += (folderId, subfolderId, title, body, markAdmin) =>
            SendMessage(new CreateDatabaseDocumentMessage(folderId, subfolderId, title, body, markAdmin));
        _window.OnEditDatabaseDocument += (docId, body) =>
            SendMessage(new EditDatabaseDocumentMessage(docId, body));
        _window.OnRenameDatabaseEntry += (name, folderId, subParent, subId, docId) =>
            SendMessage(new RenameDatabaseEntryMessage(name, folderId, subParent, subId, docId));
        _window.OnDeleteDatabaseFolder += (folderId, subfolderId) =>
            SendMessage(new DeleteDatabaseFolderMessage(folderId, subfolderId));
        _window.OnDeleteDatabaseDocument += docId =>
            SendMessage(new DeleteDatabaseDocumentMessage(docId));
        _window.OnRollbackDatabaseDocument += (docId, rev) =>
            SendMessage(new RollbackDatabaseDocumentMessage(docId, rev));
        _window.OnRestoreDatabaseEntry += (folderId, subParent, subId, docId) =>
            SendMessage(new RestoreDatabaseEntryMessage(folderId, subParent, subId, docId));
        // #Misfits Add - Forward Database document export requests to the server.
        _window.OnExportDatabaseDocument += docId =>
            SendMessage(new ExportDatabaseDocumentMessage(docId));
        // #Misfits Add - Forward permanent delete requests to the server.
        _window.OnPermanentDeleteDatabaseEntry += (folderId, subParent, subId, docId) =>
            SendMessage(new PermanentDeleteDatabaseEntryMessage(folderId, subParent, subId, docId));
        // #Misfits Add - Forward Leadership "move" (tidying) requests to the server.
        _window.OnMoveDatabaseEntry += (folderId, subParent, subId, docId, targetFolderId, targetSubfolderId) =>
            SendMessage(new MoveDatabaseEntryMessage(folderId, subParent, subId, docId, targetFolderId, targetSubfolderId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null || state is not HolotapeBoundUserInterfaceState cast)
            return;

        // Update the DATA tab content
        // #Misfits Add - Set header/title based on whether holotape item or built-in terminal
        _window.SetHolotapeMode(cast.IsHolotapeItem);
        _window.UpdateContent(cast.Title, cast.Content);

        // Update the NOTES tab (null notes means no notebook on this terminal)
        _window.UpdateNotes(cast.Notes, cast.ViewerUserId);

        // #Misfits Add - Update the LINKS tab (shows device link port buttons when terminal has links)
        _window.UpdateLinks(cast.HasLinkSource, cast.LinkPorts);

        // #Misfits Add - Update the DATABASE tab (faction-shared knowledge base)
        _window.UpdateDatabase(cast.Database);

        var watch = EntMan.System<OverwatchConsoleSystem>().GetLocalWatch();
        var eye = ResolveOverwatchEye(watch);
        _window.UpdateOverwatch(cast.Overwatch, watch, eye);
    }

    private IEye? ResolveOverwatchEye(OverwatchWatchingComponent? watch)
    {
        var target = watch?.Watching;
        if (target == null)
        {
            ClearOverwatchTarget();
            return null;
        }

        if (_currentOverwatchTarget == null)
        {
            _eyeLerpingSystem.AddEye(target.Value);
            _currentOverwatchTarget = target;
        }
        else if (_currentOverwatchTarget != target)
        {
            _eyeLerpingSystem.RemoveEye(_currentOverwatchTarget.Value);
            _eyeLerpingSystem.AddEye(target.Value);
            _currentOverwatchTarget = target;
        }

        return EntMan.TryGetComponent<EyeComponent>(target, out var eye) ? eye.Eye : null;
    }

    private void ClearOverwatchTarget()
    {
        if (_currentOverwatchTarget == null)
            return;

        _eyeLerpingSystem.RemoveEye(_currentOverwatchTarget.Value);
        _currentOverwatchTarget = null;
    }

    protected override void Dispose(bool disposing)
    {
        ClearOverwatchTarget();

        base.Dispose(disposing);
    }
}
