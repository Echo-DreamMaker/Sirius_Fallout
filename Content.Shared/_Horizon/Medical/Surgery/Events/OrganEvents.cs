namespace Content.Shared._Horizon.Medical.Surgery.Events;

[ByRefEvent]
public record struct SurgeryOrganImplantationCompleted(EntityUid Body, EntityUid Part, EntityUid Organ);

[ByRefEvent]
public record struct SurgeryOrganExtracted(EntityUid Body, EntityUid Part, EntityUid Organ);
