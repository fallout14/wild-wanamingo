using Content.Shared._Misfits.Weapons.Attachments;
using Content.Shared._Misfits.Weapons.Attachments.Components;
using Content.Shared.Containers.ItemSlots;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._Misfits.Weapons.Attachments;

/// <summary>
/// Draws installed muzzle attachments and optics as offset layers on their firearm.
/// </summary>
public sealed class FirearmAttachmentVisualizerSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly HashSet<EntityUid> _pendingRefresh = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FirearmAttachmentHostComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FirearmAttachmentHostComponent, FirearmAttachmentVisualsChangedEvent>(OnVisualsChanged);
    }

    private void OnStartup(Entity<FirearmAttachmentHostComponent> ent, ref ComponentStartup args)
    {
        _pendingRefresh.Add(ent);
    }

    private void OnVisualsChanged(
        Entity<FirearmAttachmentHostComponent> ent,
        ref FirearmAttachmentVisualsChangedEvent args)
    {
        _pendingRefresh.Add(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        foreach (var uid in _pendingRefresh)
            Refresh(uid);

        _pendingRefresh.Clear();
    }

    private void Refresh(EntityUid uid)
    {
        if (!TryComp<FirearmAttachmentHostComponent>(uid, out var host) ||
            !TryComp<SpriteComponent>(uid, out var sprite))
        {
            return;
        }

        // Refresh independently: an optic can be mounted without a muzzle attachment.
        var opticLayer = sprite.LayerMapReserveBlank(FirearmAttachmentVisualLayers.Optic);
        _sprite.LayerSetVisible((uid, sprite), opticLayer, false);
        if (host.EnableOpticSlot && host.OpticVisualState != null &&
            _itemSlots.TryGetSlot(uid, host.OpticSlotId, out var opticSlot) &&
            opticSlot.Item != null)
        {
            _sprite.LayerSetSprite((uid, sprite), opticLayer,
                new SpriteSpecifier.Rsi(host.OpticVisualRsi, host.OpticVisualState));
            _sprite.LayerSetOffset((uid, sprite), opticLayer, host.OpticVisualOffset);
            _sprite.LayerSetRotation((uid, sprite), opticLayer, host.OpticVisualRotation);
            _sprite.LayerSetVisible((uid, sprite), opticLayer, true);
        }

        var layer = sprite.LayerMapReserveBlank(FirearmAttachmentVisualLayers.Muzzle);
        _sprite.LayerSetVisible((uid, sprite), layer, false);

        if (!host.ShowMuzzleVisual || !_itemSlots.TryGetSlot(uid, host.MuzzleSlotId, out var slot) ||
            slot.Item is not { } attachment)
        {
            return;
        }

        string? attachmentName = null;
        if (HasComp<SuppressorAttachmentComponent>(attachment))
            attachmentName = "suppressor";
        else if (HasComp<BayonetAttachmentComponent>(attachment))
            attachmentName = "bayonet";

        if (attachmentName == null)
            return;

        var orientation = host.VisualOrientation == FirearmAttachmentVisualOrientation.Horizontal
            ? "horizontal"
            : "diagonal";
        var suppressor = attachmentName == "suppressor";
        var state = (suppressor ? host.SuppressorVisualState : host.BayonetVisualState)
            ?? $"{attachmentName}-{orientation}";
        var offset = (suppressor ? host.SuppressorVisualOffset : host.BayonetVisualOffset)
            ?? host.MuzzleVisualOffset;
        var rotation = suppressor ? host.SuppressorVisualRotation : host.BayonetVisualRotation;

        _sprite.LayerSetSprite((uid, sprite), layer, new SpriteSpecifier.Rsi(host.MuzzleVisualRsi, state));
        _sprite.LayerSetOffset((uid, sprite), layer, offset);
        _sprite.LayerSetRotation((uid, sprite), layer, rotation);
        _sprite.LayerSetVisible((uid, sprite), layer, true);
    }
}

public enum FirearmAttachmentVisualLayers : byte
{
    Muzzle,
    Optic,
}
