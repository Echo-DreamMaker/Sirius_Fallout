// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Speech.EntitySystems;
using Content.Server.Speech;
using Content.Shared.Speech;
using Content.Shared._Misfits.Speech;
using Robust.Shared.Random;

namespace Content.Server._Misfits.Speech;

public sealed class BritishAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BritishAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, BritishAccentComponent component, ref AccentGetEvent args)
    {
        if (args.Message.Length == 0)
            return;
        var message = _replacement.ApplyReplacements(args.Message, "british");
        if (_random.Prob(0.10f))
            message = Loc.GetString($"accent-british-prefix-{_random.Next(1, 5)}") + " " + char.ToLower(message[0]) + message[1..];
        message = char.ToUpper(message[0]) + message[1..];
        if (_random.Prob(0.05f))
            message += Loc.GetString($"accent-british-suffix-{_random.Next(1, 6)}");
        args.Message = message;
    }
}
