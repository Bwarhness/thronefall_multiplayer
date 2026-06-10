using HarmonyLib;
using ThronefallMP.Components;
using ThronefallMP.Network.Packets.Game;
using UnityEngine;

namespace ThronefallMP.Patches;

public class EnemySpawnerPatch
{
	private static int _nextEnemyId;
    public static void Apply()
    {
	    On.EnemySpawner.Start += Start;
	    On.EnemySpawner.Update += Update;
	    On.EnemySpawner.OnStartOfTheDay += OnStartOfTheDay;
    }

    private static void Start(On.EnemySpawner.orig_Start original, EnemySpawner self)
    {
	    // EnemySpawner.OnLoad (Awake-time save-load pass) zeroes goldBalanceAtStart and sets
	    // loadedBalanceFromSave on retry/continue, so both must be read before original.
	    var loadedFromSave = Traverse.Create(self).Field<bool>("loadedBalanceFromSave").Value;
	    var captured = self.goldBalanceAtStart;
	    var restored = PlayerInteractionPatch.ConsumeLoadedBalance();

	    // Zero the field so vanilla's per-player AddCoin loop grants nothing (the shared balance is
	    // assigned once below, not once per registered player). Vanilla still adds its start bonuses
	    // to the field inside original (Loan always, Royal Mint only on fresh runs), so the field
	    // value after original is exactly the bonus gold vanilla intended.
	    self.goldBalanceAtStart = 0;
	    original(self);
	    var vanillaBonuses = self.goldBalanceAtStart;
	    self.goldBalanceAtStart = captured + vanillaBonuses;

	    GlobalData.LocalBalanceDelta = 0;
	    if (!Plugin.Instance.Network.Server)
	    {
		    return;
	    }

	    if (loadedFromSave)
	    {
		    // Retry/continue: vanilla grants no start gold or mint bonus and instead restores the
		    // dawn-save balance (captured by PlayerInteractionPatch from PlayerInteraction.OnLoad).
		    if (restored == null)
		    {
			    Plugin.Log.LogWarning("Level loaded from save but no restored balance was captured.");
		    }

		    Plugin.Log.LogInfo($"Restoring dawn-save balance {restored ?? 0}");
		    GlobalData.Internal.Balance = restored ?? 0;
		    GlobalData.Internal.NetWorth = 0;
	    }
	    else
	    {
		    Plugin.Log.LogInfo($"Give starting balance {captured + vanillaBonuses}");
		    GlobalData.Internal.Balance = captured + vanillaBonuses;
		    GlobalData.Internal.NetWorth = captured + vanillaBonuses;
	    }
    }

    private static void OnStartOfTheDay(On.EnemySpawner.orig_OnStartOfTheDay original, EnemySpawner self)
    {
	    Identifier.Clear(IdentifierType.Enemy);
	    _nextEnemyId = 0;

	    var treasureHunterActive = Traverse.Create(self).Field<bool>("treasureHunterActive");
	    var old = treasureHunterActive.Value;
	    treasureHunterActive.Value = false;
	    original(self);
	    treasureHunterActive.Value = old;
	    // game 2024+ replaced the single PerkManager.treasureHunterGoldAmount with per-wave amounts surfaced
	    // through EnemySpawner.GetTreasureHunterBonus(wave, out amount) (covers the two-before/one-before/final
	    // waves). Route the bonus through the synced GlobalData.Balance on the server, as before.
	    if (old && Plugin.Instance.Network.Server && self.GetTreasureHunterBonus(self.Wavenumber, out var treasureHunterBonus))
	    {
		    GlobalData.Balance += treasureHunterBonus;
	    }
    }

