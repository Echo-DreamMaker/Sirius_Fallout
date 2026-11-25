using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Materials;
using Content.Shared.Roles; // #Misfits Add: for faction whitelist
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

namespace Content.Shared.Research.Prototypes
{
    [NetSerializable, Serializable, Prototype]
    public sealed partial class LatheRecipePrototype : IPrototype, IInheritingPrototype
    {
        [ViewVariables]
        [IdDataField]
        public string ID { get; private set; } = default!;

        /// <inheritdoc/>
        [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<LatheRecipePrototype>))]
        public string[]? Parents { get; set; }

        /// <inheritdoc />
        [NeverPushInheritance]
        [AbstractDataField]
        public bool Abstract { get; set; }

        /// <summary>
        ///     Name displayed in the lathe GUI.
        /// </summary>
        [DataField]
        public LocId? Name;

        /// <summary>
        ///     Short description displayed in the lathe GUI.
        /// </summary>
        [DataField]
        public LocId? Description;

        /// <summary>
        ///     The prototype name of the resulting entity when the recipe is printed.
        /// </summary>
        [DataField]
        public EntProtoId? Result;

        [DataField]
        public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2>? ResultReagents;

        /// <summary>
        ///     An entity whose sprite is displayed in the ui in place of the actual recipe result.
        /// </summary>
        [DataField]
        public SpriteSpecifier? Icon;

        [DataField("completetime")]
        public TimeSpan CompleteTime = TimeSpan.FromSeconds(5);

        /// <summary>
        ///     The materials required to produce this recipe.
        ///     Takes a material ID as string.
        /// </summary>
        [DataField]
        public Dictionary<ProtoId<MaterialPrototype>, int> Materials = new();

        // #Sirius Add: Concrete entity costs (armor, dynamite, microchips, wires, batteries, ...).
        // Unlike Materials these are whole item prototypes, consumed from the workbench storage
        // container, the acting player's hands and the player's inventory.
        [DataField]
        public Dictionary<EntProtoId, int> ItemCosts = new();

        // #Sirius Add: alternative whole-item costs. Each dictionary is a complete option;
        // one of them must be affordable and is consumed. Example: "leather armor OR light leather armor".
        [DataField]
        public List<Dictionary<EntProtoId, int>> AnyOfItemCosts = new();

        [DataField]
        public bool ApplyMaterialDiscount = true;

        /// <summary>
        /// A category used for visually sorting lathe recipes in the UI.
        /// </summary>
        [DataField]
        public ProtoId<LatheCategoryPrototype>? Category;

        /// <summary>
        /// DeltaV: Number of mining points this recipe adds to an oreproc when printed.
        /// Scales with stack count.
        /// </summary>
        [DataField]
        public uint MiningPoints;

        /// <summary>
        /// #Misfits Add: Optional faction whitelist (DepartmentPrototype IDs).
        /// When non-empty, only players whose job belongs to one of these factions
        /// are allowed to queue this recipe at a lathe.
        /// Mirrors CraftingPrototype.AvailableFaction for the lathe queue system.
        /// </summary>
        [DataField]
        public List<ProtoId<DepartmentPrototype>> AvailableFaction = new();

        // #Misfits Add: Marks recipes that are unlocked by placing a blueprint item in
        // the workbench storage. When true, the recipe is classified into the Blueprints
        // subsection of the Lathe UI (instead of All Items) so players see it there only
        // when the relevant blueprint is present, with the item's actual material costs.
        [DataField]
        public bool RequiresBlueprint = false;
    }
}