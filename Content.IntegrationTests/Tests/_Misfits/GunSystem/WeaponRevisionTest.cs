using System.Collections.Generic;
using Content.Shared._Misfits.Weapons.Attachments;
using Content.Shared._Misfits.Weapons.Attachments.Components;
using Content.Shared._Misfits.Weapons.TwinTube;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Item;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[TestFixture]
public sealed class WeaponRevisionTest
{
    [TestCase("N14WeaponRifle10mmM1Carbine")]
    public async Task FullSpriteStates(string prototype)
    {
        // Reconnecting a recycled client currently re-registers the unrelated "raid" command.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Fresh = true,
            Destructive = true,
        });
        await pair.Client.WaitAssertion(() =>
        {
            var entMan = pair.Client.EntMan;
            var gun = entMan.Spawn(prototype);
            var sprite = entMan.GetComponent<SpriteComponent>(gun);
            var appearance = entMan.GetComponent<AppearanceComponent>(gun);
            var system = entMan.System<AppearanceSystem>();
            var layer = sprite.LayerMapGet(Content.Client.Weapons.Ranged.Components.GunVisualLayers.Base);

            void Check(bool closed, bool magazine, int rounds, string expected)
            {
                system.SetData(gun, AmmoVisuals.BoltClosed, closed);
                system.SetData(gun, AmmoVisuals.MagLoaded, magazine);
                system.SetData(gun, AmmoVisuals.AmmoCount, rounds);
                var ev = new AppearanceChangeEvent
                {
                    Component = appearance,
                    Sprite = sprite,
                    AppearanceData = new Dictionary<Enum, object>(),
                };
                entMan.EventBus.RaiseLocalEvent(gun, ref ev);
                Assert.That(sprite.LayerGetState(layer).ToString(), Is.EqualTo(expected));
            }

            Check(true, false, 0, "base");
            Check(true, true, 25, "mag-1");
            Check(true, true, 0, "mag-0");
            Check(false, true, 25, "bolt-open");
            Check(true, true, 25, "mag-1");
            if (entMan.TryGetComponent<FoldableComponent>(gun, out var fold))
            {
                var folding = entMan.System<FoldableSystem>();
                folding.SetFolded(gun, fold, true);
                Check(true, false, 0, "folded-base");
                Check(true, true, 25, "folded-mag-1");
                Check(true, true, 0, "folded-mag-0");
                Check(false, true, 25, "folded-bolt-open");
                folding.SetFolded(gun, fold, false);
                Check(true, true, 25, "mag-1");
            }
            entMan.DeleteEntity(gun);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MagazineOverlaysAndBoltStates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Fresh = true,
            Destructive = true,
        });
        await pair.Client.WaitAssertion(() =>
        {
            var entMan = pair.Client.EntMan;
            var appearance = entMan.System<AppearanceSystem>();
            foreach (var prototype in new[] { "N14WeaponRifle762Rangemaster", "N14WeaponSniper556Tribal",
                         "N14WeaponSniper556TribalUpgraded", "N14WeaponSniper556VarmintRifle", "N14WeaponShotgunBlowback" })
            {
                var gun = entMan.Spawn(prototype);
                var sprite = entMan.GetComponent<SpriteComponent>(gun);
                var baseLayer = sprite.LayerMapGet(Content.Client.Weapons.Ranged.Components.GunVisualLayers.Base);
                var magLayer = sprite.LayerMapGet(Content.Client.Weapons.Ranged.Components.GunVisualLayers.Mag);
                foreach (var closed in new[] { true, false })
                foreach (var loaded in new[] { true, false })
                foreach (var count in new[] { 0, 12, 25 })
                {
                    var data = new Dictionary<Enum, object>
                    {
                        [AmmoVisuals.BoltClosed] = closed,
                        [AmmoVisuals.MagLoaded] = loaded,
                        [AmmoVisuals.AmmoCount] = count,
                        [AmmoVisuals.AmmoMax] = 25,
                    };
                    foreach (var (key, value) in data)
                        appearance.SetData(gun, key, value);
                    var ev = new AppearanceChangeEvent
                    {
                        Component = entMan.GetComponent<AppearanceComponent>(gun),
                        Sprite = sprite,
                        AppearanceData = data,
                    };
                    entMan.EventBus.RaiseLocalEvent(gun, ref ev);
                    Assert.That(sprite.LayerGetState(baseLayer).ToString(), Is.EqualTo(closed ? "base" : "bolt-open"), prototype);
                    Assert.That(sprite.LayerGetState(magLayer).ToString(), Is.EqualTo("mag-0"), prototype);
                    Assert.That(sprite[magLayer].Visible, Is.EqualTo(loaded), prototype);
                }
                entMan.DeleteEntity(gun);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ParatrooperStockSizeAndRecoilRestoreWithoutStacking()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var gun = entMan.SpawnEntity("N14WeaponRifle10mmM1Carbine", map.GridCoords);
            var fold = entMan.GetComponent<FoldableComponent>(gun);
            var folding = entMan.System<FoldableSystem>();
            var item = entMan.GetComponent<ItemComponent>(gun);
            var firearm = entMan.GetComponent<GunComponent>(gun);
            var guns = entMan.System<SharedGunSystem>();
            guns.RefreshModifiers((gun, firearm));
            var recoil = firearm.AngleIncreaseModified.Degrees;
            var camera = firearm.CameraRecoilScalarModified;
            var slots = entMan.GetComponent<ItemSlotsComponent>(gun);
            var magazine = slots.Slots["gun_magazine"].Item;

            for (var i = 0; i < 3; i++)
            {
                Assert.That(folding.TrySetFolded(gun, fold, true), Is.True);
                Assert.That(item.Size.Id, Is.EqualTo("Normal"));
                Assert.That(firearm.AngleIncreaseModified.Degrees, Is.EqualTo(recoil * 0.8).Within(0.001));
                Assert.That(firearm.CameraRecoilScalarModified, Is.EqualTo(camera * 0.8).Within(0.001));
                guns.RefreshModifiers((gun, firearm));
                Assert.That(firearm.AngleIncreaseModified.Degrees, Is.EqualTo(recoil * 0.8).Within(0.001));
                Assert.That(folding.TrySetFolded(gun, fold, false), Is.True);
                Assert.That(item.Size.Id, Is.EqualTo("Large"));
                Assert.That(firearm.AngleIncreaseModified.Degrees, Is.EqualTo(recoil).Within(0.001));
                Assert.That(slots.Slots["gun_magazine"].Item, Is.EqualTo(magazine));
            }

            folding.TrySetFolded(gun, fold, true);
            var holder = entMan.SpawnEntity(null, map.GridCoords);
            var containers = entMan.System<SharedContainerSystem>();
            var storage = containers.EnsureContainer<ContainerSlot>(holder, "test-storage");
            Assert.That(containers.Insert(gun, storage), Is.True);
            Assert.That(folding.TrySetFolded(gun, fold, false), Is.False);
            Assert.That(item.Size.Id, Is.EqualTo("Normal"));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponRifle762Marksman", "Magazine556Rifle")]
    [TestCase("N14WeaponRifle762Rangemaster", "Magazine556Rifle")]
    [TestCase("N14WeaponRifle762Marksman", "LongMagazine556Rifle")]
    [TestCase("N14WeaponSMG10mmPipe", "N14MagazineSMG10mm")]
    [TestCase("N14WeaponPistol10mmPipe", "N14MagazinePistol10mm")]
    [TestCase("N14WeaponRifle10mmM1Carbine", "N14MagazineSMG10mm")]
    [TestCase("N14WeaponRifle10mmM1Carbine", "N14MagazinePistol10mm")]
    [TestCase("N14WeaponRifle556CarbineOld", "N14MagazineSMG10mm")]
    [TestCase("N14WeaponRifle556CarbineOld", "N14MagazinePistol10mm")]
    [TestCase("N14WeaponSniper556Tribal", "Magazine556Rifle")]
    [TestCase("N14WeaponSniper556TribalUpgraded", "Magazine556Rifle")]
    public async Task DetachableMagazineCanBeReloadedAndFired(string gunId, string magazineId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var gun = entMan.SpawnEntity(gunId, map.GridCoords);
            var slots = entMan.System<ItemSlotsSystem>();
            Assert.That(slots.TryEject(gun, "gun_magazine", null, out _), Is.True);
            slots.TryEject(gun, "gun_chamber", null, out _);
            var magazine = entMan.SpawnEntity(magazineId, map.GridCoords);
            Assert.That(slots.TryInsert(gun, "gun_magazine", magazine, null), Is.True);
            Assert.That(slots.TryEject(gun, "gun_magazine", null, out var removed), Is.True);
            Assert.That(removed, Is.EqualTo(magazine));
            Assert.That(slots.TryInsert(gun, "gun_magazine", magazine, null), Is.True);
            var gunSystem = entMan.System<SharedGunSystem>();
            entMan.EventBus.RaiseLocalEvent(gun, new UseInHandEvent(gun));
            Assert.That(gunSystem.DoTakeAmmo(1, gun), Has.Count.EqualTo(1));
            Assert.That(entMan.HasComponent<BallisticAmmoProviderComponent>(gun), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NeosteadTubesStayIndependentWhenFiringAndReloading()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var gun = entMan.SpawnEntity("N14WeaponShotgunNeostead", map.GridCoords);
            var component = entMan.GetComponent<TwinTubeAmmoProviderComponent>(gun);
            var tubes = entMan.System<TwinTubeAmmoProviderSystem>();
            var guns = entMan.System<SharedGunSystem>();
            var left = entMan.GetComponent<BallisticAmmoProviderComponent>(tubes.GetTube(gun, false)!.Value);
            var right = entMan.GetComponent<BallisticAmmoProviderComponent>(tubes.GetTube(gun, true)!.Value);
            Assert.That(left.AmmoCount, Is.EqualTo(6));
            Assert.That(right.AmmoCount, Is.EqualTo(6));
            Assert.That(entMan.HasComponent<GunRequiresWieldComponent>(gun), Is.True);
            Assert.That(guns.DoTakeAmmo(6, gun), Has.Count.EqualTo(6));
            Assert.That(left.AmmoCount, Is.Zero);
            Assert.That(right.AmmoCount, Is.EqualTo(6));
            Assert.That(component.RightSelected, Is.True, "The gun switches to the remaining loaded tube immediately.");

            tubes.SelectTube((gun, component), true);
            Assert.That(guns.DoTakeAmmo(2, gun), Has.Count.EqualTo(2));
            Assert.That(right.AmmoCount, Is.EqualTo(4));
            tubes.SelectTube((gun, component), false);
            var shell = entMan.SpawnEntity("N14ShellShotgun12", map.GridCoords);
            var load = new InteractUsingEvent(gun, shell, gun, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(gun, load);
            Assert.That(load.Handled, Is.True);
            Assert.That(left.AmmoCount, Is.EqualTo(1));
            Assert.That(right.AmmoCount, Is.EqualTo(4));
            var wrongRound = entMan.SpawnEntity("N14CartridgePistol10", map.GridCoords);
            var wrongLoad = new InteractUsingEvent(gun, wrongRound, gun, map.GridCoords);
            entMan.EventBus.RaiseLocalEvent(gun, wrongLoad);
            Assert.That(left.AmmoCount, Is.EqualTo(1));
            Assert.That(guns.DoTakeAmmo(1, gun)[0].Item1, Is.EqualTo(shell));
            var total = new GetAmmoCountEvent();
            entMan.EventBus.RaiseLocalEvent(gun, ref total);
            Assert.That(total.Count, Is.EqualTo(4));
            Assert.That(total.Capacity, Is.EqualTo(12));

            Assert.That(guns.DoTakeAmmo(5, gun), Has.Count.EqualTo(4));
            Assert.That(guns.DoTakeAmmo(1, gun), Is.Empty);
            var freshGun = entMan.SpawnEntity("N14WeaponShotgunNeostead", map.GridCoords);
            Assert.That(guns.DoTakeAmmo(12, freshGun), Has.Count.EqualTo(12), "A single request can cross both tubes.");

            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(user, gun), Is.True);
            entMan.EventBus.RaiseLocalEvent(gun, new UseInHandEvent(user));
            Assert.That(entMan.GetComponent<WieldableComponent>(gun).Wielded, Is.True);
            entMan.EventBus.RaiseLocalEvent(gun, new UseInHandEvent(user));
            Assert.That(entMan.GetComponent<WieldableComponent>(gun).Wielded, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InternalRiflesAndIntegralSuppressor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var svt = entMan.SpawnEntity("N14WeaponRifleSKS", map.GridCoords);
            Assert.That(entMan.GetComponent<MetaDataComponent>(svt).EntityName, Is.EqualTo("SVT-40"));
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            Assert.That(entMan.System<SharedHandsSystem>().TryPickupAnyHand(user, svt), Is.True);
            var optic = entMan.SpawnEntity("N14WeaponScopeVariable", map.GridCoords);
            Assert.That(entMan.System<ItemSlotsSystem>().TryInsert(svt, "weapon_optic", optic, null), Is.True);
            Assert.That(entMan.GetComponent<FirearmAttachmentHostComponent>(svt).OpticToggleActionEntity, Is.Not.Null);
            Assert.That(entMan.HasComponent<ChamberMagazineAmmoProviderComponent>(svt), Is.False);
            Assert.That(entMan.System<SharedGunSystem>().DoTakeAmmo(11, svt), Has.Count.EqualTo(10));
            var rifle = entMan.SpawnEntity("N14WeaponRifle762MarksmanChinese", map.GridCoords);
            Assert.That(entMan.GetComponent<FirearmAttachmentHostComponent>(rifle).ShowMuzzleVisual, Is.False);
            var suppressed = new IsGunSuppressedEvent();
            entMan.EventBus.RaiseLocalEvent(rifle, ref suppressed);
            Assert.That(suppressed.Suppressed, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
