namespace ThronefallMP.Network.Packets.Game;

// "Replay the loadout popup state to me" — sent to the host by a peer that just became able to show
// the popup (finished joining / returned to the overworld) and may have missed the open broadcast.
public class LoadoutStateRequestPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutStateRequest;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;

    public override void Send(Buffer writer) { }

    public override void Receive(Buffer reader) { }
}
