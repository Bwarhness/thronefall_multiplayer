using System.Collections.Generic;
using HarmonyLib;
using ThronefallMP.Patches;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThronefallMP.UI;

// Resolves the frames that make up the pre-level loadout popup: the level-select frame (level info +
// continue/new-run buttons) and the overworld loadout grid frame(s) (weapon/perk/mutator picker, owned
// by LoadoutUIHelper). LevelSelectManager.PreLevelMenuIsOpen is dead in the current game build (nothing
// ever writes openedLevel), so popup state is derived from UIFrameManager.ActiveFrame instead.
public static class LoadoutFrames
{
    private static bool _resolved;
    private static UIFrame _levelSelect;
    private static readonly List<LoadoutUIHelper> _loadoutHelpers = new();

    static LoadoutFrames()
    {
        SceneManager.sceneLoaded += (_, _) => Invalidate();
    }

    public static void Invalidate()
    {
        _resolved = false;
        _levelSelect = null;
        _loadoutHelpers.Clear();
    }

    private static void Resolve()
    {
        if (_resolved || UIFrameManager.instance == null)
        {
            return;
        }

        _levelSelect = Traverse.Create(UIFrameManager.instance).Field<UIFrame>("levelSelectFrame").Value;
        foreach (var helper in Object.FindObjectsOfType<LoadoutUIHelper>(true))
        {
            if (!helper.inMatch && helper.frame != null)
            {
                _loadoutHelpers.Add(helper);
            }
        }

        _resolved = true;
    }

    public static bool IsPopupFrame(UIFrame frame)
    {
        if (frame == null)
        {
            return false;
        }

        Resolve();
        if (frame == _levelSelect)
        {
            return true;
        }

        foreach (var helper in _loadoutHelpers)
        {
            if (helper != null && helper.frame == frame)
            {
                return true;
            }
        }

        return false;
    }

    // The overworld loadout grid helper whose frame is currently shown, or null.
    public static LoadoutUIHelper HelperFor(UIFrame frame)
    {
        if (frame == null)
        {
            return null;
        }

        Resolve();
        foreach (var helper in _loadoutHelpers)
        {
            if (helper != null && helper.frame == frame)
            {
                return helper;
            }
        }

        return null;
    }

    // The first overworld grid frame — where weapon picks happen and the status strip lives.
    public static UIFrame PrimaryGridFrame
    {
        get
        {
            Resolve();
            foreach (var helper in _loadoutHelpers)
            {
                if (helper != null && helper.frame != null)
                {
                    return helper.frame;
                }
            }

            return _levelSelect;
        }
    }

    // The Classic campaign grid frame, or null if it is not resolved yet.
    public static UIFrame GridFrame
    {
        get
        {
            Resolve();
            foreach (var helper in _loadoutHelpers)
            {
                if (helper != null && helper.frame != null && helper.mode == LocalGamestate.GameMode.Classic)
                {
                    return helper.frame;
                }
            }

            return null;
        }
    }

    public static bool PopupOpen =>
        SceneTransitionManagerPatch.InLevelSelect &&
        UIFrameManager.instance != null &&
        IsPopupFrame(UIFrameManager.instance.ActiveFrame);

    // Diagnostic snapshot used by LoadoutWatcher heartbeat logging.
    public static string DebugState()
    {
        Resolve();
        var active = UIFrameManager.instance != null ? UIFrameManager.instance.ActiveFrame : null;
        var sb = new System.Text.StringBuilder();
        sb.Append($"resolved={_resolved} levelSelect={(_levelSelect != null ? _levelSelect.name : "null")} ");
        sb.Append($"helpers={_loadoutHelpers.Count} activeFrame={(active != null ? active.name : "null")}");
        return sb.ToString();
    }

    // Closing can pop the grid back to the level-select frame rather than fully exiting, so close until
    // no popup-family frame is active (bounded — the family is at most a few frames deep).
    public static void CloseAllPopupFrames()
    {
        var ui = UIFrameManager.instance;
        for (var i = 0; i < 4 && ui != null && IsPopupFrame(ui.ActiveFrame); ++i)
        {
            var helper = HelperFor(ui.ActiveFrame);
            if (helper != null)
            {
                // Closing the grid frame fires ResetLoadoutOnCancel.OnDeactivate, which reverts
                // CurrentlyEquipped to its on-open snapshot unless the loadout was locked in. A
                // programmatic (remote/level-start) close must keep the synced selection.
                var cancel = helper.GetComponentInChildren<ResetLoadoutOnCancel>(true);
                if (cancel != null)
                {
                    cancel.LockInLoadout();
                }
            }

            ui.CloseActiveFrame();
        }
    }
}
