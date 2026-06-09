using HarmonyLib;

namespace ThronefallMP.Patches;

// MinimapRenderer.DrawPlayer dereferences the PlayerMovement.instance singleton. The mod sets that singleton on
// the local player, but there is a brief window during scene load (minimap coroutine ticks before the local
// player is instantiated) where it is still null. HookGen emits no On. hook for MinimapRenderer, so guard the
// private DrawPlayer via Harmony and skip it until a player exists.
[HarmonyPatch(typeof(MinimapRenderer), "DrawPlayer")]
internal static class MinimapRendererPatch
{
    private static bool Prefix()
    {
        return PlayerMovement.instance != null; // false => skip original (nothing to draw yet)
    }
}
