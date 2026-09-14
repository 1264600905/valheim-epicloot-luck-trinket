using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace LuckyTrinket
{
    /// <summary>
    /// 幸运护符：佩戴后，附近（默认 100 米）EpicLoot 魔法掉落的质量下限
    /// 提升到护符自身稀有度。效果只由装备者的玩家 ZDO 广播，其他玩家
    /// 不装饰品时不受影响。另提供固有幸运加成（默认 +25），不占用词条、
    /// 不受附魔操作影响。
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("randyknapp.mods.epicloot")]
    [BepInDependency("shudnal.ExtraSlots", BepInDependency.DependencyFlags.SoftDependency)]
    public class LuckyTrinketPlugin : BaseUnityPlugin
    {
        public const string Guid = "trigger.epicloot.luckytrinket";
        public const string PluginName = "EpicLoot Luck Trinket";
        public const string PluginVersion = "0.2.2";

        /// <summary>物品 prefab 名（与注册名一致）。</summary>
        internal const string ItemPrefab = "LuckyTrinket";

        internal static LuckyTrinketPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<float> LuckBonus;
        internal static ConfigEntry<int> MaterialTolerance;
        internal static ConfigEntry<bool> DebugLog;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "启用幸运护符的掉落品质下限效果。");
            Range = Config.Bind("General", "Range", 100f,
                new ConfigDescription(
                    "生效半径（米）：掉落点附近该范围内的装备者护符参与门槛计算，取最高品质。",
                    new AcceptableValueRange<float>(1f, 200f)));
            LuckBonus = Config.Bind("General", "LuckBonus", 25f,
                new ConfigDescription(
                    "护符提供的固有幸运加成（EpicLoot 幸运点数，1 点 = 1%）。0 = 关闭。",
                    new AcceptableValueRange<float>(0f, 200f)));
            MaterialTolerance = Config.Bind("General", "MaterialTolerance", 1,
                new ConfigDescription(
                    "带品质材料（碎片石/魔法材料/符文）允许低于门槛多少级保留：" +
                    "0 = 只保留达到门槛的材料，1 = 低一级的材料也保留，5 = 所有材料都保留。",
                    new AcceptableValueRange<int>(0, 5)));
            DebugLog = Config.Bind("General", "DebugLog", false,
                "输出详细调试日志（排查用，默认关闭）。");

            LtrLog.Info($"{PluginName} v{PluginVersion} 初始化中...");

            _harmony = new Harmony(Guid);
            Patches.Install(_harmony);

            LuckyTrinketLocalization.Register();
            LuckyTrinketItems.Register();
            RarityFloorSystem.Register();

            LtrLog.Info($"{PluginName} v{PluginVersion} 初始化完成。" +
                        $"Enabled={Enabled.Value}, Range={Range.Value}, LuckBonus={LuckBonus.Value}");
        }

        private void Update()
        {
            RarityFloorSystem.Update();
        }

        private void OnDestroy()
        {
            RarityFloorSystem.Unregister();
            if (_harmony != null)
            {
                _harmony.UnpatchSelf();
            }
        }
    }
}
