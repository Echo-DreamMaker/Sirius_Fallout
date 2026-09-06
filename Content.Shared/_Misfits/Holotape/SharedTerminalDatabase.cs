using System;
using System.Collections.Generic;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

// #Misfits Add - Shared types for the faction-database terminal tab.
// Defines: networked state record sent to the client (DatabaseState), summary
// records for folders/subfolders/documents/revisions, and BUI message classes
// for client → server actions (create/edit/delete/rollback/open).

namespace Content.Shared._Misfits.Holotape;

// ── Networked state (server → client) ─────────────────────────────────────

/// <summary>
/// Bundle of all database-tab state pushed to the client when the terminal UI is open.
/// Null on the parent BUI state means "this terminal has no database — hide tab".
/// </summary>
[Serializable, NetSerializable]
public sealed class TerminalDatabaseState
{
    public readonly string DatabaseId;
    public readonly string DisplayName;
    public readonly string AccentColor;

    /// <summary>
    /// True if the viewer's ID card satisfies AccessRead. Determines tab visibility.
    /// </summary>
    public readonly bool CanRead;

    /// <summary>
    /// True if the viewer's ID card satisfies AccessWrite (subfolder/document create + edit
    /// underneath existing roots). Tier 6.
    /// </summary>
    public readonly bool CanWrite;

    /// <summary>
    /// #Misfits Add - True if the viewer's currently-assigned JOB matches one of the
    /// prototype's LeadershipJobs. Job-based (not access-tag-based) so spawned ID cards
    /// cannot grant it. Tier 7. Powers: create root entries and roll back revisions.
    /// </summary>
    public readonly bool CanLeadership;

    /// <summary>
    /// #Misfits Add - True if the viewer's currently-assigned JOB matches one of the
    /// prototype's AdminJobs. Tier 8/highest rank. Powers include delete/restore of
    /// every entry and management of Admin-marked entries.
    /// </summary>
    public readonly bool CanAdmin;

    /// <summary>
    /// #Misfits Add - Faction-themed label for the Admin checkbox ("ELDER COUNCIL",
    /// "HIGH COMMAND", "MANAGEMENT", etc).
    /// </summary>
    public readonly string AdminLabel;

    /// <summary>
    /// All non-deleted folders for this database. Subfolders are nested inside.
    /// Admins also receive deleted entries for restoration.
    /// </summary>
    public readonly List<DatabaseFolderSummary> Folders;

    /// <summary>
    /// If non-null, the client should display this document's full body in the viewer pane.
    /// Set when the client sent OpenDocumentMessage and the server resolved it.
    /// </summary>
    public readonly DatabaseDocumentView? OpenDocument;

    public TerminalDatabaseState(
        string databaseId,
        string displayName,
        string accentColor,
        bool canRead,
        bool canWrite,
        bool canLeadership,
        bool canAdmin,
        string adminLabel,
        List<DatabaseFolderSummary> folders,
        DatabaseDocumentView? openDocument = null)
    {
        DatabaseId = databaseId;
        DisplayName = displayName;
        AccentColor = accentColor;
        CanRead = canRead;
        CanWrite = canWrite;
        CanLeadership = canLeadership;
        CanAdmin = canAdmin;
        AdminLabel = adminLabel;
        Folders = folders;
        OpenDocument = openDocument;
    }
}

/// <summary>
/// Summary of a top-level folder in a database. Carries subfolders and direct docs (title only).
/// </summary>
[Serializable, NetSerializable]
public sealed class DatabaseFolderSummary
{
    public readonly Guid FolderId;
    public readonly string Name;
    public readonly bool Deleted;
    /// <summary>#Misfits Add - True if this root folder is Admin-protected; only Admin-tier (job-gated) actors can delete/restore it.</summary>
    public readonly bool IsAdmin;
    public readonly Guid? CreatedByUserId;
    public readonly string CreatedByCharName;
    public readonly DateTime CreatedAt;
    public readonly List<DatabaseSubfolderSummary> Subfolders;
    public readonly List<DatabaseDocumentSummary> Documents;

    public DatabaseFolderSummary(
        Guid folderId,
        string name,
        bool deleted,
        bool isAdmin,
        Guid? createdByUserId,
        string createdByCharName,
        DateTime createdAt,
        List<DatabaseSubfolderSummary> subfolders,
        List<DatabaseDocumentSummary> documents)
    {
        FolderId = folderId;
        Name = name;
        Deleted = deleted;
        IsAdmin = isAdmin;
        CreatedByUserId = createdByUserId;
        CreatedByCharName = createdByCharName;
        CreatedAt = createdAt;
        Subfolders = subfolders;
        Documents = documents;
    }
}

