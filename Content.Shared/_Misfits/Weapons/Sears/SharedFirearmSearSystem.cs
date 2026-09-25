using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;

namespace Content.Shared._Misfits.Weapons.Sears;

/// <summary>
/// Installs consumable burst and automatic sears into explicitly compatible firearms.
/// </summary>
public sealed class SharedFirearmSearSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FirearmSearComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FirearmSearComponent, InstallFirearmSearDoAfterEvent>(OnInstallFinished);
        SubscribeLocalEvent<InstalledFirearmSearComponent, ComponentInit>(OnInstalledInit);
    }

    private void OnAfterInteract(Entity<FirearmSearComponent> sear, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !TryComp<GunComponent>(target, out var gun))
        {
            return;
        }

        args.Handled = true;

        if (!CanInstall(target, sear.Comp.Mode, gun, out var reason))
        {
            _popup.PopupClient(reason, target, args.User);
            return;
        }

        _popup.PopupClient("You begin installing the sear.", target, args.User);
        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            sear.Comp.InstallTime,
            new InstallFirearmSearDoAfterEvent(),
            sear,
            target: target,
            used: sear)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            CancelDuplicate = true,
            NeedHand = true,
        });
    }

    private void OnInstallFinished(Entity<FirearmSearComponent> sear, ref InstallFirearmSearDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target ||
            !TryComp<GunComponent>(target, out var gun))
        {
            return;
        }

        args.Handled = true;

        if (!CanInstall(target, sear.Comp.Mode, gun, out var reason))
        {
            _popup.PopupClient(reason, target, args.User);
            return;
        }

        if (!_net.IsServer)
            return;

        AddComp(target, new InstalledFirearmSearComponent { Mode = sear.Comp.Mode });
        ApplyMode(target, gun, sear.Comp.Mode);
        QueueDel(sear);
        _popup.PopupEntity("The sear locks permanently into the firearm's action.", target, args.User);
    }

    private void OnInstalledInit(Entity<InstalledFirearmSearComponent> ent, ref ComponentInit args)
    {
        if (TryComp<GunComponent>(ent, out var gun))
            ApplyMode(ent, gun, ent.Comp.Mode);
    }

    private bool CanInstall(EntityUid target, SelectiveFire mode, GunComponent gun, out string reason)
    {
        reason = string.Empty;

        if (!TryComp<FirearmSearCompatibleComponent>(target, out var compatible) ||
            mode == SelectiveFire.Burst && !compatible.AllowBurst ||
            mode == SelectiveFire.FullAuto && !compatible.AllowFullAuto)
        {
            reason = "This firearm is not compatible with that sear.";
            return false;
        }

        if (HasComp<InstalledFirearmSearComponent>(target))
        {
            reason = "This firearm already has a permanent sear upgrade.";
            return false;
        }

        if ((gun.AvailableModes & mode) != 0)
        {
            reason = "This firearm already supports that fire mode.";
            return false;
        }

        if (SlotHasItem(target, "gun_magazine") || SlotHasItem(target, "gun_chamber"))
        {
            reason = "Unload the firearm completely before installing a sear.";
            return false;
        }

        return true;
    }

    private bool SlotHasItem(EntityUid uid, string slotId)
    {
        return _itemSlots.TryGetSlot(uid, slotId, out var slot) && slot.HasItem;
    }

    private void ApplyMode(EntityUid uid, GunComponent gun, SelectiveFire mode)
    {
        _gun.AddFireMode(uid, mode, gun);
    }
}
