using System.Collections.Generic;
using HarmonyLib;
using ThronefallMP.Utils;

namespace ThronefallMP;

public enum Equipment
{
    Invalid,
    PerkPoint,
        
    LongBow,
    LightSpear,
    HeavySword,
    LightningWand,
        
    RoyalMint,
    ArcaneTowers,
    HeavyArmor,
    CastleFortifications,
    RingOfResurrection,
    PumpkinFields,
    ArchitectsCouncil,
    GodsLotion,
    CastleBlueprints,
    GladiatorSchool,
    WarHorse,
    GlassCannon,
    BigHarbours,
    EliteWarriors,
    ArcherySkills,
    FasterResearch,
    TowerSupport,
    FortifiedHouses,
    CommanderMode,
    HealingSpirits,
    IceMagic,
    MeleeResistance,
    PowerTower,
    RangedResistance,
    TreasureHunter,
    IndestructibleMines,
    WarriorMode,
    MeleeDamage,
    FirewingHero,
    AntiAirTelescope,
    RangedDamage,
    StrongerHeroes,
    LizardRiderHero,
        
    AssassinsTraining,
    MagicArmor,
    GodlyCurse,
    CastleUp,
        
    MillScarecrow,
    MillWindSpirits,
        
    TowerHotOil,
        
    MeleeFlails,
    RangedHunters,
    MeleeBerserkers,
    RangedFireArchers,
        
    WarGods,
    Turtle,
    Tiger,
    Rat,
    Falcon,
    Destruction,
    Wasp,
    Death,
    Phoenix,
    
    NoWallsPact,
    NoTowersPact,
    NoUnitsPact,

    // 2024+ game additions (appended only — enum order is the wire format).
    // Weapons
    BattleAxe,
    BloodWand,
    CursedBlowpipe,
    FalchionAndTrap,
    // Perks
    AgileHorse,
    AncientShrines,
    Cobbler,
    DoubleHealing,
    EliteTowers,
    EmergencyRepairs,
    ExperienceGain,
    ExplosiveRevival,
    ExplosiveWalls,
    HealingGold,
    HealthPotions,
    Interest,
    LastStand,
    LightMaterials,
    Loan,
    Outpost,
    PotionVials,
    PristineArchers,
    PristineWarriors,
    RelentlessResearch,
    ResilientResidences,
    RiskTaker,
    RoyalProtection,
    SpellScroll,
    TimberScaffolding,
    // Tower upgrades
    TowerArmor,
    TowerBunker,
    // Mutators
    EliteGod,
    GrowthGod,
    RangeGod,
    ChaosGod,
    AfterlifeGod,
    ChoiceGod,
    PacifistPact
}

public static class Equip
{
    public static readonly HashSet<Equipment> Weapons = new()
    {
        Equipment.LongBow,
        Equipment.LightSpear,
        Equipment.HeavySword,
        Equipment.LightningWand,
        Equipment.BattleAxe,
        Equipment.BloodWand,
        Equipment.CursedBlowpipe,
        Equipment.FalchionAndTrap,
    };
    