/// <summary>
/// Summary of a subfolder (1 level deep). Carries direct docs only — no further nesting.
/// </summary>
[Serializable, NetSerializable]
public sealed class DatabaseSubfolderSummary
{
    public readonly Guid SubfolderId;
    public readonly string Name;
    public readonly bool Deleted;
    public readonly Guid? CreatedByUserId;
    public readonly string CreatedByCharName;
    public readonly DateTime CreatedAt;
    public readonly List<DatabaseDocumentSummary> Documents;

    public DatabaseSubfolderSummary(
        Guid subfolderId,
        string name,
        bool deleted,
        Guid? createdByUserId,
        string createdByCharName,
        DateTime createdAt,
        List<DatabaseDocumentSummary> documents)
    {
        SubfolderId = subfolderId;
        Name = name;
        Deleted = deleted;
        CreatedByUserId = createdByUserId;
        CreatedByCharName = createdByCharName;
        CreatedAt = createdAt;
        Documents = documents;
    }
}

/// <summary>
/// Summary of a document (title only). Body is fetched via OpenDocumentMessage.
/// </summary>
[Serializable, NetSerializable]
public sealed class DatabaseDocumentSummary
{
    public readonly Guid DocumentId;
    public readonly string Title;
    public readonly bool Deleted;
    /// <summary>#Misfits Add - True if THIS document is independently Admin-protected
    /// (independent of its parent folder). Either flag forces Admin tier for edit/delete.</summary>
    public readonly bool IsAdmin;
    public readonly Guid? CreatedByUserId;
    public readonly string CreatedByCharName;
    public readonly DateTime CreatedAt;
    public readonly DateTime LastEditedAt;
    public readonly int RevisionCount;

    public DatabaseDocumentSummary(
        Guid documentId,
        string title,
        bool deleted,
        bool isAdmin,
        Guid? createdByUserId,
        string createdByCharName,
        DateTime createdAt,
        DateTime lastEditedAt,
        int revisionCount)
    {
        DocumentId = documentId;
        Title = title;
        Deleted = deleted;
        IsAdmin = isAdmin;
        CreatedByUserId = createdByUserId;
        CreatedByCharName = createdByCharName;
        CreatedAt = createdAt;
        LastEditedAt = lastEditedAt;
        RevisionCount = revisionCount;
    }
}

/// <summary>
/// Full document payload — title, current body (latest revision), all revision metadata.
/// </summary>
[Serializable, NetSerializable]
public sealed class DatabaseDocumentView
{
    public readonly Guid DocumentId;
    public readonly string Title;
    public readonly string Body;
    public readonly bool Deleted;
    /// <summary>#Misfits Add - Mirrors DatabaseDocumentSummary.IsAdmin so the viewer knows when to gate Edit on Admin tier.</summary>
    public readonly bool IsAdmin;
    public readonly Guid? CreatedByUserId;
    public readonly string CreatedByCharName;
    public readonly DateTime CreatedAt;
    public readonly List<DatabaseRevisionSummary> Revisions;

    public DatabaseDocumentView(
        Guid documentId,
        string title,
        string body,
        bool deleted,
        bool isAdmin,
        Guid? createdByUserId,
        string createdByCharName,
        DateTime createdAt,
        List<DatabaseRevisionSummary> revisions)
    {
        DocumentId = documentId;
        Title = title;
        Body = body;
        Deleted = deleted;
        IsAdmin = isAdmin;
        CreatedByUserId = createdByUserId;
        CreatedByCharName = createdByCharName;
        CreatedAt = createdAt;
        Revisions = revisions;
    }
}

/// <summary>
/// Per-revision metadata — number, author, timestamp. Body content not included
/// here; rollback fetches it server-side.
/// </summary>
[Serializable, NetSerializable]
public sealed class DatabaseRevisionSummary
{
    public readonly int RevisionNumber;
    public readonly string AuthorCharName;
    public readonly DateTime Timestamp;

    public DatabaseRevisionSummary(int revisionNumber, string authorCharName, DateTime timestamp)
    {
        RevisionNumber = revisionNumber;
        AuthorCharName = authorCharName;
        Timestamp = timestamp;
    }
}

// ── BUI Messages (client → server) ────────────────────────────────────────

/// <summary>
/// Refresh: client wants the current folder/subfolder/doc structure for this terminal's database.
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestDatabaseStateMessage : BoundUserInterfaceMessage;

/// <summary>
/// Client wants to open and read a specific document. Server pushes a state with OpenDocument set.
/// </summary>
[Serializable, NetSerializable]
public sealed class OpenDatabaseDocumentMessage : BoundUserInterfaceMessage
{
    public readonly Guid DocumentId;

