using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Pathfinding.RVO;
using Steamworks;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets.Game;
using ThronefallMP.Utils;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ThronefallMP.Patches;

static class PlayerMovementPatch
{
	private static readonly int Moving = Animator.StringToHash("Moving");
	private static readonly int Sprinting = Animator.StringToHash("Sprinting");
	
	public static void Apply()
	{
		On.PlayerMovement.Awake += Awake;
		On.PlayerMovement.Start += Start;
        On.PlayerMovement.Update += Update;
        On.PlayerMovement.TeleportTo += TeleportTo;
        On.CameraRig.Start += Start;
	}

	// The rig is relocated in PlayerMovement.Awake, so we deliberately skip vanilla CameraRig.Start (it would
	// re-parent the rig). But vanilla Start also wires the Rewired zoom input, currentTarget, and default zoom
	// that the game's UpdateZoom/Update now require — replicate just those bits here (Start runs once ReInput is
	// ready). Without this, UpdateZoom NREs on a null 'input' and the camera would zoom to targetOrthSize 0.
	private static void Start(On.CameraRig.orig_Start original, CameraRig self)
	{
		var t = Traverse.Create(self);
		t.Field<Transform>("currentTarget").Value = t.Field<Transform>("cameraTarget").Value;
		t.Field<Rewired.Player>("input").Value = Rewired.ReInput.players.GetPlayer(0);
		var zoomLevels = t.Field<float[]>("zoomLevels").Value;
		if (zoomLevels != null && zoomLevels.Length > 0)
		{
			t.Field<float>("targetOrthSize").Value = zoomLevels[0];
		}
	}

	private static void Awake(On.PlayerMovement.orig_Awake original, PlayerMovement self)
	{
		var vanillaPlayer = self.GetComponent<PlayerNetworkData>() == null;
		if (!vanillaPlayer)
		{
			return;
		}
		
		var rig = self.GetComponentInChildren<CameraRig>();
		Traverse.Create(rig).Field<Quaternion>("startRotation").Value = rig.transform.rotation;
		Traverse.Create(rig).Field<Transform>("cameraTarget").Value = rig.transform.parent;
		rig.transform.SetParent(null);
		
		Plugin.Instance.PlayerManager.SetPrefab(self.gameObject);
		self.enabled = false;
		Object.Destroy(self.gameObject);
	}
	
	
	private static void Start(On.PlayerMovement.orig_Start original, PlayerMovement self)
	{
		var vanillaPlayer = self.gameObject.GetComponent<PlayerNetworkData>() == null;
		if (!vanillaPlayer)
		{
			original(self);
		}
	}

