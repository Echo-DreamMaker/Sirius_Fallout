// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Speech.EntitySystems;
using Content.Server.Speech;
using Content.Shared.Speech;
using Content.Shared._Misfits.Speech;
using Robust.Shared.Random;

namespace Content.Server._Misfits.Speech;

public sealed class MedievalAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MedievalAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MedievalAccentComponent component, ref AccentGetEvent args)
    {
        if (args.Message.Length == 0)
            return;
        var message = _replacement.ApplyReplacements(args.Message, "medieval");
        if (_random.Prob(0.40f))
            message = Loc.GetString($"accent-medieval-prefix-{_random.Next(1, 42)}") + " " + char.ToLower(message[0]) + message[1..];
        args.Message = char.ToUpper(message[0]) + message[1..];
    }
}
