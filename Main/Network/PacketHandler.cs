using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Steamworks;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets;
using ThronefallMP.Network.Packets.Administration;
using ThronefallMP.Network.Packets.Game;
using ThronefallMP.Network.Packets.PlayerCommand;
using ThronefallMP.Network.Sync;
using ThronefallMP.Patches;
using ThronefallMP.UI;
using ThronefallMP.Utils;
using UnityEngine;

namespace ThronefallMP.Network;

public enum PacketId
{
    // These 3 will always have the same packetid between versions.
    Approval,
    Disconnect,
    PeerSync,
    
    SyncPing,
    SyncPong,
    SyncPingInfo,
    SyncLevelData,
    SyncResource,
    SyncPlayer,
    SyncPlayerInput,
    SyncAllyPathfinder,
    SyncEnemyPathfinder,
    SyncPosition,
    SyncHp,
    
    Combined,
    DayNight,
    EnemySpawn,
    DamageFeedback,
    RequestLevel,
    RestartLevel,
    Resign,
    TeleportPlayer,
    WeaponRequest,
    WeaponResponse,
    
    BuildOrUpgrade,
    CancelBuild,
    CommandAdd,
    CommandHoldPosition,
    CommandPlace,
    ConfirmBuild,
    ManualAttack,
    Victory,
    LockTarget,
    BossSpawn,
    LoadoutOpen,
    LoadoutSelection,
    LoadoutWeapon,
    LoadoutClose,
    LoadoutStateRequest,
    HostUnlocks,
}

public static class PacketHandler
{
    public static bool AwaitingConnectionApproval;
    
    private static readonly Dictionary<PacketId, Action<SteamNetworkingIdentity, BasePacket>> Handlers = new()
    {
        { ApprovalPacket.PacketID, HandleApproval },
        { DisconnectPacket.PacketID, HandleDisconnect },
        { PeerListPacket.PacketID, HandlePeerList },
        
        { BossSpawnPacket.PacketID, HandleBossSpawn },
        { LoadoutOpenPacket.PacketID, HandleLoadoutOpen },
        { LoadoutSelectionPacket.PacketID, HandleLoadoutSelection },
        { LoadoutWeaponPacket.PacketID, HandleLoadoutWeapon },
        { LoadoutClosePacket.PacketID, HandleLoadoutClose },
        { LoadoutStateRequestPacket.PacketID, HandleLoadoutStateRequest },
        { HostUnlocksPacket.PacketID, HandleHostUnlocks },
        { DamageFeedbackPacket.PacketID, HandleDamageFeedback },
        { DayNightPacket.PacketID, HandleDayNight },
        { EnemySpawnPacket.PacketID, HandleEnemySpawn },
        { LockTargetPacket.PacketID, HandleLockTarget },
        { TeleportPlayerPacket.PacketID, HandlePlayerTeleport },
        { ResignPacket.PacketID, HandleResign },
        { VictoryPacket.PacketID, HandleVictory },

        { BuildOrUpgradePacket.PacketID, HandleBuildOrUpgrade },
        { CancelBuildPacket.PacketID, HandleCancelBuild },
        { CommandAddPacket.PacketID, HandleCommandAdd },
        { CommandPlacePacket.PacketID, HandleCommandPlace },
        { CommandHoldPositionPacket.PacketID, HandleCommandHoldPosition },
        { ConfirmBuildPacket.PacketID, HandleConfirmBuild },
        { ManualAttackPacket.PacketID, HandleManualAttack },
    };

    public static void HandlePacket(SteamNetworkingIdentity sender, BasePacket packet)
    {
        if (SyncManager.HandlePacket(sender, packet))
        {
            Plugin.Log.LogDebugFiltered("PacketHandler", $"Packet {packet.TypeID} handled by sync");
            return;
        }
        
        var found = Handlers.TryGetValue(packet.TypeID, out var handler);
        if (found)
        {
            Plugin.Log.LogDebugFiltered("PacketHandler", $"Handling {packet.TypeID} packet");
            handler(sender, packet);
        }
        else
        {
            Plugin.Log.LogWarningFiltered("PacketHandler", $"No handler for packet {packet.TypeID}.");
        }
    }

