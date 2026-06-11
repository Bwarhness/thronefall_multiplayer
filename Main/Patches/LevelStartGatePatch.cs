using HarmonyLib;
using Steamworks;
using ThronefallMP.Network;
using ThronefallMP.UI;

namespace ThronefallMP.Patches;

// Gates the loadout popup's Start on every player having a weapon pick. The popup's play button is
// UnityEvent-wired to one of two TransitionToSelectedLevel methods (and PreLevelMenuIsOpen — the
// vanilla gate point — is dead in the current game build), so both are prefix-patched. Only starts
// originating from the loadout GRID frame are gated: the Continue button (level-select frame) and the
// in-map restart flow must pass through to their existing handling.
[HarmonyPatch]
public static class LevelStartGatePatch
{
    [HarmonyPatch(typeof(TransitionToSelectedLevelHelper), nameof(TransitionToSelectedLevelHelper.TransitionToSelectedLevel))]
    [HarmonyPrefix]
    private static bool GateHelper()
    {
        return GateAllows();
    }

    [HarmonyPatch(typeof(LevelSelectUIFrameHelper), nameof(LevelSelectUIFrameHelper.TransitionToSelectedLevel))]
    [HarmonyPrefix]
    private static bool GateFrameHelper()
    {
        return GateAllows();
    }

    private static bool GateAllows()
    {
        if (!Plugin.Instance.Network.Online || !SceneTransitionManagerPatch.InLevelSelect)
        {
            return true;
        }

        var ui = UIFrameManager.instance;
        if (ui == null || LoadoutFrames.HelperFor(ui.ActiveFrame) == null)
        {
            return true;
        }

        foreach (var player in Plugin.Instance.PlayerManager.GetAllPlayers())
        {
            LoadoutState.WeaponPicks.TryGetValue(player.Id, out var pick);
            if (pick == Equipment.Invalid)
            {
                var name = SteamFriends.GetFriendPersonaName(player.SteamID);
                UIManager.CreateMessageDialog(
                    "Not so fast",
                    $"Waiting for {name} to pick a weapon.");
                return false;
            }
        }

        return true;
    }
}
