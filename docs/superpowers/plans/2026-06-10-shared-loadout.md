# Shared MP Loadout Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When any player opens a level's loadout popup, it opens for all players; perk/mutator toggles sync live; each player picks their own weapon (visible to all via a status strip); Start is gated until everyone has a weapon and feeds the existing level-start flow (no more separate weapon dialog).

**Architecture:** Pure state-sync, no new vanilla-UI hooks: a per-frame `LoadoutWatcher` MonoBehaviour polls `LevelSelectManager.instance.PreLevelMenuIsOpen` for open/close edges and diff-watches `PerkManager.instance.CurrentlyEquipped` for selection changes, broadcasting 4 new packets (host relays via `ShouldPropagate`). Remote open replicates vanilla `LevelInteractor.InteractionBegin` (set static `LevelInteractor.lastActiveLevelInfo`, call `ForceOpenLevelSelect()`). The Start gate hooks `On.LevelSelectManager.PlayButtonPressed`. At level start, the host pre-fills the weapon handshake from the loadout picks so the old `WeaponDialog` flow is skipped (kept as fallback).

**Tech Stack:** C# (BepInEx 5 plugin), MonoMod HookGen (`On.*` events), HarmonyLib `Traverse`, TextMeshPro, Steamworks.NET. **No test framework exists** (game mod, no .NET SDK on this machine): the verification for every task is `bash /c/Users/nalar/build_mod.sh` (compile = the test; it validates every game-API reference), plus `tf_traverse` for reflection targets, plus a final live checklist.

**Build command (all tasks):** `bash /c/Users/nalar/build_mod.sh` → expected last lines: `EXIT: 0` and the dll path. If EXIT != 0, fix compile errors before proceeding.

