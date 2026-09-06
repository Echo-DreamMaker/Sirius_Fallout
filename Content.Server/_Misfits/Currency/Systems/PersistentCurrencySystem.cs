// #Misfits Change - Persistent currency system
using System.IO;
using System.Text.Json;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Shared._Misfits.Currency;
using Content.Shared._Misfits.Currency.Components;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Stacks;
using Robust.Server.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Player;

namespace Content.Server._Misfits.Currency.Systems;

/// <summary>
/// Handles consuming currency items and adding them to a player's persistent balance.
/// </summary>
public sealed class PersistentCurrencySystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IChatManager _chatManager = default!; // #Misfits Change - for private deposit notifications

    // #Misfits Change - Sawmill for wallet logging
    private ISawmill _log = default!;

    /// <summary>
    /// Maps CurrencyType to the entity prototype ID to spawn when withdrawing.
    /// </summary>
    private static readonly Dictionary<CurrencyType, string> CurrencyPrototypes = new()
    {
        { CurrencyType.Bottlecaps, "N14CurrencyCap" },
        // #Misfits Change - NCRDollars, LegionDenarii, and PrewarMoney are now tracked persistently again
        { CurrencyType.NCRDollars, "N14CurrencyNCRDollar" },
        { CurrencyType.LegionDenarii, "N14CurrencyLegionDenarius" },
        { CurrencyType.PrewarMoney, "N14CurrencyPrewar" },
    };

    // #Nuclear14 Add - display names for chat/wallet messages, keyed by currency type
    private static readonly Dictionary<CurrencyType, string> CurrencyDisplayNames = new()
    {
        { CurrencyType.Bottlecaps, "Bottlecaps" },
        { CurrencyType.NCRDollars, "NCR Dollars" },
        { CurrencyType.LegionDenarii, "Legion Denarii" },
        { CurrencyType.PrewarMoney, "Pre-War Money" },
    };

    public override void Initialize()
    {
        base.Initialize();

        // #Misfits Change - initialise the sawmill before any other setup
        _log = Logger.GetSawmill("persistent_currency");

        SubscribeLocalEvent<ConsumableCurrencyComponent, UseInHandEvent>(OnUseCurrency);
        SubscribeLocalEvent<PersistentCurrencyComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PersistentCurrencyComponent, ComponentShutdown>(OnCurrencyShutdown);
        SubscribeLocalEvent<PersistentCurrencyComponent, OpenCurrencyWalletEvent>(OnOpenWallet);
        SubscribeNetworkEvent<WithdrawCurrencyRequest>(OnWithdrawRequest);
        SubscribeNetworkEvent<OpenWalletHudMessage>(OnHudOpenWallet); // #Misfits Change - HUD button support
        SubscribeNetworkEvent<DepositHeldCurrencyRequest>(OnDepositHeldRequest); // #Misfits Change - wallet Deposit In Hand button

        // #Misfits Fix - Use PlayerSpawnCompleteEvent to load currency; ComponentStartup and
        // PlayerAttachedEvent fire before mind attachment so TryGetMind always fails.
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        // One-time migration from legacy JSON file to database
        MigrateJsonToDatabase();
    }

    // #Misfits Change - Z key now silently deposits held currency and posts a private chat confirmation
    // instead of opening the wallet window.
    private void OnOpenWallet(Entity<PersistentCurrencyComponent> ent, ref OpenCurrencyWalletEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var uid = ent.Owner;
        var comp = ent.Comp;
        var session = actor.PlayerSession;

        // Find a ConsumableCurrencyComponent item in any held hand
        EntityUid? heldItem = null;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (HasComp<ConsumableCurrencyComponent>(held))
            {
                heldItem = held;
                break;
            }
        }

        if (heldItem == null)
        {
            // Nothing to deposit — inform the player privately
            var nothingMsg = Loc.GetString("misfits-currency-no-currency");
            _chatManager.ChatMessageToOne(ChatChannel.Server, nothingMsg, nothingMsg, EntityUid.Invalid, false, session.Channel);
            return;
        }

        if (!TryComp<ConsumableCurrencyComponent>(heldItem.Value, out var currency))
            return;

        // Calculate amount (stack-aware)
        var amount = currency.ValuePerUnit;
        if (TryComp<StackComponent>(heldItem.Value, out var stackComp))
            amount *= stackComp.Count;

        // #Misfits Change - all tracked currency types are again supported for deposit -Tytos
        if (!CurrencyDisplayNames.TryGetValue(currency.CurrencyType, out var typeName))
        {
            var unsupportedMsg = Loc.GetString("misfits-currency-unsupported-type");
            _chatManager.ChatMessageToOne(ChatChannel.Server, unsupportedMsg, unsupportedMsg, EntityUid.Invalid, false, session.Channel);
            return;
        }

        SetBalance(comp, currency.CurrencyType, GetBalance(comp, currency.CurrencyType) + amount);

        var total = GetBalance(comp, currency.CurrencyType);

        Dirty(uid, comp);

        if (comp.UserId != null && comp.CharacterName != null)
            SaveCurrency(comp.UserId, comp.CharacterName, comp);

        // Remove the deposited item
        QueueDel(heldItem.Value);

        // Send a private chat message only the player can see
        var depositMsg = Loc.GetString("misfits-currency-deposited", ("amount", amount), ("type", typeName), ("total", total));
        _chatManager.ChatMessageToOne(ChatChannel.Server, depositMsg, depositMsg, EntityUid.Invalid, false, session.Channel);
    }

    // #Misfits Change - handles wallet open request from the dedicated HUD button
    private void OnHudOpenWallet(OpenWalletHudMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        // #Misfits Change - ensure the component exists so new players can open the wallet before touching any currency
        var comp = EnsureComp<PersistentCurrencyComponent>(uid);

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var stateMsg = new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
            NcrDollars = comp.NcrDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney = comp.PrewarMoney,
            OpenWindow = true,
        };

        RaiseNetworkEvent(stateMsg, actor.PlayerSession.Channel);
    }

    // #Misfits Change - Deposit In Hand button: deposit whatever ConsumableCurrency item the player is holding
    private void OnDepositHeldRequest(DepositHeldCurrencyRequest msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        // Find a ConsumableCurrencyComponent item in any held hand
        EntityUid? heldItem = null;
        foreach (var held in _hands.EnumerateHeld(uid))
        {
            if (HasComp<ConsumableCurrencyComponent>(held))
            {
                heldItem = held;
                break;
            }
        }

        if (heldItem == null)
        {
            var noCurrencyMsg = Loc.GetString("misfits-currency-no-currency");
            _chatManager.ChatMessageToOne(ChatChannel.Server, noCurrencyMsg, noCurrencyMsg, EntityUid.Invalid, false, player.Channel);
            return;
        }

        if (!TryComp<ConsumableCurrencyComponent>(heldItem.Value, out var currency))
            return;

        // Determine deposit amount (stack-aware)
        var amount = currency.ValuePerUnit;
        if (TryComp<StackComponent>(heldItem.Value, out var stack))
            amount *= stack.Count;

        // #Nuclear14 Change - all tracked currency types are now supported for deposit
        if (!CurrencyDisplayNames.TryGetValue(currency.CurrencyType, out var typeName))
        {
            var unsupportedMsg = Loc.GetString("misfits-currency-unsupported-type");
            _chatManager.ChatMessageToOne(ChatChannel.Server, unsupportedMsg, unsupportedMsg, EntityUid.Invalid, false, player.Channel);
            return;
        }

        SetBalance(comp, currency.CurrencyType, GetBalance(comp, currency.CurrencyType) + amount);

        var total = GetBalance(comp, currency.CurrencyType);
        var depositMsg = Loc.GetString("misfits-currency-deposited", ("amount", amount), ("type", typeName), ("total", total));
        _chatManager.ChatMessageToOne(ChatChannel.Server, depositMsg, depositMsg, EntityUid.Invalid, false, player.Channel);

        Dirty(uid, comp);

        if (comp.UserId != null && comp.CharacterName != null)
            SaveCurrency(comp.UserId, comp.CharacterName, comp);

        // Delete the held currency item
        QueueDel(heldItem.Value);

        // Send refreshed state back so the window updates immediately
        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        RaiseNetworkEvent(new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
            NcrDollars = comp.NcrDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney = comp.PrewarMoney,
        }, actor.PlayerSession.Channel);
    }

    private void OnWithdrawRequest(WithdrawCurrencyRequest msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (player.AttachedEntity is not { } uid)
            return;

        if (!TryComp<PersistentCurrencyComponent>(uid, out var comp))
            return;

        if (msg.Amount <= 0)
            return;

        // Check balance
        var balance = GetBalance(comp, msg.CurrencyType);
        if (balance < msg.Amount)
        {
            var insufficientMsg = Loc.GetString("misfits-currency-insufficient");
            _chatManager.ChatMessageToOne(ChatChannel.Server, insufficientMsg, insufficientMsg, EntityUid.Invalid, false, player.Channel);
            return;
        }

        // Deduct
        SetBalance(comp, msg.CurrencyType, balance - msg.Amount);
        Dirty(uid, comp);

        // Save
        if (comp.UserId != null && comp.CharacterName != null)
            SaveCurrency(comp.UserId, comp.CharacterName, comp);

        // Spawn the currency items
        if (!CurrencyPrototypes.TryGetValue(msg.CurrencyType, out var protoId))
            return;

        var spawned = Spawn(protoId, Transform(uid).Coordinates);

        // Set stack count if applicable
        if (TryComp<StackComponent>(spawned, out var stackComp) && msg.Amount > 1)
            _stack.SetCount(spawned, msg.Amount);

        // Try to put in hand
        _hands.TryPickupAnyHand(uid, spawned);

        var typeName = CurrencyDisplayNames.GetValueOrDefault(msg.CurrencyType, "Bottlecaps");
        var withdrawMsg = Loc.GetString("misfits-currency-withdrew", ("amount", msg.Amount), ("type", typeName));
        _chatManager.ChatMessageToOne(ChatChannel.Server, withdrawMsg, withdrawMsg, EntityUid.Invalid, false, player.Channel);

        // Send updated state to client
        var stateMsg = new CurrencyWalletStateMessage
        {
            Bottlecaps = comp.Bottlecaps,
            NcrDollars = comp.NcrDollars,
            LegionDenarii = comp.LegionDenarii,
            PrewarMoney = comp.PrewarMoney,
        };

        RaiseNetworkEvent(stateMsg, player.Channel);
    }

    private int GetBalance(PersistentCurrencyComponent comp, CurrencyType type)
    {
        // #Misfits Tytos Change - Bottlecaps, NCR Dollars, Legion Denarii, and Pre-War Money are all tracked persistently
        return type switch
        {
            CurrencyType.Bottlecaps => comp.Bottlecaps,
            CurrencyType.NCRDollars => comp.NcrDollars,
            CurrencyType.LegionDenarii => comp.LegionDenarii,
            CurrencyType.PrewarMoney => comp.PrewarMoney,
            _ => 0,
        };
    }

    private void SetBalance(PersistentCurrencyComponent comp, CurrencyType type, int value)
    {
        // #Misfits Change - Bottlecaps, NCR Dollars, Legion Denarii, and Pre-War Money are all tracked persistently -Tytos
        switch (type)
        {
            case CurrencyType.Bottlecaps:
                comp.Bottlecaps = value;
                break;
            case CurrencyType.NCRDollars:
                comp.NcrDollars = value;
                break;
            case CurrencyType.LegionDenarii:
                comp.LegionDenarii = value;
                break;
            case CurrencyType.PrewarMoney:
                comp.PrewarMoney = value;
                break;
        }
    }

    private void OnUseCurrency(Entity<ConsumableCurrencyComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;

        // Ensure the user has the persistent currency component
        var currencyComp = EnsureComp<PersistentCurrencyComponent>(user);

        // Get the amount to add (from stack or single item)
        int amount = ent.Comp.ValuePerUnit;
        if (TryComp<StackComponent>(ent, out var stack))
        {
            amount *= stack.Count;
        }

        // #Misfits Change - all tracked currency types can now be consumed into the wallet
        if (!CurrencyDisplayNames.TryGetValue(ent.Comp.CurrencyType, out var typeName))
            return;

        SetBalance(currencyComp, ent.Comp.CurrencyType, GetBalance(currencyComp, ent.Comp.CurrencyType) + amount);

        var total = GetBalance(currencyComp, ent.Comp.CurrencyType);
        if (TryComp<ActorComponent>(user, out var actorComp))
        {
            var depositMsg = Loc.GetString("misfits-currency-deposited", ("amount", amount), ("type", typeName), ("total", total));
            _chatManager.ChatMessageToOne(ChatChannel.Server, depositMsg, depositMsg, EntityUid.Invalid, false, actorComp.PlayerSession.Channel);
        }

        Dirty(user, currencyComp);

        // Save to file
        if (currencyComp.UserId != null && currencyComp.CharacterName != null)
        {
            SaveCurrency(currencyComp.UserId, currencyComp.CharacterName, currencyComp);
        }

        // Delete the currency item
        QueueDel(ent);

        // #Misfits Change - refresh the wallet window if the player has it open
        if (TryComp<ActorComponent>(user, out var actor))
        {
            RaiseNetworkEvent(new CurrencyWalletStateMessage
            {
                Bottlecaps = currencyComp.Bottlecaps,
                NcrDollars = currencyComp.NcrDollars,
                LegionDenarii = currencyComp.LegionDenarii,
                PrewarMoney = currencyComp.PrewarMoney,
            }, actor.PlayerSession.Channel);
        }

        args.Handled = true;
    }

    // #Misfits Fix - Load currency on spawn, when mind is guaranteed to be ready.
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        // #Misfits Fix - EnsureComp (mirrors PersistentPlayerDataSystem) so ANY player species
        // mob gets a wallet, even if its prototype forgot to declare PersistentCurrency.
        // Previously TryComp silently skipped e.g. the C-27 chassis (inherits BaseMobSpecies,
        // not BaseMobSpeciesOrganic), so its balance was never loaded or saved between rounds.
        var comp = EnsureComp<PersistentCurrencyComponent>(args.Mob);

        if (args.Player.AttachedEntity == args.Mob)
            LoadCurrencyAsync(args.Mob, comp, args.Player);
    }

    private void OnCurrencyShutdown(Entity<PersistentCurrencyComponent> ent, ref ComponentShutdown args)
    {
        // #Misfits Fix - Flush the balance to the DB on entity deletion so the wallet is never
        // lost at round end, even if the last change didn't trigger an explicit SaveCurrency.
        // Mirrors PersistentPlayerDataSystem.OnShutdown.
        if (ent.Comp.Loaded && ent.Comp.UserId != null && ent.Comp.CharacterName != null)
            SaveCurrency(ent.Comp.UserId, ent.Comp.CharacterName, ent.Comp);
    }

    private void OnPlayerAttached(Entity<PersistentCurrencyComponent> ent, ref PlayerAttachedEvent args)
    {
        // Handles reconnects and late-attachment; LoadCurrencyAsync is idempotent (checks Loaded)
        LoadCurrencyAsync(ent, ent.Comp, args.Player);
    }

    private async void LoadCurrencyAsync(EntityUid uid, PersistentCurrencyComponent comp, ICommonSession session)
    {
        if (comp.Loaded)
            return;

        if (!_mind.TryGetMind(uid, out _, out var mind))
            return;

        var characterName = mind.CharacterName;
        if (string.IsNullOrEmpty(characterName))
            return;

        comp.UserId = session.UserId.ToString();
        comp.CharacterName = characterName;

        var playerId = session.UserId.UserId;

        try
        {
            var row = await _db.GetCharacterCurrencyAsync(playerId, characterName);
            comp.Bottlecaps = row?.Bottlecaps ?? 0;
            comp.NcrDollars = row?.NcrDollars ?? 0; // #Cythisiax Add
            comp.Silver = row?.Silver ?? 0; // #Cythisiax Add
            comp.Gold = row?.Gold ?? 0; // #Cythisiax Add
            comp.LegionDenarii = row?.LegionDenarii ?? 0; // #Nuclear14 Add
            comp.PrewarMoney = row?.PrewarMoney ?? 0; // #Nuclear14 Add
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to load currency for {characterName}: {ex}");
        }

        comp.Loaded = true;
        Dirty(uid, comp);

        // #Misfits Fix - Send updated wallet state to client after async load completes.
        // Without this the wallet window shows 0 if opened before the load finishes.
        if (TryComp<ActorComponent>(uid, out var actor))
        {
            RaiseNetworkEvent(new CurrencyWalletStateMessage
            {
                Bottlecaps = comp.Bottlecaps,
                NcrDollars = comp.NcrDollars,
                LegionDenarii = comp.LegionDenarii,
                PrewarMoney = comp.PrewarMoney,
            }, actor.PlayerSession.Channel);
        }
    }

    private void SaveCurrency(string userId, string characterName, PersistentCurrencyComponent comp)
    {
        if (!Guid.TryParse(userId, out var playerId))
            return;

        _db.UpsertCharacterCurrencyAsync(playerId, characterName, comp.Bottlecaps,
            comp.NcrDollars, comp.Silver, comp.Gold, comp.LegionDenarii, comp.PrewarMoney); // #Cythisiax Add - multi-currency
    }

    // ── One-time JSON → database migration ─────────────────────────────────────

    private async void MigrateJsonToDatabase()
    {
        var userDataPath = _resourceManager.UserData.RootDir ?? ".";
        var jsonPath = Path.Combine(userDataPath, "currency_data.json");

        if (!File.Exists(jsonPath))
            return;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var data = JsonSerializer.Deserialize<Dictionary<string, LegacyCurrencyData>>(json);

            if (data == null || data.Count == 0)
            {
                File.Move(jsonPath, jsonPath + ".migrated");
                return;
            }

            _log.Info($"Migrating {data.Count} currency records from JSON to database...");

            foreach (var (_, record) in data)
            {
                if (string.IsNullOrEmpty(record.UserId) || !Guid.TryParse(record.UserId, out var pid))
                    continue;

                await _db.UpsertCharacterCurrencyAsync(pid, record.CharacterName, record.Bottlecaps); // #Cythisiax - multi-currency migration (legacy records only have caps)
            }

            File.Move(jsonPath, jsonPath + ".migrated");
            _log.Info("Currency JSON migration complete.");
        }
        catch (Exception ex)
        {
            _log.Error($"Failed to migrate currency_data.json to database: {ex}");
        }
    }
}

/// <summary>
/// Legacy JSON data model for one-time migration from currency_data.json.
/// </summary>
internal sealed class LegacyCurrencyData
{
    public string UserId { get; set; } = string.Empty;
    public string CharacterName { get; set; } = string.Empty;
    public int Bottlecaps { get; set; }
}