    private static void HandlePeerList(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (PeerListPacket)ipacket;

        if (AwaitingConnectionApproval)
        {
            // Currently we only allow joining a lobby if we are in level select.
            SceneTransitionManager.instance.TransitionFromNullToLevelSelect();
            UIManager.LobbyListPanel.CloseConnectingDialog();
            UIManager.CloseAllPanels();
            AwaitingConnectionApproval = false;
        }
        
        Plugin.Log.LogInfoFiltered("PacketHandler", "Received player list");
        var steamId = SteamUser.GetSteamID();
        Plugin.Instance.PlayerManager.LocalId = 0;
        foreach (var data in packet.Players)
        {
            if (data.SteamId == steamId)
            {
                Plugin.Instance.PlayerManager.LocalId = data.Id;
            }
            
            var player = Plugin.Instance.PlayerManager.CreateOrGet(data.SteamId, data.Id);
            player.SpawnID = data.SpawnId;
            if (player.Object == null && !Plugin.Instance.PlayerManager.InstantiatePlayer(player, data.Position))
            {
                continue;
            }
            
            player.Controller.enabled = false;
            player.Object.transform.position = data.Position;
            player.Controller.enabled = true;
        }
    }

    private static void HandleDayNight(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (DayNightPacket)ipacket;
        if (packet.Timestate == DayNightCycle.Instance.CurrentTimestate)
        {
            return;
        }
        
        if (Plugin.Instance.Network.Server && sender.GetSteamID() != Network.SteamId)
        {
            Plugin.Instance.Network.Send(packet);
        }
        
        if (packet.Timestate == DayNightCycle.Timestate.Night)
        {
            NightCallPatch.TriggerNightFall();
        }
        else
        {
            if (TagManager.instance != null && EnemySpawner.instance != null)
            {
                EnemySpawner.instance.StopSpawnAfterWaveAndReset();
                foreach (var enemy in Identifier.GetGameObjects(IdentifierType.Enemy))
                {
                    HpPatch.AllowHealthChangeOnClient = true;
                    enemy.GetComponent<Hp>().TakeDamage(9999, null, true);
                    HpPatch.AllowHealthChangeOnClient = false;
                }
            }
            
            Traverse.Create(DayNightCycle.Instance).Field<float>("currentNightLength").Value = packet.NightLength;
            var switchToDayCoroutine = Traverse.Create(DayNightCycle.Instance).Method("SwitchToDayCoroutine");
            DayNightCycle.Instance.StartCoroutine(switchToDayCoroutine.GetValue<IEnumerator>());
        }
    }

    private static void HandleEnemySpawn(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (EnemySpawnPacket)ipacket;
        EnemySpawnerPatch.SpawnEnemy(packet.Wave, packet.Spawn, packet.Position, packet.Id, packet.Coins, packet.Elite);
    }

    private static void HandleBossSpawn(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (BossSpawnPacket)ipacket;
        var spawn = BossSpawnRegistry.Resolve(packet.SpawnRef);
        if (spawn == null)
        {
            Plugin.Log.LogWarningFiltered("PacketHandler", $"No boss spawn registered for ref {packet.SpawnRef}");
            return;
        }

        // Spawns the minion with its authoritative Identifier id and advances the Spawn's
        // spawnedUnits/finished bookkeeping so the boss' wave loop terminates on this peer too.
        EnemySpawnerPatch.SpawnEnemy(spawn, packet.DifficultyMulti, packet.Position, packet.Id, packet.Coins, packet.Elite);
    }

