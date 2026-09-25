using Content.Shared._Misfits.Weapons.Attachments;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.GunSystem;

[TestFixture]
public sealed class MarksmanRifleTest
{
    [Test]
    public async Task IntegralSuppressorCannotBeRemoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var rifle = entMan.SpawnEntity("N14WeaponRifle762MarksmanChinese", map.GridCoords);
            var slots = entMan.GetComponent<ItemSlotsComponent>(rifle);
            var muzzle = slots.Slots["weapon_muzzle"];
            Assert.That(muzzle.Item, Is.Not.Null, "The integral suppressor must spawn even in a locked slot.");
            Assert.That(entMan.System<ItemSlotsSystem>().CanEject(rifle, null, muzzle), Is.False);

            var suppressed = new IsGunSuppressedEvent();
            entMan.EventBus.RaiseLocalEvent(rifle, ref suppressed);
            Assert.That(suppressed.Suppressed, Is.True);
            var flash = new GunMuzzleFlashAttemptEvent(false);
            entMan.EventBus.RaiseLocalEvent(rifle, ref flash);
            Assert.That(flash.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }
}
