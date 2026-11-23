using Content.Shared.Examine;

namespace Content.Shared.Construction.Steps
{
    [DataDefinition]
    public sealed partial class ComponentConstructionGraphStep : ArbitraryInsertConstructionGraphStep
    {
        [DataField("component")] public string Component { get; private set; } = string.Empty;

        public override bool EntityValid(EntityUid uid, IEntityManager entityManager, IComponentFactory compFactory)
        {
            foreach (var component in entityManager.GetComponents(uid))
            {
                if (compFactory.GetComponentName(component.GetType()) == Component)
                    return true;
            }

            return false;
        }

        public override void DoExamine(ExaminedEvent examinedEvent)
        {
            if (string.IsNullOrEmpty(Name))
            {
                examinedEvent.PushMarkup(Loc.GetString(
                    "construction-insert-entity-with-component",
                    ("componentName", Component)));// Terrible.
                return;
            }

            // Sirius: localize ingredient/step name when a step-* key exists.
            var entityName = Loc.TryGetString($"step-{Name.Replace(' ', '-')}", out var locName) ? locName : Name;
            examinedEvent.PushMarkup(Loc.GetString("construction-insert-exact-entity", ("entityName", entityName)));
        }
    }
}
