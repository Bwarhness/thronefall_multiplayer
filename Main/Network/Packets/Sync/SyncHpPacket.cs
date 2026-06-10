using ThronefallMP.Components;

namespace ThronefallMP.Network.Packets.Sync;

public class SyncHpPacket : BasePacket
{
    public const PacketId PacketID = PacketId.SyncHp;

    public IdentifierData Target;
    //public float MaxHp; // Maybe sync this?
    public float Hp;
    public float MaxHp;
    public bool KnockedOut;
    public bool Invulnerable;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Player;

    public override void Send(Buffer writer)
    {
        writer.Write(Target);
        writer.Write(Hp);
        writer.Write(KnockedOut);
        writer.Write(Invulnerable);
        writer.Write(MaxHp);
    }

    public override void Receive(Buffer reader)
    {
        Target = reader.ReadIdentifierData();
        Hp = reader.ReadFloat();
        KnockedOut = reader.ReadBoolean();
        Invulnerable = reader.ReadBoolean();
        MaxHp = reader.ReadFloat();
    }
}