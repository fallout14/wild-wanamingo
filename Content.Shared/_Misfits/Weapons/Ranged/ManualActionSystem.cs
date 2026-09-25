using Content.Shared.ActionBlocker;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Shared._Misfits.Weapons.Ranged;

public sealed class ManualActionSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ManualActionComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<ManualActionComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<ManualActionComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<Verb>>(OnCycleVerb);

        CommandBinds.Builder
            .Bind(ContentKeyFunctions.CycleFirearmAction,
                InputCmdHandler.FromDelegate(OnCyclePressed, OnCycleReleased, handle: false, outsidePrediction: false))
            .Register<ManualActionSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<ManualActionSystem>();
        base.Shutdown();
    }

    private void OnAttemptShoot(Entity<ManualActionComponent> ent, ref AttemptShootEvent args)
    {
        if (ent.Comp.NeedsCycle && IsSlamFiring(args.User, ent.Owner))
            TryCycle(args.User);

        if (!ent.Comp.NeedsCycle)
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("gun-manual-action-needs-cycle");
    }

    private void OnShot(Entity<ManualActionComponent> ent, ref GunShotEvent args)
    {
        ent.Comp.NeedsCycle = true;
        Dirty(ent);
    }

    private void OnCyclePressed(ICommonSession? session)
    {
        if (session?.AttachedEntity is { } user)
        {
            SetCycleHeld(user, true);
            TryCycle(user);
        }
    }

    private void OnCycleReleased(ICommonSession? session)
    {
        if (session?.AttachedEntity is { } user)
            SetCycleHeld(user, false);
    }

    public void SetCycleHeld(EntityUid user, bool held)
    {
        var input = EnsureComp<FirearmCycleInputComponent>(user);
        input.Held = held;
        Dirty(user, input);
    }

    public bool IsSlamFiring(EntityUid user, EntityUid gun)
    {
        return TryComp<ManualActionComponent>(gun, out var manual) && manual.SlamFire &&
            TryComp<FirearmCycleInputComponent>(user, out var input) && input.Held &&
            _hands.TryGetActiveItem(user, out var held) && held == gun;
    }

    private void OnCycleVerb(Entity<GunComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;
        var user = args.User;
        var gun = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("gun-ballistic-cycle"),
            Act = () => TryCycle(user, gun),
        });
    }

    /// <summary>Cycles only the active held gun; internal providers must not discard a live round.</summary>
    public bool TryCycle(EntityUid user)
    {
        return _hands.TryGetActiveItem(user, out var held) && TryCycle(user, held.Value);
    }

    public bool TryCycle(EntityUid user, EntityUid gun)
    {
        if (!_hands.TryGetActiveItem(user, out var held) || held != gun ||
            !HasComp<GunComponent>(gun) ||
            !_blocker.CanInteract(user, gun) || !_blocker.CanUseHeldEntity(user, gun))
            return false;

        TryComp<ManualActionComponent>(gun, out var manual);
        if (TryComp<ChamberMagazineAmmoProviderComponent>(held, out var chamber))
        {
            // Also allow initial chambering after spawning or reloading an empty gun.
            if (manual is { NeedsCycle: false } && chamber.BoltClosed != false && _guns.GetChamberEntity(held.Value) != null)
                return false;

            if (chamber.CanRack)
                _guns.UseChambered(held.Value, chamber, user);
            else
                _guns.ToggleBolt(held.Value, chamber, user);
        }
        else if (manual != null)
        {
            if (!manual.NeedsCycle)
                return false;

            _audio.PlayPredicted(manual.CycleSound, held.Value, user);
        }
        else
        {
            var ev = new UseInHandEvent(user);
            RaiseLocalEvent(gun, ev);
            return ev.Handled;
        }

        if (manual != null)
        {
            manual.NeedsCycle = false;
            Dirty(held.Value, manual);
        }
        return true;
    }

    private void OnExamine(Entity<ManualActionComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString(ent.Comp.NeedsCycle
            ? "gun-manual-action-needs-cycle"
            : "gun-manual-action-examine"));
        if (ent.Comp.SlamFire)
            args.PushMarkup(Loc.GetString("gun-manual-action-slam-fire"));
    }
}