    public OpenDatabaseDocumentMessage(Guid documentId)
    {
        DocumentId = documentId;
    }
}

/// <summary>
/// Create a top-level folder (ParentFolderId == null) or a subfolder under an existing folder.
/// </summary>
[Serializable, NetSerializable]
public sealed class CreateDatabaseFolderMessage : BoundUserInterfaceMessage
{
    public readonly Guid? ParentFolderId;
    public readonly string Name;
    /// <summary>#Misfits Add - If true and the actor's job is in AdminJobs, the new root folder is marked ADMIN.</summary>
    public readonly bool MarkAsAdmin;

    public CreateDatabaseFolderMessage(Guid? parentFolderId, string name, bool markAsAdmin = false)
    {
        ParentFolderId = parentFolderId;
        Name = name;
        MarkAsAdmin = markAsAdmin;
    }
}

/// <summary>
/// Create a new document inside a folder or subfolder.
/// SubfolderId optional — null = doc lives directly in the folder, non-null = inside that subfolder.
/// </summary>
[Serializable, NetSerializable]
public sealed class CreateDatabaseDocumentMessage : BoundUserInterfaceMessage
{
    public readonly Guid FolderId;
    public readonly Guid? SubfolderId;
    public readonly string Title;
    public readonly string Body;
    /// <summary>#Misfits Add - If true and the actor's job is in AdminJobs, mark this new document as Admin-protected.</summary>
    public readonly bool MarkAsAdmin;

    public CreateDatabaseDocumentMessage(Guid folderId, Guid? subfolderId, string title, string body, bool markAsAdmin = false)
    {
        FolderId = folderId;
        SubfolderId = subfolderId;
        Title = title;
        Body = body;
        MarkAsAdmin = markAsAdmin;
    }
}

/// <summary>
/// Edit an existing document — pushes a new revision onto its revision list.
/// </summary>
[Serializable, NetSerializable]
public sealed class EditDatabaseDocumentMessage : BoundUserInterfaceMessage
{
    public readonly Guid DocumentId;
    public readonly string Body;

    public EditDatabaseDocumentMessage(Guid documentId, string body)
    {
        DocumentId = documentId;
        Body = body;
    }
}

/// <summary>
/// Leadership-tier structural action: rename a folder, subfolder, or document title.
/// Exactly one of FolderId / SubfolderParentFolderId+SubfolderId / DocumentId is set.
/// The server resolves authority and sanitises the submitted name.
/// </summary>
[Serializable, NetSerializable]
public sealed class RenameDatabaseEntryMessage : BoundUserInterfaceMessage
{
    public readonly Guid? FolderId;
    public readonly Guid? SubfolderParentFolderId;
    public readonly Guid? SubfolderId;
    public readonly Guid? DocumentId;
    public readonly string Name;

    public RenameDatabaseEntryMessage(
        string name,
        Guid? folderId = null,
        Guid? subfolderParentFolderId = null,
        Guid? subfolderId = null,
        Guid? documentId = null)
    {
        Name = name;
        FolderId = folderId;
        SubfolderParentFolderId = subfolderParentFolderId;
        SubfolderId = subfolderId;
        DocumentId = documentId;
    }
}

/// <summary>
/// Soft-delete a folder (and all its contents) or a subfolder.
/// Use SubfolderId == null to delete a top-level folder; non-null deletes only that subfolder.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeleteDatabaseFolderMessage : BoundUserInterfaceMessage
{
    public readonly Guid FolderId;
    public readonly Guid? SubfolderId;

    public DeleteDatabaseFolderMessage(Guid folderId, Guid? subfolderId)
    {
        FolderId = folderId;
        SubfolderId = subfolderId;
    }
}

/// <summary>
/// Soft-delete a document.
/// </summary>
[Serializable, NetSerializable]
public sealed class DeleteDatabaseDocumentMessage : BoundUserInterfaceMessage
{
    public readonly Guid DocumentId;

    public DeleteDatabaseDocumentMessage(Guid documentId)
    {
        DocumentId = documentId;
    }
}

/// <summary>
/// Approver-tier action: roll back a document to a prior revision. Implemented as
/// duplicating the chosen revision's body as a new top revision (preserves history).
/// </summary>
[Serializable, NetSerializable]
public sealed class RollbackDatabaseDocumentMessage : BoundUserInterfaceMessage
{
    public readonly Guid DocumentId;
    public readonly int RevisionNumber;

    public RollbackDatabaseDocumentMessage(Guid documentId, int revisionNumber)
    {
        DocumentId = documentId;
        RevisionNumber = revisionNumber;
    }
}

