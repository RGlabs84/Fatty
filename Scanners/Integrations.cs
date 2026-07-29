using System.Collections.Generic;
using BepInEx.Bootstrap;
using Fatty.Configuration;

namespace Fatty.Scanners
{
    public static class Integrations
    {
        // The real BepInPlugin GUID (confirmed by grepping ValheimCuisine.dll's strings table - the
        // old "com.rocker.valheimcuisine" never matched anything, so this whole block was dead code
        // and every VC_ item fell through to the word-heuristic in FoodScanner alone).
        public static bool IsValheimCuisineInstalled => Chainloader.PluginInfos.ContainsKey("XutzBR.ValheimCuisine");

        public static void MapHardcodedIntegrations(Dictionary<string, List<string>> categories)
        {
            if (IsValheimCuisineInstalled)
            {
                FattyPlugin.Instance.Log.LogInfo("Valheim Cuisine detected! Injecting hardcoded categories...");

                // Most VC dish names are Norse/thematic titles ("Fensalir Skause", "Bonemaw Chowder")
                // with no recognizable food keyword in them at all, so FoodScanner's name-heuristic
                // silently defaults them all to Vegetable - not wrong exactly, just uninformed. These
                // overrides come from cross-referencing every real scanned VC_ prefab (Fatty_FoodData.json)
                // against VC's own published recipe doc (dish name -> actual ingredients), not guessing -
                // e.g. "Fensalir Skause" contains Deer Meat -> Meat, "Pike Fillets" contains Pike -> Fish.
                // Matters for gameplay: the Balanced Diet / Fisherman's Friend / Lumberjack's Feast
                // synergies require specific category combinations to trigger.

                // Meat
                CategoryManager.AddHardcodedCategory("Meat","VC_NeckSoup");
                CategoryManager.AddHardcodedCategory("Meat","VC_ForestSkewer");
                CategoryManager.AddHardcodedCategory("Meat","VC_TrollStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_BoarStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_NeckStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_SmokedBearStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_FensalirSkause");
                CategoryManager.AddHardcodedCategory("Meat","VC_BloodyBroth");
                CategoryManager.AddHardcodedCategory("Meat","VC_TrollJerky");
                CategoryManager.AddHardcodedCategory("Meat","VC_ColdCuredNeck");
                CategoryManager.AddHardcodedCategory("Meat","VC_YdalirSkause");
                CategoryManager.AddHardcodedCategory("Meat","VC_UlvTailStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_HatchlingSoup");
                CategoryManager.AddHardcodedCategory("Meat","VC_LoxJerky");
                CategoryManager.AddHardcodedCategory("Meat","VC_NeckBarleyStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_AlfablotStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_JarnbjornStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_HareJerky");
                CategoryManager.AddHardcodedCategory("Meat","VC_TrollAspic");
                CategoryManager.AddHardcodedCategory("Meat","VC_ChickenStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_HareStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_DvergrMageBroth");
                CategoryManager.AddHardcodedCategory("Meat","VC_MorgenJerky");
                CategoryManager.AddHardcodedCategory("Meat","VC_Biksemad");
                CategoryManager.AddHardcodedCategory("Meat","VC_BonemawStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_VarangianStew");
                CategoryManager.AddHardcodedCategory("Meat","VC_BonemawChowder");
                CategoryManager.AddHardcodedCategory("Meat","VC_SerpentChowder");
                CategoryManager.AddHardcodedCategory("Meat","VC_LyngbakrChowder");
                CategoryManager.AddHardcodedCategory("Meat","VC_RegalPorridge");
                CategoryManager.AddHardcodedCategory("Meat","VC_FolkvangrNattmal");
                CategoryManager.AddHardcodedCategory("Meat","VC_WolfWraps");
                CategoryManager.AddHardcodedCategory("Meat","VC_DvergrDagmal");
                CategoryManager.AddHardcodedCategory("Meat","VC_Bacon");
                CategoryManager.AddHardcodedCategory("Meat","VC_SmokedBoarHam");
                CategoryManager.AddHardcodedCategory("Meat","VC_SmokedDrakeHeart");
                CategoryManager.AddHardcodedCategory("Meat","VC_BoarHam");
                CategoryManager.AddHardcodedCategory("Meat","VC_BonemawHakarl");
                CategoryManager.AddHardcodedCategory("Meat","VC_Hakarl");
                CategoryManager.AddHardcodedCategory("Meat","VC_UncuredWolfSalami");
                CategoryManager.AddHardcodedCategory("Meat","VC_PickledEntrails");
                CategoryManager.AddHardcodedCategory("Meat","VC_UnfermentedPickledEntrails");

                // Fish
                CategoryManager.AddHardcodedCategory("Fish","VC_PikeFillets");
                CategoryManager.AddHardcodedCategory("Fish","VC_PerchSteak"); // "steak" would otherwise win it as Meat - it's Perch, a fish
                CategoryManager.AddHardcodedCategory("Fish","VC_BloodmoonStew");
                CategoryManager.AddHardcodedCategory("Fish","VC_GrouperPottage");
                CategoryManager.AddHardcodedCategory("Fish","VC_CrispyPuffers");
                CategoryManager.AddHardcodedCategory("Fish","VC_DeepNorthRagout");
                CategoryManager.AddHardcodedCategory("Fish","VC_MunarvagrSkause");
                CategoryManager.AddHardcodedCategory("Fish","VC_JomsvikingStew");
                CategoryManager.AddHardcodedCategory("Fish","VC_Gravlaks");
                CategoryManager.AddHardcodedCategory("Fish","VC_HighlanderDagmal");
                CategoryManager.AddHardcodedCategory("Fish","VC_Stockfish");
                CategoryManager.AddHardcodedCategory("Fish","VC_PickledHerring");
                CategoryManager.AddHardcodedCategory("Fish","VC_UnfermentedPickledHerring");
                CategoryManager.AddHardcodedCategory("Fish","VC_UnfermentedRakfisk");
                CategoryManager.AddHardcodedCategory("Fish","VC_UnfermentedLutefisk");

                // Sweet
                CategoryManager.AddHardcodedCategory("Sweet","VC_EctoplasmSoup");
                CategoryManager.AddHardcodedCategory("Sweet","VC_Lefse");
                CategoryManager.AddHardcodedCategory("Sweet","VC_Multekrem");
                CategoryManager.AddHardcodedCategory("Sweet","VC_CreamBastarde");
                CategoryManager.AddHardcodedCategory("Sweet","VC_Blodplattar");
                CategoryManager.AddHardcodedCategory("Sweet","VC_FruitBowl");
                CategoryManager.AddHardcodedCategory("Sweet","VC_HerbalRemedy");

                // Drinks: these take the dedicated drink slot (dishes whose standout ingredient is a
                // mead/ale/perry, e.g. stews and "Dagmál" platters flavored with one). This only sets
                // the SLOT - the diet category is still derived from the item's own stats and name, so
                // VC_ElivagarTea lands as an Eitr drink and VC_ShroomBeer as a Vegetable one without
                // either being spelled out here.
                CategoryManager.MarkHardcodedDrink("VC_PerryBroth");
                CategoryManager.MarkHardcodedDrink("VC_GlowingStew");
                CategoryManager.MarkHardcodedDrink("VC_PerryPorridge");
                CategoryManager.MarkHardcodedDrink("VC_MistyFondue");
                CategoryManager.MarkHardcodedDrink("VC_Bragafull");
                CategoryManager.MarkHardcodedDrink("VC_PeasantDagmal");
                CategoryManager.MarkHardcodedDrink("VC_VarangianDagmal");
                CategoryManager.MarkHardcodedDrink("VC_SpicedPerry");
                CategoryManager.MarkHardcodedDrink("VC_ShroomBeer");
                CategoryManager.MarkHardcodedDrink("VC_UnfermentedSpicedPerry"); // matches its fermented counterpart, VC_SpicedPerry
                CategoryManager.MarkHardcodedDrink("VC_Blaand");
                CategoryManager.MarkHardcodedDrink("VC_FulingBeer");
                CategoryManager.MarkHardcodedDrink("VC_FulingBlaand");
                CategoryManager.MarkHardcodedDrink("VC_Booze");
                CategoryManager.MarkHardcodedDrink("VC_Tunnelrumbler");
                CategoryManager.MarkHardcodedDrink("VC_Nogginfog");
            }
        }
    }
}