    private static void HandleLoadoutOpen(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (LoadoutOpenPacket)ipacket;
        Plugin.Log.LogInfo(
            $"[LoadoutDiag] open rx scene={packet.Scene} inLevelSelect={SceneTransitionManagerPatch.InLevelSelect} " +
            $"mgr={LevelSelectManager.instance != null}");
        if (!SceneTransitionManagerPatch.InLevelSelect || LevelSelectManager.instance == null ||
            UIFrameManager.instance == null)
        {
            return;
        }

        var interactors = Traverse.Create(LevelSelectManager.instance)
            .Field<LevelInteractor[]>("levelInteractors").Value;
        foreach (var interactor in interactors)
        {
            if (interactor.levelInfo.sceneName != packet.Scene)
            {
                continue;
            }

            if (!interactor.CanBePlayed)
            {
                // Vanilla InteractionBegin refuses locked levels; progress can differ per machine.
                Plugin.Log.LogInfo($"LoadoutOpen for locked level '{packet.Scene}', ignoring");
                return;
            }

            var ui = UIFrameManager.instance;
            if (LoadoutFrames.IsPopupFrame(ui.ActiveFrame))
            {
                if (LevelInteractor.lastActiveLevelInfo == interactor.levelInfo)
                {
                    // Already open on the same level (simultaneous open). No UI edge will occur, so the
                    // pending flags must NOT be set — they would leak and misclassify the next genuine
                    // local open as remote.
                    return;
                }

                // Open on a DIFFERENT level (possible with mixed progression, not just a race):
                // rebuild for the arbitrated level. Close the whole family so the reopen genuinely
                // re-shows the level-select frame (CloseActiveFrame alone pops grid->level-select and
                // ForceOpenLevelSelect would early-return, leaving the old level's title/buttons).
                // The close+reopen completes inside this call — no UI edge — so set only
                // SuppressNextDiff (the reopen's OnShow can mutate the selection).
                LevelInteractor.lastActiveLevelInfo = interactor.levelInfo;
                LoadoutState.SuppressNextDiff = true;
                LoadoutFrames.CloseAllPopupFrames();
                UIFrameManager.ForceOpenLevelSelect();
                OpenRemoteLoadoutGrid();
                if (Plugin.Instance.Network.Server)
                {
                    // The relay excludes the original sender, so when two machines opened different
                    // levels their Open packets cross and they end up swapped. A fresh host-origin
                    // packet reaches everyone (same-level receivers no-op), converging on the host's
                    // adopted level.
                    Plugin.Instance.Network.Send(new LoadoutOpenPacket { Scene = packet.Scene });
                }
                return;
            }

            // Replicate vanilla LevelInteractor.InteractionBegin for this level.
            LoadoutState.RemoteOpenPending = true;
            LevelInteractor.lastActiveLevelInfo = interactor.levelInfo;
            UIFrameManager.ForceOpenLevelSelect();
            if (!LoadoutFrames.PopupOpen)
            {
                // The frame didn't actually open (unexpected active frame) — don't leak the flag.
                LoadoutState.RemoteOpenPending = false;
                return;
            }

            OpenRemoteLoadoutGrid();
            return;
        }

        Plugin.Log.LogInfo($"LoadoutOpen for unknown level '{packet.Scene}', ignoring");
    }

    private static void OpenRemoteLoadoutGrid()
    {
        var grid = LoadoutFrames.GridFrame;
        if (grid == null || UIFrameManager.instance.ActiveFrame == grid)
        {
            return;
        }

        // LoadoutUIHelper.OnShow() rebuilds the grid and can mutate CurrentlyEquipped
        // (auto-equip first weapon, etc.). Do not broadcast that rebuild as a local edit.
        LoadoutState.SuppressNextDiff = true;
        UIFrameManager.instance.ChangeActiveFrame(grid);
    }

    private static void HandleLoadoutSelection(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (LoadoutSelectionPacket)ipacket;
        if (PerkManager.instance == null || !SceneTransitionManagerPatch.InLevelSelect || !LoadoutFrames.PopupOpen)
        {
            // Never rewrite the live perk set mid-level (a toggle relayed during the level-start race
            // would otherwise strip and re-add perks during gameplay). Likewise a machine whose popup
            // isn't open (it ignored the Open: locked level / different scene) must not have its
            // session-persistent loadout silently rewritten by remote toggles.
            return;
        }

        // Replace the non-weapon portion of CurrentlyEquipped; keep the local player's weapon entries untouched.
        var equipped = PerkManager.instance.CurrentlyEquipped;
        for (var i = equipped.Count - 1; i >= 0; --i)
        {
            if (!Equip.Weapons.Contains(Equip.Convert(equipped[i].name)))
            {
                equipped.RemoveAt(i);
            }
        }

        var requested = new List<Equipment>();
        foreach (var item in packet.Selection)
        {
            if (item == Equipment.Invalid || Equip.Weapons.Contains(item) || Equip.Convert(item) == null)
            {
                // Convert == null: unknown value from the wire — equipping it would throw deeper in.
                continue;
            }

            Equip.EquipEquipment(item);
            requested.Add(item);
        }

        LoadoutState.SuppressNextDiff = true;

        // The grid caches per-button state (TFUIEquippable.selectedForLoadout is only written on user
        // input, not derived per frame), so a remotely-applied selection must force a rebuild or the
        // visuals — and the max-perk eviction that reads them — go stale. OnShow destroys and rebuilds.
        // OnShow can itself clamp the selection (locked items, perk-count limits, weapon auto-equip);
        // when it does, the clamped result must NOT be suppressed — letting the watcher diff it
        // broadcasts the correction (the clamp is idempotent, so this converges instead of looping).
        var active = UIFrameManager.instance != null ? UIFrameManager.instance.ActiveFrame : null;
        var helper = LoadoutFrames.HelperFor(active);
        if (helper != null)
        {
            helper.OnShow();
            var applied = CaptureNonWeaponSelection();
            requested.Sort();
            if (!applied.SequenceEqual(requested))
            {
                LoadoutState.SuppressNextDiff = false;
            }
        }
    }