/// <summary>
/// Approver-tier action: restore a soft-deleted folder, subfolder, or document.
/// Exactly one of FolderId / SubfolderParent+SubfolderId / DocumentId is set.
/// </summary>
[Serializable, NetSerializable]
public sealed class RestoreDatabaseEntryMessage : BoundUserInterfaceMessage
{
    public readonly Guid? FolderId;
    public readonly Guid? SubfolderParentFolderId;
    public readonly Guid? SubfolderId;
    public readonly Guid? DocumentId;

    public RestoreDatabaseEntryMessage(
        Guid? folderId = null,
        Guid? subfolderParentFolderId = null,
        Guid? subfolderId = null,
        Guid? documentId = null)
    {
        FolderId = folderId;
        SubfolderParentFolderId = subfolderParentFolderId;
        SubfolderId = subfolderId;
        DocumentId = documentId;
    }
}

// #Misfits Add - Export a database document to a physical holotape entity.
// The server spawns a holotape with the document's title + body, then places it
// in the actor's hands (or drops it at their feet if hands are full).
[Serializable, NetSerializable]
public sealed class ExportDatabaseDocumentMessage : BoundUserInterfaceMessage
{
    public readonly Guid DocumentId;

    public ExportDatabaseDocumentMessage(Guid documentId)
    {
        DocumentId = documentId;
    }
}

// #Misfits Add - Permanently delete a folder or document from the database.
// Unlike soft-delete, this actually REMOVES the entry from the data store.
// Authorization (server-side): the database-specific highest rank from AdminJobs.
// Authorship never grants destructive control over shared entries. After deletion,
// the entry cannot be restored by anyone.
[Serializable, NetSerializable]
public sealed class PermanentDeleteDatabaseEntryMessage : BoundUserInterfaceMessage
{
    /// <summary>Set this to delete a top-level folder (and everything inside it).</summary>
    public readonly Guid? FolderId;
    /// <summary>Parent folder of a subfolder to delete. Must be set with SubfolderId.</summary>
    public readonly Guid? SubfolderParentFolderId;
    /// <summary>Subfolder to delete. Must be set with SubfolderParentFolderId.</summary>
    public readonly Guid? SubfolderId;
    /// <summary>Set this to delete a single document.</summary>
    public readonly Guid? DocumentId;

    public PermanentDeleteDatabaseEntryMessage(
        Guid? folderId = null,
        Guid? subfolderParentFolderId = null,
        Guid? subfolderId = null,
        Guid? documentId = null)
    {
        FolderId = folderId;
        SubfolderParentFolderId = subfolderParentFolderId;
        SubfolderId = subfolderId;
        DocumentId = documentId;
    }
}

// #Misfits Add - Leadership-tier action: MOVE a folder, subfolder, or document into another
// container (tidying / organizing). This is NOT a delete — the entry stays fully visible,
// just relocated. Intended flow: leadership moves unwanted entries into a folder they create
// (e.g. "TRASH"), then Admin-tier roles permanently delete the trash as they already can.
// Exactly one of FolderId / SubfolderParentFolderId+SubfolderId / DocumentId is set (source).
// TargetFolderId + TargetSubfolderId identify the destination container (subfolder destination
// is only valid for document moves — subfolders cannot nest).
[Serializable, NetSerializable]
public sealed class MoveDatabaseEntryMessage : BoundUserInterfaceMessage
{
    // ── Source entry (exactly one group set) ───────────────────────────────
    /// <summary>Set to move a top-level folder (becomes a subfolder of the target).</summary>
    public readonly Guid? FolderId;
    /// <summary>Current parent folder of the subfolder to move.</summary>
    public readonly Guid? SubfolderParentFolderId;
    /// <summary>Subfolder to move (must be set with SubfolderParentFolderId).</summary>
    public readonly Guid? SubfolderId;
    /// <summary>Set to move a single document.</summary>
    public readonly Guid? DocumentId;

    // ── Destination container ──────────────────────────────────────────────
    /// <summary>Destination top-level folder.</summary>
    public readonly Guid? TargetFolderId;
    /// <summary>Destination subfolder (null = folder root). Only valid for document moves.</summary>
    public readonly Guid? TargetSubfolderId;

    public MoveDatabaseEntryMessage(
        Guid? folderId = null,
        Guid? subfolderParentFolderId = null,
        Guid? subfolderId = null,
        Guid? documentId = null,
        Guid? targetFolderId = null,
        Guid? targetSubfolderId = null)
    {
        FolderId = folderId;
        SubfolderParentFolderId = subfolderParentFolderId;
        SubfolderId = subfolderId;
        DocumentId = documentId;
        TargetFolderId = targetFolderId;
        TargetSubfolderId = targetSubfolderId;
    }
}
