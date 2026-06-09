using HarmonyLib;

namespace ThronefallMP.Patches;

// The 2024+ game's UnitTypeDisplay.Update calls CommandUnits.ShouldShowUnitTypes, whose getter dereferences the
// serialized 'rangeIndicator' and 'commandingIndicator' references. On mod-instantiated player prefabs those
// scene-level references are not remapped and end up null, producing a per-frame NullReferenceException.
// Guard the getter (via Harmony, since HookGen does not emit an On. hook for property getters) and report
// "don't show" when the indicators aren't wired up.
[HarmonyPatch(typeof(CommandUnits), nameof(CommandUnits.ShouldShowUnitTypes), MethodType.Getter)]
internal static class CommandUnitsShouldShowUnitTypesPatch
{
    private static bool Prefix(CommandUnits __instance, ref bool __result)
    {
        if (__instance.rangeIndicator == null || __instance.commandingIndicator == null)
        {
            __result = false;
            return false; // skip the original getter to avoid the NRE
        }

        return true;
    }
}
