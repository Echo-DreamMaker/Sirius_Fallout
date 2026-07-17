using Content.Shared.Actions;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;

namespace Content.Shared._Horizon.NightVision;

public sealed class NVSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = null!;
    [Dependency] private readonly InventorySystem _inventory = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NVComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<NVComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<NVComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NVComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NVComponent, InventoryRelayedEvent<CanVisionAttemptEvent>>(OnNVTrySee);
        SubscribeLocalEvent<NVComponent, OrganAddedToBodyEvent>(OnOrganAdded);
        SubscribeLocalEvent<NVComponent, OrganRemovedFromBodyEvent>(OnOrganRemoved);
        SubscribeLocalEvent<NightVisionUserComponent, NVInstantActionEvent>(OnActionToggle);
    }

    private void OnNVTrySee(EntityUid uid, NVComponent component, InventoryRelayedEvent<CanVisionAttemptEvent> args)
    {
        args.Args.Cancel();
    }

    private void OnEquipped(EntityUid uid, NVComponent component, GotEquippedEvent args)
    {
        if (args.Slot != "eyes" && args.Slot != "mask" && args.Slot != "head")
            return;

        if (!TryComp<NightVisionUserComponent>(args.Equipee, out var nvComp))
            return;

        UpdateNightVision(args.Equipee);
        _actionsSystem.AddAction(args.Equipee, ref component.ActionContainer, component.ActionProto);
    }

    private void OnUnequipped(EntityUid uid, NVComponent component, GotUnequippedEvent args)
    {
        if (args.Slot != "eyes" && args.Slot != "mask" && args.Slot != "head")
            return;

        if (!TryComp<NightVisionUserComponent>(args.Equipee, out var nvComp))
            return;

        UpdateNightVision(args.Equipee);
        _actionsSystem.RemoveAction(args.Equipee, component.ActionContainer);
    }

    private void OnComponentInit(EntityUid uid, NVComponent component, ComponentInit args)
    {
        if (TryComp<OrganComponent>(uid, out var organ) && organ.Body is { } body)
        {
            if (!TryComp<NightVisionUserComponent>(body, out var nvComp))
            {
                nvComp = AddComp<NightVisionUserComponent>(body);
                nvComp.NightVisionColor = Color.Green;
                nvComp.IsToggle = true;
                Dirty(body, nvComp);
            }
            _actionsSystem.AddAction(body, ref nvComp.ActionContainer, component.ActionProto);
            UpdateNightVision(body);
            return;
        }

        if (!TryComp<NightVisionUserComponent>(uid, out var userComp))
            return;

        UpdateNightVision(uid);
        _actionsSystem.AddAction(uid, ref component.ActionContainer, component.ActionProto);
    }

    private void OnComponentRemove(EntityUid uid, NVComponent component, ComponentRemove args)
    {
        if (TryComp<OrganComponent>(uid, out var organ) && organ.Body is { } body)
        {
            if (TryComp<NightVisionUserComponent>(body, out var nvComp))
            {
                _actionsSystem.RemoveAction(body, nvComp.ActionContainer);
            }
            return;
        }

        if (TryComp<NightVisionUserComponent>(uid, out var userComp))
        {
            _actionsSystem.RemoveAction(uid, component.ActionContainer);
        }
    }

    private void OnOrganAdded(Entity<NVComponent> ent, ref OrganAddedToBodyEvent args)
    {
        if (!TryComp<NightVisionUserComponent>(args.Body, out var nvComp))
        {
            nvComp = AddComp<NightVisionUserComponent>(args.Body);
            nvComp.NightVisionColor = Color.Green;
            nvComp.IsToggle = true;
            Dirty(args.Body, nvComp);
        }

        _actionsSystem.AddAction(args.Body, ref nvComp.ActionContainer, ent.Comp.ActionProto);
        UpdateNightVision(args.Body);
    }

    private void OnOrganRemoved(Entity<NVComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        if (!TryComp<NightVisionUserComponent>(args.OldBody, out var nvComp))
            return;

        _actionsSystem.RemoveAction(args.OldBody, nvComp.ActionContainer);

        var hasOtherNV = false;
        var entUid = ent.Owner;

        foreach (var organ in EntityManager.GetComponents<OrganComponent>(args.OldBody))
        {
            if (organ.Body == args.OldBody &&
                HasComp<NVComponent>(organ.Owner) &&
                organ.Owner != entUid)
            {
                hasOtherNV = true;
                break;
            }
        }

        if (!hasOtherNV && TryComp<InventoryComponent>(args.OldBody, out var invComp))
        {
            foreach (var slot in invComp.Slots)
            {
                if (_inventory.TryGetSlotEntity(args.OldBody, slot.Name, out var slotEntity))
                {
                    if (HasComp<NVComponent>(slotEntity.Value))
                    {
                        hasOtherNV = true;
                        break;
                    }
                }
            }
        }

        if (!hasOtherNV)
        {
            RemComp<NightVisionUserComponent>(args.OldBody);
        }
        else
        {
            UpdateNightVision(args.OldBody);
        }
    }

    private void OnActionToggle(EntityUid uid, NightVisionUserComponent component, NVInstantActionEvent args)
    {
        component.IsNightVision = !component.IsNightVision;
        var changeEvent = new NightVisionnessChangedEvent(component.IsNightVision);
        RaiseLocalEvent(uid, ref changeEvent);
        Dirty(uid, component);
    }

    public void UpdateNightVision(EntityUid uid, NightVisionUserComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        var canVisionEvent = new CanVisionAttemptEvent();
        RaiseLocalEvent(uid, canVisionEvent);

        component.IsNightVision = canVisionEvent.NightVision;
        Dirty(uid, component);
    }
}

[ByRefEvent]
public record struct NightVisionnessChangedEvent(bool NightVision);

public sealed class CanVisionAttemptEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public bool NightVision => Cancelled;
    public SlotFlags TargetSlots => SlotFlags.EYES | SlotFlags.MASK | SlotFlags.HEAD;
}
