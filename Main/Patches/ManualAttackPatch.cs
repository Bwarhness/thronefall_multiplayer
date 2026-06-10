using HarmonyLib;
using ThronefallMP.Components;

namespace ThronefallMP.Patches;

// ManualAttack.Tick reads the LOCAL Rewired input ("Lock Target") via its private 'input' field and mutates
// 'preferredTarget'/'preferredTargetPreviouslySet'. PlayerAttackPatch ticks EVERY pawn on every peer, so one
// machine's Lock Target press would retarget OTHER players' pawns simulated on that machine. For remote pawns we
// snapshot both fields before the original runs and restore them after, so their lock state is driven only by
// LockTargetPacket. HookGen emits no On. hook for ManualAttack, so use a Harmony prefix/postfix pair instead.
[HarmonyPatch(typeof(ManualAttack), nameof(ManualAttack.Tick))]
internal static class ManualAttackPatch
{
    private static void Prefix(ManualAttack __instance, out (bool restore, TaggedObject target, bool set) __state)
    {
        var pawn = __instance.GetComponentInParent<PlayerNetworkData>();
        if (pawn == null || pawn.IsLocal)
        {
            __state = (false, null, false);
            return;
        }

        var traverse = Traverse.Create(__instance);
        __state = (
            true,
            traverse.Field<TaggedObject>("preferredTarget").Value,
            traverse.Field<bool>("preferredTargetPreviouslySet").Value
        );
    }

    private static void Postfix(ManualAttack __instance, (bool restore, TaggedObject target, bool set) __state)
    {
        if (!__state.restore)
        {
            return;
        }

        var traverse = Traverse.Create(__instance);
        traverse.Field<TaggedObject>("preferredTarget").Value = __state.target;
        traverse.Field<bool>("preferredTargetPreviouslySet").Value = __state.set;
    }
}
