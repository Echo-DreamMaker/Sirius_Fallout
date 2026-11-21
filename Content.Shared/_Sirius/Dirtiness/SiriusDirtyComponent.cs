using System.Collections.Generic;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sirius.Dirtiness;

// #Sirius Add: states of the SiriusDirty cleanliness scale, from the filthiest to the sweetest smelling one.
public enum SiriusDirtyState : byte
{
    Animal = 0,       // 5 or less,   -3 charisma
    VeryDirty = 1,    // 10 or less,  -2 charisma
    Dirty = 2,        // 15 or less,  -1 charisma
    SlightlySmelly = 3, // 20 or less, no effect
    RelativelyClean = 4, // 25 or less, no effect
    Clean = 5,        // 30 or less,  +1 charisma
    Fragrant = 6,     // more than 30, +2 charisma
}

// #Sirius Add: shared locale keys keep the examine text and the HUD tooltip in sync.
public static class SiriusDirtyStateLoc
{
    public static string Key(SiriusDirtyState state) => state switch
    {
        SiriusDirtyState.Animal => "sirius-dirty-examine-animal",
        SiriusDirtyState.VeryDirty => "sirius-dirty-examine-very-dirty",
        SiriusDirtyState.Dirty => "sirius-dirty-examine-dirty",
        SiriusDirtyState.SlightlySmelly => "sirius-dirty-examine-slightly-smelly",
        SiriusDirtyState.RelativelyClean => "sirius-dirty-examine-relatively-clean",
        SiriusDirtyState.Clean => "sirius-dirty-examine-clean",
        _ => "sirius-dirty-examine-fragrant",
    };

    // #Sirius Add: the examine text is markup-coloured; the tooltip needs a plain name.
    public static string NameKey(SiriusDirtyState state) => state switch
    {
        SiriusDirtyState.Animal => "sirius-dirty-level-animal",
        SiriusDirtyState.VeryDirty => "sirius-dirty-level-very-dirty",
        SiriusDirtyState.Dirty => "sirius-dirty-level-dirty",
        SiriusDirtyState.SlightlySmelly => "sirius-dirty-level-slightly-smelly",
        SiriusDirtyState.RelativelyClean => "sirius-dirty-level-relatively-clean",
        SiriusDirtyState.Clean => "sirius-dirty-level-clean",
        _ => "sirius-dirty-level-fragrant",
    };
}

// #Sirius Add: ghouls are the only species with doubled dirtiness decay.
public static class SiriusDirtySpecies
{
    private static readonly HashSet<string> Ghouls = new() {"Ghoul", "GhoulGlowing"};

    public static bool IsGhoul(string? species) => species != null && Ghouls.Contains(species);
}

// #Sirius Add: central component of the dirtiness mechanic. 0 = filthy, 40 = fragrant.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SiriusDirtyComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public float Cleanliness = 30f;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField, AutoNetworkedField]
    public SiriusDirtyState State = SiriusDirtyState.Fragrant;

    /// <summary>
    /// #Sirius Add: one alert with seven icons, the severity is the state index.
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> DirtyAlert = "SiriusDirty";

    [DataField] public float MaxCleanliness = 40f;
    [DataField] public float StartingCleanliness = 30f;
    [DataField] public float DecayPerMinute = 1f;

    // #Sirius Add: ghouls are naturally filthier and lose cleanliness twice as fast.
    [DataField] public float GhoulDecayMultiplier = 2f;

    [DataField] public float WaterCleanPerMinute = 4f;
    [DataField] public float WaterCleanCap = 30f;
    [DataField] public float ShowerCleanPerSecond = 1f;
    [DataField] public float ShowerCleanCap = 30f;
    [DataField] public float ShowerRange = 1.5f;

    [DataField] public float PlainSoapPerSecond = 2f;
    [DataField] public float PlainSoapCap = 35f;
    [DataField] public float ScentedSoapPerSecond = 4f;
    [DataField] public float ScentedSoapCap = 40f;

    [DataField] public float BleedDecayMultiplierMin = 2f;
    [DataField] public float BleedDecayMultiplierMax = 4f;
    [DataField] public float DirtyWaterDecayMultiplier = 3f;
    [DataField] public float SewageDecayMultiplier = 4f;

    [DataField] public TimeSpan WaterWetDuration = TimeSpan.FromSeconds(60);
    [DataField] public TimeSpan SplashWetPerUnit = TimeSpan.FromSeconds(2);

    // #Sirius Add: other characters within this radius (in tiles) contribute a "surrounding smell" mood.
    [DataField] public float SmellRadius = 3f;

    /// <summary>
    /// #Sirius Add: internal update accumulator, one full pass per second is enough for minute-based decay.
    /// </summary>
    [ViewVariables]
    public float Accumulator;

    [ViewVariables]
    public float PreviousCharismaModifier;

    // #Sirius Add: last mood effect applied into each smell slot. Re-sending the same uncategorised
    // effect ID twice would stack timers and make it expire early, so we only send on a real change.
    [ViewVariables]
    public string? LastSelfSmellEffect;

    [ViewVariables]
    public string? LastOthersSmellEffect;

    // #Sirius Add: the character currently supplying the "surrounding smell" slot, and their state.
    [ViewVariables]
    public EntityUid? LastSmellSource;

    [ViewVariables]
    public SiriusDirtyState LastSmellSourceState;
}