**Verified game facts this plan relies on (decompiled 2026-06-10, do not re-derive):**
- `LevelInteractor.InteractionBegin`: `if (!CanBePlayed) return; LevelInteractor.lastActiveLevelInfo = levelInfo; UIFrameManager.TryOpenLevelSelect();` — `lastActiveLevelInfo` is a **public static** field of type `LevelInfo`.
- `LevelInfo.sceneName` — public string field.
- `UIFrameManager.instance` (public static), `ForceOpenLevelSelect()` (public, switches to the level-select frame, early-returns if already active), `CloseActiveFrame()` (public), `ActiveFrame` (public property), private field `levelSelectFrame` (`UIFrame`).
- `LevelSelectManager.instance` (public static), `PreLevelMenuIsOpen` (public bool property, = `openedLevel != null`; the vanilla machinery downstream of `TryOpen/ForceOpenLevelSelect` maintains `openedLevel`), private field `levelInteractors` (`LevelInteractor[]`), `PlayButtonPressed()` (public; applies fixed loadouts then calls `SceneTransitionManager.TransitionFromLevelSelectToLevel(sceneName)` → `TransitionToScene` → the mod's existing intercept).
- MMHOOK exposes `On.LevelSelectManager.PlayButtonPressed`.
- `PerkManager.instance.CurrentlyEquipped` — `List<Equippable>`; `Equippable.name` (Unity name) maps via `Equip.Convert(string)`; `Equip.Convert(Equipment)` returns the `Equippable` (its `displayName` is the pretty name); `Equip.Weapons` is `HashSet<Equipment>` of the 4 weapons.
- `Buffer` has `Write(string)/Write(ushort)/Write(byte)/Write(Equipment)` and `ReadString()/ReadUInt16()/ReadByte()/ReadEquipment()`.
- `Network.Send(BasePacket, bool handleLocal = false, CSteamID except = default)`; server auto-rebroadcasts received packets with `ShouldPropagate => true` to all other peers (Network.cs ~line 588).
- `PlayerManager`: `LocalId` (ushort), `Get(int id)`, `GetAllPlayers()` (`IEnumerable<Player>`); `Player` has `Id` (ushort), `SteamID` (CSteamID), `Weapon` (Equipment).
- `UIManager.CreateMessageDialog(string title, string message, string button = null, Color? color = null, MessageDialog.ClickDelegate onClick = null)` exists in `Main/UI/UIManager.cs`.
- Player display names: `SteamFriends.GetFriendPersonaName(CSteamID)` (Steamworks).
- `SceneTransitionManagerPatch.InLevelSelect` (public static bool) is true while in the overworld/level-select scene.
- `Plugin` adds the `Network` MonoBehaviour to its own `gameObject` in `Awake` — the same place to add `LoadoutWatcher`.

**Versioning note:** This feature ships AFTER the pending 1.0.10 (mutator+victory+wall fixes), as **1.1.0**. Do not bump versions in this plan; the release task at the end only builds and commits the feature.

---

### Task 1: LoadoutState — shared static state

**Files:**
- Create: `Main/Network/LoadoutState.cs`

- [ ] **Step 1: Write the file**

```csharp
using System.Collections.Generic;

namespace ThronefallMP.Network;

// Shared state for the synced pre-level loadout popup. Written by the packet handlers and the LoadoutWatcher
// component; read by the Start gate (LevelSelectManagerPatch) and the host's level-start pre-fill (LevelDataSync).
public static class LoadoutState
{
    // Every player's current weapon pick (including the local player). Equipment.Invalid = no pick.
    public static readonly Dictionary<ushort, Equipment> WeaponPicks = new();

    // Set by HandleLoadoutOpen before it force-opens the popup, so the watcher's open-edge knows the open was
    // remote-initiated and must not rebroadcast a LoadoutOpenPacket (or the opener's selection).
    public static bool RemoteOpenPending;

    // Set by HandleLoadoutClose/HandleLoadoutOpen before closing the frame, so the watcher's close-edge knows
    // not to rebroadcast a LoadoutClosePacket.
    public static bool RemoteClosePending;

    // Set by HandleLoadoutSelection after mutating CurrentlyEquipped, so the watcher re-snapshots once without
    // treating the remote change as a local edit (echo-loop guard).
    public static bool SuppressNextDiff;

    public static void Reset()
    {
        WeaponPicks.Clear();
        RemoteOpenPending = false;
        RemoteClosePending = false;
        SuppressNextDiff = false;
    }
}
```

- [ ] **Step 2: Build**

Run: `bash /c/Users/nalar/build_mod.sh` — Expected: `EXIT: 0`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add Main/Network/LoadoutState.cs && git commit -m "feat(loadout): add shared loadout state store"
```

---

### Task 2: The four loadout packets

**Files:**
- Create: `Main/Network/Packets/Game/LoadoutOpenPacket.cs`
- Create: `Main/Network/Packets/Game/LoadoutSelectionPacket.cs`
- Create: `Main/Network/Packets/Game/LoadoutWeaponPacket.cs`
- Create: `Main/Network/Packets/Game/LoadoutClosePacket.cs`
- Modify: `Main/Network/PacketHandler.cs` — PacketId enum (4 values after `Victory`)

- [ ] **Step 1: Add enum values** — in `Main/Network/PacketHandler.cs`, change the end of the `PacketId` enum:

```csharp
    ConfirmBuild,
    ManualAttack,
    Victory,
    LoadoutOpen,
    LoadoutSelection,
    LoadoutWeapon,
    LoadoutClose,
}
```

- [ ] **Step 2: Create `Main/Network/Packets/Game/LoadoutOpenPacket.cs`**

```csharp
namespace ThronefallMP.Network.Packets.Game;

// "A player opened the pre-level loadout popup for this level." Receivers open the same popup by replicating
// vanilla LevelInteractor.InteractionBegin (set lastActiveLevelInfo, force-open the frame).
public class LoadoutOpenPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutOpen;

    public string Scene;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer)
    {
        writer.Write(Scene);
    }

    public override void Receive(Buffer reader)
    {
        Scene = reader.ReadString();
    }
}
```

- [ ] **Step 3: Create `Main/Network/Packets/Game/LoadoutSelectionPacket.cs`**

```csharp
using System.Collections.Generic;

