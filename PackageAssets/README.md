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
