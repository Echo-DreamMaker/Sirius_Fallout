using Robust.Shared.GameObjects;

namespace Content.Shared.Power.Generator;

/// <summary>
/// Routes the portable-generator context verb to its control panel instead of
/// directly starting or stopping it. Normal activation can therefore remain
/// available for another interface, such as a machine's storage hopper.
/// </summary>
[RegisterComponent]
public sealed partial class GeneratorControlPanelComponent : Component;
