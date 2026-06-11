namespace ThronefallMP.Network.Packets.Game;

// One player's personal weapon pick in the loadout popup (Equipment.Invalid = un-picked). Per-player, never merged
// into the shared selection; rendered in the status strip and used to pre-fill the level-start weapon handshake.
public class LoadoutWeaponPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutWeapon;

    public ushort PlayerId;
    public Equipment Weapon;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer)
    {
        writer.Write(PlayerId);
        writer.Write(Weapon);
    }

    public override void Receive(Buffer reader)
    {
        PlayerId = reader.ReadUInt16();
        Weapon = reader.ReadEquipment();
    }
}
