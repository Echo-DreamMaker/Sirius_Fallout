using System;
using System.Collections.Generic;
using Content.Server.UserInterface;
using Content.Shared._Misfits.Holotape;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Access;
using Content.Shared.Mind; // #Misfits Add - SharedMindSystem for job lookup.
using Content.Shared.Roles.Jobs; // #Misfits Add - SharedJobSystem.MindHasJobWithId.
using Content.Shared.Hands.EntitySystems; // #Misfits Add - Pickup-or-drop spawned holotape on export.
using Content.Shared.Popups; // #Misfits Add - Popup feedback when export succeeds/fails.
using Content.Shared.Silicons.StationAi;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

// #Misfits Add - Server-side system for the faction-shared terminal database tab.
// Handles BUI messages (create/edit/delete/rollback/restore/open), validates
// access via AccessReaderSystem.FindAccessTags against the prototype's access
// tag lists, enforces hard caps (folders/subfolders/docs/title/body), and
// pushes updated state via the existing HolotapeUiKey.

namespace Content.Server._Misfits.Holotape;

public sealed class TerminalDatabaseSystem : EntitySystem
{
    [Dependency] private readonly TerminalDatabaseDataStore _dataStore = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    // #Misfits Add - Leadership/Admin tiers gate on job prototype id, not access tags,
    // so admin-spawned ID cards can't grant deletion power.
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _job = default!;
    // #Misfits Add - Used by the document [ EXPORT ] action to place the spawned holotape
    // into the actor's hands (or drop it at their feet) and surface a popup.
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private ISawmill _sawmill = default!;

    // ── Hard caps ────────────────────────────────────────────────────────────
    public const int MaxFoldersPerDatabase = 32;
    public const int MaxSubfoldersPerFolder = 32;
    public const int MaxDocumentsPerContainer = 256;
    public const int MaxTitleChars = 64;
    public const int MaxBodyChars = 16_000;
    private const string ZaxVaultDatabaseId = "vault-longwatch";

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("terminal.database");

        _dataStore.Initialize();

