using Content.Shared.Speech; // #Sirius Add - ReplacementAccentPrototype перенесён в Content.Shared
using Robust.Shared.Prototypes;

namespace Content.Server.Speech.Components
{
    /// <summary>
    /// Replaces full sentences or words within sentences with new strings.
    /// </summary>
    [RegisterComponent]
    public sealed partial class ReplacementAccentComponent : Component
    {
        [DataField("accent", required: true)]
        public ProtoId<ReplacementAccentPrototype> Accent = default!;

        /// <summary>
        /// Allows you to substitute words, not always, but with some chance
        /// </summary>
        [DataField]
        public float ReplacementChance = 1f;
    }
}
