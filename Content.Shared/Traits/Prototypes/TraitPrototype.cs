using Content.Shared.Customization.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization;

namespace Content.Shared.Traits;


/// <summary>
///     Describes a trait.
/// </summary>
[Prototype("trait")]
public sealed partial class TraitPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Which customization tab to place this entry in
    /// </summary>
    [DataField(required: true)]
    public ProtoId<TraitCategoryPrototype> Category = "Uncategorized";

    /// <summary>
    ///     How many points this will give the character
    /// </summary>
    [DataField]
    public int Points = 0;

    // #Cythisiax Added - tier hint for the trait talent-tree graph layout (higher = deeper row).
    // -1 (default) means the tree graph auto-places the node by prerequisite depth.
    /// <summary>
    ///     Vertical row hint for the talent-tree graph. Higher values place the node deeper.
    /// </summary>
    [DataField]
    public int Tier = -1;

    /// <summary>
    ///     Hidden traits can be granted by systems or jobs, but are not valid character-creation picks.
    /// </summary>
    [DataField]
    public bool Hidden = false;

    [DataField]
    public List<CharacterRequirement> Requirements = new();

    [DataField(serverOnly: true)]
    public TraitFunction[] Functions { get; private set; } = Array.Empty<TraitFunction>();
}

/// This serves as a hook for trait functions to modify a player character upon spawning in.
[ImplicitDataDefinitionForInheritors]
public abstract partial class TraitFunction
{
    public abstract void OnPlayerSpawn(
        EntityUid mob,
        IComponentFactory factory,
        IEntityManager entityManager,
        ISerializationManager serializationManager);
}
