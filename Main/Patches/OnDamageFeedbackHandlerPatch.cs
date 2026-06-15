using HarmonyLib;

namespace ThronefallMP.Patches;

// The host broadcasts a DamageFeedbackPacket for every identified Hp that takes damage; the client
// replays it by invoking Hp.OnReceiveDamage, which runs vanilla OnDamageFeedbackHandler.TakeDamage.
// Its player-hit branch (causedByPlayer == true) dereferences onDmgByPlayerFX.transform to spawn a
// hit-effect prefab — but plenty of damageable targets have no such prefab assigned, so every
// player-caused hit on one of them throws a NullReferenceException on the client, flooding the log.
// (No On.OnDamageFeedbackHandler hook in the runtime MMHOOK — Harmony patch.)
[HarmonyPatch(typeof(OnDamageFeedbackHandler), "TakeDamage")]
public static class OnDamageFeedbackHandlerPatch
{
    [HarmonyPrefix]
    private static bool TakeDamage(OnDamageFeedbackHandler __instance, bool causedByPlayer)
    {
        // Both vanilla branches deref flasher; if it is missing there is nothing safe to do.
        if (__instance.flasher == null)
        {
            return false;
        }

        if (causedByPlayer && __instance.onDmgByPlayerFX == null)
        {
            // Reproduce just the flash (the half that does not need the missing prefab) and skip the
            // FX instantiation that would NRE.
            var duration = Traverse.Create(__instance).Field<float>("flashDuration").Value;
            __instance.flasher.TriggerFlash(true, duration);
            return false;
        }

        return true;
    }
}
