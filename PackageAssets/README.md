# Thronefall Multiplayer (community-updated)

> **Community fork of [Thronefall Multiplayer](https://github.com/MunWolf/thronefall_multiplayer) by Rikhardur Bjarni Einarsson (BadWolf).**
> All credit for the mod goes to BadWolf, who archived the original in Feb 2024 and invited others to fork and
> release it with credit. This build only updates it to run on the current (2024+) Thronefall, which broke the
> original 1.0.5 release on load.

Using the Steamworks backend and API to connect players in Thronefall to play together in a true multiplayer experience.
Each player selects their own weapon to bring into battle as you build a town together against the growing tide of enemies.

## Installation (manual)

If you are installing this manually, do the following

1. Extract the archive into a folder. **Do not extract into the game folder.**
2. Install [BepInEx](https://thunderstore.io/c/thronefall/p/BepInEx/BepInExPack_Thronefall/)
3. Move the contents of `ThronefallMultiplayer_Mod` folder into the game folder (the same folder `Thronefall.exe` is in).
4. Run the game. If everything runs correctly, you should see a Multiplayer option in the main menu.

## Issues, questions, etc.

At this moment, you can use the following channels to ask for help

* [Thronefall Discord](https://discord.gg/gVYctptyg8) (#game-mods channel or this mods entry in #mod-showcase)
* [Thronefall Multiplayer GitHub](https://github.com/MunWolf/thronefall_multiplayer/issues) (Look through existing issues before creating a new one)

## Author

Rikhardur Bjarni Einarsson (BadWolf)

## Changelog

#### 1.2.1 (community update)
* Fixed the game getting stuck on the night→day transition after defeating a boss (an already-dead boss/
  minion in the cleanup list threw an error that aborted the transition, so dawn never arrived and the host
  spammed errors). Boss days now end correctly.
* Both players must be on 1.2.1.

#### 1.2.0 (community update)
* Fixed the long-standing bug where walls (and other buildings) built by one player were invisible to the
  other. Such structures keep their object disabled until "unlocked" locally, which never happened for a
  networked build, so the mesh never activated or rendered. The build now activates the structure on every
  player's screen so it shows up (and the constant "Main Mesh inactive" errors are gone).
* Fixed a leftover "phantom" player lingering after retries, which caused error spam around gates and player
  spacing.
* Both players must be on 1.2.0.

#### 1.1.9 (community update)
* Fixed Retry Day wiping your team's gold. The per-day save was recording a stale, far-too-low balance
  instead of your actual shared gold, so retrying could throw away your whole economy. It now saves the
  real shared balance (the number on the gold counter).
* Fixed an error flood when a player hit certain targets (damage-feedback effect with no assigned prefab).
* Fixed an error flood from leftover player references after retries (player-spacing component).
* Both players must be on 1.1.9.

#### 1.1.8 (community update)
* Fixed a bogus instant "Defeat" that could hit a joining player the moment they entered a fresh level.
  Each player was deciding the loss for themselves, so while a client's castle health was still syncing in
  it could briefly look destroyed and trigger an immediate defeat — even though the host's game was fine
  (clicking New Game again usually dodged it). Defeat is now decided only by the host, like victory already
  is, so clients can't lose to a sync hiccup on load.
* Both players must be on 1.1.8.

#### 1.1.7 (community update)
* Fixed the shared loadout grid going "dead" for everyone except the last player to open it — you could see
  the grid but clicking perks did nothing. When another player's selection arrived, the grid was rebuilt
  without re-registering its buttons with the input system, so all clicks (mouse and controller) missed.
  The buttons are now re-registered after every remote update, so all players can interact at the same time.
* Both players must be on 1.1.7.

#### 1.1.6 (community update)
* Fixed the shared loadout popup never auto-opening on the other player's machine. The receiver bailed
  immediately because it relied on a level-select manager that is never initialised on a joined client, so
  the open packet was silently ignored every time (this is why it only ever "worked" when both players
  opened it manually). It now finds the level the proper way and opens for everyone.
* Includes the 1.1.5 client-crash fix (HP sync could throw every frame and leak network messages until the
  game ran out of memory).
* Both players must be on 1.1.6.

#### 1.1.5 (community update)
* Fixed a client crash: HP sync could hit a target with no health component and throw every frame, and the
  network receive loop leaked the underlying message each time until the game ran out of memory and crashed.
  The HP case is now guarded, and a thrown packet handler can no longer leak or take down the connection.
* Both players must be on 1.1.5.

#### 1.1.4 (community update)
* New: the army group-select hotkeys (1/2/3 — and the heroes key) now work in multiplayer. Pressing one
  grabs your whole army / melee / ranged / heroes from anywhere on the map, same as single player; pressing
  again places them.
* Fixed two NullReferenceException floods (gates near upgraded walls, and a vanilla position-freeze
  component on multiplayer pawns).
* Both players must be on 1.1.4.

#### 1.1.3 (community update)
* Fixed picking certain perks giving you a DIFFERENT perk in multiplayer (e.g. Resilient Residences turning
  into Last Stand): every perk, weapon, mutator and tower upgrade added to the game since 2023 was missing
  from the mod's sync table — all 38 are now mapped, including the four newer weapons (Battle Axe, Blood
  Wand, Cursed Blowpipe, Falchion and Trap) and the "Challenge the..." mutators.
* The "Taunt the Phoenix God" mutator now actually syncs (a name mismatch made it silently inert in MP).
* Both players must be on 1.1.3.

#### 1.1.2 (community update)
* **Both players must be on 1.1.2** — the wire format changed.
* New: joining players can now select the same loadout items (perks, weapons, mutators) as the host for the
  duration of the session. Host unlocks are sent to joining clients and are not saved to their game.
* Fixed: the shared loadout popup now opens the actual perk/weapon/mutator grid on remote players, not just
  the level info frame.

#### 1.1.1 (community update)
* Fixed the shared loadout popup not opening on the other player's machine: opens that arrived while a
  player was still joining (or mid scene-fade) were silently dropped and never re-sent — the popup state
  is now re-requested and replayed once the machine is actually ready. Also fixed an internal flag leak
  that could stop a player's popup-opens from broadcasting for the rest of the session.
* Added temporary [LoadoutDiag] log lines to pin down any remaining popup sync issues.

#### 1.1.0 (community update)
* New: shared loadout screen. Opening a level's popup now opens it for every player; perks and mutators are
  selected together in real time; each player picks their own weapon right in the popup and everyone can see
  the picks in a status strip; the level starts once everybody has a weapon (your last-used weapon counts as
  your pick, so returning players don't have to re-click). The old mid-overworld weapon dialog only appears
  for flows that skip the popup (e.g. "Continue").
* Both players must be on 1.1.0 (the multiplayer version check will refuse mismatched builds).

#### 1.0.11 (community update)
* Retrying a day after a defeat ("Try again") no longer wipes the shared gold — everyone now starts the
  retried day with the money they had at dawn. (The shared balance is now stored in the per-day autosave.)
* The retry / "Start over" buttons now apply the pressing player's perk & mutator selection on every machine.
  Before, the host silently kept the old loadout, so a re-picked selection didn't change the difficulty or
  bonuses at all.
* "Start over" now restarts from day 1 for ALL players — previously only the player who pressed it started
  fresh while the others reloaded their day save, desyncing the world.
* The Loan perk's bonus starting gold now actually arrives in multiplayer, and Royal Mint's start bonus is no
  longer incorrectly re-granted when resuming a saved day.

#### 1.0.10 (community update)
* Mutators now actually work in multiplayer: the host applies the selected mutators' difficulty (they were
  silently dropped at level start) AND enemies receive the vanilla spawn buffs that were missing in MP — elite
  waves (4x HP / 3x damage + visual), per-wave difficulty scaling, anti-air, War/Growth/Range/Chaos/Afterlife god
  effects. The mutator score bonus is paid out correctly on victory.
* Victory is now broadcast by the host, fixing the stuck end screen where one player saw victory and the other
  kept playing.
* Boss fights are now synced: the Ghost Queen's and Eismoloch's summoned minions used to spawn separately on each
  player's machine (frozen "ghost" copies on the client that never died); they now spawn host-authoritatively
  like every other enemy.
* Royal Protection no longer permanently shrinks commanded units' max HP with every command.
* The victory gold bonus (and recorded highscore) no longer reads an empty remote wallet on some machines.
* Client-side invulnerability windows (boss phases, dashes, shields) no longer block authoritative HP updates —
  fixes stale boss HP and enemies that refused to die on one screen.
* The on-hit slow debuff now affects client players too (they were immune) and remote players visibly slow down.
* Weapon target-lock is now synced: your lock applies to the real (host) damage calculation, and your keypress no
  longer re-targets other players' heroes on your machine.

#### 1.0.9 (community update)
* Fixed the "Oops, you died. You'll respawn shortly." message getting stuck on screen while you were alive and
  playing (the revive panel is now tied to your actual state).
* Fixed two NullReferenceException floods: one near gates with siege weapons / certain units, and one on the
  level-select screen when returning from a level.

#### 1.0.8 (community update)
* Netcode overhaul (round 2): fixed a bug that resent every unit's full path each frame (flooding the network);
  client enemies/allies now keep their local movement prediction and fold the host's authoritative position in as
  a smoothly-decaying correction (instead of trailing behind), with a hard snap for teleports/respawns; capped
  sync to ~20 Hz so it no longer scales with the host's frame rate. Synced pawns should track the host much more
  tightly.

#### 1.0.7 (community update)
* Netcode: tightened client position correction for players/allies/enemies so synced pawns track the host
  closely instead of trailing by seconds (large errors now snap instead of slowly catching up). Also shows the
  shared gold on the treasury counter.

#### 1.0.6 (community update)
* Recompiled against the current Thronefall; fixed the load crash (`CameraRig.TransitionToTarget` hook) and a
  series of in-level NullReferenceExceptions introduced by the game update (camera/RVO/minimap/treasury/unit-type
  display, ally & enemy pathfinder sync, gate logic). See the GitHub repo for the full commit list.

#### 1.0.4
* Update to work with content update 1.

#### 1.0.3
* Fixed serialization issue to do with identifiers, caused some pathfinding packets to be ignored.

#### 1.0.2
* Update to use netstandard2.1 for Mac support

#### 1.0.1
* Fixes to packaging script

#### 1.0.0
* Initial Thunderstore release
