using HarmonyLib;

namespace ThronefallMP.Patches;

public static class RevivePanelPatch
{
    public static void Apply()
    {
        On.RevivePanel.Update += Update;
    }

    private static void Update(On.RevivePanel.orig_Update original, RevivePanel self)
    {
        var data = Plugin.Instance.PlayerManager.LocalPlayer?.Data;
        if (data != null)
        {
            var autoRevive = data.GetComponent<AutoRevive>();
            self.playerReviveComponent = autoRevive;

            // In MP the local AutoRevive's knockout timer (hasBeenKnockedOutFor) can stay > 0 after the player has
            // actually been revived: the authoritative revive arrives via HpSync / the host-driven dawn, which don't
            // always line up with this client's own AutoRevive bookkeeping. That leaves "Oops, you died. You'll
            // respawn shortly." stuck on screen while the player is alive and walking around. Tie the panel to the
            // real state instead: RevivePanel shows iff TimeTillRevive > 0, and TimeTillRevive is -1 once
            // hasBeenKnockedOutFor is 0, so the moment the player is no longer knocked out, clear the timer.
            var hp = data.GetComponent<Hp>();
            if (autoRevive != null && hp != null && !hp.KnockedOut)
            {
                Traverse.Create(autoRevive).Field<float>("hasBeenKnockedOutFor").Value = 0f;
            }
        }

        original(self);
    }
}
