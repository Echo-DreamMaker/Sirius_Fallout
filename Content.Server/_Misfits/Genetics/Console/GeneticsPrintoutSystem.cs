// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Paper;
using Content.Shared._Misfits.Genetics.Console;

namespace Content.Server._Misfits.Genetics.Console;

public sealed class GeneticsPrintoutSystem : EntitySystem
{
    [Dependency] private PaperSystem _paper = default!;

    public override void Initialize()
        => SubscribeLocalEvent<PaperComponent, GeneticsPrintoutPopulateEvent>(OnPopulate);

    private void OnPopulate(Entity<PaperComponent> ent, ref GeneticsPrintoutPopulateEvent args)
        => _paper.SetContent(ent, args.Text);
}
