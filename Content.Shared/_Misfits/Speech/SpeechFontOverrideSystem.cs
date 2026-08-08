// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Misfits.Common.Speech;

namespace Content.Shared._Misfits.Speech;

public sealed class SpeechFontOverrideSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpeechFontOverrideComponent, SpeechFontOverrideEvent>(OnOverride);
    }

    private void OnOverride(Entity<SpeechFontOverrideComponent> ent, ref SpeechFontOverrideEvent args)
    {
        if (!ent.Comp.SourceOnly || args.Source == ent.Owner)
            args.Font = ent.Comp.Font;
    }
}
