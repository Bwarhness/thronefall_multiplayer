using HarmonyLib;
using ThronefallMP.Network.Packets.Game;

namespace ThronefallMP.Patches;

// Makes defeat host-authoritative, like victory (VictoryPacket) and resign (ResignPacket) already are.
// Vanilla LocalGamestate.OnVitalObjectKill (the HQ "vital object" dying) sets AfterMatchDefeat locally on
// EVERY machine. On a client the castle's Hp is driven by the host's HpSync, and during the load / first
// sync window it can momentarily read as dead — so the client would declare an instant, bogus defeat the
// moment it enters a fresh level while the host's real simulation is perfectly fine (observed: a client
// joined a new game and immediately saw the defeat screen). Clients must mirror the host, never evaluate
// the loss themselves. (No On.LocalGamestate.OnVitalObjectKill in the runtime MMHOOK — Harmony patch.)
[HarmonyPatch(typeof(LocalGamestate), "OnVitalObjectKill")]
public static class LocalGamestateDefeatPatch
{
    [HarmonyPrefix]
    private static bool OnVitalObjectKill()
    {
        var network = Plugin.Instance.Network;
        if (!network.Online)
        {
            return true; // Singleplayer: vanilla defeat as normal.
        }

        if (network.Server)
        {
            // The host runs the authoritative simulation, so its vital-object death is the real defeat.
            // Tell every client to follow (ResignPacket -> HandleResign sets AfterMatchDefeat), then let
            // vanilla apply the host's own defeat. Only meaningful while InMatch — vanilla guards on that
            // too, so mirror it to avoid a spurious broadcast during teardown.
            if (LocalGamestate.Instance != null &&
                LocalGamestate.Instance.CurrentState == LocalGamestate.State.InMatch)
            {
                network.Send(new ResignPacket());
            }

            return true;
        }

        // Client: never self-declare defeat. The host broadcasts ResignPacket on a genuine loss; until
        // then a locally-dead-looking castle is just an un-synced HP value that HpSync will correct.
        return false;
    }
}