namespace ThronefallMP.Network.Packets.Game;

// Full non-weapon selection (perks + mutators) of the shared loadout. Sent whenever any player toggles something;
// receivers replace the non-weapon part of PerkManager.CurrentlyEquipped with this list. Weapons never ride here.
public class LoadoutSelectionPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutSelection;

    public List<Equipment> Selection = new();

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer)
    {
        writer.Write((byte)Selection.Count);
        foreach (var item in Selection)
        {
            writer.Write(item);
        }
    }

    public override void Receive(Buffer reader)
    {
        var count = reader.ReadByte();
        Selection.Clear();
        for (var i = 0; i < count; ++i)
        {
            Selection.Add(reader.ReadEquipment());
        }
    }
}
```

- [ ] **Step 4: Create `Main/Network/Packets/Game/LoadoutWeaponPacket.cs`**

```csharp
namespace ThronefallMP.Network.Packets.Game;

// One player's personal weapon pick in the loadout popup (Equipment.Invalid = un-picked). Per-player, never merged
// into the shared selection; rendered in the status strip and used to pre-fill the level-start weapon handshake.
public class LoadoutWeaponPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutWeapon;

    public ushort PlayerId;
    public Equipment Weapon;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer)
    {
        writer.Write(PlayerId);
        writer.Write(Weapon);
    }

    public override void Receive(Buffer reader)
    {
        PlayerId = reader.ReadUInt16();
        Weapon = reader.ReadEquipment();
    }
}
```

- [ ] **Step 5: Create `Main/Network/Packets/Game/LoadoutClosePacket.cs`**

```csharp
namespace ThronefallMP.Network.Packets.Game;

// "A player closed the loadout popup" — closes it for everyone and clears pending weapon picks.
public class LoadoutClosePacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutClose;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer) { }

    public override void Receive(Buffer reader) { }
}
```

- [ ] **Step 6: Build**

Run: `bash /c/Users/nalar/build_mod.sh` — Expected: `EXIT: 0`

- [ ] **Step 7: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add Main/Network/Packets/Game/Loadout*.cs Main/Network/PacketHandler.cs && git commit -m "feat(loadout): add the four loadout sync packets"
```

---

### Task 3: Packet registration + handlers

**Files:**
- Modify: `Main/Network/Network.cs` — `PacketTypes` dictionary (after the `VictoryPacket` entry, ~line 502)
- Modify: `Main/Network/PacketHandler.cs` — `Handlers` dictionary + 4 handler methods

- [ ] **Step 1: Register packet types** — in `Main/Network/Network.cs`, after `{ VictoryPacket.PacketID, typeof(VictoryPacket)},` add:

```csharp
        { LoadoutOpenPacket.PacketID, typeof(LoadoutOpenPacket) },
        { LoadoutSelectionPacket.PacketID, typeof(LoadoutSelectionPacket) },
        { LoadoutWeaponPacket.PacketID, typeof(LoadoutWeaponPacket) },
        { LoadoutClosePacket.PacketID, typeof(LoadoutClosePacket) },
```

- [ ] **Step 2: Register handlers** — in `Main/Network/PacketHandler.cs`, after `{ VictoryPacket.PacketID, HandleVictory },` add:

```csharp
        { LoadoutOpenPacket.PacketID, HandleLoadoutOpen },
        { LoadoutSelectionPacket.PacketID, HandleLoadoutSelection },
        { LoadoutWeaponPacket.PacketID, HandleLoadoutWeapon },
        { LoadoutClosePacket.PacketID, HandleLoadoutClose },
```

- [ ] **Step 3: Add handler methods** — in `Main/Network/PacketHandler.cs`, after the `HandleVictory` method add (file already has `using HarmonyLib;` — if not, add it; it also needs `using ThronefallMP.Patches;` which it already has):

