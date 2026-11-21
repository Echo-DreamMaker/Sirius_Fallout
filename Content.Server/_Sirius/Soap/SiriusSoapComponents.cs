namespace Content.Server._Sirius.Soap;

/// <summary>
/// #Sirius Add: placed on a solution container by the soap reaction. The solution is cooled
/// down and the soap reagent is turned into bars of the corresponding soap family.
/// </summary>
[RegisterComponent]
public sealed partial class SiriusSoapSolidifyingComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    public float Elapsed;

    [DataField] public float SolidifyTemperature = 320f;
    [DataField] public float CoolingPerSecond = 15f;
    [DataField] public float MaxCoolingTime = 10f;
    [DataField] public float BarVolume = 50f;
}
