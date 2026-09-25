using Content.Shared._Misfits.Weapons.Ranged;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System;
using System.Numerics;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[TestFixture]
public sealed class ManualActionTest
{
    [Test]
    public async Task TrenchSlamFireRequiresHeldCycleKey()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var user = em.SpawnEntity("MobHuman", map.GridCoords);
            var weapon = em.SpawnEntity("N14WeaponShotgunTrench", map.GridCoords);
            var hands = em.System<SharedHandsSystem>();
            var actions = em.System<ManualActionSystem>();
            var guns = em.System<SharedGunSystem>();
            var gun = em.GetComponent<GunComponent>(weapon);
            Assert.That(hands.TryPickupAnyHand(user, weapon), Is.True);
            Assert.That(em.System<WieldableSystem>().TryWield(weapon,
                em.GetComponent<WieldableComponent>(weapon), user), Is.True);
            guns.RefreshModifiers((weapon, gun));
            var manual = em.GetComponent<ManualActionComponent>(weapon);
            var ammo = em.GetComponent<BallisticAmmoProviderComponent>(weapon);
            var before = ammo.AmmoCount;
            actions.SetCycleHeld(user, true);
            // Separate trigger pulls in the same tick must bypass the ordinary fire-rate delay.
            Fire();
            Fire();
            Fire();
            Assert.That(ammo.AmmoCount, Is.EqualTo(before - 3));
            Assert.That(manual.NeedsCycle, Is.True);
            actions.SetCycleHeld(user, false);
#pragma warning disable RA0002 // Isolate the manual-action gate from the normal firing cooldown.
            gun.NextFire = TimeSpan.Zero;
#pragma warning restore RA0002
            Fire();
            Assert.That(ammo.AmmoCount, Is.EqualTo(before - 3));
            Assert.That(actions.TryCycle(user), Is.True);
            Assert.That(manual.NeedsCycle, Is.False);

            void Fire() => guns.AttemptShoot(user, weapon, gun,
                new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(10, 0)));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponRifle762Rangemaster")]
    [TestCase("N14WeaponSniper556VarmintRifle")]
    public async Task CycleKeyChambersMagazineFedGuns(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var user = em.SpawnEntity("MobHuman", map.GridCoords);
            var weapon = em.SpawnEntity(prototype, map.GridCoords);
            Assert.That(em.System<SharedHandsSystem>().TryPickupAnyHand(user, weapon), Is.True);
            var guns = em.System<SharedGunSystem>();
            var chamber = em.GetComponent<ChamberMagazineAmmoProviderComponent>(weapon);
            guns.SetBoltClosed(weapon, chamber, false, user);
            Assert.That(em.System<ManualActionSystem>().TryCycle(user), Is.True);
            Assert.That(chamber.BoltClosed, Is.True);
            Assert.That(guns.GetChamberEntity(weapon), Is.Not.Null);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponSniperHunting")]
    [TestCase("N14WeaponSniper44LeverCarbine")]
    [TestCase("N14WeaponShotgun")]
    [TestCase("N14WeaponShotgunNeostead")]
    [TestCase("N14WeaponSniper556VarmintRifle")]
    public async Task MustCycleBetweenShotsWithoutLosingLiveAmmo(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var weapon = entMan.SpawnEntity(prototype, map.GridCoords);
            var hands = entMan.System<SharedHandsSystem>();
            var actions = entMan.System<ManualActionSystem>();
            var guns = entMan.System<SharedGunSystem>();
            var gun = entMan.GetComponent<GunComponent>(weapon);
            var manual = entMan.GetComponent<ManualActionComponent>(weapon);
            Assert.That(hands.TryPickupAnyHand(user, weapon), Is.True);
            Assert.That(entMan.System<WieldableSystem>().TryWield(weapon,
                entMan.GetComponent<WieldableComponent>(weapon), user), Is.True);
            guns.RefreshModifiers((weapon, gun));

            // Chamber magazine-fed rifles before the first shot.
            actions.TryCycle(user);
            Assert.That(manual.NeedsCycle, Is.False);
            var before = AmmoCount();
            Fire();
            Assert.That(manual.NeedsCycle, Is.True, "Firing must require a cycle.");
            var afterShot = AmmoCount();
            Fire();
            Assert.That(AmmoCount(), Is.EqualTo(afterShot), "A blocked shot must not consume ammunition.");
            Assert.That(manual.NeedsCycle, Is.True);
            Assert.That(actions.TryCycle(user), Is.True);
            Assert.That(manual.NeedsCycle, Is.False);
            Assert.That(AmmoCount(), Is.EqualTo(before - 1), "Cycling must not discard another live round.");
            Assert.That(actions.TryCycle(user), Is.False, "Repeated cycling of a ready gun is a no-op.");
            Assert.That(AmmoCount(), Is.EqualTo(before - 1));
            Fire();
            Assert.That(manual.NeedsCycle, Is.True);
            Assert.That(hands.TryDrop(user, weapon), Is.True);
            Assert.That(actions.TryCycle(user), Is.False, "Dropped guns cannot be cycled remotely.");
            Assert.That(manual.NeedsCycle, Is.True, "Dropping does not reset the action.");

            int AmmoCount()
            {
                var ev = new GetAmmoCountEvent();
                entMan.EventBus.RaiseLocalEvent(weapon, ref ev);
                return ev.Count;
            }

            void Fire()
            {
                // Bypass the cooldown to prove the manual-action gate blocks the next shot.
#pragma warning disable RA0002 // Test-only cooldown override; keep GunComponent access restricted in production.
                gun.NextFire = TimeSpan.Zero;
#pragma warning restore RA0002
                guns.AttemptShoot(user, weapon, gun,
                    new EntityCoordinates(map.GridCoords.EntityId, map.GridCoords.Position + new Vector2(10, 0)));
            }
        });

        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponRifle762Rangemaster")]
    [TestCase("N14WeaponRifle762SKS")]
    [TestCase("N14WeaponRifleSKS")]
    [TestCase("N14WeaponShotgunBlowback")]
    [TestCase("N14WeaponShotgunDoubleBarrel")]
    [TestCase("N14WeaponShotgunPipe")]
    public async Task OtherActionsDoNotRequireCycling(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entMan = pair.Server.ResolveDependency<IEntityManager>();
            var weapon = entMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(entMan.HasComponent<ManualActionComponent>(weapon), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
