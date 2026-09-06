using System.Numerics;
using Content.Server.Administration;
using Content.Server._Misfits.MaterialExtractor;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Misfits.Administration.Commands;

/// <summary>Teleports an admin to the round's randomly spawned seismic extractor.</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class GotoMaterialExtractorCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "gotoextractor";
    public string Description => "Teleports you to the active seismic material extractor.";
    public string Help => "gotoextractor";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { Valid: true } player)
        {
            shell.WriteError("You must have an attached entity to use this command.");
            return;
        }

        var extractors = _entManager.AllEntityQueryEnumerator<MaterialExtractorComponent, TransformComponent>();
        if (!extractors.MoveNext(out var extractor, out _, out var extractorTransform))
        {
            shell.WriteError("No seismic material extractor exists in this round.");
            return;
        }

        var xform = _entManager.System<SharedTransformSystem>();
        xform.SetCoordinates(player, extractorTransform.Coordinates);
        xform.AttachToGridOrMap(player);

        if (_entManager.TryGetComponent(player, out PhysicsComponent? physics))
            _entManager.System<SharedPhysicsSystem>().SetLinearVelocity(player, Vector2.Zero, body: physics);

        shell.WriteLine($"Teleported to {_entManager.ToPrettyString(extractor)}.");
    }
}
