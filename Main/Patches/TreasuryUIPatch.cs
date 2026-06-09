using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ThronefallMP.Components;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Debug = System.Diagnostics.Debug;

namespace ThronefallMP.Patches;

public class TreasuryUIPatch
{
	public static bool OverrideFocus = false;
	
    public static void Apply()
    {
        On.TreasuryUI.Start += Start;
        On.TreasuryUI.Update += Update;

        //On.TreasuryUI.Update += Update;
        // if (this.coinQeue < 0 && this.addCounter <= 0f)
        // should be if (this.coinQeue < 0 && this.removalCounter <= 0f)
        // but doesn't matter because the removal interval is 0
    }
    
    private static void Start(On.TreasuryUI.orig_Start original, TreasuryUI self)
    {
	    Plugin.Log.LogInfo($"Registering Treasury UI");
        DayNightCycle.Instance.RegisterDaytimeSensitiveObject(self);
        var currentState = Traverse.Create(self).Field<TreasuryUI.AnimationState>("currentState");

        Traverse.Create(self).Field<Transform>("scaleTarget").Value = UIFrameManager.instance.TreasureChest.scaleTarget;
        Traverse.Create(self).Field<TextMeshProUGUI>("displayText").Value = UIFrameManager.instance.TreasureChest.balanceNumber;
        Traverse.Create(self).Method("SetState", currentState.Value).GetValue();
    }

    private static void Update(On.TreasuryUI.orig_Update original, TreasuryUI self)
    {
	    var overrideActivation = Traverse.Create(self).Field<bool>("overrideActivation");
	    var activationCounter = Traverse.Create(self).Field<float>("activationCounter");
	    var activationLifetime = Traverse.Create(self).Field<float>("activationLifetime");
	    var displayText = Traverse.Create(self).Field<TextMeshProUGUI>("displayText");
	    if (!OverrideFocus && overrideActivation.Value)
	    {
		    activationCounter.Value = activationLifetime.Value;
	    }

	    overrideActivation.Value = OverrideFocus;

	    // game 2024+ added TreasuryUI.targetPlayer, set in the vanilla Start (which we override). The vanilla
	    // Update renders the coin count + spawns coins from targetPlayer.Balance. Lazily bind it to the local
	    // player, and mirror the shared multiplayer gold (GlobalData.Balance) onto that player's balance so the
	    // vanilla Update shows the correct amount instead of the per-player value (which is ~0 in shared-gold MP).
	    var targetPlayer = Traverse.Create(self).Field<PlayerInteraction>("targetPlayer");
	    if (targetPlayer.Value == null)
	    {
		    var localData = Plugin.Instance.PlayerManager.LocalPlayer?.Data;
		    if (localData != null)
		    {
			    targetPlayer.Value = localData.GetComponent<PlayerInteraction>();
		    }
	    }

	    if (targetPlayer.Value != null)
	    {
		    Traverse.Create(targetPlayer.Value).Field<int>("balance").Value = GlobalData.Balance;
		    original(self);
	    }

	    // Safety net (and covers the brief window before a local player exists): always show the shared balance.
	    displayText.Value.text = $"<sprite name=\"coin\">{GlobalData.Balance}";
    }
}