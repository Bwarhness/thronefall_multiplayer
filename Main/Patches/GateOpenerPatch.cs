using System.Collections.Generic;
using HarmonyLib;

namespace ThronefallMP.Patches;

public static class GateOpenerPatch
{
    public static void Apply()
    {
        On.GateOpener.Update += Update;
    }

    private static void Update(On.GateOpener.orig_Update original, GateOpener self)
    {
        if (!TagManager.instance)
        {
            return;
        }

        var tm = TagManager.instance;

        // The 2024+ game reworked GateOpener internals (Open/Close, the player/unit lists, the door & bars
        // animation). The previous hand-rolled reimplementation here NRE'd against those changes, so we push the
        // mod-synced player/unit lists into the gate each frame (so late joiners are handled too) and defer to the
        // vanilla Update, which now contains the correct, current logic.
        //
        // BUT vanilla's Update dereferences unit.GetComponent<PathfindMovementPlayerunit>().HomePosition for every
        // PlayerOwned unit within range. Siege weapons (and stale/destroyed units the syncs can leave buffered)
        // have no PathfindMovementPlayerunit, so that deref NRE-floods the log every frame near an open gate.
        // Feed the gate only units that actually have the component (which also drops Unity fake-null/destroyed
        // entries), and temporarily mask TagManager's global buffer that the vanilla "stay open" loop reads
        // directly, so that path is null-safe too.
        var safeUnits = new List<TaggedObject>();
        foreach (var unit in tm.PlayerUnits)
        {
            if (unit != null && unit.GetComponent<PathfindMovementPlayerunit>() != null)
            {
                safeUnits.Add(unit);
            }
        }

        Traverse.Create(self).Field<IReadOnlyList<TaggedObject>>("players").Value = tm.Players;
        Traverse.Create(self).Field<IReadOnlyList<TaggedObject>>("playerUnits").Value = safeUnits;

        var bufferedPlayerUnits = Traverse.Create(tm).Field<List<TaggedObject>>("bufferedPlayerUnits");
        var savedBuffer = bufferedPlayerUnits.Value;
        bufferedPlayerUnits.Value = safeUnits;
        try
        {
            original(self);
        }
        catch (System.Exception e)
        {
            // Still throws with the lists sanitized — most likely the door transform refs dying when
            // the gate's wall is upgraded (mesh swap). Log the gate's state once instead of flooding;
            // the gate simply doesn't animate while broken.
            if (_loggedGates.Add(self.GetInstanceID()))
            {
                var t = Traverse.Create(self);
                var doorL = t.Field<UnityEngine.Transform>("doorL").Value;
                var doorR = t.Field<UnityEngine.Transform>("doorR").Value;
                Plugin.Log.LogWarning(
                    $"[GateDiag] {e.GetType().Name} in gate '{self.gameObject.name}' " +
                    $"mode={t.Field("mode").GetValue()} open={t.Field("open").GetValue()} " +
                    $"doorL={(doorL != null ? "ok" : "null/destroyed")} " +
                    $"doorR={(doorR != null ? "ok" : "null/destroyed")} " +
                    $"players={(tm.Players != null ? tm.Players.Count : -1)} units={safeUnits.Count}: {e.Message}");
            }
        }
        finally
        {
            bufferedPlayerUnits.Value = savedBuffer;
        }
    }

    private static readonly HashSet<int> _loggedGates = new();
}