```csharp
    private static void HandleLoadoutOpen(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (LoadoutOpenPacket)ipacket;
        if (!SceneTransitionManagerPatch.InLevelSelect || LevelSelectManager.instance == null)
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

            var ui = UIFrameManager.instance;
            var levelSelectFrame = Traverse.Create(ui).Field<UIFrame>("levelSelectFrame").Value;
            if (ui.ActiveFrame == levelSelectFrame && LevelInteractor.lastActiveLevelInfo != interactor.levelInfo)
            {
                // Popup already open on a DIFFERENT level (simultaneous-open race): close it so the re-open
                // below rebuilds the frame for the arbitrated level. ForceOpen alone would early-return.
                LoadoutState.RemoteClosePending = true;
                ui.CloseActiveFrame();
            }

            // Replicate vanilla LevelInteractor.InteractionBegin for this level.
            LoadoutState.RemoteOpenPending = true;
            LevelInteractor.lastActiveLevelInfo = interactor.levelInfo;
            ui.ForceOpenLevelSelect();
            return;
        }

        Plugin.Log.LogInfo($"LoadoutOpen for unknown level '{packet.Scene}', ignoring");
    }

    private static void HandleLoadoutSelection(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (LoadoutSelectionPacket)ipacket;
        if (PerkManager.instance == null)
        {
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

        foreach (var item in packet.Selection)
        {
            if (item != Equipment.Invalid && !Equip.Weapons.Contains(item))
            {
                Equip.EquipEquipment(item);
            }
        }

        LoadoutState.SuppressNextDiff = true;
    }

    private static void HandleLoadoutWeapon(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        var packet = (LoadoutWeaponPacket)ipacket;
        if (packet.PlayerId == Plugin.Instance.PlayerManager.LocalId)
        {
            // Our own pick echoed back by the host relay; the watcher already stored it.
            return;
        }

        LoadoutState.WeaponPicks[packet.PlayerId] = packet.Weapon;
    }

    private static void HandleLoadoutClose(SteamNetworkingIdentity sender, BasePacket ipacket)
    {
        if (!SceneTransitionManagerPatch.InLevelSelect || UIFrameManager.instance == null)
        {
            return;
        }

        var ui = UIFrameManager.instance;
        var levelSelectFrame = Traverse.Create(ui).Field<UIFrame>("levelSelectFrame").Value;
        if (ui.ActiveFrame == levelSelectFrame)
        {
            LoadoutState.RemoteClosePending = true;
            ui.CloseActiveFrame();
        }

        LoadoutState.WeaponPicks.Clear();
    }
```

- [ ] **Step 4: Build**

Run: `bash /c/Users/nalar/build_mod.sh` — Expected: `EXIT: 0`

- [ ] **Step 5: Verify the new Traverse targets resolve**

Run: `cd /c/Users/nalar && ./tf_traverse.exe "/c/Program Files (x86)/Steam/steamapps/common/Thronefall/Thronefall_Data/Managed/Assembly-CSharp.dll" build_out/com.badwolf.thronefall_mp.dll 2>&1 | grep -E "resolved|MISSING"`
Expected: `MISSING ... (0)` — the new `levelInteractors` / `levelSelectFrame` targets must NOT appear under MISSING.

- [ ] **Step 6: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add Main/Network/Network.cs Main/Network/PacketHandler.cs && git commit -m "feat(loadout): register and handle the loadout packets"
```

---

### Task 4: Status strip UI helper

**Files:**
- Create: `Main/UI/LoadoutStatusStrip.cs`

- [ ] **Step 1: Write the file**

```csharp
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace ThronefallMP.UI;

// Small text strip parented into the vanilla level-select (loadout) frame showing each player's weapon pick:
//   "GenoC: Long Bow   ·   Too Damn FilthY: —"
// Created lazily on first Show(); the GameObject lives inside the frame so it shows/hides with it, but we also
// toggle it explicitly so single-player popups don't show an MP strip.
public static class LoadoutStatusStrip
{
    private static GameObject _root;
    private static TextMeshProUGUI _text;

