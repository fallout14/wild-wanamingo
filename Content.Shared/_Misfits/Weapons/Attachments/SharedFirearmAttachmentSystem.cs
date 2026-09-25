using Content.Shared._Misfits.Weapons.Attachments.Components;
using Content.Shared._Misfits.Scope;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Content.Shared.Wieldable;
using Content.Shared.Verbs;

namespace Content.Shared._Misfits.Weapons.Attachments;

/// <summary>
/// Applies firearm attachment effects from items installed in a host's attachment slots.
/// Runs in shared code so predicted shots use the same sound, flash, and melee modifiers.
/// </summary>
public sealed class SharedFirearmAttachmentSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private readonly HashSet<EntityUid> _pendingRefresh = new();
    private readonly Dictionary<EntityUid, Angle> _baseMeleeAnimationRotations = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FirearmAttachmentHostComponent, ComponentInit>(OnHostInit);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, EntInsertedIntoContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, EntRemovedFromContainerMessage>(OnContainerModified);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, GunRefreshModifiersEvent>(OnGunRefreshModifiers);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, GunMuzzleFlashAttemptEvent>(OnMuzzleFlashAttempt);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, GetMeleeDamageEvent>(OnGetMeleeDamage,
            after: [typeof(WieldableSystem)]);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, IsGunSuppressedEvent>(OnIsGunSuppressed);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, ToggleActionEvent>(OnToggleOpticAction);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, ScopeCycleZoomLevelEvent>(OnCycleOpticAction);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, ComponentShutdown>(OnHostShutdown);
    }

    private void OnHostInit(Entity<FirearmAttachmentHostComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent, ent.Comp.MuzzleSlotId, ent.Comp.MuzzleSlot);

        if (ent.Comp.EnableOpticSlot)
            _itemSlots.AddItemSlot(ent, ent.Comp.OpticSlotId, ent.Comp.OpticSlot);

        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
            _baseMeleeAnimationRotations[ent] = melee.WideAnimationRotation;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in _pendingRefresh)
        {
            if (TryComp<GunComponent>(uid, out var gun))
                _gun.RefreshModifiers((uid, gun));

            RefreshMeleeAnimation(uid);
        }

        _pendingRefresh.Clear();
    }

    private void OnContainerModified(
        Entity<FirearmAttachmentHostComponent> ent,
        ref EntInsertedIntoContainerMessage args)
    {
        RefreshGunIfAttachmentChanged(ent, args.Container.ID);

        if (args.Container.ID == ent.Comp.OpticSlotId)
            RefreshOpticActions(ent);
    }

    private void OnContainerModified(
        Entity<FirearmAttachmentHostComponent> ent,
        ref EntRemovedFromContainerMessage args)
    {
        RefreshGunIfAttachmentChanged(ent, args.Container.ID);

        if (args.Container.ID == ent.Comp.OpticSlotId)
            RefreshOpticActions(ent, removing: true);
    }

    private void OnGetItemActions(Entity<FirearmAttachmentHostComponent> ent, ref GetItemActionsEvent args)
    {
        if (!TryGetOptic(ent, out var optic))
            return;

        args.AddAction(ref ent.Comp.OpticToggleActionEntity, ent.Comp.OpticToggleAction);

        if (TryComp<ScopeComponent>(optic, out var scope) && scope.ZoomLevels.Count > 1)
            args.AddAction(ref ent.Comp.OpticCycleActionEntity, ent.Comp.OpticCycleAction);

        Dirty(ent);
    }

    private void OnToggleOpticAction(Entity<FirearmAttachmentHostComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled || !TryGetOptic(ent, out var optic))
            return;

        // Forward the rifle-owned proxy action to the actual contained scope.
        args.Handled = true;
        var forwarded = args;
        forwarded.Handled = false;
        RaiseLocalEvent(optic, forwarded);
    }

    private void OnCycleOpticAction(Entity<FirearmAttachmentHostComponent> ent, ref ScopeCycleZoomLevelEvent args)
    {
        if (args.Handled || !TryGetOptic(ent, out var optic))
            return;

        args.Handled = true;
        var forwarded = args;
        forwarded.Handled = false;
        RaiseLocalEvent(optic, forwarded);
    }

    private void RefreshOpticActions(Entity<FirearmAttachmentHostComponent> ent, bool removing = false)
    {
        var user = Transform(ent).ParentUid;
        if (!_hands.IsHolding(user, ent, out _))
            return;

        // The gun is the action provider, so removing or replacing its optic can refresh
        // the granted action without forcing the player to drop and pick the rifle up.
        _actions.RemoveProvidedActions(user, ent);
        if (removing || !TryGetOptic(ent, out var optic))
            return;

        if (!_actionContainer.EnsureAction(
                ent,
                ref ent.Comp.OpticToggleActionEntity,
                ent.Comp.OpticToggleAction))
        {
            return;
        }

        var actions = new List<EntityUid> { ent.Comp.OpticToggleActionEntity.Value };

        if (TryComp<ScopeComponent>(optic, out var scope) && scope.ZoomLevels.Count > 1 &&
            _actionContainer.EnsureAction(
                ent,
                ref ent.Comp.OpticCycleActionEntity,
                ent.Comp.OpticCycleAction))
        {
            actions.Add(ent.Comp.OpticCycleActionEntity.Value);
        }

        _actions.GrantActions(user, actions, ent);
        Dirty(ent);
    }

    private void RefreshGunIfAttachmentChanged(Entity<FirearmAttachmentHostComponent> ent, string containerId)
    {
        if (containerId != ent.Comp.MuzzleSlotId && containerId != ent.Comp.OpticSlotId)
            return;

        var visualEvent = new FirearmAttachmentVisualsChangedEvent();
        RaiseLocalEvent(ent, ref visualEvent);

        // Container removal events are raised before the item leaves the slot. Refresh on the
        // next frame so both insertion and removal observe the final container contents.
        _pendingRefresh.Add(ent);
    }

    private void OnGunRefreshModifiers(
        Entity<FirearmAttachmentHostComponent> ent,
        ref GunRefreshModifiersEvent args)
    {
        if (TryGetMuzzleAttachment(ent, out var attachment) &&
            TryComp<SuppressorAttachmentComponent>(attachment, out var suppressor))
        {
            args.SoundGunshot = suppressor.SoundGunshot;
        }
    }

    private void OnMuzzleFlashAttempt(
        Entity<FirearmAttachmentHostComponent> ent,
        ref GunMuzzleFlashAttemptEvent args)
    {
        if (TryGetMuzzleAttachment(ent, out var attachment) &&
            TryComp<SuppressorAttachmentComponent>(attachment, out var suppressor) &&
            suppressor.HideMuzzleFlash)
        {
            args.Cancelled = true;
        }
    }

    private void OnGetMeleeDamage(
        Entity<FirearmAttachmentHostComponent> ent,
        ref GetMeleeDamageEvent args)
    {
        if (TryGetMuzzleAttachment(ent, out var attachment) &&
            TryComp<BayonetAttachmentComponent>(attachment, out var bayonet))
        {
            // A mounted bayonet replaces the rifle-butt bash instead of adding to it.
            // Clear after wield modifiers so no inherited blunt damage remains.
            args.Damage.DamageDict.Clear();
            args.Damage += bayonet.Damage;
        }
    }

    private void OnMeleeHit(
        Entity<FirearmAttachmentHostComponent> ent,
        ref MeleeHitEvent args)
    {
        if (TryGetMuzzleAttachment(ent, out var attachment) &&
            TryComp<BayonetAttachmentComponent>(attachment, out var bayonet))
        {
            args.HitSoundOverride = bayonet.HitSound;
        }
    }

    private void OnIsGunSuppressed(
        Entity<FirearmAttachmentHostComponent> ent,
        ref IsGunSuppressedEvent args)
    {
        if (TryGetMuzzleAttachment(ent, out var attachment) &&
            HasComp<SuppressorAttachmentComponent>(attachment))
        {
            args.Suppressed = true;
        }
    }

    private void OnGetVerbs(
        Entity<FirearmAttachmentHostComponent> ent,
        ref GetVerbsEvent<Verb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        if (_itemSlots.TryGetSlot(ent, ent.Comp.MuzzleSlotId, out var muzzleSlot) &&
            muzzleSlot.Item is { } muzzleAttachment &&
            _itemSlots.CanEject(ent, user, muzzleSlot))
        {
            args.Verbs.Add(new Verb
            {
                Text = "Remove muzzle attachment",
                IconEntity = GetNetEntity(muzzleAttachment),
                Act = () => _itemSlots.TryEjectToHands(ent, muzzleSlot, user, excludeUserAudio: true),
            });
        }

        if (ent.Comp.EnableOpticSlot &&
            _itemSlots.TryGetSlot(ent, ent.Comp.OpticSlotId, out var opticSlot) &&
            opticSlot.Item is { } opticAttachment &&
            _itemSlots.CanEject(ent, user, opticSlot))
        {
            args.Verbs.Add(new Verb
            {
                Text = "Remove optic",
                IconEntity = GetNetEntity(opticAttachment),
                Act = () => _itemSlots.TryEjectToHands(ent, opticSlot, user, excludeUserAudio: true),
            });
        }
    }

    private void OnHostShutdown(
        Entity<FirearmAttachmentHostComponent> ent,
        ref ComponentShutdown args)
    {
        _pendingRefresh.Remove(ent);
        _baseMeleeAnimationRotations.Remove(ent);
        _itemSlots.RemoveItemSlot(ent, ent.Comp.MuzzleSlot);

        if (ent.Comp.EnableOpticSlot)
            _itemSlots.RemoveItemSlot(ent, ent.Comp.OpticSlot);
    }

    private void RefreshMeleeAnimation(EntityUid uid)
    {
        if (!TryComp<FirearmAttachmentHostComponent>(uid, out var host) ||
            !TryComp<MeleeWeaponComponent>(uid, out var melee))
        {
            return;
        }

        var rotation = _baseMeleeAnimationRotations.GetValueOrDefault(uid, Angle.Zero);

        if (TryGetMuzzleAttachment((uid, host), out var attachment) &&
            TryComp<BayonetAttachmentComponent>(attachment, out var bayonet))
        {
            rotation = bayonet.AnimationRotation;
        }

        if (melee.WideAnimationRotation.Equals(rotation))
            return;

        melee.WideAnimationRotation = rotation;
        Dirty(uid, melee);
    }

    private bool TryGetMuzzleAttachment(
        Entity<FirearmAttachmentHostComponent> ent,
        out EntityUid attachment)
    {
        attachment = default;

        if (!_itemSlots.TryGetSlot(ent, ent.Comp.MuzzleSlotId, out var slot) ||
            slot.Item is not { } item)
        {
            return false;
        }

        attachment = item;
        return true;
    }

    private bool TryGetOptic(Entity<FirearmAttachmentHostComponent> ent, out EntityUid optic)
    {
        optic = default;

        if (!ent.Comp.EnableOpticSlot ||
            !_itemSlots.TryGetSlot(ent, ent.Comp.OpticSlotId, out var slot) ||
            slot.Item is not { } item)
        {
            return false;
        }

        optic = item;
        return true;
    }
}

/// <summary>
/// Raised on a gun to determine whether systems such as distant gunshot audio should treat it as suppressed.
/// </summary>
[ByRefEvent]
public record struct IsGunSuppressedEvent(bool Suppressed = false);

/// <summary>
/// Raised when a firearm's installed attachment visuals need to be refreshed.
/// </summary>
[ByRefEvent]
public record struct FirearmAttachmentVisualsChangedEvent;
