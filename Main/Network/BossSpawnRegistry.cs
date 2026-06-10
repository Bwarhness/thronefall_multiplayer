using System.Collections.Generic;
using HarmonyLib;
using ThronefallMP.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThronefallMP.Network;

// Boss minion waves (Ghostqueen's ghosts, Eismoloch/KeyframedBoss' wave) live on private Spawn
// instances inside the boss components, so they cannot be addressed through EnemySpawner.waves
// indices like the regular pipeline. Instead every peer lazily builds the same index over all
// boss-reachable Spawns in the loaded scene: entries are ordered by the owning boss' hierarchy
// path and the spawn's position inside its wave, both serialized scene data and therefore
// identical on every peer. Invalidated on every scene load so a level restart (same scene name,
// fresh boss instances) rebuilds against the new objects.
public static class BossSpawnRegistry
{
    private static List<Spawn> _spawns;

    static BossSpawnRegistry()
    {
        SceneManager.sceneLoaded += (_, _) => _spawns = null;
    }

    // Returns the deterministic index of a boss-owned Spawn, or -1 if the Spawn is not reachable
    // from any boss (i.e. some other vanilla code is driving it).
    public static int GetRef(Spawn spawn)
    {
        EnsureBuilt();
        return _spawns.IndexOf(spawn);
    }

    public static Spawn Resolve(int index)
    {
        EnsureBuilt();
        return index >= 0 && index < _spawns.Count ? _spawns[index] : null;
    }

    private static void EnsureBuilt()
    {
        if (_spawns != null)
        {
            return;
        }

        // Bosses can be inactive until triggered, so include inactive objects.
        var entries = new List<(string path, int index, Spawn spawn)>();
        foreach (var boss in Object.FindObjectsOfType<Ghostqueen>(true))
        {
            var spawn = Traverse.Create(boss).Field<Spawn>("waveToSpawn").Value;
            if (spawn != null)
            {
                entries.Add((Helpers.GetPath(boss.transform), 0, spawn));
            }
        }

        foreach (var boss in Object.FindObjectsOfType<KeyframedBoss>(true))
        {
            var wave = Traverse.Create(boss).Field<Wave>("waveToSpawn").Value;
            if (wave?.spawns == null)
            {
                continue;
            }

            for (var i = 0; i < wave.spawns.Count; ++i)
            {
                entries.Add((Helpers.GetPath(boss.transform), i, wave.spawns[i]));
            }
        }

        entries.Sort((a, b) =>
        {
            var order = string.CompareOrdinal(a.path, b.path);
            return order != 0 ? order : a.index.CompareTo(b.index);
        });

        _spawns = new List<Spawn>(entries.Count);
        foreach (var entry in entries)
        {
            _spawns.Add(entry.spawn);
        }
    }
}