    private static void Update(On.EnemySpawner.orig_Update original, EnemySpawner self)
    {
        var numberOfEnemiesOnTheMap = Traverse.Create(self).Field<int>("numberOfEnemiesOnTheMap");
        if (!self.SpawningInProgress)
        {
            // Vanilla keeps the enemy count fresh while idle so the pacing gate below
            // does not act on a stale value when the next wave starts.
            numberOfEnemiesOnTheMap.Value =
                TagManager.instance.CountAllTaggedObjectsWithTag(TagManager.ETag.EnemyOwned);
            return;
        }

        var lastSpawnPeriodDuration = Traverse.Create(self).Field<float>("lastSpawnPeriodDuration");
        lastSpawnPeriodDuration.Value += Time.deltaTime;
        // Vanilla pacing gate: hold back wave processing while too many enemies are alive.
        if (numberOfEnemiesOnTheMap.Value < Traverse.Create(self).Field<int>("pauseSpawningAtEnemyCount").Value)
        {
	        if (Plugin.Instance.Network.Server)
	        {
		        for (var i = 0; i < self.waves[self.Wavenumber].spawns.Count; i++)
		        {
			        UpdateSpawn(self.waves[self.Wavenumber].spawns[i], self.Wavenumber, i);
		        }
	        }

	        if (self.waves[self.Wavenumber].HasFinished())
	        {
		        if (!self.InfinitelySpawning)
		        {
			        self.StopSpawnAfterWaveAndReset();
		        }
		        else if (TagManager.instance.CountAllTaggedObjectsWithTag(TagManager.ETag.EnemyOwned) <= 0)
		        {
			        foreach (var spawn in self.waves[self.Wavenumber].spawns)
			        {
				        spawn.Reset(false);
			        }
		        }
	        }
        }

        numberOfEnemiesOnTheMap.Value =
            TagManager.instance.CountAllTaggedObjectsWithTag(TagManager.ETag.EnemyOwned);
        if (numberOfEnemiesOnTheMap.Value <= 0)
        {
            self.waves[self.Wavenumber].ReduceMaxDelayTillNextSpawn();
        }
    }

    // Single id source for every synced enemy spawn (regular waves and boss minion waves) so the
    // two pipelines can never hand out colliding Identifier ids. Reset in OnStartOfTheDay together
    // with Identifier.Clear(Enemy).
    internal static ushort AllocateEnemyId()
    {
	    return (ushort)_nextEnemyId++;
    }

    // Shared by the regular pipeline (UpdateSpawn) and the boss minion pipeline (SpawnPatch).
    // game 2024+ added a required NNConstraint param to GetRandomPointOnSpawnLine; pick the constraint with
    // the same 3-way priority as vanilla Spawn.Update (LargeUnit > Flying > ground).
    internal static Vector3 RollSpawnPosition(Spawn self)
    {
		var tags = self.enemyPrefab.GetComponentInChildren<TaggedObject>().Tags;
		var flying = tags.Contains(TagManager.ETag.Flying);
		var large = tags.Contains(TagManager.ETag.LargeUnit);
		var randomPointOnSpawnLine = self.GetRandomPointOnSpawnLine(
			flying,
			large
				? EnemySpawner.instance.groundConstraintLarge
				: (flying ? EnemySpawner.instance.flyingConstraint : EnemySpawner.instance.groundConstraint));

		// Vanilla god of chaos drags every spawn point toward the castle in a 5 step cycle,
		// except for exploding enemies. Spawn.godOfChaos is just a cached GodOfChaosEquipped.
		if (PerkManager.instance.GodOfChaosEquipped && !tags.Contains(TagManager.ETag.Exploding))
		{
			randomPointOnSpawnLine = Vector3.Lerp(
				randomPointOnSpawnLine,
				CastleCenter.CastleCenterPosition + (flying ? Vector3.up * 5f : Vector3.zero),
				self.SpawnedUnits % 5 / 5f);
		}

		return randomPointOnSpawnLine;
    }

    private static void UpdateSpawn(Spawn self, int waveNumber, int spawnIndex)
    {
	    var finished = Traverse.Create(self).Field<bool>("finished");
		if (finished.Value)
		{
			return;
		}

		var waitBeforeNextSpawn = Traverse.Create(self).Field<float>("waitBeforeNextSpawn");
		waitBeforeNextSpawn.Value -= Time.deltaTime;
		if (waitBeforeNextSpawn.Value > 0f)
		{
			return;
		}

		waitBeforeNextSpawn.Value = self.interval;
		var randomPointOnSpawnLine = RollSpawnPosition(self);

		var coins = 0;
		var spawnedUnits = Traverse.Create(self).Field<int>("spawnedUnits");
		var goldCoinsPerEnemy = Traverse.Create(self).Field<int[]>("goldCoinsPerEnemy");
		if (goldCoinsPerEnemy.Value.Length > spawnedUnits.Value)
		{
			coins = goldCoinsPerEnemy.Value[spawnedUnits.Value];
		}

		var packet = new EnemySpawnPacket
		{
			Wave = (byte)waveNumber,
			Spawn = (byte)spawnIndex,
			Id = AllocateEnemyId(),
			Position = randomPointOnSpawnLine,
			Coins = (byte)coins,
			Elite = self.eliteEnemies
		};

		Plugin.Instance.Network.Send(packet, true);
    }

