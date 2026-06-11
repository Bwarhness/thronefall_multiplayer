namespace ThronefallMP.Network.Packets.Game;

// "A player closed the loadout popup" — closes it for everyone and clears pending weapon picks.
public class LoadoutClosePacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutClose;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer) { }

    public override void Receive(Buffer reader) { }
}