    // The non-weapon part of CurrentlyEquipped, sorted (mirrors the LoadoutWatcher's Capture filter).
    private static List<Equipment> CaptureNonWeaponSelection()
    {
        var selection = new List<Equipment>();
        foreach (var item in PerkManager.instance.CurrentlyEquipped)
        {
            var equipment = Equip.Convert(item.name);
            if (equipment != Equipment.Invalid && !Equip.Weapons.Contains(equipment))
            {
                selection.Add(equipment);
            }
        }

        selection.Sort();
        return selection;
    }

    private static void HandleLoadoutWeapon(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (LoadoutWeaponPacket)ipacket;
        if (packet.PlayerId == Plugin.Instance.PlayerManager.LocalId)
        {
            // Defensive: the relay excludes the sender, so this only fires via handleLocal sends or a
            // spoofed PlayerId — the local pick is already authoritative in WeaponPicks.
            return;
        }

        LoadoutState.WeaponPicks[packet.PlayerId] = packet.Weapon;
    }

    private static void HandleLoadoutStateRequest(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        if (!Plugin.Instance.Network.Server || !LoadoutFrames.PopupOpen ||
            LevelInteractor.lastActiveLevelInfo == null)
        {
            return;
        }

        // A peer that finished joining (or returned to the overworld) while the popup is open missed
        // the original broadcasts — replay the full popup state to just that peer.
        Plugin.Log.LogInfo("[LoadoutDiag] replaying loadout state to a late peer");
        var network = Plugin.Instance.Network;
        network.SendSingle(new LoadoutOpenPacket { Scene = LevelInteractor.lastActiveLevelInfo.sceneName }, sender);
        network.SendSingle(BuildHostUnlocksPacket(), sender);
        network.SendSingle(new LoadoutSelectionPacket { Selection = CaptureNonWeaponSelection() }, sender);
        foreach (var pick in LoadoutState.WeaponPicks)
        {
            network.SendSingle(new LoadoutWeaponPacket { PlayerId = pick.Key, Weapon = pick.Value }, sender);
        }
    }

    internal static HostUnlocksPacket BuildHostUnlocksPacket()
    {
        var packet = new HostUnlocksPacket();
        if (PerkManager.instance == null)
        {
            return packet;
        }

        foreach (var equippable in PerkManager.instance.allEquippables)
        {
            if (!equippable.IsUnlocked)
            {
                continue;
            }

            var equipment = Equip.Convert(equippable.name);
            if (equipment != Equipment.Invalid)
            {
                packet.Unlocks.Add(equipment);
            }
        }

        return packet;
    }

    private static void HandleHostUnlocks(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        if (!Plugin.Instance.Network.IsServer(sender.GetSteamID()))
        {
            return;
        }

        var packet = (HostUnlocksPacket)ipacket;
        HostUnlocks.Session.Clear();
        foreach (var equipment in packet.Unlocks)
        {
            if (equipment != Equipment.Invalid)
            {
                HostUnlocks.Session.Add(equipment);
            }
        }

        if (LoadoutFrames.PopupOpen && UIFrameManager.instance != null)
        {
            LoadoutFrames.HelperFor(UIFrameManager.instance.ActiveFrame)?.OnShow();
        }
    }

    private static void HandleLoadoutClose(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        if (!SceneTransitionManagerPatch.InLevelSelect || UIFrameManager.instance == null)
        {
            return;
        }

        if (LoadoutFrames.IsPopupFrame(UIFrameManager.instance.ActiveFrame))
        {
            LoadoutState.RemoteClosePending = true;
            // Closing can revert the loadout to its on-open snapshot (ResetLoadoutOnCancel); the
            // re-snapshot guard keeps the watcher from broadcasting that revert as a local edit.
            LoadoutState.SuppressNextDiff = true;
            LoadoutFrames.CloseAllPopupFrames();
        }

        LoadoutState.WeaponPicks.Clear();
    }

