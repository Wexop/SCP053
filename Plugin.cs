using System;
using System.Collections.Generic;
using BepInEx;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using LethalConfig;
using LethalConfig.ConfigItems;
using LethalConfig.ConfigItems.Options;
using UnityEngine;
using LethalLib.Modules;
using SCP053.Scripts;
using SCP053.Utils;
using Object = UnityEngine.Object;

namespace SCP053
{
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Scp053Plugin : BaseUnityPlugin
    {

        const string GUID = "projectSCP.scp053";
        const string NAME = "scp053";
        const string VERSION = "1.0.0";

        public static Scp053Plugin instance;
        
        public bool isSCP682Installed;
        public static string SCP682pReferenceChain = "ProjectSCP.SCP682";

        public GameObject SCP053ActionsObject;
        public Scp053Actions currentSCP053Actions;
        
        public ConfigEntry<string> spawnMoonRarity;
        public ConfigEntry<int> maxSpawn;
        public ConfigEntry<int> powerLevel;

        void Awake()
        {
            instance = this;
            
            Logger.LogInfo($"Scp053 starting....");

            string assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "scp053");
            AssetBundle bundle = AssetBundle.LoadFromFile(assetDir);
            
            Logger.LogInfo($"Scp053 bundle found !");
            
            NetcodePatcher();
            LoadConfigs();
            RegisterMonster(bundle);

            Check682();
            
            Logger.LogInfo($"Scp053 is ready!");
        }

        public void Check682()
        {
            if (Chainloader.PluginInfos.ContainsKey(SCP682pReferenceChain))
            {
                Debug.Log("SCP682 found !");
                isSCP682Installed = true;
            }
        }

        string RarityString(int rarity)
        {
            return
                $"Modded:{rarity},ExperimentationLevel:{rarity},AssuranceLevel:{rarity},VowLevel:{rarity},OffenseLevel:{rarity},MarchLevel:{rarity},RendLevel:{rarity},DineLevel:{rarity},TitanLevel:{rarity},Adamance:{rarity},Embrion:{rarity},Artifice:{rarity}";

        }

        void LoadConfigs()
        {
            
            //GENERAL
            spawnMoonRarity = Config.Bind("General", "SpawnRarity",
                "Modded:50,ExperimentationLevel:40,AssuranceLevel:40,VowLevel:40,OffenseLevel:45,MarchLevel:45,RendLevel:50,DineLevel:50,TitanLevel:60,Adamance:45,Embrion:50,Artifice:60",
                "Chance for SCP 053 to spawn for any moon, example => assurance:100,offense:50 . You need to restart the game.");
            CreateStringConfig(spawnMoonRarity, true);

            maxSpawn = Config.Bind("General", "maxSpawn", 1,
                "Max SCP053 spawn in one day");
            CreateIntConfig(maxSpawn, 1, 30);

            powerLevel = Config.Bind("General", "powerLevel", 1,
                "SCP053 power level");
            CreateIntConfig(maxSpawn, 1, 10);
 
        }
        
        void RegisterMonster(AssetBundle bundle)
        {
            //creature
            EnemyType creature = bundle.LoadAsset<EnemyType>("Assets/LethalCompany/Mods/SCP053/SCP053.asset");
            TerminalNode terminalNode =
                bundle.LoadAsset<TerminalNode>("Assets/LethalCompany/Mods/SCP053/SCP053TerminalNode.asset");
            TerminalKeyword terminalKeyword =
                bundle.LoadAsset<TerminalKeyword>("Assets/LethalCompany/Mods/SCP053/SCP053TerminalKeyword.asset");

            creature.MaxCount = maxSpawn.Value;
            creature.PowerLevel = powerLevel.Value;

            Logger.LogInfo($"{creature.name} FOUND");
            Logger.LogInfo($"{creature.enemyPrefab} prefab");
            NetworkPrefabs.RegisterNetworkPrefab(creature.enemyPrefab);
            Utilities.FixMixerGroups(creature.enemyPrefab);


            RegisterUtil.RegisterEnemyWithConfig(spawnMoonRarity.Value, creature, terminalNode, terminalKeyword,
                creature.PowerLevel, creature.MaxCount);
            
            //actions
            
            SCP053ActionsObject = bundle.LoadAsset<GameObject>("Assets/LethalCompany/Mods/SCP053/SCP053Actions.prefab");
            Logger.LogInfo($"{SCP053ActionsObject.name} FOUND");

        }

        public void SpawnActionsObject()
        {
            if(currentSCP053Actions != null ) return;
            GameObject o = GameNetworkManager.Instantiate(SCP053ActionsObject);
            currentSCP053Actions = o.GetComponent<Scp053Actions>();
            Debug.Log($"{currentSCP053Actions.name} SPAWNED");

        }
        
        /// <summary>
        ///     Slightly modified version of: https://github.com/EvaisaDev/UnityNetcodePatcher?tab=readme-ov-file#preparing-mods-for-patching
        /// </summary>
        private static void NetcodePatcher()
        {
            Type[] types;
            try
            {
                types = Assembly.GetExecutingAssembly().GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                // This goofy try catch is needed here to be able to use soft dependencies in the future, though none are present at the moment.
                types = e.Types.Where(type => type != null).ToArray();
            }

            foreach (Type type in types)
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                {
                    try
                    {
                        if (method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false).Length >
                            0)
                        {
                            // Do weird magic...
                            _ = method.Invoke(null, null);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
        }
        private void CreateFloatConfig(ConfigEntry<float> configEntry, float min = 0f, float max = 100f)
        {
            var exampleSlider = new FloatSliderConfigItem(configEntry, new FloatSliderOptions
            {
                Min = min,
                Max = max,
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        private void CreateIntConfig(ConfigEntry<int> configEntry, int min = 0, int max = 100)
        {
            var exampleSlider = new IntSliderConfigItem(configEntry, new IntSliderOptions()
            {
                Min = min,
                Max = max,
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        private void CreateStringConfig(ConfigEntry<string> configEntry, bool requireRestart = false)
        {
            var exampleSlider = new TextInputFieldConfigItem(configEntry, new TextInputFieldOptions()
            {
                RequiresRestart = requireRestart
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
        public bool StringContain(string name, string verifiedName)
        {
            var name1 = name.ToLower();
            while (name1.Contains(" ")) name1 = name1.Replace(" ", "");

            var name2 = verifiedName.ToLower();
            while (name2.Contains(" ")) name2 = name2.Replace(" ", "");

            return name1.Contains(name2);
        }
        
        private void CreateBoolConfig(ConfigEntry<bool> configEntry)
        {
            var exampleSlider = new BoolCheckBoxConfigItem(configEntry, new BoolCheckBoxOptions
            {
                RequiresRestart = false
            });
            LethalConfigManager.AddConfigItem(exampleSlider);
        }
        
    }
}