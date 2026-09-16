using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace LuckyTrinket
{
    /// <summary>
    /// 幸运护符：佩戴后，附近（默认 100 米）EpicLoot 魔法掉落的质量下限
    /// 提升到护符自身稀有度——只取消低于门槛的掉落，不提升物品品质。
    /// 效果只由装备者的玩家 ZDO 广播，其他玩家不装饰品时不受影响。
    /// 强运护符：幸运护符的全部效果 + 穿戴装备注入——附近掉落池会按比例追加
    /// 佩戴者身上可附魔的穿戴装备（不屏蔽原池条目；注入装备按原池品质分布
    /// roll，不钳制、不提升）。
    /// </summary>
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("randyknapp.mods.epicloot")]
    [BepInDependency("shudnal.ExtraSlots", BepInDependency.DependencyFlags.SoftDependency)]
    public class LuckyTrinketPlugin : BaseUnityPlugin
    {
        public const string Guid = "trigger.epicloot.luckytrinket";
        public const string PluginName = "EpicLoot Luck Trinket";
        public const string PluginVersion = "0.3.0";

        /// <summary>幸运护符物品 prefab 名（与注册名一致）。</summary>
        internal const string ItemPrefab = "LuckyTrinket";

        /// <summary>强运护符物品 prefab 名（幸运护符 + 10 铁锭合成）。</summary>
        internal const string GreatItemPrefab = "GreatLuckTrinket";

        internal static LuckyTrinketPlugin Instance;
        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> Enabled;
        internal static ConfigEntry<float> Range;
        internal static ConfigEntry<int> MaterialTolerance;
        internal static ConfigEntry<bool> DebugLog;
        internal static ConfigEntry<bool> WornPool;
        internal static ConfigEntry<float> WornPoolShare;
        internal static ConfigEntry<float> WornPoolWeight;
        internal static ConfigEntry<float> WornPoolHeldWeaponWeight;
        internal static ConfigEntry<float> WornPoolUtilityWeight;

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
            MaterialTolerance = Config.Bind("General", "MaterialTolerance", 1,
                new ConfigDescription(
                    "带品质材料（碎片石/魔法材料/符文）允许低于门槛多少级保留：" +
                    "0 = 只保留达到门槛的材料，1 = 低一级的材料也保留，5 = 所有材料都保留。",
                    new AcceptableValueRange<int>(0, 5)));
            DebugLog = Config.Bind("General", "DebugLog", false,
                "输出详细调试日志（排查用，默认关闭）。");
            WornPool = Config.Bind("General", "WornPool", true,
                "强运护符的穿戴装备注入：把你身上穿戴的装备追加到附近掉落池，" +
                "原掉落池条目不受影响；没有佩戴强运护符时无效果。");
            WornPoolShare = Config.Bind("General", "WornPoolShare", 0.2f,
                new ConfigDescription(
                    "注入装备占掉落池总权重的比例：0.2 = 我们的装备约 20%、原版池约 80%。0 = 关闭注入。",
                    new AcceptableValueRange<float>(0f, 0.95f)));
            WornPoolWeight = Config.Bind("General", "WornPoolWeight", 1f,
                new ConfigDescription(
                    "注入组内每件穿戴装备的相对权重（组内比例；实际占池比例由 WornPoolShare 控制）。",
                    new AcceptableValueRange<float>(0.1f, 10f)));
            WornPoolHeldWeaponWeight = Config.Bind("General", "WornPoolHeldWeaponWeight", 2f,
                new ConfigDescription(
                    "手持武器在注入组内的相对权重（组内比例）。",
                    new AcceptableValueRange<float>(0.1f, 10f)));
            WornPoolUtilityWeight = Config.Bind("General", "WornPoolUtilityWeight", 0.4f,
                new ConfigDescription(
                    "饰品（Utility 类型）在注入组内的相对权重；仅在安装 ExtraSlots 时生效。",
                    new AcceptableValueRange<float>(0.1f, 10f)));

            LtrLog.Info($"{PluginName} v{PluginVersion} 初始化中...");

            _harmony = new Harmony(Guid);
            Patches.Install(_harmony);

            LuckyTrinketLocalization.Register();
            LuckyTrinketItems.Register();
            RarityFloorSystem.Register();

            LtrLog.Info($"{PluginName} v{PluginVersion} 初始化完成。" +
                        $"Enabled={Enabled.Value}, Range={Range.Value}, " +
                        $"WornPool={WornPool.Value}, WornPoolWeight={WornPoolWeight.Value}");
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
