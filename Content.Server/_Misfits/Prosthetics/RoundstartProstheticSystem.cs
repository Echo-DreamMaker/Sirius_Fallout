using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid;
using Content.Shared._Misfits.Prosthetics;
using Robust.Shared.Containers;

namespace Content.Server._Misfits.Prosthetics;

/// <summary>
/// Applies the whole body-part replacements selected in a humanoid's roundstart profile.
/// </summary>
public sealed partial class RoundstartProstheticSystem : EntitySystem
{
    private static readonly Dictionary<HumanoidVisualLayers, (BodyPartType Type, BodyPartSymmetry Symmetry)> PartByLayer = new()
    {
        [HumanoidVisualLayers.LHand] = (BodyPartType.Hand, BodyPartSymmetry.Left),
        [HumanoidVisualLayers.RHand] = (BodyPartType.Hand, BodyPartSymmetry.Right),
        [HumanoidVisualLayers.LFoot] = (BodyPartType.Foot, BodyPartSymmetry.Left),
        [HumanoidVisualLayers.RFoot] = (BodyPartType.Foot, BodyPartSymmetry.Right),
    };

    private static readonly HashSet<string> AllowedProsthetics =
    [
        "MisfitsProstheticSimpleLeftHand",
        "MisfitsProstheticVaultTecLeftHand",
        "MisfitsProstheticNCRLeftHand",
        "MisfitsProstheticSimpleRightHand",
        "MisfitsProstheticVaultTecRightHand",
        "MisfitsProstheticNCRRightHand",
        "MisfitsProstheticSimpleLeftFoot",
        "MisfitsProstheticVaultTecLeftFoot",
        "MisfitsProstheticSimpleRightFoot",
        "MisfitsProstheticVaultTecRightFoot",
    ];

    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();
        // #Misfits Fixed - Subscribe to our own RoundstartProfileLoadedEvent instead of the Shitmed
        // ProfileLoadFinishedEvent: SharedBodySystem already owns that directed (BodyComponent, event)
        // subscription, and a second one crashes the event bus with "Duplicate Subscriptions".
        SubscribeLocalEvent<BodyComponent, RoundstartProfileLoadedEvent>(OnProfileLoadFinished,
            after: [typeof(SharedBodySystem)]);
    }

    private void OnProfileLoadFinished(EntityUid uid, BodyComponent body, RoundstartProfileLoadedEvent args)
    {
        if (!TryComp<HumanoidAppearanceComponent>(uid, out var appearance)
            || appearance.LastProfileLoaded is not { } profile)
            return;

        foreach (var (layer, info) in profile.Appearance.CustomBaseLayers)
        {
            if (info.Id is not { } prototypeId
                || !AllowedProsthetics.Contains(prototypeId.ToString())
                || !PartByLayer.TryGetValue(layer, out var expected))
                continue;

            ReplacePart(uid, body, layer, prototypeId.ToString(), expected.Type, expected.Symmetry);
        }
    }

    private void ReplacePart(
        EntityUid bodyUid,
        BodyComponent body,
        HumanoidVisualLayers layer,
        string prototypeId,
        BodyPartType expectedType,
        BodyPartSymmetry expectedSymmetry)
    {
        var existing = _body.GetBodyChildren(bodyUid, body)
            .FirstOrDefault(part => part.Component.PartType == expectedType && part.Component.Symmetry == expectedSymmetry);

        if (existing.Id == EntityUid.Invalid
            || _body.GetParentPartAndSlotOrNull(existing.Id) is not { } parentSlot
            || !_containers.TryGetContainingContainer((existing.Id, null, null), out var oldContainer))
        {
            Log.Warning("Could not find the body slot for roundstart prosthetic {Prototype} on {Entity} ({Layer}).",
                prototypeId, bodyUid, layer);
            return;
        }

        if (!_containers.Remove(existing.Id, oldContainer, reparent: false, force: true))
            return;

        var replacement = Spawn(prototypeId, Transform(bodyUid).Coordinates);
        if (_body.AttachPart(parentSlot.Parent, parentSlot.Slot, replacement))
        {
            QueueDel(existing.Id);
            return;
        }

        QueueDel(replacement);
        _body.AttachPart(parentSlot.Parent, parentSlot.Slot, existing.Id);
        Log.Warning("Could not attach roundstart prosthetic {Prototype} to {Entity} ({Layer}).",
            prototypeId, bodyUid, layer);
    }
}
