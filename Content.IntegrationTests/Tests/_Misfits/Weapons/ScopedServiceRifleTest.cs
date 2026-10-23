// #Misfits Add - The scoped service rifle is a plain service rifle with a rifle
// scope attachment pre-installed in its top slot (and removable), not a separate
// frame with its own stats.
using Content.Shared.Containers.ItemSlots;
using Content.Shared._Misfits.WeaponAttachments.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits.Weapons;

[TestFixture]
public sealed class ScopedServiceRifleTest
{
    private const string ScopedProto = "N14WeaponRifle556ServiceScoped";
    private const string PlainProto = "N14WeaponRifle556Service";
    private const string ScopeProto = "N14WeaponScopeAttachment";

    [Test]
    public async Task ScopedServiceRifleSpawnsWithARemovableScope()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var slots = entMan.System<ItemSlotsSystem>();

        await server.WaitAssertion(() =>
        {
            var scoped = entMan.SpawnEntity(ScopedProto, map.GridCoords);
            Assert.That(slots.TryGetSlot(scoped, "gun_top", out var top), Is.True,
                "the scoped rifle should expose a top attachment slot");
            Assert.That(top!.Item, Is.Not.Null,
                "the scoped rifle should spawn with a scope installed");

            var scope = top.Item!.Value;
            Assert.That(entMan.GetComponent<MetaDataComponent>(scope).EntityPrototype!.ID,
                Is.EqualTo(ScopeProto));
            Assert.That(entMan.HasComponent<WeaponAttachmentComponent>(scope), Is.True,
                "the installed optic must be a removable attachment, not baked in");

            var plain = entMan.SpawnEntity(PlainProto, map.GridCoords);
            Assert.That(slots.TryGetSlot(plain, "gun_top", out var plainTop), Is.True);
            Assert.That(plainTop!.Item, Is.Null,
                "the plain service rifle should not spawn with a scope");
        });

        await pair.CleanReturnAsync();
    }
}