        SubscribeLocalEvent<TerminalDatabaseAccessComponent, RequestDatabaseStateMessage>(OnRequestState);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, OpenDatabaseDocumentMessage>(OnOpenDocument);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, CreateDatabaseFolderMessage>(OnCreateFolder);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, CreateDatabaseDocumentMessage>(OnCreateDocument);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, EditDatabaseDocumentMessage>(OnEditDocument);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, RenameDatabaseEntryMessage>(OnRenameEntry);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, DeleteDatabaseFolderMessage>(OnDeleteFolder);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, DeleteDatabaseDocumentMessage>(OnDeleteDocument);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, RollbackDatabaseDocumentMessage>(OnRollbackDocument);
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, RestoreDatabaseEntryMessage>(OnRestoreEntry);
        // #Misfits Add - Export current document body+title into a physical holotape entity.
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, ExportDatabaseDocumentMessage>(OnExportDocument);
        // #Misfits Add - Permanent delete: actually remove entries from the data store.
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, PermanentDeleteDatabaseEntryMessage>(OnPermanentDeleteEntry);
        // #Misfits Add - Leadership "move" (tidying): relocate entries into another container.
        SubscribeLocalEvent<TerminalDatabaseAccessComponent, MoveDatabaseEntryMessage>(OnMoveEntry);
    }

    // ── Public API: state assembly used by HolotapeSystem on UI open ─────────

    /// <summary>
    /// Builds the database-state bundle for a viewer. The terminal itself no longer
    /// pins a database id; instead, we scan all faction database prototypes and pick the
    /// first one whose AccessRead tags overlap with the viewer's ID access. If no
    /// database matches, returns a sentinel "NO ACCESS" state so the client can render
    /// a deny screen rather than hide the tab.
    /// </summary>
    public TerminalDatabaseState BuildState(EntityUid uid, EntityUid viewer, Guid? openDocumentId = null)
    {
        var proto = ResolveDatabaseForViewer(viewer);
        if (proto == null)
        {
            // NO ACCESS sentinel — empty folder list, all flags false.
            return new TerminalDatabaseState(
                databaseId: string.Empty,
                displayName: "NO ACCESS",
                accentColor: "#FF3333",
                canRead: false,
                canWrite: false,
                canLeadership: false,
                canAdmin: false,
                adminLabel: "ADMIN",
                folders: new List<DatabaseFolderSummary>(),
                openDocument: null);
        }

        var (canRead, canWrite, canLeadership, canAdmin) = ResolveAccess(viewer, proto);

        // Build folder summaries. Only the highest-rank tier sees deleted entries.
        var folders = _dataStore.GetFolders(proto.ID);
        var folderSummaries = new List<DatabaseFolderSummary>();
        foreach (var f in folders)
        {
            if (f.Deleted && !canAdmin)
                continue;

            var subSummaries = new List<DatabaseSubfolderSummary>();
            foreach (var s in f.Subfolders)
            {
                if (s.Deleted && !canAdmin)
                    continue;
                subSummaries.Add(new DatabaseSubfolderSummary(
                    s.SubfolderId, s.Name, s.Deleted, s.CreatedByUserIdGuid, s.CreatedByCharName, s.CreatedAt,
                    BuildDocSummaries(s.Documents, canAdmin)));
            }

            folderSummaries.Add(new DatabaseFolderSummary(
                f.FolderId, f.Name, f.Deleted, f.IsAdmin, f.CreatedByUserIdGuid, f.CreatedByCharName, f.CreatedAt,
                subSummaries,
                BuildDocSummaries(f.Documents, canAdmin)));
        }

        DatabaseDocumentView? openDoc = null;
        if (openDocumentId.HasValue)
            openDoc = BuildDocView(proto.ID, openDocumentId.Value, canAdmin);

        return new TerminalDatabaseState(
            proto.ID,
            proto.DisplayName,
            proto.AccentColor,
            canRead,
            canWrite,
            canLeadership,
            canAdmin,
            proto.AdminLabel,
            folderSummaries,
            openDoc);
    }

    /// <summary>
    /// Walks all faction database prototypes and returns the first one whose AccessRead
    /// matches the viewer's ID card. Iteration order is the prototype manager's natural
    /// order (effectively load order). If a viewer has multiple matching factions, the
    /// first match wins — design DB prototypes accordingly.
    /// </summary>
    private MisfitsTerminalDatabasePrototype? ResolveDatabaseForViewer(EntityUid viewer)
    {
        if (HasComp<StationAiHeldComponent>(viewer) &&
            _prototype.TryIndex<MisfitsTerminalDatabasePrototype>(ZaxVaultDatabaseId, out var vaultDatabase))
        {
            return vaultDatabase;
        }

        foreach (var proto in _prototype.EnumeratePrototypes<MisfitsTerminalDatabasePrototype>())
        {
            var (read, _, _, _) = ResolveAccess(viewer, proto);
            if (read)
                return proto;
        }
        return null;
    }

    private static List<DatabaseDocumentSummary> BuildDocSummaries(IReadOnlyList<TerminalDatabaseDataStore.DocumentDto> docs, bool canSeeDeleted)
    {
        var result = new List<DatabaseDocumentSummary>();
        foreach (var d in docs)
        {
            if (d.Deleted && !canSeeDeleted)
                continue;
            var lastEdit = d.Revisions.Count == 0 ? d.CreatedAt : d.Revisions[^1].Timestamp;
            result.Add(new DatabaseDocumentSummary(
                d.DocumentId, d.Title, d.Deleted, d.IsAdmin, d.CreatedByUserIdGuid, d.CreatedByCharName, d.CreatedAt, lastEdit, d.Revisions.Count));
        }
        return result;
    }

    private DatabaseDocumentView? BuildDocView(string databaseId, Guid documentId, bool canSeeDeleted)
    {
        var doc = _dataStore.FindDocument(databaseId, documentId);
        if (doc == null)
            return null;
        if (doc.Deleted && !canSeeDeleted)
            return null;

        var body = doc.Revisions.Count == 0 ? string.Empty : doc.Revisions[^1].Body;
        var revs = new List<DatabaseRevisionSummary>();
        foreach (var r in doc.Revisions)
            revs.Add(new DatabaseRevisionSummary(r.RevisionNumber, r.AuthorCharName, r.Timestamp));

        return new DatabaseDocumentView(
            doc.DocumentId, doc.Title, body, doc.Deleted, doc.IsAdmin, doc.CreatedByUserIdGuid, doc.CreatedByCharName, doc.CreatedAt, revs);
    }

    // ── Message handlers ─────────────────────────────────────────────────────
    // #Misfits Change - All handlers now resolve the database by the actor's ID
    // access (ResolveDatabaseForViewer), not by a component on the terminal.

    private void OnRequestState(EntityUid uid, TerminalDatabaseAccessComponent comp, RequestDatabaseStateMessage msg)
    {
        PushFullState(uid, msg.Actor, openDocumentId: null);
    }

    private void OnOpenDocument(EntityUid uid, TerminalDatabaseAccessComponent comp, OpenDatabaseDocumentMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        PushFullState(uid, msg.Actor, openDocumentId: msg.DocumentId);
    }

    // #Misfits Change - Tiered create:
    //   - Subfolder under existing root requires AccessWrite (Tier 6+).
    //   - Root folder requires Leadership (Tier 7+).
    //   - MarkAsAdmin additionally requires Admin (Tier 8).
    private void OnCreateFolder(EntityUid uid, TerminalDatabaseAccessComponent comp, CreateDatabaseFolderMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);

        var name = SanitiseTitle(msg.Name);
        if (string.IsNullOrEmpty(name))
            return;

        var folders = _dataStore.GetFolders(proto.ID);

        if (msg.ParentFolderId.HasValue)
        {
            // Subfolder creation — anyone with write access can add inside a folder, even Admin folders.
            if (!perms.write)
                return;
            var parent = folders.Find(f => f.FolderId == msg.ParentFolderId.Value && !f.Deleted);
            if (parent == null)
                return;
            if (parent.Subfolders.Count >= MaxSubfoldersPerFolder)
            {
                _sawmill.Warning($"Subfolder cap reached on folder {parent.FolderId} in db '{proto.ID}'.");
                return;
            }
            _dataStore.AddSubfolder(proto.ID, parent.FolderId, name, GetUserId(msg.Actor), GetCharName(msg.Actor));
        }
        else
        {
            // Top-level folder creation — Leadership-only (Tier 7+).
            if (!perms.leadership)
                return;
            // Marking ADMIN requires Admin tier (Tier 8).
            var asAdmin = msg.MarkAsAdmin && perms.leadership;
            if (msg.MarkAsAdmin && !perms.leadership)
                return; // user requested admin flag but lacks the job — reject silently.

            var liveCount = 0;
            foreach (var f in folders)
                if (!f.Deleted)
                    liveCount++;
            if (liveCount >= MaxFoldersPerDatabase)
            {
                _sawmill.Warning($"Folder cap reached for db '{proto.ID}'.");
                return;
            }
            _dataStore.AddFolder(proto.ID, name, asAdmin, GetUserId(msg.Actor), GetCharName(msg.Actor));
        }

        PushFullState(uid, msg.Actor, openDocumentId: null);
    }

    private void OnCreateDocument(EntityUid uid, TerminalDatabaseAccessComponent comp, CreateDatabaseDocumentMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);
        if (!perms.write)
            return;
        // #Misfits Tweak - MarkAsAdmin only requires Leadership tier (was Admin). The
        // protection itself still requires Admin to bypass on edit/delete; this just lets
        // Leadership protect their own docs from peer leaders.
        if (msg.MarkAsAdmin && !perms.leadership)
            return;

        var title = SanitiseTitle(msg.Title);
        var body = SanitiseBody(msg.Body);
        if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(body))
            return;

        var folders = _dataStore.GetFolders(proto.ID);
        var folder = folders.Find(f => f.FolderId == msg.FolderId && !f.Deleted);
        if (folder == null)
            return;

        // Cap check on the target container
        List<TerminalDatabaseDataStore.DocumentDto> targetDocs;
        if (msg.SubfolderId.HasValue)
        {
            var sub = folder.Subfolders.Find(s => s.SubfolderId == msg.SubfolderId.Value && !s.Deleted);
            if (sub == null)
                return;
            targetDocs = sub.Documents;
        }
        else
        {
            targetDocs = folder.Documents;
        }

        var liveDocs = 0;
        foreach (var d in targetDocs)
            if (!d.Deleted)
                liveDocs++;
        if (liveDocs >= MaxDocumentsPerContainer)
        {
            _sawmill.Warning($"Document cap reached in db '{proto.ID}' folder {folder.FolderId} sub {msg.SubfolderId}.");
            return;
        }

        var newDoc = _dataStore.AddDocument(proto.ID, msg.FolderId, msg.SubfolderId, title, body,
            isAdmin: msg.MarkAsAdmin && perms.leadership,
            GetUserId(msg.Actor), GetCharName(msg.Actor));

        // Push state with the newly-created document opened so author lands on it
        PushFullState(uid, msg.Actor, openDocumentId: newDoc?.DocumentId);
    }

    private void OnEditDocument(EntityUid uid, TerminalDatabaseAccessComponent comp, EditDatabaseDocumentMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);
        if (!perms.write)
            return;
        // #Misfits Add - Admin-marked docs (or docs in Admin folders) need Admin tier to edit.
        if (IsDocumentInAdminFolder(proto.ID, msg.DocumentId) && !perms.admin)
            return;

        var body = SanitiseBody(msg.Body);
        if (string.IsNullOrEmpty(body))
            return;

        if (!_dataStore.AppendRevision(proto.ID, msg.DocumentId, body, GetUserId(msg.Actor), GetCharName(msg.Actor)))
            return;

        PushFullState(uid, msg.Actor, openDocumentId: msg.DocumentId);
    }

    // Leaders may organize the shared archive without being granted deletion. Protected
    // entries remain exclusive to the database's highest configured rank.
    private void OnRenameEntry(EntityUid uid, TerminalDatabaseAccessComponent comp, RenameDatabaseEntryMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);
        if (!perms.leadership)
            return;

        var name = SanitiseTitle(msg.Name);
        if (string.IsNullOrEmpty(name))
            return;

        var protectedEntry = false;
        if (msg.FolderId.HasValue && !msg.SubfolderId.HasValue && !msg.DocumentId.HasValue)
            protectedEntry = IsFolderAdminProtected(proto.ID, msg.FolderId.Value);
        else if (msg.SubfolderParentFolderId.HasValue && msg.SubfolderId.HasValue && !msg.DocumentId.HasValue)
            protectedEntry = IsSubfolderAdminProtected(proto.ID, msg.SubfolderParentFolderId.Value, msg.SubfolderId.Value);
        else if (msg.DocumentId.HasValue && !msg.FolderId.HasValue && !msg.SubfolderId.HasValue)
            protectedEntry = IsDocumentInAdminFolder(proto.ID, msg.DocumentId.Value);
        else
            return;

        if (protectedEntry && !perms.admin)
            return;

        if (!_dataStore.RenameEntry(proto.ID, name, msg.FolderId, msg.SubfolderParentFolderId, msg.SubfolderId, msg.DocumentId))
            return;

        PushFullState(uid, msg.Actor, openDocumentId: msg.DocumentId);
    }

    // #Misfits Change - Delete is restricted to the database-specific highest rank
    // configured in AdminJobs, regardless of protection or authorship.
    private void OnDeleteFolder(EntityUid uid, TerminalDatabaseAccessComponent comp, DeleteDatabaseFolderMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);

        // Look up the parent folder to check IsAdmin gating.
        var folders = _dataStore.GetFolders(proto.ID);
        var folder = folders.Find(f => f.FolderId == msg.FolderId);
        if (folder == null)
            return;

        // #Misfits Change - Deletion is structural administration. Only the highest
        // database-specific rank configured in AdminJobs may delete any folder.
        if (!perms.admin)
            return;

        if (!_dataStore.SoftDeleteFolder(proto.ID, msg.FolderId, msg.SubfolderId))
            return;

        PushFullState(uid, msg.Actor, openDocumentId: null);
    }

    private void OnDeleteDocument(EntityUid uid, TerminalDatabaseAccessComponent comp, DeleteDatabaseDocumentMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);

        // #Misfits Change - Only the highest database-specific rank may delete.
        if (!perms.admin)
            return;

        if (!_dataStore.SoftDeleteDocument(proto.ID, msg.DocumentId))
            return;

        PushFullState(uid, msg.Actor, openDocumentId: null);
    }

    /// <summary>
    /// #Misfits Add - Returns true if the document lives inside an Admin-flagged root folder.
    /// </summary>
    // #Misfits Tweak - Renamed semantically to "is this doc Admin-protected": true if EITHER
    // its parent root folder is Admin-marked OR the document itself was created with the Admin flag.
    private bool IsDocumentInAdminFolder(string databaseId, Guid documentId)
    {
        foreach (var folder in _dataStore.GetFolders(databaseId))
        {
            foreach (var doc in folder.Documents)
                if (doc.DocumentId == documentId)
                    return folder.IsAdmin || doc.IsAdmin;
            foreach (var sub in folder.Subfolders)
                foreach (var doc in sub.Documents)
                    if (doc.DocumentId == documentId)
                        return folder.IsAdmin || doc.IsAdmin;
        }
        return false;
    }

    /// <summary>
    /// #Misfits Add - Returns true if a top-level folder is Admin-protected: the folder itself
    /// is Admin-marked, OR any nested document is independently Admin-marked. Used to gate the
    /// Leadership "move" action — Admin-protected entries cannot be relocated.
    /// </summary>
    private bool IsFolderAdminProtected(string databaseId, Guid folderId)
    {
        var folder = _dataStore.GetFolders(databaseId).Find(f => f.FolderId == folderId);
        if (folder == null)
            return true; // treat missing as protected
        if (folder.IsAdmin)
            return true;
        foreach (var doc in folder.Documents)
            if (doc.IsAdmin)
                return true;
        foreach (var sub in folder.Subfolders)
            foreach (var doc in sub.Documents)
                if (doc.IsAdmin)
                    return true;
        return false;
    }

    /// <summary>
    /// #Misfits Add - Returns true if a subfolder is Admin-protected: its parent root folder is
    /// Admin-marked, OR any nested document is independently Admin-marked.
    /// </summary>
    private bool IsSubfolderAdminProtected(string databaseId, Guid parentFolderId, Guid subfolderId)
    {
        var folder = _dataStore.GetFolders(databaseId).Find(f => f.FolderId == parentFolderId);
        if (folder == null)
            return true;
        if (folder.IsAdmin)
            return true;
        var sub = folder.Subfolders.Find(s => s.SubfolderId == subfolderId);
        if (sub == null)
            return true;
        foreach (var doc in sub.Documents)
            if (doc.IsAdmin)
                return true;
        return false;
    }

    private void OnRollbackDocument(EntityUid uid, TerminalDatabaseAccessComponent comp, RollbackDatabaseDocumentMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);
        // Rollback is a Leadership power; Admin-marked docs additionally require Admin.
        var requiresAdmin = IsDocumentInAdminFolder(proto.ID, msg.DocumentId);
        if (requiresAdmin ? !perms.admin : !perms.leadership)
            return;

        if (!_dataStore.RollbackDocument(proto.ID, msg.DocumentId, msg.RevisionNumber,
                GetUserId(msg.Actor), GetCharName(msg.Actor)))
            return;

        PushFullState(uid, msg.Actor, openDocumentId: msg.DocumentId);
    }

    // #Misfits Change - Restore mirrors delete and is highest-rank only.
    private void OnRestoreEntry(EntityUid uid, TerminalDatabaseAccessComponent comp, RestoreDatabaseEntryMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);

        // Restore is the inverse of deletion and uses the same highest-rank gate.
        if (!perms.admin)
            return;

        var changed = false;
        if (msg.FolderId.HasValue && !msg.SubfolderId.HasValue)
            changed = _dataStore.RestoreFolder(proto.ID, msg.FolderId.Value, null);
        else if (msg.SubfolderParentFolderId.HasValue && msg.SubfolderId.HasValue)
            changed = _dataStore.RestoreFolder(proto.ID, msg.SubfolderParentFolderId.Value, msg.SubfolderId.Value);
        else if (msg.DocumentId.HasValue)
            changed = _dataStore.RestoreDocument(proto.ID, msg.DocumentId.Value);

        if (!changed)
            return;

        PushFullState(uid, msg.Actor, openDocumentId: msg.DocumentId);
    }

    // #Misfits Add - Legacy permanent-delete handler. Highest-rank only; this will move
    // out of the normal faction UI during the recursive hierarchy refactor.
    private void OnPermanentDeleteEntry(EntityUid uid, TerminalDatabaseAccessComponent comp, PermanentDeleteDatabaseEntryMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);
        // #Misfits Change - Authorship no longer grants destructive control over shared
        // database trees. Until permanent purge moves to an administration tool, only
        // the highest database-specific rank can use this legacy message.
        if (!perms.admin)
            return;

        if (msg.FolderId.HasValue && !msg.SubfolderId.HasValue)
        {
            // Top-level folder permanent delete
            var folders = _dataStore.GetFolders(proto.ID);
            var folder = folders.Find(f => f.FolderId == msg.FolderId.Value);
            if (folder == null)
                return;
            _dataStore.HardDeleteFolder(proto.ID, msg.FolderId.Value);
        }
        else if (msg.SubfolderParentFolderId.HasValue && msg.SubfolderId.HasValue)
        {
            // Subfolder permanent delete
            var folders = _dataStore.GetFolders(proto.ID);
            var parent = folders.Find(f => f.FolderId == msg.SubfolderParentFolderId.Value);
            if (parent == null)
                return;
            var sub = parent.Subfolders.Find(s => s.SubfolderId == msg.SubfolderId.Value);
            if (sub == null)
                return;
            _dataStore.HardDeleteSubfolder(proto.ID, msg.SubfolderParentFolderId.Value, msg.SubfolderId.Value);
        }
        else if (msg.DocumentId.HasValue)
        {
            // Document permanent delete
            var doc = _dataStore.FindDocument(proto.ID, msg.DocumentId.Value);
            if (doc == null)
                return;
            _dataStore.HardDeleteDocument(proto.ID, msg.DocumentId.Value);
        }
        else
        {
            return; // nothing specified
        }

        PushFullState(uid, msg.Actor, openDocumentId: null);
    }

    // #Misfits Add - Leadership-tier "move" (tidying). Leaders can relocate ANY non-admin
    // entry into another live container (e.g. a "TRASH" folder they created). This is NOT a
    // delete — moved entries stay visible; Admin-tier roles then delete them as they already can.
    // Rules:
    //   • Requires Leadership tier (Tier 7+); Admin (Tier 8) inherits it via ResolveAccess.
    //   • Cannot move Admin-protected entries (IsAdmin folder, admin-marked doc, or doc inside
    //     an Admin folder) — those remain Admin-only territory.
    //   • Root folders can only become subfolders when they contain no subfolders of their own
    //     (the schema nests one level deep).
    private void OnMoveEntry(EntityUid uid, TerminalDatabaseAccessComponent comp, MoveDatabaseEntryMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);
        if (!perms.leadership)
            return;

        var folders = _dataStore.GetFolders(proto.ID);

        // Destination container must exist and be live.
        if (msg.TargetFolderId == null)
            return;
        var targetFolder = folders.Find(f => f.FolderId == msg.TargetFolderId.Value && !f.Deleted);
        if (targetFolder == null)
            return;
        TerminalDatabaseDataStore.SubfolderDto? targetSub = null;
        if (msg.TargetSubfolderId.HasValue)
        {
            targetSub = targetFolder.Subfolders.Find(s => s.SubfolderId == msg.TargetSubfolderId.Value && !s.Deleted);
            if (targetSub == null)
                return;
        }

        var changed = false;
        if (msg.FolderId.HasValue && !msg.SubfolderId.HasValue && !msg.DocumentId.HasValue)
        {
            // Move a top-level folder into another root folder (becomes a subfolder).
            if (msg.TargetSubfolderId.HasValue)
                return; // folders cannot be nested inside subfolders
            var folder = folders.Find(f => f.FolderId == msg.FolderId.Value);
            if (folder == null || folder.Deleted)
                return;
            if (folder.FolderId == targetFolder.FolderId)
                return; // no-op: already lives in the target
            if (IsFolderAdminProtected(proto.ID, folder.FolderId))
                return;
            if (folder.Subfolders.Count > 0)
                return; // cannot flatten a folder that contains subfolders
            changed = _dataStore.MoveFolderToSubfolder(proto.ID, folder.FolderId, targetFolder.FolderId);
        }
        else if (msg.SubfolderParentFolderId.HasValue && msg.SubfolderId.HasValue && !msg.DocumentId.HasValue)
        {
            // Move a subfolder to another root folder.
            if (msg.TargetSubfolderId.HasValue)
                return; // subfolders cannot nest
            var srcFolder = folders.Find(f => f.FolderId == msg.SubfolderParentFolderId.Value);
            if (srcFolder == null || srcFolder.Deleted)
                return;
            var sub = srcFolder.Subfolders.Find(s => s.SubfolderId == msg.SubfolderId.Value);
            if (sub == null || sub.Deleted)
                return;
            if (srcFolder.FolderId == targetFolder.FolderId)
                return; // no-op: already lives in the target
            if (IsSubfolderAdminProtected(proto.ID, srcFolder.FolderId, sub.SubfolderId))
                return;
            changed = _dataStore.MoveSubfolder(proto.ID, sub.SubfolderId, targetFolder.FolderId);
        }
        else if (msg.DocumentId.HasValue)
        {
            // Move a document into a folder root or subfolder.
            var doc = _dataStore.FindDocument(proto.ID, msg.DocumentId.Value);
            if (doc == null || doc.Deleted)
                return;
            if (IsDocumentInAdminFolder(proto.ID, doc.DocumentId))
                return;
            changed = _dataStore.MoveDocument(proto.ID, doc.DocumentId, targetFolder.FolderId, targetSub?.SubfolderId);
        }
        else
        {
            return; // nothing specified
        }

        if (!changed)
            return;

        PushFullState(uid, msg.Actor, openDocumentId: msg.DocumentId);
    }

    // #Misfits Add - Spawn a holotape with the document's title + body and put it in the
    // actor's hands. Read access is required (you can't export what you can't see). Deleted
    // documents are blocked unless the actor has Leadership (mirrors viewer visibility).
    private void OnExportDocument(EntityUid uid, TerminalDatabaseAccessComponent comp, ExportDatabaseDocumentMessage msg)
    {
        var proto = ResolveDatabaseForViewer(msg.Actor);
        if (proto == null)
            return;
        var perms = ResolveAccess(msg.Actor, proto);
        if (!perms.read)
            return;

        var doc = _dataStore.FindDocument(proto.ID, msg.DocumentId);
        if (doc == null)
            return;
        if (doc.Deleted && !perms.leadership)
            return;

        var body = doc.Revisions.Count == 0 ? string.Empty : doc.Revisions[^1].Body;
        if (string.IsNullOrWhiteSpace(body))
        {
            _popup.PopupEntity(Loc.GetString("terminal-database-export-empty"), msg.Actor, msg.Actor);
            return;
        }

        // Spawn the holotape at the actor's position; the parent prototype defines the sprite/sounds.
        var coords = Transform(msg.Actor).Coordinates;
        var holotapeUid = Spawn(proto.ExportHolotapePrototype, coords);

        // Stamp the document content directly. Localized=true prevents OnHolotapeDataMapInit
        // from overwriting Title/Content with FTL lookups (the document body is plain text/BBCode,
        // not a locale key).
        var data = EnsureComp<HolotapeDataComponent>(holotapeUid);
        data.Title = doc.Title;
        data.Content = body;
        data.Localized = true;

        // Rename the in-world entity so its hand tooltip reads as the document title.
        var meta = MetaData(holotapeUid);
        _metaData.SetEntityName(holotapeUid, doc.Title, meta);
        _metaData.SetEntityDescription(holotapeUid,
            Loc.GetString("terminal-database-export-desc", ("db", proto.DisplayName)),
            meta);

        // Try to slip into the actor's hands; fall back to dropping at their feet.
        _hands.PickupOrDrop(msg.Actor, holotapeUid);

        _sawmill.Info($"Exported document '{doc.Title}' ({doc.DocumentId}) from db '{proto.ID}' to holotape {holotapeUid} for {GetCharName(msg.Actor)}.");
        _popup.PopupEntity(Loc.GetString("terminal-database-export-ok", ("title", doc.Title)), msg.Actor, msg.Actor);
    }

    // ── State push ───────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the full HolotapeBoundUserInterfaceState (preserving non-database fields)
    /// and pushes it to all viewers. Delegated through HolotapeSystem to keep state ownership consistent.
    /// </summary>
    private void PushFullState(EntityUid uid, EntityUid actor, Guid? openDocumentId)
    {
        if (!_ui.HasUi(uid, HolotapeUiKey.Key))
            return;

        // Hand off to HolotapeSystem so the merged state (data + notes + links + database) stays consistent.
        var holotape = EntityManager.System<HolotapeSystem>();
        holotape.RefreshTerminalState(uid, actor, openDatabaseDocumentId: openDocumentId);
    }

    // ── Access / identity helpers ────────────────────────────────────────────

    private (bool read, bool write, bool leadership, bool admin) ResolveAccess(EntityUid viewer, MisfitsTerminalDatabasePrototype proto)
    {
        if (HasComp<StationAiHeldComponent>(viewer) && proto.ID == ZaxVaultDatabaseId)
            return (true, true, true, true);

        // Tag tiers (Read/Write) — checked against the actor's ID card.
        var tags = _access.FindAccessTags(viewer);

        bool MatchAny(List<string> required)
        {
            if (required.Count == 0)
                return false;
            foreach (var req in required)
            {
                foreach (var owned in tags)
                {
                    if (owned.Id == req)
                        return true;
                }
            }
            return false;
        }

        // #Misfits Change - Read requires explicit AccessRead overlap (empty list = nobody).
        bool readOk = MatchAny(proto.AccessRead);
        bool writeOk = MatchAny(proto.AccessWrite);

        // Job tiers (Leadership/Admin) — gated on actor's mind-job, not access tags,
        // so admin-spawned ID cards cannot grant the right to delete.
        bool leadershipOk = false;
        bool adminOk = false;
        if (_mind.TryGetMind(viewer, out var mindId, out _))
        {
            foreach (var jobId in proto.AdminJobs)
            {
                if (_job.MindHasJobWithId(mindId, jobId))
                {
                    adminOk = true;
                    break;
                }
            }
            // Admin implicitly grants Leadership powers.
            if (adminOk)
            {
                leadershipOk = true;
            }
            else
            {
                foreach (var jobId in proto.LeadershipJobs)
                {
                    if (_job.MindHasJobWithId(mindId, jobId))
                    {
                        leadershipOk = true;
                        break;
                    }
                }
            }
        }

        return (readOk, writeOk, leadershipOk, adminOk);
    }

    private NetUserId? GetUserId(EntityUid actor)
    {
        if (!TryComp<ActorComponent>(actor, out var actorComp))
            return null;
        return actorComp.PlayerSession.UserId;
    }

    private string GetCharName(EntityUid actor)
    {
        var name = MetaData(actor).EntityName;
        return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
    }

    private static string SanitiseTitle(string raw)
    {
        var t = raw?.Trim() ?? string.Empty;
        if (t.Length > MaxTitleChars)
            t = t[..MaxTitleChars];
        return t;
    }

    private static string SanitiseBody(string raw)
    {
        var b = raw?.Trim() ?? string.Empty;
        if (b.Length > MaxBodyChars)
            b = b[..MaxBodyChars];
        return b;
    }
}
