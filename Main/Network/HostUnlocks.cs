using System.Collections.Generic;

namespace ThronefallMP.Network;

// Session-only set of equipment unlocked by the current MP host. Joining clients can select these items
// in the shared loadout UI for the duration of the session without persisting anything to their save.
public static class HostUnlocks
{
    public static readonly HashSet<Equipment> Session = new();

    public static void Reset()
    {
        Session.Clear();
    }
}