    private static void HandlePlayerTeleport(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (TeleportPlayerPacket)ipacket;
        var player = Plugin.Instance.PlayerManager.Get(packet.PlayerId);
        if (player.Object == null)
        {
            return;
        }
        
        player.Controller.enabled = false;
        player.Object.transform.position = packet.Position;
        player.Controller.enabled = true;
    }

    private static void HandleResign(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        try
        {
            Plugin.Log.LogDebug("Resign");
            UIFrameManager.instance.CloseAllFrames();
            LocalGamestate.Instance.SetState(LocalGamestate.State.AfterMatchDefeat, false, true);
        }
        catch
        {
            Plugin.Log.LogError("InGameResignUIHelper: Bro we can not resign here.");
        }
    }

    private static void HandleVictory(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        // Host-authoritative victory. The server auto-rebroadcasts this (ShouldPropagate) just like ResignPacket, so
        // we must NOT re-Send here. SetState early-returns if already in a post-match state, so it's idempotent with
        // the vanilla SwitchToDayCoroutine trigger and with the host's own loopback copy.
        try
        {
            Plugin.Log.LogDebug("Victory");
            UIFrameManager.instance.CloseAllFrames();
            LocalGamestate.Instance.SetState(LocalGamestate.State.AfterMatchVictory, false, true);
        }
        catch
        {
            Plugin.Log.LogError("Could not transition to victory state.");
        }
    }

    private static void HandleCommandAdd(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (CommandAddPacket)ipacket;
        var player = Plugin.Instance.PlayerManager.Get(packet.Player);
        if (player.Object == null)
        {
            return;
        }
        
        var command = player.Object.GetComponent<CommandUnits>();
        foreach (var unit in packet.Units)
        {
            var component = unit.Get()?.GetComponent<PathfindMovementPlayerunit>();
            if (component != null)
            {
                CommandUnitsPatch.AddUnit(command, component);
            }
        }
    }

    private static void HandleCommandPlace(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (CommandPlacePacket)ipacket;
        var player = Plugin.Instance.PlayerManager.Get(packet.Player);
        if (player.Object == null)
        {
            return;
        }
        
        var command = player.Object.GetComponent<CommandUnits>();
        CommandUnitsPatch.EmitWaypoint(command, packet.Units.Count > 0);
        foreach (var unit in packet.Units)
        {
            var component = unit.Unit.Get()?.GetComponent<PathfindMovementPlayerunit>();
            if (component != null)
            {
                CommandUnitsPatch.PlaceUnit(command, component, unit.Home);
            }
        }
    }

    private static void HandleCommandHoldPosition(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (CommandHoldPositionPacket)ipacket;
        var player = Plugin.Instance.PlayerManager.Get(packet.Player);
        if (player?.Object == null)
        {
            return;
        }
        
        var command = player.Object.GetComponent<CommandUnits>();
        if (packet.Units.Count > 0)
        {
            CommandUnitsPatch.PlayHoldSound(command);
        }
        
        foreach (var unit in packet.Units)
        {
            var component = unit.Unit.Get()?.GetComponent<PathfindMovementPlayerunit>();
            if (component != null)
            {
                CommandUnitsPatch.HoldPosition(component, unit.Home);
            }
        }
    }

    private static void HandleManualAttack(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (ManualAttackPacket)ipacket;
        var player = Plugin.Instance.PlayerManager.Get(packet.Player)?.Object;
        if (player == null)
        {
            return;
        }
        
        var attack = player.GetComponent<PlayerInteraction>().ManualAttack;
        attack.TryToAttack();
    }

    private static void HandleLockTarget(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (LockTargetPacket)ipacket;
        if (packet.PlayerId == Plugin.Instance.PlayerManager.LocalId)
        {
            // Our own lock state is authoritative locally; ignore the echo.
            return;
        }

        var player = Plugin.Instance.PlayerManager.Get(packet.PlayerId)?.Object;
        if (player == null)
        {
            return;
        }

        var attack = player.GetComponent<PlayerInteraction>().ManualAttack;
        if (attack == null)
        {
            return;
        }

        var target = packet.Target.Get()?.GetComponent<TaggedObject>();
        var traverse = Traverse.Create(attack);
        traverse.Field<TaggedObject>("preferredTarget").Value = target;
        traverse.Field<bool>("preferredTargetPreviouslySet").Value = target != null;
    }

