// #Misfits Add - Integration tests for the Enclave blast door and its hacking terminal.
using System.Linq;
using Content.Server.DeviceLinking.Components;
using Content.Shared._Misfits.RaidRequest;
using Content.Shared.Access.Components;
using Content.Shared.Lock;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._Misfits;

/// <summary>
/// The Enclave door and its panels are bound together by an AutoLink channel string, not by wiring,
/// so a typo in one prototype silently produces a door nothing can open. These tests pin the
/// channel down, check it cannot reach the Vault-Tec door, and check who the panel opens for.
/// </summary>
[TestFixture]
public sealed class EnclaveVaultDoorTest
{
    private const string EnclaveDoor = "MisfitsDoorVaultEnclave";
    private const string EnclavePanel = "MisfitsDoorVaultEnclaveControls";
    private const string EnclavePanelLocked = "MisfitsDoorVaultEnclaveControlsLocked";
    private const string EnclaveTerminal = "MisfitsDoorVaultEnclaveExteriorControls";

    private const string VaultDoor = "N14DoorVault";
    private const string VaultTerminal = "MisfitsDoorVaultExteriorControls";

    private const string RequiredAccess = "EnclaveNCO";

    [Test]
    public async Task EveryEnclavePanelDrivesTheEnclaveDoor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var door = entMan.SpawnEntity(EnclaveDoor, map.GridCoords);
            var channel = entMan.GetComponent<AutoLinkReceiverComponent>(door).AutoLinkChannel;

            Assert.That(channel, Is.Not.Null.And.Not.Empty, "Enclave door has no autolink channel");

            foreach (var proto in new[] { EnclavePanel, EnclavePanelLocked, EnclaveTerminal })
            {
                var panel = entMan.SpawnEntity(proto, map.GridCoords);
                Assert.That(entMan.GetComponent<AutoLinkTransmitterComponent>(panel).AutoLinkChannel,
                    Is.EqualTo(channel), $"{proto} is not on the Enclave door's channel");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// Door discovery is channel plus same-grid, so sharing a channel with the Vault-Tec door would
    /// let either faction's terminal bolt open the other faction's door.
    /// </summary>
    [Test]
    public async Task EnclaveAndVaultTecDoNotShareAChannel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var enclave = entMan.SpawnEntity(EnclaveDoor, map.GridCoords);
            var vault = entMan.SpawnEntity(VaultDoor, map.GridCoords);

            Assert.That(entMan.GetComponent<AutoLinkReceiverComponent>(enclave).AutoLinkChannel,
                Is.Not.EqualTo(entMan.GetComponent<AutoLinkReceiverComponent>(vault).AutoLinkChannel),
                "Enclave and Vault-Tec doors share an autolink channel");

            // The Vault-Tec terminal must be left exactly as it was.
            var vaultTerminal = entMan.SpawnEntity(VaultTerminal, map.GridCoords);
            Assert.That(entMan.GetComponent<AutoLinkTransmitterComponent>(vaultTerminal).AutoLinkChannel,
                Is.EqualTo(entMan.GetComponent<AutoLinkReceiverComponent>(vault).AutoLinkChannel),
                "the Vault-Tec terminal no longer drives the Vault-Tec door");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// EnclaveNCO is granted by the NCO, Junior Officer, Senior Officer, Command and Head Scientist
    /// access groups and by nothing below them, so it is exactly "NCO and above, plus head sci".
    /// Lock is what enforces it - SignalSwitchSystem runs no access check of its own.
    /// </summary>
    [Test]
    public async Task LockedPanelOpensForNcoAndAbove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var panel = entMan.SpawnEntity(EnclavePanelLocked, map.GridCoords);

            Assert.That(entMan.HasComponent<LockComponent>(panel),
                "locked panel has no Lock, so its AccessReader would never be consulted");

            var reader = entMan.GetComponent<AccessReaderComponent>(panel);
            Assert.That(reader.AccessLists.Any(set => set.Contains(RequiredAccess)),
                $"locked panel does not require {RequiredAccess}");

            // The unlocked variant is deliberately open; it exists for mappers who want that.
            var open = entMan.SpawnEntity(EnclavePanel, map.GridCoords);
            Assert.That(entMan.HasComponent<LockComponent>(open), Is.False,
                "the unlocked panel variant is locked");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    /// The terminal only unlocks while its faction is under an active raid. If "Enclave" were not a
    /// faction-tier raid target, the console could never open at all.
    /// </summary>
    [Test]
    public void EnclaveCanBeRaidTargeted()
    {
        Assert.That(RaidRequestConfig.FactionTierFactions, Does.Contain("Enclave"));
    }
}
