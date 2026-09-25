using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Whitelist;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[TestFixture]
public sealed class RifleCaliberTest
{
    [TestCase("Magazine308Rifle", "N14Cartridge762x51FMJ")]
    [TestCase("Magazine308Rifle", "N14Cartridge762x51AP")]
    [TestCase("Magazine308Rifle", "N14Cartridge308JHP")]
    [TestCase("Magazine308Rifle", "N14Cartridge308Match")]
    [TestCase("Magazine556Rifle", "N14Cartridge556FMJ")]
    [TestCase("Magazine556Rifle", "N14Cartridge556AP")]
    [TestCase("Magazine556Rifle", "N14Cartridge223JHP")]
    [TestCase("Magazine556Rifle", "N14Cartridge223Match")]
    public async Task SharedCalibersFitExistingMagazines(string magazineId, string cartridgeId)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var magazine = em.SpawnEntity(magazineId, map.GridCoords);
            var cartridge = em.SpawnEntity(cartridgeId, map.GridCoords);
            var provider = em.GetComponent<BallisticAmmoProviderComponent>(magazine);
            Assert.That(em.System<EntityWhitelistSystem>().IsWhitelistPass(provider.Whitelist, cartridge), Is.True);
            var wrongRound = em.SpawnEntity("N14Cartridge303", map.GridCoords);
            Assert.That(em.System<EntityWhitelistSystem>().IsWhitelistPass(provider.Whitelist, wrongRound), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("N14WeaponSniperEnfield")]
    [TestCase("N14WeaponSniper308Ross")]
    [TestCase("N14Magazine303Bren")]
    [TestCase("N14SpeedLoader303")]
    public async Task BritishProvidersLoadOnly303(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var entity = em.SpawnEntity(prototype, map.GridCoords);
            var provider = em.GetComponent<BallisticAmmoProviderComponent>(entity);
            Assert.That(provider.Proto?.Id, Is.EqualTo("N14Cartridge303"));
            var whitelist = em.System<EntityWhitelistSystem>();
            var correct = em.SpawnEntity("N14Cartridge303", map.GridCoords);
            var incorrect = em.SpawnEntity("N14Cartridge762x51FMJ", map.GridCoords);
            Assert.That(whitelist.IsWhitelistPass(provider.Whitelist, correct), Is.True);
            Assert.That(whitelist.IsWhitelistPass(provider.Whitelist, incorrect), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BrenUsesDedicatedMagazineAndChamber()
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var em = pair.Server.ResolveDependency<IEntityManager>();
            var weapon = em.SpawnEntity("N14WeaponLMGBren", map.GridCoords);
            var slots = em.System<ItemSlotsSystem>();
            Assert.That(slots.TryEject(weapon, "gun_magazine", null, out var loaded), Is.True);
            Assert.That(em.GetComponent<MetaDataComponent>(loaded!.Value).EntityPrototype!.ID, Is.EqualTo("N14Magazine303Bren"));
            Assert.That(slots.TryInsert(weapon, "gun_magazine", loaded.Value, null), Is.True);
            Assert.That(slots.TryEject(weapon, "gun_magazine", null, out _), Is.True);
            var wrongMagazine = em.SpawnEntity("Magazine308RifleLong", map.GridCoords);
            Assert.That(slots.TryInsert(weapon, "gun_magazine", wrongMagazine, null), Is.False);
            Assert.That(slots.TryEject(weapon, "gun_chamber", null, out var chambered), Is.True);
            Assert.That(em.GetComponent<MetaDataComponent>(chambered!.Value).EntityPrototype!.ID, Is.EqualTo("N14Cartridge303"));
        });
        await pair.CleanReturnAsync();
    }
}