	private static void Update(On.PlayerMovement.orig_Update original, PlayerMovement self)
    {
	    var playerNetworkData = self.GetComponent<PlayerNetworkData>();
	    if (playerNetworkData == null)
	    {
		    return;
	    }

	    // Vanilla sets the PlayerMovement.instance singleton in Awake, which we skip for networked players.
	    // Various vanilla systems (e.g. MinimapRenderer.DrawPlayer) read it, so point it at the local player.
	    if (playerNetworkData.IsLocal && PlayerMovement.instance != self)
	    {
		    PlayerMovement.instance = self;
	    }

        var hp = Traverse.Create(self).Field<Hp>("hp").Value;
        var rvoController = Traverse.Create(self).Field<RVOController>("rvoController").Value;
        var heavyArmorEquipped = Traverse.Create(self).Field<bool>("heavyArmorEquipped").Value;
        var racingHorseEquipped = Traverse.Create(self).Field<bool>("racingHorseEquipped").Value;
        
        var velocity = Traverse.Create(self).Field<Vector3>("velocity");
        var yVelocity = Traverse.Create(self).Field<float>("yVelocity");
        var viewTransform = Traverse.Create(self).Field<Transform>("viewTransform");
        var sprintingToggledOn = Traverse.Create(self).Field<bool>("sprintingToggledOn");
        var sprinting = Traverse.Create(self).Field<bool>("sprinting");
        var moving = Traverse.Create(self).Field<bool>("moving");
        var slowedFor = Traverse.Create(self).Field<float>("slowedFor");
        var desiredMeshRotation = Traverse.Create(self).Field<Quaternion>("desiredMeshRotation");
        var controller = Traverse.Create(self).Field<CharacterController>("controller");

        if (viewTransform.Value == null)
        {
	        return;
        }
        
        // Normal code
		var zero = new Vector2(playerNetworkData.SharedData.MoveVertical, playerNetworkData.SharedData.MoveHorizontal);
		if (LocalGamestate.Instance.PlayerFrozen && playerNetworkData.IsLocal)
		{
			zero = Vector2.zero;
		}
		
		var normalized = Vector3.ProjectOnPlane(viewTransform.Value.forward, Vector3.up).normalized;
		var normalized2 = Vector3.ProjectOnPlane(viewTransform.Value.right, Vector3.up).normalized;
		velocity.Value = Vector3.zero;
		velocity.Value += normalized * zero.x;
		velocity.Value += normalized2 * zero.y;
		velocity.Value = Vector3.ClampMagnitude(velocity.Value, 1f);
		var shouldToggleSprint = playerNetworkData.SharedData.SprintToggleButton && !playerNetworkData.PlayerMovementSprintLast;
		playerNetworkData.PlayerMovementSprintLast = playerNetworkData.SharedData.SprintToggleButton;
		if (shouldToggleSprint)
		{
			sprintingToggledOn.Value = !sprintingToggledOn.Value;
		}
		if (sprintingToggledOn.Value && playerNetworkData.SharedData.SprintButton)
		{
			sprintingToggledOn.Value = false;
		}
		slowedFor.Value -= Time.deltaTime;
		if (Plugin.Instance.Network.Server)
		{
			playerNetworkData.SharedData.Slowed = slowedFor.Value > 0f;
		}
		sprinting.Value = (playerNetworkData.SharedData.SprintButton || sprintingToggledOn.Value) && hp.HpPercentage >= 1f;
		velocity.Value *= (sprinting.Value ? self.sprintSpeed : self.speed);
		velocity.Value *= playerNetworkData.SharedData.Slowed ? 0.33f : 1f;
		if (heavyArmorEquipped && DayNightCycle.Instance.CurrentTimestate == DayNightCycle.Timestate.Night)
		{
			velocity.Value *= PerkManager.instance.heavyArmor_SpeedMultiplyer;
		}
		if (racingHorseEquipped)
		{
			velocity.Value *= PerkManager.instance.racingHorse_SpeedMultiplyer;
		}
		// Match vanilla: only push velocity into the RVO agent when the controller is enabled. On the overworld
		// the RVO agent isn't registered with a simulator, and set_velocity NREs otherwise.
		if (rvoController != null && rvoController.enabled)
		{
			rvoController.velocity = velocity.Value;
		}
		moving.Value = velocity.Value.sqrMagnitude > 0.1f;
		if (moving.Value)
		{
			desiredMeshRotation.Value = Quaternion.LookRotation(velocity.Value.normalized, Vector3.up);
		}
		if (desiredMeshRotation.Value != self.meshParent.rotation)
		{
			self.meshParent.rotation = Quaternion.RotateTowards(self.meshParent.rotation, desiredMeshRotation.Value, self.maxMeshRotationSpeed * Time.deltaTime);
		}
		
		self.meshAnimator.SetBool(Moving, moving.Value);
		self.meshAnimator.SetBool(Sprinting, sprinting.Value);
		if (!controller.Value.enabled)
		{
			return;
		}
		
		if (controller.Value.isGrounded)
		{
			yVelocity.Value = 0f;
		}
		else
		{
			yVelocity.Value += -9.81f * Time.deltaTime;
		}
			
		velocity.Value += Vector3.up * yVelocity.Value;
		controller.Value.Move(velocity.Value * Time.deltaTime);

		var yFallThroughMapDetection = Traverse.Create(self).Field<float>("yFallThroughMapDetection");
		if (!(self.transform.position.y < yFallThroughMapDetection.Value))
		{
			return;
		}
				
		velocity.Value = Vector3.zero;
		yVelocity.Value = 0f;
		self.TeleportTo(
			Helpers.GetSpawnLocation(
				Plugin.Instance.PlayerManager.SpawnLocation,
				playerNetworkData.Player.SpawnID
			)
		);
    }
	
	private static void TeleportTo(On.PlayerMovement.orig_TeleportTo original, PlayerMovement self, Vector3 position)
	{
		var playerNetworkData = self.GetComponent<PlayerNetworkData>();
		if (playerNetworkData == null)
		{
			return;
		}

		if (playerNetworkData.IsLocal)
		{
			var packet = new TeleportPlayerPacket
			{
				PlayerId = playerNetworkData.id,
				Position = position
			};
			Plugin.Instance.Network.Send(packet);
		}
		
		original(self, position);
	}
}
