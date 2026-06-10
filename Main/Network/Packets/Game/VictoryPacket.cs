namespace ThronefallMP.Network.Packets.Game;

// Host-authoritative "the match was won" signal. Mirrors ResignPacket (the defeat counterpart): vanilla decides
// victory locally in SwitchToDayCoroutine off the private, unsynced EnemySpawner.wavenumber, so a peer whose wave
// count lags never triggers it and its victory screen sticks. The host broadcasts this when EnemySpawner.MatchOver,
// and every peer transitions to AfterMatchVictory together. ShouldPropagate => the server auto-rebroadcasts it.
public class VictoryPacket : BasePacket
{
    public const PacketId PacketID = PacketId.Victory;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool ShouldPropagate => true;

    public override void Send(Buffer writer) { }

    public override void Receive(Buffer reader) { }
}
