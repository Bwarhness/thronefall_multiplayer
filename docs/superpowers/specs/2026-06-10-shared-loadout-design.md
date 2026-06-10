# Shared Loadout Screen (MP) — Design

**Date:** 2026-06-10
**Status:** Approved by user (pre-implementation)
**Target:** ThronefallMultiplayer mod, after v1.0.10

## Problem

In multiplayer today, only the player who walks to a level banner sees the pre-level loadout popup
(perks/mutators/weapon). Other players never see it; they get a bare mod-built weapon dialog wherever they happen to
be standing, and have no visibility into — or influence over — the selected perks and mutators.

## Goal / Requirements (user-confirmed)

1. When any player opens a level's loadout popup, it **auto-opens on every player's screen** for the same level.
   Any player closing it closes it for everyone.
2. **Everyone can toggle perks and mutators**; changes appear live on all screens (last-write-wins, host
   authoritative on conflict).
3. **Each player picks their own weapon inside the popup.** Duplicate weapons are allowed. Everyone can see each
   player's pick.
4. The **Start button is gated** until every connected player has picked a weapon; once gated-open, **any player**
   can start the level.
5. The separate mid-overworld weapon dialog is no longer part of the normal flow (kept only as a fallback).

## Approach (chosen: networked vanilla popup)

Keep the game's own `levelSelectFrame` UI on every peer and synchronize its state. Rejected alternatives: a fully
custom mod-built lobby panel (large effort, loses native polish/tooltips/slot rules) and a view-only overlay (does
not satisfy requirement 2).

## Architecture

### Game API surface (verified in IL)

- `UIFrameManager.TryOpenLevelSelect()` / `ForceOpenLevelSelect()` — open the popup frame.
- `UIFrameManager.CloseActiveFrame()` / `CloseAllFrames()` — close paths.
- `LevelSelectManager.instance` — private `LevelInteractor openedLevel` (which level the popup shows), private
  `LevelInteractor[] levelInteractors` (lookup table), `bool PreLevelMenuIsOpen`, private `bool fixedLoadout`.
- `LevelInteractor.levelInfo.sceneName` — stable level identifier for the wire.
- `PerkManager.instance.CurrentlyEquipped` (`List<Equippable>`) — THE selection state for perks, mutators, and the
  local weapon. The mod's `Equip` class already maps `Equippable.name` ⇄ `Equipment` enum for serialization.

### New packets (appended to end of `PacketId` enum; all `Channel.Game`)

| Packet | Fields | Sent when | Effect on receivers |
|---|---|---|---|
| `LoadoutOpenPacket` | `string Scene` | a player opens a level popup | resolve `LevelInteractor` by `levelInfo.sceneName`, set `LevelSelectManager.openedLevel` (Traverse), call `ForceOpenLevelSelect()` (re-entrancy-guarded) |
| `LoadoutSelectionPacket` | `List<Equipment> Selection` (non-weapon items only) | local shared selection changed (diff-watch) | suspend watcher, replace non-weapon portion of `CurrentlyEquipped`, resume |
| `LoadoutWeaponPacket` | `ushort PlayerId, Equipment Weapon` | a player clicks a weapon | store on player record (`player.Weapon`), update status strip |
| `LoadoutClosePacket` | — | a player closes the popup | close frame everywhere, clear weapon picks for the pending start |

Authority: all four route through the host (`ShouldPropagate` pattern like `ResignPacket`); the host's state is
authoritative. If two players open different levels near-simultaneously, the host accepts the first `LoadoutOpen`
and the loser's popup is redirected to the winner's level by the relayed packet.

### Selection sync = diff-watch, not button hooks

While `PreLevelMenuIsOpen`, a small component polls `PerkManager.CurrentlyEquipped` once per frame and compares
against the last-broadcast snapshot:

- Non-weapon changes → broadcast full `LoadoutSelectionPacket` (list is tiny; full-state beats delta bookkeeping).
- Weapon-type changes (membership in `Equip.Weapons`) → treated as THIS player's personal pick: broadcast
  `LoadoutWeaponPacket`; remote players' weapons are NOT merged into the local `CurrentlyEquipped`.
- Applying a remote packet suspends the watcher to prevent echo loops.

Rationale: hooking the popup's internal toggle buttons couples the mod to UI internals that the 2024+ game update
already reworked once; polling one public list is far more robust to future game updates.

### Weapon pick visibility (status strip)

A small mod-rendered TMP text strip anchored inside the popup (the mod already builds UI panels — `Main/UI`):
`GenoC: Long Bow ✓ · Too Damn FilthY: —`. It re-renders on every `LoadoutWeaponPacket`/peer change. No attempt to
decorate the vanilla weapon icons in v1 (fragile positioning for little gain).

### Start flow (reuses the proven path)

The mod already intercepts the popup's Start press at `SceneTransitionManager.TransitionToScene`
(`SceneTransitionManagerPatch`). Add a gate there:

- If any connected player lacks a weapon pick → suppress the transition and show the mod's `MessageDialog`:
  "Waiting for «name» to pick a weapon." (Status strip shows who.)
- Else → proceed exactly as today (`RequestLevelPacket` with the shared perks).

`LevelDataSync.HandleRequestPacket` (host) pre-fills `_activeRequest.SelectedWeapons` from the loadout picks
collected via `LoadoutWeaponPacket`. The existing `WeaponRequestPacket`/`WeaponDialog` handshake remains in the code
as an automatic fallback for any player without a pick (should not occur given the gate, but keeps the flow robust).
The mutator/perks path is the one fixed for 1.0.10 (host re-equips non-weapon perks), so mutator difficulty + win
bonus work unchanged.

## Edge cases

- **Close from any peer** → `LoadoutClosePacket` → closes everywhere, pending weapon picks cleared.
- **Disconnect mid-popup** → host recomputes the start gate from remaining players (PlayerManager removal already
  fires); status strip re-renders.
- **Fixed-loadout levels** (`LevelSelectManager.fixedLoadout`) → vanilla blocks toggles; the watcher simply sees no
  changes; weapon picks still sync.
- **Solo (no second player connected)** → gate passes trivially; behavior identical to today.
- **Single-player (offline)** → all hooks no-op behind the existing `Network.Online` checks.
- **Version safety** → new `PacketId` values appended at the enum end; the mod's strict version check already
  prevents mixed-version lobbies.

## Out of scope (v1)

- Bonus-level and Eternal Trials loadout frames (`TryOpenBonusLevelSelect`, `eternalTrialsLoadoutPickFrame`) — they
  keep today's behavior.
- Showing which tooltip/hover the other player is viewing.
- Chat or ping inside the popup.
- Decorating vanilla weapon icons with per-player tags (status strip covers visibility).

## Testing

- Compile + the existing verify pipeline (hook signature scan, Traverse-target check — `openedLevel`,
  `levelInteractors`, `fixedLoadout` must exist; verified in IL 2026-06-10).
- Live 2-player checks: open propagates both directions; toggle race (both toggle within a second) converges to
  host state; weapon dupes allowed; Start gated until both picked; either player starts; close from either side;
  disconnect while popup open; fixed-loadout level; a full match win afterwards (mutator bonus still paid).