    private static readonly Dictionary<string, Equipment> NameToEquip = new()
    {
        { "", Equipment.Invalid },
        { "Perk Point", Equipment.PerkPoint },
        
        // Weapons
        { "Long Bow", Equipment.LongBow },
        { "Light Spear", Equipment.LightSpear },
        { "Heavy Sword", Equipment.HeavySword },
        { "Lightning Wand", Equipment.LightningWand },
        
        // Perks
        { "Universal Income", Equipment.RoyalMint },
        { "Arcane Towers", Equipment.ArcaneTowers },
        { "Heavy Armor", Equipment.HeavyArmor },
        { "Castle Fortifications", Equipment.CastleFortifications },
        { "Ring of Resurection", Equipment.RingOfResurrection },
        { "Pumpkin Fields", Equipment.PumpkinFields },
        { "Architect's Council", Equipment.ArchitectsCouncil },
        { "Gods Lotion", Equipment.GodsLotion },
        { "Castle Blueprints", Equipment.CastleBlueprints },
        { "Gladiator School", Equipment.GladiatorSchool },
        { "War Horse", Equipment.WarHorse },
        { "Glass Canon", Equipment.GlassCannon },
        { "Big Harbours", Equipment.BigHarbours },
        { "Ellite Warriors", Equipment.EliteWarriors },
        { "Archery Skills", Equipment.ArcherySkills },
        { "Faster Research", Equipment.FasterResearch },
        { "TowerSupport", Equipment.TowerSupport },
        { "Fortified Houses", Equipment.FortifiedHouses },
        { "Commander Mode", Equipment.CommanderMode },
        { "Healing Spirits", Equipment.HealingSpirits },
        { "Ice Magic", Equipment.IceMagic },
        { "Melee Resistence", Equipment.MeleeResistance },
        { "Power Tower", Equipment.PowerTower },
        { "Ranged Resistence", Equipment.RangedResistance },
        { "Treasure Hunter", Equipment.TreasureHunter },
        { "Indestructible Mines", Equipment.IndestructibleMines },
        { "Warrior Mode", Equipment.WarriorMode },
        { "Melee Damage", Equipment.MeleeDamage },
        { "Firewing Hero", Equipment.FirewingHero },
        { "Anti Air Telescope", Equipment.AntiAirTelescope },
        { "Ranged Damage", Equipment.RangedDamage },
        { "Stronger Heros", Equipment.StrongerHeroes },
        { "LizzardRider Hero", Equipment.LizardRiderHero },
        
        // Castle Upgrades
        { "CCAssassinsTraining", Equipment.AssassinsTraining },
        { "CCMagicArmor", Equipment.MagicArmor },
        { "CCGodlyCurse", Equipment.GodlyCurse },
        { "CCCastleUp", Equipment.CastleUp },
        
        // Mill Upgrades
        { "MillScarecrow", Equipment.MillScarecrow },
        { "MillWindSpirits", Equipment.MillWindSpirits },
        
        // Tower Upgrades
        { "TowerHotOil", Equipment.TowerHotOil },
        
        // Units
        { "MeleeFlails", Equipment.MeleeFlails },
        { "RangedHunters", Equipment.RangedHunters },
        { "MeleeBerserks", Equipment.MeleeBerserkers },
        { "RangedFireArchers", Equipment.RangedFireArchers },
        
        // Mutators
        { "Pray to The God of Strength", Equipment.WarGods },
        { "Taunt The Turtle God", Equipment.Turtle },
        { "Taunt The Tiger God", Equipment.Tiger },
        { "Taunt The Rat God", Equipment.Rat },
        { "Taunt The Falcon God", Equipment.Falcon },
        { "Taunt God of Destruction", Equipment.Destruction },
        { "Taunt The Cheese God", Equipment.Wasp },
        { "Taunt The Disease God", Equipment.Death },
        // The game asset is "Taunt the Phoenix God" (lowercase "the") — the original "Taunt The
        // Phoenix God" key never matched anything.
        { "Taunt the Phoenix God", Equipment.Phoenix },
        { "No Walls Pact", Equipment.NoWallsPact },
        { "No Towers Pact", Equipment.NoTowersPact },
        { "No Units Pact", Equipment.NoUnitsPact },

        // 2024+ game additions (asset names taken from a live allEquippables dump).
        // Weapons
        { "Battle Axe", Equipment.BattleAxe },
        { "Blood Wand", Equipment.BloodWand },
        { "Cursed Blowpipe", Equipment.CursedBlowpipe },
        { "Falchion and Trap", Equipment.FalchionAndTrap },
        // Perks
        { "Agile Horse", Equipment.AgileHorse },
        { "Ancient Shrines", Equipment.AncientShrines },
        { "Cobbler", Equipment.Cobbler },
        { "Double Healing", Equipment.DoubleHealing },
        { "Elite Towers", Equipment.EliteTowers },
        { "Emergency Repairs", Equipment.EmergencyRepairs },
        { "Experience Gain", Equipment.ExperienceGain },
        { "Explosive Revival", Equipment.ExplosiveRevival },
        { "Explosive Walls", Equipment.ExplosiveWalls },
        { "Healing Gold", Equipment.HealingGold },
        { "Health Potions", Equipment.HealthPotions },
        { "Interest", Equipment.Interest },
        { "Last Stand", Equipment.LastStand },
        { "Light Materials", Equipment.LightMaterials },
        { "Loan", Equipment.Loan },
        { "Outpost", Equipment.Outpost },
        { "Potion Vials", Equipment.PotionVials },
        { "Pristine Archers", Equipment.PristineArchers },
        { "Pristine Warriors", Equipment.PristineWarriors },
        { "Relentless Research", Equipment.RelentlessResearch },
        { "Resilient Residences", Equipment.ResilientResidences },
        { "Risk Taker", Equipment.RiskTaker },
        { "Royal Protection", Equipment.RoyalProtection },
        { "Spell Scroll", Equipment.SpellScroll },
        { "Timber Scaffolding", Equipment.TimberScaffolding },
        // Tower upgrades
        { "TowerArmor", Equipment.TowerArmor },
        { "TowerBunker", Equipment.TowerBunker },
        // Mutators
        { "Challenge the Elite God", Equipment.EliteGod },
        { "Challenge the Growth God", Equipment.GrowthGod },
        { "Challenge the Range God", Equipment.RangeGod },
        { "Challenge the God of Chaos", Equipment.ChaosGod },
        { "Challenge the God of Afterlife", Equipment.AfterlifeGod },
        { "Challenge the God of Choice", Equipment.ChoiceGod },
        { "Pacifist Pact", Equipment.PacifistPact },
    };
    
