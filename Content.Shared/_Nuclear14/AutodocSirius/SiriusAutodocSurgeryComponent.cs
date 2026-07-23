using Content.Shared._Shitmed.Body.Organ;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Nuclear14.AutodocSirius;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SiriusAutodocSurgeryComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<BodyPartType, EntityUid> AvailableParts = new();

    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, EntityUid> AvailableOrgans = new();

    [ViewVariables, AutoNetworkedField]
    public bool IsOperating = false;

    [ViewVariables, AutoNetworkedField]
    public float OperationProgress = 0f;

    [DataField]
    public float AllSurgeryDuration = 10f;

    [ViewVariables]
    public EntityUid? CurrentPatient = null;

    [ViewVariables]
    public List<string> PendingSurgeries = new();

    public static readonly Dictionary<BodyPartType, List<BodyPartType>> PartDependencies = new()
    {
        { BodyPartType.Arm, new List<BodyPartType> { BodyPartType.Hand } },
        { BodyPartType.Leg, new List<BodyPartType> { BodyPartType.Foot } },
        { BodyPartType.Hand, new List<BodyPartType>() },
        { BodyPartType.Foot, new List<BodyPartType>() },
        { BodyPartType.Head, new List<BodyPartType>() },
        { BodyPartType.Torso, new List<BodyPartType> { BodyPartType.Arm, BodyPartType.Leg, BodyPartType.Head } },
        { BodyPartType.Other, new List<BodyPartType>() }
    };

    public static readonly HashSet<string> TransplantableOrgans = new()
    {
        "Brain",
        "Heart",
        "Liver",
        "Lung",
        "Eyes",
        "Stomach"
    };
}
