using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Shared._Misfits.Weapons.Ranged;

public sealed class FoldableStockSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _items = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly FoldableSystem _foldable = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FoldableStockComponent, FoldedEvent>(OnFolded);
        SubscribeLocalEvent<FoldableStockComponent, FoldAttemptEvent>(OnFoldAttempt);
        SubscribeLocalEvent<FoldableStockComponent, GunRefreshModifiersEvent>(OnModifiers);
    }

    private void OnFoldAttempt(Entity<FoldableStockComponent> ent, ref FoldAttemptEvent args)
    {
        // Take the gun out of storage/equipment before changing its footprint.
        if (_containers.TryGetContainingContainer(ent.Owner, out var container) &&
            !_hands.IsHolding(container.Owner, ent.Owner, out _))
            args.Cancelled = true;
    }

    private void OnFolded(Entity<FoldableStockComponent> ent, ref FoldedEvent args)
    {
        _items.SetSize(ent, args.IsFolded ? ent.Comp.FoldedSize : ent.Comp.UnfoldedSize);
        _guns.RefreshModifiers((ent.Owner, null));
    }

    private void OnModifiers(Entity<FoldableStockComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (!_foldable.IsFolded(ent))
            return;

        args.AngleIncrease *= ent.Comp.FoldedRecoilMultiplier;
        args.CameraRecoilScalar *= ent.Comp.FoldedRecoilMultiplier;
    }
}
