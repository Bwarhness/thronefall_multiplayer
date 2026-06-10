using ThronefallMP.Components;

namespace ThronefallMP.Network.Packets.Game;

public class LockTargetPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LockTarget;

    public ushort PlayerId;
    public IdentifierData Target; // Invalid = unlock

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer)
    {
        writer.Write(PlayerId);
        writer.Write(Target);
    }

    public override void Receive(Buffer reader)
    {
        PlayerId = reader.ReadUInt16();
        Target = reader.ReadIdentifierData();
    }
}
