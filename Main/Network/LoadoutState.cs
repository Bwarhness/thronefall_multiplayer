using System.Collections.Generic;

namespace ThronefallMP.Network;

// Shared state for the synced pre-level loadout popup. Written by the packet handlers and the LoadoutWatcher
// component; read by the Start gate (LevelSelectManagerPatch) and the host's level-start pre-fill (LevelDataSync).
public static class LoadoutState
{
    // Every player's current weapon pick (including the local player). Equipment.Invalid = no pick.
    public static readonly Dictionary<ushort, Equipment> WeaponPicks = new();

    // Set by HandleLoadoutOpen before it force-opens the popup, so the watcher's open-edge knows the open was
    // remote-initiated and must not rebroadcast a LoadoutOpenPacket (or the opener's selection).
    public static bool RemoteOpenPending;

    // Set by HandleLoadoutClose/HandleLoadoutOpen before closing the frame, so the watcher's close-edge knows
    // not to rebroadcast a LoadoutClosePacket.
    public static bool RemoteClosePending;

    // Set by HandleLoadoutSelection after mutating CurrentlyEquipped, so the watcher re-snapshots once without
    // treating the remote change as a local edit (echo-loop guard).
    public static bool SuppressNextDiff;

    public static void Reset()
    {
        WeaponPicks.Clear();
        RemoteOpenPending = false;
        RemoteClosePending = false;
        SuppressNextDiff = false;
    }
}
