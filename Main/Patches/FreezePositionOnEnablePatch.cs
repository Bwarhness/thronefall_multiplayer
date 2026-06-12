using HarmonyLib;
using UnityEngine;

namespace ThronefallMP.Patches;

// FreezePositionOnEnable re-parents its object back to oldParent when that parent deactivates, but
// oldParent is captured in OnEnable from transform.parent — null for objects enabled at the scene
// root (the mod's pawn spawns) and destroyed when the mod destroys the vanilla player. Vanilla's
// Update derefs it unguarded, flooding NullReferenceExceptions every frame. Skip the update when
// there is no parent to restore to. (No On.* hook for this type in the profile MMHOOK — Harmony.)
[HarmonyPatch(typeof(FreezePositionOnEnable), "Update")]
public static class FreezePositionOnEnablePatch
{
    [HarmonyPrefix]
    private static bool Update(FreezePositionOnEnable __instance)
    {
        // Unity-aware null check: also skips a destroyed parent.
        return Traverse.Create(__instance).Field<Transform>("oldParent").Value != null;
    }
}
