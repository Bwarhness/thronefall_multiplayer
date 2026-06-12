using HarmonyLib;
using ThronefallMP.Network;
using ThronefallMP.Patches;

namespace ThronefallMP.Patches;

// Makes host-only unlocks selectable for joining clients in the shared loadout UI for the current session only.
[HarmonyPatch(typeof(Equippable), "get_IsUnlocked")]
public static class EquippableIsUnlockedPatch
{
    private static void Postfix(Equippable __instance, ref bool __result)
    {
        if (__result)
        {
            return;
        }

        var network = Plugin.Instance?.Network;
        if (network == null || !network.Online || network.Server)
        {
            return;
        }

        if (!SceneTransitionManagerPatch.InLevelSelect)
        {
            return;
        }

        var equipment = Equip.Convert(__instance.name);
        if (equipment != Equipment.Invalid && HostUnlocks.Session.Contains(equipment))
        {
            __result = true;
        }
    }
}
