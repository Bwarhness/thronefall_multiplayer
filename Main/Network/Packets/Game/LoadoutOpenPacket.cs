namespace ThronefallMP.Network.Packets.Game;

// "A player opened the pre-level loadout popup for this level." Receivers open the same popup by replicating
// vanilla LevelInteractor.InteractionBegin (set lastActiveLevelInfo, force-open the frame).
public class LoadoutOpenPacket : BasePacket
{
    public const PacketId PacketID = PacketId.LoadoutOpen;

    public string Scene;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer)
    {
        writer.Write(Scene);
    }

    public override void Receive(Buffer reader)
    {
        Scene = reader.ReadString();
    }
}