    public static void Show()
    {
        if (_root == null)
        {
            var frame = Traverse.Create(UIFrameManager.instance).Field<UIFrame>("levelSelectFrame").Value;
            if (frame == null)
            {
                return;
            }

            _root = new GameObject("MP Loadout Status", typeof(RectTransform));
            _root.transform.SetParent(frame.transform, false);
            var rect = (RectTransform)_root.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(1400f, 40f);

            _text = _root.AddComponent<TextMeshProUGUI>();
            // Steal the font from an existing label in the frame so it matches the game's style.
            var existing = frame.GetComponentInChildren<TMP_Text>(true);
            if (existing != null)
            {
                _text.font = existing.font;
            }
            _text.fontSize = 24f;
            _text.alignment = TextAlignmentOptions.Center;
            _text.color = Color.white;
            _text.raycastTarget = false;
        }

        _root.SetActive(true);
    }

    public static void Hide()
    {
        if (_root != null)
        {
            _root.SetActive(false);
        }
    }

    public static void SetText(string value)
    {
        if (_text != null)
        {
            _text.text = value;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `bash /c/Users/nalar/build_mod.sh` — Expected: `EXIT: 0`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add Main/UI/LoadoutStatusStrip.cs && git commit -m "feat(loadout): add weapon-pick status strip"
```

---

### Task 5: LoadoutWatcher component + Plugin wiring

**Files:**
- Create: `Main/Components/LoadoutWatcher.cs`
- Modify: `Main/Plugin.cs` — `Awake`, directly after the line that does `gameObject.AddComponent<Network.Network>()` (search for `AddComponent` in Awake; the Network component add is ~line 50)

- [ ] **Step 1: Create `Main/Components/LoadoutWatcher.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using Steamworks;
using ThronefallMP.Network;
using ThronefallMP.Network.Packets.Game;
using ThronefallMP.Patches;
using ThronefallMP.UI;
using UnityEngine;

namespace ThronefallMP.Components;

// Drives the shared loadout popup: detects open/close edges by polling PreLevelMenuIsOpen, diff-watches
// PerkManager.CurrentlyEquipped for selection changes, broadcasts the loadout packets, and renders the
// weapon-pick status strip. Attached to the Plugin GameObject (always alive).
public class LoadoutWatcher : MonoBehaviour
{
    private bool _open;
    private int _lastPlayerCount;
    private List<Equipment> _selectionSnapshot = new();
    private Equipment _weaponSnapshot = Equipment.Invalid;

    private void Update()
    {
        var network = Plugin.Instance.Network;
        if (!network.Online || !SceneTransitionManagerPatch.InLevelSelect || LevelSelectManager.instance == null)
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

        var open = LevelSelectManager.instance.PreLevelMenuIsOpen;
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

        // Late join while the popup is open: the host re-broadcasts the full loadout context so the new player's
        // popup opens and converges.
        var playerCount = Plugin.Instance.PlayerManager.GetAllPlayers().Count();
        if (network.Server && _lastPlayerCount > 0 && playerCount > _lastPlayerCount)
        {
            BroadcastOpen(network);
            BroadcastSelection(network);
            BroadcastWeapon(network);
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
            network.Send(new LoadoutSelectionPacket { Selection = selection });
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
```

- [ ] **Step 2: Wire into Plugin** — in `Main/Plugin.cs` `Awake`, directly after the existing `gameObject.AddComponent` line for the Network component, add:

```csharp
        gameObject.AddComponent<Components.LoadoutWatcher>();
```

- [ ] **Step 3: Build**

Run: `bash /c/Users/nalar/build_mod.sh` — Expected: `EXIT: 0`

- [ ] **Step 4: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add Main/Components/LoadoutWatcher.cs Main/Plugin.cs && git commit -m "feat(loadout): add loadout watcher (open/close + selection sync + strip)"
```

---

### Task 6: Start gate (PlayButtonPressed hook)

**Files:**
- Modify: `Main/Patches/LevelSelectManagerPatch.cs`

- [ ] **Step 1: Add the hook registration** — in `Apply()`, after the existing `On.LevelSelectManager.MovePlayerToTheLevelYouCameFrom += ...;` line add:

```csharp
        On.LevelSelectManager.PlayButtonPressed += PlayButtonPressed;
```

- [ ] **Step 2: Add the hook method** — append to the class (file needs `using Steamworks;`, `using ThronefallMP.Network;`, and `using ThronefallMP.UI;` added to its usings):

```csharp
    private static void PlayButtonPressed(On.LevelSelectManager.orig_PlayButtonPressed original, LevelSelectManager self)
    {
        if (Plugin.Instance.Network.Online && self.PreLevelMenuIsOpen)
        {
            // Start is gated until every connected player has picked a weapon in the shared loadout popup.
            foreach (var player in Plugin.Instance.PlayerManager.GetAllPlayers())
            {
                LoadoutState.WeaponPicks.TryGetValue(player.Id, out var pick);
                if (pick == Equipment.Invalid)
                {
                    var name = SteamFriends.GetFriendPersonaName(player.SteamID);
                    UIManager.CreateMessageDialog(
                        "Not so fast",
                        $"Waiting for {name} to pick a weapon.");
                    return;
                }
            }
        }

        original(self);
    }
```

- [ ] **Step 3: Build**

Run: `bash /c/Users/nalar/build_mod.sh` — Expected: `EXIT: 0`

- [ ] **Step 4: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add Main/Patches/LevelSelectManagerPatch.cs && git commit -m "feat(loadout): gate level start until all players picked a weapon"
```

---

### Task 7: Host pre-fills weapons at level start (skip the weapon dialog)

**Files:**
- Modify: `Main/Network/Sync/LevelDataSync.cs` — `HandleRequestPacket` (the block after the perk loop that currently always broadcasts a `WeaponRequestPacket`)

- [ ] **Step 1: Replace the weapon-request block.** In `HandleRequestPacket`, the code after the `foreach (var perk in packet.Perks)` loop currently reads:

```csharp
        Plugin.Log.LogInfo("Sending weapon request.");
        var sentByServer = peer == Plugin.Instance.PlayerManager.LocalPlayer.SteamID;
        var request = new WeaponRequestPacket();
        Plugin.Instance.Network.Send(
            request,
            !sentByServer,
            peer
        );
        
        Plugin.Instance.StartCoroutine(RequestHandler());
```

Replace it with:

```csharp
        // Pre-fill weapon picks made in the shared loadout popup so the legacy weapon-request dialog is skipped.
        // (The popup's Start gate guarantees picks exist; the dialog below remains as a fallback for start paths
        // that bypass the popup.)
        foreach (var pick in LoadoutState.WeaponPicks)
        {
            if (pick.Value != Equipment.Invalid && !_activeRequest.SelectedWeapons.ContainsKey(pick.Key))
            {
                _activeRequest.SelectedWeapons[pick.Key] = pick.Value;
            }
        }

        var allPicked = Plugin.Instance.PlayerManager.GetAllPlayers()
            .All(p => _activeRequest.SelectedWeapons.ContainsKey(p.Id));
        if (!allPicked)
        {
            Plugin.Log.LogInfo("Sending weapon request.");
            var sentByServer = peer == Plugin.Instance.PlayerManager.LocalPlayer.SteamID;
            var request = new WeaponRequestPacket();
            Plugin.Instance.Network.Send(
                request,
                !sentByServer,
                peer
            );
        }

        Plugin.Instance.StartCoroutine(RequestHandler());
```

(`LevelDataSync.cs` already has `using System.Linq;` and is in the `ThronefallMP.Network.Sync` namespace, so `LoadoutState` resolves via the existing `ThronefallMP.Network` import — if the file lacks `using ThronefallMP.Network;`, add it.)

- [ ] **Step 2: Build**

Run: `bash /c/Users/nalar/build_mod.sh` — Expected: `EXIT: 0`

- [ ] **Step 3: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add Main/Network/Sync/LevelDataSync.cs && git commit -m "feat(loadout): pre-fill level-start weapons from loadout picks"
```

---

### Task 8: Full verification + changelog

**Files:**
- Modify: `PackageAssets/README.md` (changelog entry under `## Changelog`)

- [ ] **Step 1: Full verify pipeline**

```bash
cd /c/Users/nalar && bash build_mod.sh && \
./tf_traverse.exe "/c/Program Files (x86)/Steam/steamapps/common/Thronefall/Thronefall_Data/Managed/Assembly-CSharp.dll" build_out/com.badwolf.thronefall_mp.dll 2>&1 | tail -5
```
Expected: `EXIT: 0`, `MISSING ... (0)`, `Unresolved receivers ... (0)`.

- [ ] **Step 2: Add changelog entry** — at the top of the `## Changelog` section in `PackageAssets/README.md`:

```markdown
#### 1.1.0 (community update)
* New: shared loadout screen. Opening a level's loadout popup now opens it for every player; perks and mutators
  are selected together in real time; each player picks their own weapon right in the popup (everyone can see the
  picks), and the level starts once everybody has chosen. The old mid-overworld weapon dialog is gone.
```

- [ ] **Step 3: Commit**

```bash
cd /c/Users/nalar/tfmp_src && git add PackageAssets/README.md && git commit -m "docs: changelog for shared loadout screen"
```

---

### Task 9: Live 2-player test checklist (user-driven)

No code. Deploy a build to the local profile and hand the matching zip to the second player, then verify:

- [ ] Host opens a level popup → opens on client (and vice versa: client opens → opens on host).
- [ ] Toggling a perk/mutator on either side appears on the other within a moment; rapid both-sides toggling converges (host wins).
- [ ] **KNOWN RISK to check first:** after a remote toggle, the popup's perk buttons VISUALLY reflect the change (the underlying list is correct; if the vanilla buttons don't re-render from state each frame, the icons may look stale). If stale → follow-up task: identify the popup's button component at runtime and force a refresh after `HandleLoadoutSelection` (fallback: close+reopen the frame on remote selection).
- [ ] Weapon pick on each side shows in both status strips; duplicates allowed; un-pick shows "—".
- [ ] Start blocked with the "Waiting for X to pick a weapon" dialog while a player has no weapon; allowed when all picked; **either** player can start.
- [ ] On start: no weapon dialog appears for anyone; level loads with correct weapons for both; mutator difficulty + win bonus work (rides the 1.0.10 fix).
- [ ] Closing the popup (ESC) on either side closes both; reopening works; picks reset.
- [ ] Fixed-loadout level still starts correctly.
- [ ] Returning to the overworld after a match: popup flow still works (state reset).

---

## Self-review notes (completed at plan time)

- **Spec coverage:** auto-open (Task 3 HandleLoadoutOpen + Task 5 OnOpened) ✓; live shared toggles (Task 3 HandleLoadoutSelection + Task 5 diff-watch) ✓; per-player weapons with visibility (Task 4 strip + LoadoutWeaponPacket) ✓; Start gate, anyone starts (Task 6) ✓; dialog-skip via pre-fill (Task 7) ✓; close-for-all (Task 3/5) ✓; late join (Task 5 host re-broadcast) ✓; disconnect (gate iterates *current* players only — stale dict entries are ignored) ✓; fixed-loadout + offline no-ops (guards) ✓.
- **Known open risk** is explicitly tracked in Task 9 (vanilla button visuals on remote toggle) rather than hidden.
- **Type consistency:** `WeaponPicks` is `Dictionary<ushort, Equipment>` everywhere; `SelectedWeapons` is `Dictionary<int, Equipment>` (existing code) — ushort keys implicitly widen to int at the only merge point (Task 7). `Capture()` sorts, so `SequenceEqual` comparison is order-stable.
- **No placeholders:** every step has the actual code/commands.
