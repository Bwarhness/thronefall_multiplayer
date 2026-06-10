using Steamworks;
using UnityEngine;

namespace ThronefallMP.Network.Packets.Game;

// Host-authoritative spawn of a boss minion (Ghostqueen ghosts, Eismoloch/KeyframedBoss waves).
// These spawns come from private Spawn instances inside the boss components, so they are addressed
// through BossSpawnRegistry indices instead of EnemySpawner wave/spawn indices. DifficultyMulti is
// the value the boss passed to Spawn.Update (1 for Ghostqueen, the private wave's difficultyMulti
// for KeyframedBoss); the receiver cannot reconstruct it from the Spawn alone.
public class BossSpawnPacket : BasePacket
{
    public const PacketId PacketID = PacketId.BossSpawn;

    public ushort SpawnRef;
    public ushort Id;
    public Vector3 Position;
    public int Coins;
    public bool Elite;
    public float DifficultyMulti;

    public override PacketId TypeID => PacketID;
    public override Channel Channel => Channel.Game;
    public override bool CanHandle(CSteamID sender)
    {
        return Plugin.Instance.Network.IsServer(sender);
    }

    public override void Send(Buffer writer)
    {
        writer.Write(SpawnRef);
        writer.Write(Id);
        writer.Write(Position);
        writer.Write(Coins);
        writer.Write(Elite);
        writer.Write(DifficultyMulti);
    }

    public override void Receive(Buffer reader)
    {
        SpawnRef = reader.ReadUInt16();
        Id = reader.ReadUInt16();
        Position = reader.ReadVector3();
        Coins = reader.ReadInt32();
        Elite = reader.ReadBoolean();
        DifficultyMulti = reader.ReadFloat();
    }
}
