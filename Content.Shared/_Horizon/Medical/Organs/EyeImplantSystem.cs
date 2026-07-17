using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Overlays;
using Content.Shared.Chemistry.Components;

namespace Content.Shared._Horizon.Medical.Organs;

public sealed class EyeImplantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EyeImplantWeldingComponent, OrganAddedToBodyEvent>(OnWeldingImplantAdded);
        SubscribeLocalEvent<EyeImplantDiagnosticComponent, OrganAddedToBodyEvent>(OnDiagnosticImplantAdded);
        SubscribeLocalEvent<EyeImplantMedicalComponent, OrganAddedToBodyEvent>(OnMedicalImplantAdded);
        SubscribeLocalEvent<EyeImplantChemicalComponent, OrganAddedToBodyEvent>(OnChemicalImplantAdded);
        SubscribeLocalEvent<EyeImplantSecurityComponent, OrganAddedToBodyEvent>(OnSecurityImplantAdded);
        SubscribeLocalEvent<EyeImplantSyndieComponent, OrganAddedToBodyEvent>(OnSyndieImplantAdded);

        SubscribeLocalEvent<EyeImplantWeldingComponent, OrganRemovedFromBodyEvent>(OnWeldingImplantRemoved);
        SubscribeLocalEvent<EyeImplantDiagnosticComponent, OrganRemovedFromBodyEvent>(OnDiagnosticImplantRemoved);
        SubscribeLocalEvent<EyeImplantMedicalComponent, OrganRemovedFromBodyEvent>(OnMedicalImplantRemoved);
        SubscribeLocalEvent<EyeImplantChemicalComponent, OrganRemovedFromBodyEvent>(OnChemicalImplantRemoved);
        SubscribeLocalEvent<EyeImplantSecurityComponent, OrganRemovedFromBodyEvent>(OnSecurityImplantRemoved);
        SubscribeLocalEvent<EyeImplantSyndieComponent, OrganRemovedFromBodyEvent>(OnSyndieImplantRemoved);
    }

    // Welding Implant
    private void OnWeldingImplantAdded(Entity<EyeImplantWeldingComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!HasComp<EyeProtectionComponent>(args.Body))
            AddComp<EyeProtectionComponent>(args.Body);
    }

    private void OnWeldingImplantRemoved(Entity<EyeImplantWeldingComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var hasOtherProtection = false;
        var entUid = ent.Owner;
        foreach (var organ in EntityManager.GetComponents<OrganComponent>(args.OldBody))
        {
            if (organ.Body == args.OldBody &&
                HasComp<EyeImplantWeldingComponent>(organ.Owner) &&
                organ.Owner != entUid)
            {
                hasOtherProtection = true;
                break;
            }
        }

        if (!hasOtherProtection)
            RemComp<EyeProtectionComponent>(args.OldBody);
    }

    // Diagnostic Implant
    private void OnDiagnosticImplantAdded(Entity<EyeImplantDiagnosticComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!HasComp<ShowHealthBarsComponent>(args.Body))
        {
            var comp = AddComp<ShowHealthBarsComponent>(args.Body);
            comp.DamageContainers = new List<string> { "Inorganic", "Silicon" };
            Dirty(args.Body, comp);
        }
    }

    private void OnDiagnosticImplantRemoved(Entity<EyeImplantDiagnosticComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var hasOther = false;
        var entUid = ent.Owner;
        foreach (var organ in EntityManager.GetComponents<OrganComponent>(args.OldBody))
        {
            if (organ.Body == args.OldBody &&
                HasComp<EyeImplantDiagnosticComponent>(organ.Owner) &&
                organ.Owner != entUid)
            {
                hasOther = true;
                break;
            }
        }

        if (!hasOther)
            RemComp<ShowHealthBarsComponent>(args.OldBody);
    }

    private void OnMedicalImplantAdded(Entity<EyeImplantMedicalComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!HasComp<ShowHealthBarsComponent>(args.Body))
        {
            var barsComp = AddComp<ShowHealthBarsComponent>(args.Body);
            barsComp.DamageContainers = new List<string> { "Biological" };
            Dirty(args.Body, barsComp);
        }

        if (!HasComp<ShowHealthIconsComponent>(args.Body))
        {
            var iconsComp = AddComp<ShowHealthIconsComponent>(args.Body);
            iconsComp.DamageContainers = new List<string> { "Biological" };
            Dirty(args.Body, iconsComp);
        }
    }

    private void OnMedicalImplantRemoved(Entity<EyeImplantMedicalComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var hasOther = false;
        var entUid = ent.Owner;
        foreach (var organ in EntityManager.GetComponents<OrganComponent>(args.OldBody))
        {
            if (organ.Body == args.OldBody &&
                HasComp<EyeImplantMedicalComponent>(organ.Owner) &&
                organ.Owner != entUid)
            {
                hasOther = true;
                break;
            }
        }

        if (!hasOther)
        {
            RemComp<ShowHealthBarsComponent>(args.OldBody);
            RemComp<ShowHealthIconsComponent>(args.OldBody);
        }
    }

    // Chemical Implant
    private void OnChemicalImplantAdded(Entity<EyeImplantChemicalComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!HasComp<SolutionScannerComponent>(args.Body))
            AddComp<SolutionScannerComponent>(args.Body);
    }

    private void OnChemicalImplantRemoved(Entity<EyeImplantChemicalComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var hasOther = false;
        var entUid = ent.Owner;
        foreach (var organ in EntityManager.GetComponents<OrganComponent>(args.OldBody))
        {
            if (organ.Body == args.OldBody &&
                HasComp<EyeImplantChemicalComponent>(organ.Owner) &&
                organ.Owner != entUid)
            {
                hasOther = true;
                break;
            }
        }

        if (!hasOther)
            RemComp<SolutionScannerComponent>(args.OldBody);
    }

    // Security Implant
    private void OnSecurityImplantAdded(Entity<EyeImplantSecurityComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!HasComp<ShowJobIconsComponent>(args.Body))
            AddComp<ShowJobIconsComponent>(args.Body);

        if (!HasComp<ShowMindShieldIconsComponent>(args.Body))
            AddComp<ShowMindShieldIconsComponent>(args.Body);

        if (!HasComp<ShowCriminalRecordIconsComponent>(args.Body))
            AddComp<ShowCriminalRecordIconsComponent>(args.Body);
    }

    private void OnSecurityImplantRemoved(Entity<EyeImplantSecurityComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var hasOther = false;
        var entUid = ent.Owner;
        foreach (var organ in EntityManager.GetComponents<OrganComponent>(args.OldBody))
        {
            if (organ.Body == args.OldBody &&
                HasComp<EyeImplantSecurityComponent>(organ.Owner) &&
                organ.Owner != entUid)
            {
                hasOther = true;
                break;
            }
        }

        if (!hasOther)
        {
            RemComp<ShowJobIconsComponent>(args.OldBody);
            RemComp<ShowMindShieldIconsComponent>(args.OldBody);
            RemComp<ShowCriminalRecordIconsComponent>(args.OldBody);
        }
    }

    // Syndie Implant
    private void OnSyndieImplantAdded(Entity<EyeImplantSyndieComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!HasComp<ShowHealthBarsComponent>(args.Body))
        {
            var barsComp = AddComp<ShowHealthBarsComponent>(args.Body);
            barsComp.DamageContainers = new List<string> { "Biological" };
            Dirty(args.Body, barsComp);
        }

        if (!HasComp<ShowHealthIconsComponent>(args.Body))
        {
            var iconsComp = AddComp<ShowHealthIconsComponent>(args.Body);
            iconsComp.DamageContainers = new List<string> { "Biological" };
            Dirty(args.Body, iconsComp);
        }

        if (!HasComp<ShowJobIconsComponent>(args.Body))
            AddComp<ShowJobIconsComponent>(args.Body);

        if (!HasComp<ShowMindShieldIconsComponent>(args.Body))
            AddComp<ShowMindShieldIconsComponent>(args.Body);

        if (!HasComp<ShowCriminalRecordIconsComponent>(args.Body))
            AddComp<ShowCriminalRecordIconsComponent>(args.Body);

        if (!HasComp<ShowSyndicateIconsComponent>(args.Body))
            AddComp<ShowSyndicateIconsComponent>(args.Body);
    }

    private void OnSyndieImplantRemoved(Entity<EyeImplantSyndieComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        var hasOther = false;
        var entUid = ent.Owner;
        foreach (var organ in EntityManager.GetComponents<OrganComponent>(args.OldBody))
        {
            if (organ.Body == args.OldBody &&
                HasComp<EyeImplantSyndieComponent>(organ.Owner) &&
                organ.Owner != entUid)
            {
                hasOther = true;
                break;
            }
        }

        if (!hasOther)
        {
            RemComp<ShowHealthBarsComponent>(args.OldBody);
            RemComp<ShowHealthIconsComponent>(args.OldBody);
            RemComp<ShowJobIconsComponent>(args.OldBody);
            RemComp<ShowMindShieldIconsComponent>(args.OldBody);
            RemComp<ShowCriminalRecordIconsComponent>(args.OldBody);
            RemComp<ShowSyndicateIconsComponent>(args.OldBody);
        }
    }
}
