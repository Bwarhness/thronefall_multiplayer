using System.Collections.Generic;
using HarmonyLib;

namespace ThronefallMP.Patches;

public static class GateOpenerPatch
{
    public static void Apply()
    {
        On.GateOpener.Update += Update;
    }

    private static void Update(On.GateOpener.orig_Update original, GateOpener self)
    {
        if (!TagManager.instance)
        {
            return;
        }

        // The 2024+ game reworked GateOpener internals (Open/Close, the player/unit lists, the door & bars
        // animation). The previous hand-rolled reimplementation here NRE'd against those changes. Instead, push
        // the mod-synced player/unit lists into the gate each frame (so late joiners are handled too) and defer
        // to the vanilla Update, which now contains the correct, current logic.
        Traverse.Create(self).Field<IReadOnlyList<TaggedObject>>("players").Value = TagManager.instance.Players;
        Traverse.Create(self).Field<IReadOnlyList<TaggedObject>>("playerUnits").Value = TagManager.instance.PlayerUnits;
        original(self);
    }
}
