using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[TestFixture]
public sealed class WeaponAttachmentRevisionTest
{
    [TestCase("N14WeaponRifle762SKS")]
    [TestCase("N14WeaponRifleSKS")]
    [TestCase("N14WeaponRifle556Service")]
    [TestCase("N14WeaponRifle556Carbine")]
    [TestCase("N14WeaponRifle308Battle")]
    [TestCase("N14WeaponRifle762M14")]
    [TestCase("N14WeaponSniperHunting")]
    [TestCase("N14WeaponSniperEnfield")]
    [TestCase("N14WeaponSniper308Ross")]
    [TestCase("N14WeaponRevolver44Magnum")]
    [TestCase("N14WeaponRevolver45-70Hunter")]
    public async Task OptionalScopeCanBeInstalled(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var gun = em.SpawnEntity(prototype, map.GridCoords);
            var scope = em.SpawnEntity("N14WeaponScopeAttachment", map.GridCoords);
            Assert.That(em.System<ItemSlotsSystem>().TryInsert(gun, "weapon_optic", scope, null), Is.True);
            Assert.That(em.HasComponent<WieldableComponent>(gun), Is.True,
                "The attached scope requires its host to be wieldable.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponPistol9mm")]
    [TestCase("N14WeaponSMG9mm")]
    [TestCase("N14WeaponSMG12mmAdvancedChinese")]
    [TestCase("N14WeaponSMG12mmAdvanced")]
    [TestCase("N14WeaponPistol45Colt")]
    [TestCase("N14WeaponRifle762M14")]
    [TestCase("N14WeaponSMG10mm")]
    [TestCase("N14WeaponPistol10mm")]
    [TestCase("N14WeaponPistol12mm")]
    public async Task SuppressorCanBeInstalled(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var gun = em.SpawnEntity(prototype, map.GridCoords);
            var suppressor = em.SpawnEntity("N14FirearmSuppressor", map.GridCoords);
            Assert.That(em.System<ItemSlotsSystem>().TryInsert(gun, "weapon_muzzle", suppressor, null), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ThatGunUsesOnlyFiveRevolverChambersAndBothCalibers()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var gun = em.SpawnEntity("N14WeaponPistolThatGun", map.GridCoords);
            var provider = em.GetComponent<RevolverAmmoProviderComponent>(gun);
            Assert.That(provider.Capacity, Is.EqualTo(5));
            Assert.That(em.HasComponent<ChamberMagazineAmmoProviderComponent>(gun), Is.False);
            Assert.That(em.HasComponent<MagazineAmmoProviderComponent>(gun), Is.False);
            var whitelist = em.System<EntityWhitelistSystem>();
            foreach (var cartridge in new[] { "N14Cartridge223JHP", "N14Cartridge223Match", "N14Cartridge556FMJ", "N14Cartridge556AP" })
            {
                var round = em.SpawnEntity(cartridge, map.GridCoords);
                Assert.That(whitelist.IsWhitelistPass(provider.Whitelist, round), Is.True, cartridge);
            }
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponShotgunRiot", "N14MagazineShotgun12", "N14MagazineShotgun20")]
    [TestCase("N14WeaponRifle556EM2", "N14Magazine303EM2", "LongMagazine556Rifle")]
    [TestCase("N14WeaponSMG9mm", "Magazine45SubMachineGun", "N14MagazineSMG9mm")]
    public async Task RechamberedWeaponsRejectOldMagazines(string prototype, string correct, string wrong)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var gun = em.SpawnEntity(prototype, map.GridCoords);
            var slots = em.System<ItemSlotsSystem>();
            Assert.That(slots.TryEject(gun, "gun_magazine", null, out _), Is.True);
            var bad = em.SpawnEntity(wrong, map.GridCoords);
            Assert.That(slots.TryInsert(gun, "gun_magazine", bad, null), Is.False);
            var good = em.SpawnEntity(correct, map.GridCoords);
            Assert.That(slots.TryInsert(gun, "gun_magazine", good, null), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponRifle308Battle", true, false)]
    [TestCase("N14WeaponRifle762M14", true, true)]
    [TestCase("N14WeaponRifleSKS", true, false)]
    [TestCase("N14WeaponRifle762SKS", true, false)]
    [TestCase("N14WeaponRifle556Carbine", false, true)]
    [TestCase("N14WeaponSniperEnfield", true, false)]
    [TestCase("N14WeaponSniper308Ross", true, false)]
    [TestCase("N14WeaponRifle556Service", true, false)]
    public async Task MuzzleRestrictionsAllowScopeAlongsideAttachment(string prototype, bool bayonet, bool suppressor)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var slots = em.System<ItemSlotsSystem>();
            foreach (var (attachment, allowed) in new[] { ("N14FirearmBayonet", bayonet), ("N14FirearmSuppressor", suppressor) })
            {
                var gun = em.SpawnEntity(prototype, map.GridCoords);
                var scope = em.SpawnEntity("N14WeaponScopeAttachment", map.GridCoords);
                Assert.That(slots.TryInsert(gun, "weapon_optic", scope, null), Is.True);
                var item = em.SpawnEntity(attachment, map.GridCoords);
                Assert.That(slots.TryInsert(gun, "weapon_muzzle", item, null), Is.EqualTo(allowed), attachment);
            }
        });
        await pair.CleanReturnAsync();
    }
}
