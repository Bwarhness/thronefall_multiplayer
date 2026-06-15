using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets.PlayerCommand;
using UnityEngine;

namespace ThronefallMP.Patches;

public static class BuildSlotPatch
{
    public struct UpgradeInfo
    {
        public int Cost;
        public int CurrentLevel;
    }
    
    public static GameObject CoinPrefab;
    
    private static bool _disableNetworkHook; 
    
    public static void Apply()
    {
        On.BuildSlot.Start += Start;
        On.BuildSlot.OnUpgradeChoiceComplete += OnUpgradeChoiceComplete;
    }

    private static void Start(On.BuildSlot.orig_Start original, BuildSlot self)
    {
        original(self);
        if (self.ActivatorBuilding == null)
        {
            // TODO: Maybe change this to happen on SceneManager sceneLoaded instead?
            self.StartCoroutine(ProcessBuildings(self));
        }
    }

    private static IEnumerator ProcessBuildings(BuildSlot root)
    {
        yield return new WaitForEndOfFrame();
        Identifier.Clear(IdentifierType.BuildSlot);
        Identifier.Clear(IdentifierType.Building);
        Identifier.Clear(IdentifierType.Ally);
        
        Plugin.Log.LogInfo("Processing buildings");
        var slots = new List<BuildSlot> { root };
        while (slots.Count > 0)
        {
            var current = slots[0];
            slots.Remove(current);
            slots.AddRange(current.IsRootOf);
            var buildingId = (ushort)current.transform.GetSiblingIndex();
            AssignId(current, buildingId);
            foreach (var respawn in current.GetComponentsInChildren<UnitRespawnerForBuildings>(true))
            {
                ProcessUnits(respawn, buildingId);
            }
        }

        CoinPrefab = root.buildingInteractor.coinSpawner.coinPrefab;

        // Safety net for the phantom-player root cause: a Player-tagged TaggedObject destroyed while
        // inactive never runs OnDisable, so it lingers in TagManager.bufferedPlayers as a Unity-fake-null
        // entry into the next gameplay scene (players=3 with 2 players -> gate/PushPlayer2D NREs). Runs
        // once per gameplay-scene load on every peer (only the activator-less root BuildSlot reaches here).
        if (TagManager.instance != null)
        {
            var buffered = Traverse.Create(TagManager.instance)
                .Field<System.Collections.Generic.List<TaggedObject>>("bufferedPlayers").Value;
            buffered?.RemoveAll(o => o == null);
        }
    }

    private static void AssignId(BuildSlot self, ushort id)
    {
        {
            var identifier = self.gameObject.AddComponent<Identifier>();
            identifier.SetIdentity(IdentifierType.BuildSlot, id);
        }
        {
            var building = self.GetComponentInChildren<Hp>(true);
            if (building != null)
            {
                var identifier = building.gameObject.AddComponent<Identifier>();
                identifier.SetIdentity(IdentifierType.Building, id);
            }
        }
    }

    private static void ProcessUnits(UnitRespawnerForBuildings respawn, ushort buildingId)
    {
        for (var i = 0; i < respawn.transform.childCount; ++i)
        {
            var unit = respawn.transform.GetChild(i);
            var identifier = unit.gameObject.AddComponent<Identifier>();
            var id = (ushort)((buildingId << 7) | unit.parent.GetSiblingIndex() << 4 | unit.GetSiblingIndex());
            identifier.SetIdentity(IdentifierType.Ally, id);
        }
    }

    private static void OnUpgradeChoiceComplete(
        On.BuildSlot.orig_OnUpgradeChoiceComplete original,
        BuildSlot self,
        Choice choice)
    {
        var upgradeSelected = Traverse.Create(self).Field<BuildSlot.Upgrade>("upgradeSelected").Value;
        if (upgradeSelected == null)
        {
            // We happened to get here because we finished our choice after handling a ConfirmBuildPacket
            return;
        }
        
        if (!_disableNetworkHook && choice != null)
        {
            var buildingId = self.GetComponent<Identifier>().Id;
            var upgradeIndex = 0;
            for (; upgradeIndex < self.upgrades.Count; ++upgradeIndex)
            {
                if (self.upgrades[upgradeIndex] == upgradeSelected)
                {
                    break;
                }
            }

            var choiceIndex = 0;
            for (; choiceIndex < upgradeSelected.upgradeBranches.Count; ++choiceIndex)
            {
                if (upgradeSelected.upgradeBranches[choiceIndex].choiceDetails == choice)
                {
                    break;
                }
            }

            if (Plugin.Instance.Network.Server)
            {
                var packet = new ConfirmBuildPacket()
                {
                    BuildingId = buildingId,
                    Level = (byte)upgradeIndex,
                    Choice = (byte)choiceIndex,
                    PlayerID = Plugin.Instance.PlayerManager.LocalId
                };
                
                Plugin.Instance.Network.Send(packet, true);
            }
            else
            {
                var packet = new BuildOrUpgradePacket
                {
                    BuildingId = buildingId,
                    Level = (byte)upgradeIndex,
                    Choice = (byte)choiceIndex
                };

                Plugin.Instance.Network.Send(packet);
            }
        }
        else
        {
            original(self, choice);
        }
    }
    
