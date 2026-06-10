using HarmonyLib;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets.Game;

namespace ThronefallMP.Patches;

public static class PlayerAttackPatch
{
    // Last broadcast preferred target of the local pawn (only the local pawn is tracked). Unity's == treats a
    // destroyed TaggedObject as equal to null, so a dead target never triggers a redundant unlock packet or NREs.
    private static TaggedObject _lastPreferredTarget;

    public static void Apply()
    {
        On.PlayerAttack.Update += Update;
    }

    private static void Update(On.PlayerAttack.orig_Update orig, PlayerAttack self)
    {
        var attack = Traverse.Create(self).Field<ManualAttack>("attack");
        if (attack.Value != null)
        {
            attack.Value.Tick();
            var data = self.GetComponent<PlayerNetworkData>();
            if (data != null && data.IsLocal)
            {
                self.ui.SetCurrentCooldownPercentage(attack.Value.CooldownPercentage);
                BroadcastTargetLock(attack.Value, data);
            }
        }
    }

    private static void BroadcastTargetLock(ManualAttack attack, PlayerNetworkData data)
    {
        var target = Traverse.Create(attack).Field<TaggedObject>("preferredTarget").Value;
        if (target == _lastPreferredTarget)
        {
            return;
        }

        _lastPreferredTarget = target;
        if (!Plugin.Instance.Network.Online)
        {
            return;
        }

        var identifier = target == null ? null : target.GetComponent<Identifier>();
        Plugin.Instance.Network.Send(new LockTargetPacket
        {
            PlayerId = data.id,
            Target = identifier == null ? IdentifierData.Invalid : new IdentifierData(identifier)
        });
    }
}