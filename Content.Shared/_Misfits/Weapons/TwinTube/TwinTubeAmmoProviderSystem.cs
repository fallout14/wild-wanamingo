using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;

namespace Content.Shared._Misfits.Weapons.TwinTube;

/// <summary>
/// Routes ammunition operations to one of two contained ballistic providers.
/// Keeping separate providers preserves each tube's ammunition type, order and capacity.
/// </summary>
public sealed class TwinTubeAmmoProviderSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TwinTubeAmmoProviderComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<TwinTubeAmmoProviderComponent, GetAmmoCountEvent>(OnAmmoCount);
        SubscribeLocalEvent<TwinTubeAmmoProviderComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TwinTubeAmmoProviderComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<TwinTubeAmmoProviderComponent, ExaminedEvent>(OnExamine);
    }

    public EntityUid? GetTube(EntityUid uid, bool right)
    {
        var id = right ? TwinTubeAmmoProviderComponent.RightTube : TwinTubeAmmoProviderComponent.LeftTube;
        return _containers.TryGetContainer(uid, id, out var container) && container is ContainerSlot slot
            ? slot.ContainedEntity
            : null;
    }

    public void SelectTube(Entity<TwinTubeAmmoProviderComponent> ent, bool right)
    {
        ent.Comp.RightSelected = right;
        Dirty(ent);
        RefreshCounter(ent);
    }

    private void OnTakeAmmo(Entity<TwinTubeAmmoProviderComponent> ent, ref TakeAmmoEvent args)
    {
        for (var i = 0; i < args.Shots; i++)
        {
            SelectLoadedTube(ent);
            if (GetTube(ent, ent.Comp.RightSelected) is not { } tube)
                break;

            var before = args.Ammo.Count;
            var shot = new TakeAmmoEvent(1, args.Ammo, args.Coordinates, args.User, args.Rng);
            RaiseLocalEvent(tube, shot);
            args.Reason = shot.Reason;
            if (args.Ammo.Count == before)
                break;
        }
        SelectLoadedTube(ent);
        RefreshCounter(ent);
    }

    private void SelectLoadedTube(Entity<TwinTubeAmmoProviderComponent> ent)
    {
        if (GetCount(ent, ent.Comp.RightSelected).Count == 0 &&
            GetCount(ent, !ent.Comp.RightSelected).Count > 0)
            SelectTube(ent, !ent.Comp.RightSelected);
    }

    private GetAmmoCountEvent GetCount(EntityUid uid, bool right)
    {
        var count = new GetAmmoCountEvent();
        if (GetTube(uid, right) is { } tube)
            RaiseLocalEvent(tube, ref count);
        return count;
    }

    private void OnAmmoCount(Entity<TwinTubeAmmoProviderComponent> ent, ref GetAmmoCountEvent args)
    {
        var left = GetCount(ent, false);
        var right = GetCount(ent, true);
        args.Count = left.Count + right.Count;
        args.Capacity = left.Capacity + right.Capacity;
    }

    private void OnInteractUsing(Entity<TwinTubeAmmoProviderComponent> ent, ref InteractUsingEvent args)
    {
        if (!args.Handled && GetTube(ent, ent.Comp.RightSelected) is { } tube)
            RaiseLocalEvent(tube, args);
        RefreshCounter(ent);
    }

    private void RefreshCounter(EntityUid uid)
    {
        var ev = new UpdateClientAmmoEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    private void OnGetVerbs(Entity<TwinTubeAmmoProviderComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        // The firearm's shared cycle verb handles its action without unloading a live shell.

        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("gun-twin-tube-select-left"),
            Disabled = !ent.Comp.RightSelected,
            Act = () => SelectTube(ent, false),
        });
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("gun-twin-tube-select-right"),
            Disabled = ent.Comp.RightSelected,
            Act = () => SelectTube(ent, true),
        });
    }

    private void OnExamine(Entity<TwinTubeAmmoProviderComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("gun-twin-tube-examine",
            ("left", GetCount(ent, false).Count),
            ("right", GetCount(ent, true).Count),
            ("selected", Loc.GetString(ent.Comp.RightSelected ? "gun-twin-tube-right" : "gun-twin-tube-left"))));
    }
}