    public static UpgradeInfo GetUpgradeInfo(ushort id, byte level, byte choice)
    {
        var building = Identifier.GetGameObject(IdentifierType.BuildSlot, id).GetComponent<BuildSlot>();
        var upgrade = building.upgrades[level];
        return new UpgradeInfo()
        {
            Cost = upgrade.cost,
            CurrentLevel = building.Level
        };
    }

    public static void HandleUpgrade(ushort playerId, ushort id, byte level, byte choice)
    {
        var building = Identifier.GetGameObject(IdentifierType.BuildSlot, id).GetComponent<BuildSlot>();
        if (building == null)
        {
            Plugin.Log.LogInfo($"Unable to build {id}:{level}:{choice} for {playerId}");
        }

        if (building.buildingInteractor == null)
        {
            Plugin.Log.LogInfo($"Building interactor for {id}:{level}:{choice} inactive");
            building.buildingInteractor = building.GetComponentInChildren<BuildingInteractor>(true);
        }
        
        var upgrade = building.upgrades[level];
        var branch = upgrade.upgradeBranches[choice];
        var upgradeSelected = Traverse.Create(building).Field<BuildSlot.Upgrade>("upgradeSelected");
        upgradeSelected.Value = upgrade;
        _disableNetworkHook = true;
        building.buildingInteractor.MarkAsHarvested();
        // Activator-gated / startDeactivated slots (walls, wall-towers, tiered buildings) keep their
        // root GameObject inactive until their activator's OnUpgrade event fires — which never happens
        // for a network-driven build on the non-builder peer. Without an active ancestor,
        // OnUpgradeChoiceComplete's buildingParent.SetActive(true) leaves activeInHierarchy false, so the
        // 'Main Mesh' GO stays inactive and BuildingMeshTracker.FreezeMeshWithDelay cannot StartCoroutine
        // to bake the MeshFusionPro combined mesh — the wall/building renders invisible. Mirror vanilla
        // BuildSlot.Activate()'s root-GO activation here.
        if (!building.gameObject.activeSelf)
        {
            building.gameObject.SetActive(true);
        }
        building.OnUpgradeChoiceComplete(branch.choiceDetails);
        _disableNetworkHook = false;
        upgradeSelected.Value = null;

        // DIAGNOSTIC (walls invisible to remote peers): report the post-build state so we can tell whether the
        // slot built logically on this peer (bpActive) but its combined wall mesh (MeshFusionPro via
        // BuildingMeshTracker) failed to re-bake. Remove once the wall sync is fixed.
        var diagParent = Traverse.Create(building).Field<GameObject>("buildingParent").Value;
        var diagTracker = Traverse.Create(building).Field<BuildingMeshTracker>("buildingMeshTracker").Value;
        var diagFuserActive = false;
        if (diagTracker != null)
        {
            var fuser = Traverse.Create(diagTracker).Field<NGS.MeshFusionPro.MeshFusionSource>("meshFuser").Value;
            diagFuserActive = fuser != null && fuser.gameObject.activeInHierarchy;
        }
        Plugin.Log.LogInfo($"[BuildDiag] built id={id} lvl={building.Level} byPlayer={playerId} local={Plugin.Instance.PlayerManager.LocalId} bpActive={(diagParent != null && diagParent.activeInHierarchy)} meshTracker={(diagTracker != null)} fuserActive={diagFuserActive}");

        var focussed = Traverse.Create(building.buildingInteractor).Field<bool>("focussed");
        if (building.buildingInteractor.IsWaitingForChoice)
        {
            Plugin.Log.LogInfo("Cancel choice");
            // We are waiting on choice when the building has already been built, cancel it.
            UIFrameManager.instance.CloseActiveFrame();
            ChoiceManager.instance.CancelChoice();
        }
        else switch (focussed.Value)
        {
            case true when playerId != Plugin.Instance.PlayerManager.LocalId:
            {
                Plugin.Log.LogInfo("Redo our focus");
                //Traverse.Create(building.buildingInteractor).Field<bool>("isWaitingForChoice").Value = true;
                //building.OnUpgradeChoiceComplete(null);
                var player = Plugin.Instance.PlayerManager.LocalPlayer.Object.GetComponent<PlayerInteraction>();
                building.buildingInteractor.Unfocus(player);
                break;
            }
            case true:
            {
                Traverse.Create(building.buildingInteractor).Method("BuildComplete").GetValue();
                break;
            }
        }
    }

    public static void CancelBuild(ushort id)
    {
        var building = Identifier.GetGameObject(IdentifierType.BuildSlot, id).GetComponent<BuildSlot>();
        building.OnUpgradeChoiceComplete(null);
    }
}