    private static readonly Dictionary<Equipment, Equippable> EquipmentToEquippable = new();
    private static readonly Dictionary<Equippable, Equipment> EquippableToEquipment = new();
    private static bool _initialized;

    private static void InitializeDictionaries()
    {
        _initialized = true;
        var metaLevels = Traverse.Create(PerkManager.instance).Field<List<MetaLevel>>("metaLevels");
        Plugin.Log.LogInfoFiltered("Equipment", "Initializing converter dictionary");
        Plugin.Log.LogInfoFiltered("Equipment", "Meta levels");
        foreach (var meta in metaLevels.Value)
        {
            var equipment = Convert(meta.reward.name);
            Plugin.Log.LogInfoFiltered("Equipment", $"- {equipment} = {meta.reward.name}");
            if (equipment == Equipment.Invalid)
            {
                // An unmapped equippable must NOT occupy the Invalid slot: every unknown would
                // overwrite it, and any Invalid that reaches EquipEquipment would silently equip
                // whichever unknown perk registered last (e.g. picking Resilient Residence handed
                // out Last Stand). Unmapped perks stay unsyncable until added to NameToEquip.
                Plugin.Log.LogWarning($"Equippable '{meta.reward.name}' is not in the equipment table");
                continue;
            }

            EquipmentToEquippable[equipment] = meta.reward;
            EquippableToEquipment[meta.reward] = equipment;
        }
        
        Plugin.Log.LogInfoFiltered("Equipment", "Currently Unlocked");
        // game 2024+ renamed PerkManager.UnlockedEquippables -> allEquippables (the full, stable equippable
        // list). Using the complete list keeps the equipment<->id mapping identical across clients.
        foreach (var unlocked in PerkManager.instance.allEquippables)
        {
            var equipment = Convert(unlocked.name);
            Plugin.Log.LogInfoFiltered("Equipment", $"- {equipment} = {unlocked.name}");
            if (equipment == Equipment.Invalid)
            {
                Plugin.Log.LogWarning($"Equippable '{unlocked.name}' is not in the equipment table");
                continue;
            }

            EquipmentToEquippable[equipment] = unlocked;
            EquippableToEquipment[unlocked] = equipment;
        }
    }

    public static void ClearEquipments()
    {
        Plugin.Log.LogInfoFiltered("Equipment", "Clearing equipment");
        PerkManager.instance.CurrentlyEquipped.Clear();
    }

    public static void EquipEquipment(Equipment equipment)
    {
        if (!_initialized)
        {
            InitializeDictionaries();
        }

        if (!EquipmentToEquippable.TryGetValue(equipment, out var equippable))
        {
            // Invalid (an unmapped perk) or a value from a newer peer — equipping via the raw
            // indexer would either throw or hand out an arbitrary perk.
            Plugin.Log.LogWarning($"Cannot equip unknown equipment '{equipment}', skipping");
            return;
        }

        Plugin.Log.LogInfoFiltered("Equipment", $"Equipping {equipment} -> {equippable.displayName}");
        PerkManager.instance.CurrentlyEquipped.Add(equippable);
    }
    
    public static Equipment Convert(Equippable equip)
    {
        if (!_initialized)
        {
            InitializeDictionaries();
        }

        return EquippableToEquipment.GetValueSafe(equip);
    }
    
    public static Equippable Convert(Equipment equip)
    {
        if (!_initialized)
        {
            InitializeDictionaries();
        }

        return EquipmentToEquippable.GetValueSafe(equip);
    }
    
    public static Equipment Convert(string name)
    {
        return NameToEquip.GetValueSafe(name);
    }
}