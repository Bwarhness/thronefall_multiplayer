using System.Collections.Generic;

namespace ThronefallMP.Network.Packets.Game;

// Server -> client: the host's unlocked equipment list. The client uses this purely to make items selectable
// in the shared loadout UI for the current session; nothing is persisted to the joining player's save.
public class HostUnlocksPacket : BasePacket
{
    public const PacketId PacketID = PacketId.HostUnlocks;

    public List<Equipment> Unlocks = new();

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;

    public override void Send(Buffer writer)
    {
        writer.Write((byte)Unlocks.Count);
        foreach (var unlock in Unlocks)
        {
            writer.Write(unlock);
        }
    }

    public override void Receive(Buffer reader)
    {
        var count = reader.ReadByte();
        Unlocks.Clear();
        for (var i = 0; i < count; ++i)
        {
            Unlocks.Add(reader.ReadEquipment());
        }
    }
}
