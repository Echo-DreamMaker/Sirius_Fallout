// #Misfits Add - Handles local, volume-capped audio uploaded from the Loremaster admin tab.
using System.Collections.Concurrent;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Server.Upload;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Misfits.Administration;

/// <summary>
/// Plays Loremaster-tab uploads only around the uploading administrator. Uploaded resources are
/// deliberately isolated under a private path so ordinary runtime uploads cannot trigger audio.
/// </summary>
public sealed partial class LoreMasterAudioSystem : EntitySystem
{
    private const string UploadPrefix = "LoreMasterAudio/";
    private const string UploadedPrefix = "/Uploaded/";
    private const float SafeVolume = -10f;
    private const float MaxDistance = 16f;

    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private NetworkResourceManager _networkResources = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IResourceManager _resources = default!;

    private readonly ConcurrentQueue<(ICommonSession Session, ResPath Path)> _pendingUploads = new();

    public override void Initialize()
    {
        base.Initialize();
        _networkResources.ResourcesUploaded += OnResourcesUploaded;
    }

    public override void Shutdown()
    {
        _networkResources.ResourcesUploaded -= OnResourcesUploaded;
        base.Shutdown();
    }

    private void OnResourcesUploaded(NetworkResourcesUploadedEvent args)
    {
        // Upload callbacks run off the game thread. Queue the request before touching entities.
        foreach (var (path, _) in args.Files)
        {
            _pendingUploads.Enqueue((args.Session, path));
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // A resource-upload callback may arrive one main-thread tick before the resource itself
        // is stored under /Uploaded. Do not ask the audio system to resolve it until it is ready.
        var pendingAtStart = _pendingUploads.Count;
        for (var i = 0; i < pendingAtStart && _pendingUploads.TryDequeue(out var upload); i++)
        {
            var relativePath = upload.Path.ToRelativePath();
            if (!relativePath.ToString().StartsWith(UploadPrefix, StringComparison.Ordinal)
                || !string.Equals(relativePath.Extension, "ogg", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!_adminManager.HasAdminFlag(upload.Session, AdminFlags.Fun)
                || upload.Session.AttachedEntity is not { } source)
                continue;

            var soundPath = new ResPath(UploadedPrefix + relativePath);
            if (!_resources.ContentFileExists(soundPath))
            {
                _pendingUploads.Enqueue(upload);
                continue;
            }

            var sound = new SoundPathSpecifier(soundPath);
            _audio.PlayPvs(sound, source, AudioParams.Default
                .WithVolume(SafeVolume)
                .WithMaxDistance(MaxDistance));
        }
    }
}
