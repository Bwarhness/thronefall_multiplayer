using HarmonyLib;
using UnityEngine;

namespace ThronefallMP.Patches;

// PushPlayer2D keeps two players from overlapping by reading the OTHER player's transform (ptrans)
// every frame. In MP, pawns are destroyed/recreated on scene reloads (retries), and a PushPlayer2D
// can be left holding a destroyed ptrans (or a stale tag-list player) — its Update then dereferences
// ptrans.position with no guard and NRE-floods every frame. Skip the update when its references are
// gone. (No On.PushPlayer2D hook in the runtime MMHOOK — Harmony patch.)
[HarmonyPatch(typeof(PushPlayer2D), "Update")]
public static class PushPlayer2DPatch
{
    [HarmonyPrefix]
    private static bool Update(PushPlayer2D __instance)
    {
        var ptrans = Traverse.Create(__instance).Field<Transform>("ptrans").Value;
        var pm = Traverse.Create(__instance).Field<PlayerMovement>("pm").Value;
        // Unity-aware null check also catches destroyed objects.
        return ptrans != null && pm != null;
    }
}
