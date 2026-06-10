using System.Collections.Generic;
using HarmonyLib;
using ThronefallMP.Network;
using ThronefallMP.Network.Packets.Game;
using ThronefallMP.Utils;
using UnityEngine;

namespace ThronefallMP.Patches;

// The mod's EnemySpawner.Update replacement routes every regular wave spawn through
// EnemySpawnPacket and never calls vanilla Spawn.Update, but the two bosses drive their minion
// waves through vanilla code on every peer: Ghostqueen's SpawnGhosts coroutine calls
// Spawn.Reset/Spawn.Update(1) on its private Spawn, and KeyframedBoss.Update (Eismoloch) calls
// Wave.Reset(true)/Wave.Update -> Spawn.Update on its private Wave. Left alone, every peer rolls
// its own Random spawn positions and Instantiates Identifier-less minions that are invisible to
// PositionSync/HpSync/EnemyPathfinderSync and survive the dawn cleanup. This prefix therefore only
// fires for boss-driven spawns: the host reimplements the vanilla timer flow and broadcasts a
// BossSpawnPacket (handled locally too, like EnemySpawnPacket), while clients suppress the
// original entirely and let HandleBossSpawn advance spawnedUnits/finished, so Ghostqueen's
// !Finished loop and KeyframedBoss' HasFinished()/waveRunning logic terminate once the
// authoritative spawns have arrived (termination lags by network latency, which is fine).
// The MMHOOK shipped with the game profile has no On.Spawn type, so this is a Harmony prefix in
// the same style as ManualAttackPatch.
[HarmonyPatch(typeof(Spawn), nameof(Spawn.Update))]
internal static class SpawnPatch
{
    private static readonly HashSet<Spawn> WarnedUnknownSpawns = new();

    private static bool Prefix(Spawn __instance, float difficultyMulti)
    {
        var network = Plugin.Instance.Network;
        if (!network.Online)
        {
            return true;
        }

        // Vanilla's TaggedObject-less branch just does enemyPrefab.SetActive(true) and marks the
        // spawn finished; it consumes no Random and Instantiates nothing, so it stays
        // deterministic when every peer runs it locally.
        if (__instance.enemyPrefab == null
            || __instance.enemyPrefab.GetComponentInChildren<TaggedObject>() == null)
        {
            return true;
        }

        var spawnRef = BossSpawnRegistry.GetRef(__instance);
        if (spawnRef < 0)
        {
            // Not reachable from any boss; fall back to the vanilla local (unsynced) spawn
            // rather than stalling the wave forever. Only Ghostqueen and KeyframedBoss call
            // Spawn.Update in the current game version, so this should not happen.
            if (WarnedUnknownSpawns.Add(__instance))
            {
                Plugin.Log.LogWarningFiltered(
                    "SpawnPatch", $"Unregistered Spawn of '{__instance.enemyPrefab.name}', spawning unsynced");
            }

            return true;
        }

        if (network.Server)
        {
            UpdateOnHost(__instance, (ushort)spawnRef, difficultyMulti);
        }

        // Clients keep no local bookkeeping at all; HandleBossSpawn advances spawnedUnits and
        // finished from the authoritative packets. A client's own Spawn.Reset can briefly zero
        // that bookkeeping when its boss starts the wave later than the host's did, but the
        // count converges again on the next authoritative spawn burst.
        return false;
    }

    // Vanilla Spawn.Update's timer flow, with the Random roll and Instantiate routed through
    // BossSpawnPacket. handleLocal makes the host spawn through the same HandleBossSpawn path as
    // the clients, which also advances this Spawn's spawnedUnits/finished bookkeeping.
    private static void UpdateOnHost(Spawn self, ushort spawnRef, float difficultyMulti)
    {
        if (Traverse.Create(self).Field<bool>("finished").Value)
        {
            return;
        }

        var waitBeforeNextSpawn = Traverse.Create(self).Field<float>("waitBeforeNextSpawn");
        waitBeforeNextSpawn.Value -= Time.deltaTime;
        if (waitBeforeNextSpawn.Value > 0f)
        {
            return;
        }

        waitBeforeNextSpawn.Value = self.interval;
        var position = EnemySpawnerPatch.RollSpawnPosition(self);

        var coins = 0;
        var spawnedUnits = Traverse.Create(self).Field<int>("spawnedUnits");
        var goldCoinsPerEnemy = Traverse.Create(self).Field<int[]>("goldCoinsPerEnemy");
        if (goldCoinsPerEnemy.Value.Length > spawnedUnits.Value)
        {
            coins = goldCoinsPerEnemy.Value[spawnedUnits.Value];
        }

        var packet = new BossSpawnPacket
        {
            SpawnRef = spawnRef,
            Id = EnemySpawnerPatch.AllocateEnemyId(),
            Position = position,
            Coins = coins,
            Elite = self.eliteEnemies,
            DifficultyMulti = difficultyMulti
        };

        Plugin.Instance.Network.Send(packet, true);
    }
}
