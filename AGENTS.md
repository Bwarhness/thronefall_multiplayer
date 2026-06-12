# AGENTS.md — ThronefallMultiplayer

Community fork of BadWolf's Thronefall multiplayer mod (BepInEx 5 plugin, C#, Steamworks
networking), updated for the current (2024+) game. Upstream archived Feb 2024; this fork lives at
`Bwarhness/thronefall_multiplayer` (git remote `fork`; `origin` is the archived upstream — never
push there). All work goes directly on `main`.

## Architecture (the 60-second version)

- **Host-authoritative co-op**: the host runs the real simulation (enemy spawns, gold, difficulty,
  victory); clients predict locally and receive corrections. Shared gold lives in
  `GlobalData.Internal.Balance` (`Main/GlobalData.cs`), mirrored onto vanilla
  `PlayerInteraction.balance` every frame by patches.
- **Packets**: `Main/Network/Packets/**`. A packet must be registered in TWO places:
  `PacketHandler.Handlers` (dispatch) and `Network.PacketTypes` (deserialization). The `PacketId`
  enum order IS the wire format — **append new values at the END only**. Any enum or
  `Send`/`Receive` change means both players must update (the version check refuses mismatches).
- `Network.Send(packet, handleLocal, except)`: `handleLocal:true` loops the packet through the
  local handler; `ShouldPropagate => true` makes the server auto-rebroadcast received packets to
  everyone EXCEPT the original sender (never re-Send manually in a handler). `SendSingle` targets
  one peer. Clients only peer with the host.
- **Patching**: MonoMod HookGen `On.Type.Method += ...` where available, HarmonyLib
  `[HarmonyPatch]` attribute classes otherwise (auto-applied via `Harmony.CreateAndPatchAll` in
  `Plugin.Awake`). Private members via `Traverse.Create(obj).Field<T>("name")`.
- **Identifier system** (`Main/Components/Identifier.cs`): every synced entity needs one
  (Player/Ally/Enemy/Building/BuildSlot). Enemies without an Identifier are invisible to all syncs
  and survive dawn cleanup.

## The MMHOOK trap (most important gotcha)

The repo's compile-time `lib/MMHOOK_Assembly-CSharp.dll` contains `On.*` hooks that the RUNTIME
profile MMHOOK does **not** — code can compile and then silently fail to hook in-game. Before
using any new `On.Type.X`, verify it exists in the PROFILE MMHOOK:

```
./tf_api.exe "$APPDATA/Thunderstore Mod Manager/DataFolder/Thronefall/profiles/Default/BepInEx/plugins/MMHOOK/MMHOOK_Assembly-CSharp.dll" "T:On.TypeName"
```

If absent, use a Harmony attribute patch instead. Known absent: `On.Spawn`, `On.ManualAttack`,
`On.LocalMatchSaveLoad`, `On.MatchSave`, `On.EndscreenRetryHelper`, `On.ScoreManager`,
`On.TransitionToSelectedLevelHelper`, `On.LevelSelectUIFrameHelper`.

## Dead vanilla APIs (do not build on these)

Verified dead in the current game build — they compile fine and do nothing at runtime:
- `LevelSelectManager.openedLevel` is never written → `PreLevelMenuIsOpen` is always false.
- `LevelSelectManager.PlayButtonPressed` / `PlayLevelButton` chain is vestigial.
- Real level-start paths are UnityEvent-wired: `TransitionToSelectedLevelHelper.TransitionToSelectedLevel`
  and `LevelSelectUIFrameHelper.TransitionToSelectedLevel`. The pre-level popup is TWO frames:
  `UIFrameManager.levelSelectFrame` + a grid frame owned by `LoadoutUIHelper` (see
  `Main/UI/LoadoutFrames.cs`).

## Build & verify (no .NET SDK on this machine)

- Build: `bash /c/Users/nalar/build_mod.sh` → must end `EXIT: 0`. Compiles against the real game
  DLLs + the PROFILE MMHOOK, so a clean build validates every hook signature and game API ref —
  this is the test suite; there is no other.
