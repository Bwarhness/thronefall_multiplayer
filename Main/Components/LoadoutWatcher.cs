using System.Collections.Generic;
using System.Linq;
using Steamworks;
using ThronefallMP.Network;
using ThronefallMP.Network.Packets.Game;
using ThronefallMP.Patches;
using ThronefallMP.UI;
using UnityEngine;

namespace ThronefallMP.Components;

// Drives the shared loadout popup: detects open/close edges by polling LoadoutFrames.PopupOpen,
// diff-watches PerkManager.CurrentlyEquipped for selection changes, broadcasts the loadout packets,
// and renders the weapon-pick status strip. Attached to the Plugin GameObject (always alive).
public class LoadoutWatcher : MonoBehaviour
{
    private bool _open;
    private int _lastPlayerCount;
    private List<Equipment> _selectionSnapshot = new();
    private Equipment _weaponSnapshot = Equipment.Invalid;

    private void Update()
    {
        var network = Plugin.Instance.Network;
        if (!network.Online || !SceneTransitionManagerPatch.InLevelSelect)
        {
            if (_open)
            {
                // Scene changed / went offline while the popup was open; clean up silently.
                _open = false;
                LoadoutStatusStrip.Hide();
                LoadoutState.Reset();
            }
            return;
        }

        var open = LoadoutFrames.PopupOpen;
        if (open && !_open)
        {
            OnOpened(network);
        }
        else if (!open && _open)
        {
            OnClosed(network);
        }
        _open = open;
        if (!open)
        {
            return;
        }

        // Late join while the popup is open: the host re-broadcasts the full loadout context so the
        // new player's popup opens and converges.
        var playerCount = Plugin.Instance.PlayerManager.GetAllPlayers().Count();
        if (network.Server && _lastPlayerCount > 0 && playerCount > _lastPlayerCount)
        {
            BroadcastOpen(network);
            BroadcastSelection(network);
            // The packet carries PlayerId, so the host can speak for everyone — re-send every known
            // pick, not just its own, or the joiner's strip misses the other clients' weapons.
            foreach (var pick in LoadoutState.WeaponPicks)
            {
                network.Send(new LoadoutWeaponPacket { PlayerId = pick.Key, Weapon = pick.Value });
            }
        }
        _lastPlayerCount = playerCount;

        if (LoadoutState.SuppressNextDiff)
        {
            // A remote selection was just applied; re-snapshot without treating it as a local edit.
            CaptureSnapshot();
            LoadoutState.SuppressNextDiff = false;
            UpdateStrip();
            return;
        }

        // Diff local state against the snapshot and broadcast local edits.
        var (selection, weapon) = Capture();
        if (!selection.SequenceEqual(_selectionSnapshot))
        {
            _selectionSnapshot = selection;
            network.Send(new LoadoutSelectionPacket { Selection = new List<Equipment>(selection) });
        }
        if (weapon != _weaponSnapshot)
        {
            _weaponSnapshot = weapon;
            LoadoutState.WeaponPicks[Plugin.Instance.PlayerManager.LocalId] = weapon;
            network.Send(new LoadoutWeaponPacket
            {
                PlayerId = Plugin.Instance.PlayerManager.LocalId,
                Weapon = weapon
            });
        }

        UpdateStrip();
    }

    private void OnOpened(Network.Network network)
    {
        CaptureSnapshot();
        _lastPlayerCount = Plugin.Instance.PlayerManager.GetAllPlayers().Count();
        LoadoutState.WeaponPicks[Plugin.Instance.PlayerManager.LocalId] = _weaponSnapshot;

        if (LoadoutState.RemoteOpenPending)
        {
            // Opened because another player opened it: publish only our own weapon pick; the opener's
            // selection broadcast is authoritative for the shared perks/mutators.
            LoadoutState.RemoteOpenPending = false;
            BroadcastWeapon(network);
        }
        else
        {
            // We initiated: tell everyone to open, and publish the full initial state.
            BroadcastOpen(network);
            BroadcastSelection(network);
            BroadcastWeapon(network);
        }

        LoadoutStatusStrip.Show();
        UpdateStrip();
    }

    // ESC semantics: a local ESC from the grid only pops back to the level-select frame — no close
    // edge, no packet — and the vanilla cancel-revert that fires then is diff-broadcast like any local
    // edit, so "revert wins for all". Remote closes lock the loadout in first (LoadoutFrames) and
    // revert nothing.
    private void OnClosed(Network.Network network)
    {
        if (LoadoutState.RemoteClosePending)
        {
            LoadoutState.RemoteClosePending = false;
        }
        else
        {
            network.Send(new LoadoutClosePacket());
        }

        LoadoutStatusStrip.Hide();
        LoadoutState.WeaponPicks.Clear();
        // The close path never reaches the suppress block, so a flag set by HandleLoadoutClose would
        // otherwise leak into the next open.
        LoadoutState.SuppressNextDiff = false;
    }

    private void BroadcastOpen(Network.Network network)
    {
        var info = LevelInteractor.lastActiveLevelInfo;
        if (info != null)
        {
            network.Send(new LoadoutOpenPacket { Scene = info.sceneName });
        }
    }

    private void BroadcastSelection(Network.Network network)
    {
        network.Send(new LoadoutSelectionPacket { Selection = new List<Equipment>(_selectionSnapshot) });
    }

    private void BroadcastWeapon(Network.Network network)
    {
        network.Send(new LoadoutWeaponPacket
        {
            PlayerId = Plugin.Instance.PlayerManager.LocalId,
            Weapon = _weaponSnapshot
        });
    }

    // Snapshot = (sorted non-weapon Equipment list, the single local weapon or Invalid).
    private (List<Equipment> selection, Equipment weapon) Capture()
    {
        var selection = new List<Equipment>();
        var weapon = Equipment.Invalid;
        if (PerkManager.instance != null)
        {
            foreach (var item in PerkManager.instance.CurrentlyEquipped)
            {
                var equipment = Equip.Convert(item.name);
                if (equipment == Equipment.Invalid)
                {
                    continue;
                }

                if (Equip.Weapons.Contains(equipment))
                {
                    weapon = equipment;
                }
                else
                {
                    selection.Add(equipment);
                }
            }
        }

        selection.Sort();
        return (selection, weapon);
    }

    private void CaptureSnapshot()
    {
        (_selectionSnapshot, _weaponSnapshot) = Capture();
    }

    private void UpdateStrip()
    {
        var parts = new List<string>();
        foreach (var player in Plugin.Instance.PlayerManager.GetAllPlayers())
        {
            var name = SteamFriends.GetFriendPersonaName(player.SteamID);
            LoadoutState.WeaponPicks.TryGetValue(player.Id, out var pick);
            var weaponName = "—";
            if (pick != Equipment.Invalid)
            {
                var equippable = Equip.Convert(pick);
                weaponName = equippable != null ? equippable.displayName : pick.ToString();
            }
            parts.Add($"{name}: {weaponName}");
        }

        LoadoutStatusStrip.SetText(string.Join("   ·   ", parts));
    }
}
