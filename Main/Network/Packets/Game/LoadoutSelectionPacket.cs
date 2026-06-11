using System.Collections.Generic;

namespace ThronefallMP.Network.Packets.Game;

// Full non-weapon selection (perks + mutators) of the shared loadout. Sent whenever any player toggles something;
// receivers replace the non-weapon part of PerkManager.CurrentlyEquipped with this list. Weapons never ride here.
public class LoadoutSelectionPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutSelection;

    public List<Equipment> Selection = new();

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer)
    {
        writer.Write((byte)Selection.Count);
        foreach (var item in Selection)
        {
            writer.Write(item);
        }
    }

    public override void Receive(Buffer reader)
    {
        var count = reader.ReadByte();
        Selection.Clear();
        for (var i = 0; i < count; ++i)
        {
            Selection.Add(reader.ReadEquipment());
        }
    }
}