- Output: `C:\Users\nalar\build_out\com.badwolf.thronefall_mp.dll`.
- `Main/PluginInfo.cs` is a LOCAL-ONLY shim (gitignored). Never commit it; an SDK build would
  regenerate it from the csproj `<Version>`.
- Verify tools in `C:\Users\nalar` (game DLL:
  `C:\Program Files (x86)\Steam\steamapps\common\Thronefall\Thronefall_Data\Managed\Assembly-CSharp.dll`):
  - `tf_api.exe <dll> "T:Type"|"M:Type.Method"` — list members / dump readable IL. Ground every
    claim about vanilla behavior in IL, not memory.
  - `tf_callers.exe <dll> "Type::Method"` — find all call sites.
  - `tf_fieldrefs.exe <dll> "fieldName"` — find all field reads/writes with stored values.
  - `tf_traverse.exe <gameDll> <modDll>` — verify every `Traverse("...")` string target exists;
    must end `MISSING (0)`. (Arg order: game first.)
  - `tf_attr.exe <modDll>` — BepInPlugin version stamp.
  - `tf_scan.exe` is BROKEN (crashes) — don't use; the build supersedes it.
- Deploy target (game must be CLOSED — it locks the DLL):
  `%APPDATA%\Thunderstore Mod Manager\DataFolder\Thronefall\profiles\Default\BepInEx\plugins\Mods_for_the_Throne-ThronefallMultiplayer\ThronefallMultiplayer\com.badwolf.thronefall_mp.dll`

## Releasing

Follow the `/tf-release` skill (`~/.claude/skills/tf-release/SKILL.md`): bump
`Main/ThronefallMP.csproj` + the local `PluginInfo.cs` shim, changelog in
`PackageAssets/README.md`, build, deploy, Thunderstore zip (repo `manifest.json` keeps its
`{VERSION}` placeholder), commit/push, `gh release create`. The user uploads the zip to
Thunderstore manually (`biggestBlackest-ThronefallMultiplayerContinued`). Plugin GUID
`com.badwolf.thronefall_mp` must never change.

## Hard-won domain facts

- Vanilla match saves are per-machine local files (`persistentDataPath/{map}.json`), written at
  dawn on EVERY peer independently; `MatchSaveLoadHandler.OverwriteCurrentSave` is per-machine
  state — the mod syncs it across the restart flow (`RestartLevelPacket.OverwriteSave`). The
  shared balance is persisted as `"mpSharedBalance"` on the EnemySpawner save entity (per-pawn
  `"balance"` keys are enumeration-order luck in MP).
- `EnemySpawner.OnLoad` zeroes `goldBalanceAtStart` before `Start` runs; `loadedBalanceFromSave`
  means "the wavenumber key loaded". Loan adds to the field unconditionally, Royal Mint only on
  fresh runs — the mod reuses vanilla's own gating via the field delta (`EnemySpawnerPatch.Start`).
- Vanilla grid UI (`TFUIEquippable`) caches selection state; after mutating
  `PerkManager.CurrentlyEquipped` remotely you must call `LoadoutUIHelper.OnShow()` (full
  destroy+rebuild). Closing the grid fires `ResetLoadoutOnCancel` (loadout revert) unless
  `LockInLoadout()` ran — programmatic closes must lock in first (`LoadoutFrames.CloseAllPopupFrames`).
- Joining clients have `InLevelSelect == false` until the first scene sync completes — packets
  needing UI context are silently dropped in that window; use request/replay
  (`LoadoutStateRequestPacket`) rather than fire-once broadcasts.
- Audit of remaining known sync gaps: `docs/superpowers/audits/2026-06-10-sync-coverage-audit.json`.

## Conventions

- Read each file before editing; files mix tabs/spaces — match the file you're in.
- Comments state constraints the code can't show; no narration.
- Commit per logical change directly to `main`; push to `fork`. Releases only when the user asks.
- No test framework: build = validation, plus live 2-player testing by the user. Don't add
  speculative verification runs — ship and let the user confirm.