    private static void HandleApproval(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (ApprovalPacket)ipacket;
        Plugin.Log.LogInfoFiltered("PacketHandler", $"Handling approval of {sender.GetSteamID64()}");
        if (!packet.SameVersion)
        {
            Plugin.Log.LogInfoFiltered("PacketHandler", $"{sender.GetSteamID64()} has wrong version");
            Plugin.Instance.Network.KickPeer(sender.GetSteamID(), DisconnectPacket.Reason.WrongVersion);
        }
        else if (Plugin.Instance.Network.Authenticate(packet.Password))
        {
            Plugin.Log.LogInfoFiltered("PacketHandler", $"{sender.GetSteamID64()} Authenticated");
            Plugin.Instance.Network.AddPlayer(sender.GetSteamID());
        }
        else
        {
            Plugin.Log.LogInfoFiltered("PacketHandler", $"Authentication of {sender.GetSteamID64()} failed");
            Plugin.Instance.Network.KickPeer(sender.GetSteamID(), DisconnectPacket.Reason.WrongPassword);
        }
    }

    private static void HandleDisconnect(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (DisconnectPacket)ipacket;
        AwaitingConnectionApproval = false;
        Plugin.Log.LogInfoFiltered("PacketHandler", $"Disconnected with reason {packet.DisconnectReason}");
        var message = packet.DisconnectReason switch
        {
            DisconnectPacket.Reason.Kicked => "You were kicked!",
            DisconnectPacket.Reason.WrongPassword => "You gave the wrong password.",
            DisconnectPacket.Reason.WrongVersion => "Different multiplayer mod version.",
            _ => "Unknown"
        };
            
        UIManager.LobbyListPanel.CloseConnectingDialog();
        UIManager.CreateMessageDialog("Disconnected", message);
    }

    private static void HandleBuildOrUpgrade(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        if (!Plugin.Instance.Network.Server)
        {
            return;
        }
        
        var packet = (BuildOrUpgradePacket)ipacket;
        var info = BuildSlotPatch.GetUpgradeInfo(packet.BuildingId, packet.Level, packet.Choice);
        if (GlobalData.Balance < info.Cost || info.CurrentLevel > packet.Level)
        {
            Plugin.Log.LogInfoFiltered("PacketHandler", $"Cancel building {packet.BuildingId}:{packet.Level}:{packet.Choice} for {info.Cost}");
            Plugin.Instance.Network.SendSingle(new CancelBuildPacket()
            {
                BuildingId = packet.BuildingId
            }, sender);
        }
        else
        {
            Plugin.Log.LogInfoFiltered("PacketHandler", $"Confirmed building {packet.BuildingId}:{packet.Level}:{packet.Choice} for {info.Cost}");
            Plugin.Instance.Network.Send(new ConfirmBuildPacket()
            {
                BuildingId = packet.BuildingId,
                Level = packet.Level,
                Choice = packet.Choice,
                PlayerID = Plugin.Instance.PlayerManager.Get(sender.GetSteamID()).Id
            }, true);
            GlobalData.Balance -= info.Cost;
        }
    }

    private static void HandleCancelBuild(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (CancelBuildPacket)ipacket;
        Plugin.Log.LogInfoFiltered("PacketHandler", $"Local build of {packet.BuildingId} cancelled");
        BuildSlotPatch.CancelBuild(packet.BuildingId);
        GlobalData.LocalBalanceDelta = 0;
    }

    private static void HandleConfirmBuild(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (ConfirmBuildPacket)ipacket;
        if (packet.PlayerID == Plugin.Instance.PlayerManager.LocalPlayer.Id)
        {
            Plugin.Log.LogInfoFiltered("PacketHandler", $"Local build of {packet.BuildingId}:{packet.Level}:{packet.Choice} confirmed");
            GlobalData.LocalBalanceDelta = 0;
        }
        else
        {
            Plugin.Log.LogInfoFiltered("PacketHandler", $"Building {packet.BuildingId}:{packet.Level}:{packet.Choice}");
        }
        
        HpPatch.AllowHealthChangeOnClient = true;
        BuildSlotPatch.HandleUpgrade(
            packet.PlayerID,
            packet.BuildingId,
            packet.Level,
            packet.Choice
        );
        HpPatch.AllowHealthChangeOnClient = false;
    }

    private static void HandleDamageFeedback(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (DamageFeedbackPacket)ipacket;
        var target = packet.Target.Get();
        if (target == null)
        {
            return;
        }

        var hp = target.GetComponent<Hp>();
        hp.OnReceiveDamage?.Invoke(packet.CausedByPlayer);
    }
}