    public static void SpawnEnemy(int waveNumber, int spawnIndex, Vector3 position, ushort id, int coins, bool elite)
    {
	    var wave = EnemySpawner.instance.waves[waveNumber];
	    SpawnEnemy(wave.spawns[spawnIndex], wave.difficultyMulti, position, id, coins, elite);
    }

    // Shared spawn implementation for HandleEnemySpawn (regular waves, difficultyMulti taken from
    // the wave) and HandleBossSpawn (boss minion waves, difficultyMulti as the boss passed it to
    // Spawn.Update: 1 for Ghostqueen, the private wave's difficultyMulti for KeyframedBoss).
    public static void SpawnEnemy(Spawn self, float difficultyMulti, Vector3 position, ushort id, int coins, bool elite)
    {
		GameObject gameObject;
		if (self.spawnLine == self.enemyPrefab.transform)
		{
			gameObject = self.enemyPrefab;
			gameObject.SetActive(true);
			var found = gameObject.TryGetComponent<Identifier>(out var identifier);
			if (!found)
			{
				identifier = gameObject.AddComponent<Identifier>();
				identifier.SetIdentity(IdentifierType.Enemy, id);
			}
		}
		else
		{
			gameObject = Object.Instantiate(self.enemyPrefab, position, Quaternion.identity);
			gameObject.AddComponent<Identifier>()
				.SetIdentity(IdentifierType.Enemy, id);
			var instance = EnemySpawnManager.instance;
			if (instance.weaponOnSpawn)
			{
				instance.weaponOnSpawn.Attack(
					position + Vector3.up * instance.weaponAttackHeight,
					null,
					Vector3.forward,
					gameObject.GetComponent<TaggedObject>()
				);
			}
		}

		var singleHp = gameObject.GetComponentInChildren<Hp>();
		singleHp.coinCount = coins;
		var tags = gameObject.GetComponentInChildren<TaggedObject>().Tags;

		// The game's own post-spawn pass handles all perk buffs (turtle/tiger/falcon taunts,
		// anti-air telescope, war gods, growth/range gods) and the elite hp/dmg/material upgrade.
		Spawn.AdjustEnemyParametersAfterSpawn(gameObject, elite);

		// AdjustEnemyParametersAfterSpawn does not take the per-wave difficulty; vanilla
		// Spawn.Update folds difficultyMulti into hp and Lerp(1, multi, 0.5) into damage.
		singleHp.maxHp *= difficultyMulti;
		singleHp.SetHpToMaxHp();

		var damageMulti = Mathf.Lerp(1f, difficultyMulti, 0.5f);
		// Vanilla god of chaos forces a 3 second initial attack cooldown on non-exploding enemies.
		var godOfChaos = PerkManager.instance.GodOfChaosEquipped && !tags.Contains(TagManager.ETag.Exploding);
		foreach (var attack in gameObject.GetComponentsInChildren<AutoAttack>())
		{
			if (godOfChaos)
			{
				attack.SetCooldownTo(3f);
			}

			attack.DamageMultiplyer *= damageMulti;
		}

		// Vanilla gives every 2nd humanoid a death spawn. Host only: clients replay deaths
		// through HpSync's gated TakeDamage, so assigning this on a client too would Instantiate
		// an identifier-less ghost copy there. The host-side death spawn is still unsynced.
		// TODO(boss-minion-sync follow-up): route death-spawned enemies through a spawn packet
		// with an Identifier.
		if (Plugin.Instance.Network.Server
		    && PerkManager.instance.GodOfAfterlifeEquipped
		    && self.SpawnedUnits % 2 == 0
		    && tags.Contains(TagManager.ETag.Humanoid))
		{
			singleHp.enemyToSpawnOnDeath = PerkManager.instance.godOfAfterlifeEnemy;
		}

		// Advance the same bookkeeping vanilla Spawn.Update keeps so Spawn.Finished and
		// Wave.HasFinished() observe the wave completing on every peer (the boss minion loops in
		// Ghostqueen.SpawnGhosts and KeyframedBoss.Update depend on this).
		var spawnedUnits = Traverse.Create(self).Field<int>("spawnedUnits");
		spawnedUnits.Value++;
		if (spawnedUnits.Value >= self.count)
		{
			Traverse.Create(self).Field<bool>("finished").Value = true;
		}
    }
}
