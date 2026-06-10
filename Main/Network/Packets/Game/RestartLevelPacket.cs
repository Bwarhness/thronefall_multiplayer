namespace ThronefallMP.Network.Packets.Game;

public class RestartLevelPacket : BasePacket
{
    public const PacketId PacketID = PacketId.RestartLevel;

    public bool OverwriteSave;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;

    public override void Send(Buffer writer)
    {
        writer.Write(OverwriteSave);
    }

    public override void Receive(Buffer reader)
    {
        OverwriteSave = reader.ReadBoolean();
    }
}