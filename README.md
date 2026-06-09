# Thronefall Multiplayer (community-updated fork)

> **This is a community fork of [MunWolf/thronefall_multiplayer](https://github.com/MunWolf/thronefall_multiplayer) by Rikhardur Bjarni Einarsson (BadWolf).**
> All credit for the mod itself goes to BadWolf. The original repository was archived in February 2024, and BadWolf
> generously invited anyone to *"fork it, release their own version and do whatever they want with it as long as
> they give credit."* This fork exists only to keep the mod working after a Thronefall game update broke the
> original 1.0.5 release.

## What this fork fixes (v1.0.6)

A 2024+ Thronefall update changed several `Assembly-CSharp` APIs that the mod hooks/uses, so the original 1.0.5
build crashed on load with:

```
InvalidOperationException: Parameter #0 of hook for ... TransitionToTarget(...) doesn't match, must be CameraRig or related
```

This fork recompiles the original source against the current game and adapts every broken call site. Notable changes:

- **`CameraRig.TransitionToTarget`** — gained a `float targetCameraSize` parameter, and the private `targetPosition`
  field it relied on was removed. The reimplemented camera coroutine now exits on the camera's actual position and
  eases the orthographic size toward `targetCameraSize` (matching vanilla), fixing both the load crash and a
  potential camera lock-up.
- **`CommandUnits.PlaceCommandedUnitsAndCalculateTargetPositions`** — gained a `bool _smartCommand` parameter.
- **Treasure hunter gold** — `PerkManager.treasureHunterGoldAmount` was split per-wave; rewritten to use the new
  `EnemySpawner.GetTreasureHunterBonus(wave, out amount)`.
- **`Spawn.GetRandomPointOnSpawnLine`** — now requires an `NNConstraint`; supplied with the same flying/large/ground
  priority the game uses.
- **`PlayerInteraction.EquippedWeapon` → `ManualAttack`**, **`EquipWeapon(manual, auto)`**,
  **`PerkManager.UnlockedEquippables` → `allEquippables`**, **`UpgradeCommander.moveSpeedMultiplicator` →
  `CommandUnits.commanderUnitMoveSpeedMulti`**, **`SettingsManager.UseLargeInGameUI`** removed,
  **`UnityEvent.m_PersistentCalls`** accessed via reflection, **`Lerp.Interpolate` → `Mathf.Lerp`**.

### Install (players)

1. Install [BepInEx 5 for Thronefall](https://thunderstore.io/c/thronefall/p/BepInEx/BepInExPack_Thronefall/)
   (e.g. via Thunderstore Mod Manager / r2modman, same as the original mod).
2. Download `com.badwolf.thronefall_mp.dll` from this repo's [Releases](../../releases).
3. Drop it into `BepInEx/plugins/` (replacing the old one if present) and launch the game.

> All players in a session must use the **same** build (the mod performs a version check on connect).

---

## Original README

### Information
This is the development repository for the WIP Thronefall Multiplayer mod.
It uses BepInEx to inject code into the game.

### Development Setup

This is written in CSharp so you will have to acquire an IDE that can read .sln files (for example JetBrains Rider)
You will also need an installation of Thronefall, run the setup.ps1 script to acquire all required dlls from your installation.
After that open the solution file and run the build.

### How to Run

Download BepInEx 6 and copy it into your Thronefall directory.
When you run the build it copies the ThronefallMultiplayer dlls to the correct plugin folder.
Run your executable either directly or through Steam.
