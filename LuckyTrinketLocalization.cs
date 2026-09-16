using System;
using Jotunn.Managers;
using UnityEngine;

namespace LuckyTrinket
{
    /// <summary>
    /// 本地化：English / Chinese（简体）/ Chinese_Trad（繁体）。
    /// token 以 $ 前缀，注册到 Jotunn 的 CustomLocalization，游戏按当前语言自动显示。
    /// </summary>
    internal static class LuckyTrinketLocalization
    {
        internal const string ItemName = "$item_luckytrinket";
        internal const string ItemDesc = "$item_luckytrinket_desc";

        internal const string GreatItemName = "$item_greatlucktrinket";
        internal const string GreatItemDesc = "$item_greatlucktrinket_desc";
        internal const string WornPoolLine = "$item_greatlucktrinket_wornpool";

        private static readonly ValueTuple<string, string, string, string>[] Translations =
        {
            // token, English, Chinese, Chinese_Trad
            (ItemName, "Lucky Trinket", "幸运护符", "幸運護符"),
            (ItemDesc,
                "A charm touched by fortune. While equipped: magic gear dropped near you never " +
                "rolls below this charm's own rarity - it does not upgrade drop quality, it " +
                "removes low-quality drops instead (equipment below the floor is discarded; " +
                "materials one step below are kept). Starts unenchanted - enchant it at the " +
                "enchanting table to choose its rarity, and re-enchant it later to upgrade.",
                "一枚被幸运眷顾的护符。佩戴时，附近掉落的魔法装备品质不会低于这枚护符——" +
                "不会提升掉落物品品质，而是取消低品质物品掉落" +
                "（低于门槛的装备不再掉落；材料允许低一级保留）。" +
                "护符初始未附魔——在附魔台附魔选择品质，之后可重新附魔升级。",
                "一枚被幸運眷顧的護符。佩戴時，附近掉落的魔法裝備品質不會低於這枚護符——" +
                "不會提升掉落物品品質，而是取消低品質物品掉落" +
                "（低於門檻的裝備不再掉落；材料允許低一級保留）。" +
                "護符初始未附魔——在附魔台附魔選擇品質，之後可重新附魔升級。"),
            (GreatItemName, "Great Fortune Trinket", "强运护符", "強運護符"),
            (GreatItemDesc,
                "A charm overflowing with fortune. While equipped: magic gear dropped near you " +
                "never rolls below this charm's own rarity (it does not upgrade drop quality, it " +
                "removes low-quality drops instead), and your worn equipment is added to nearby " +
                "drop pools, so you can find your own gear as loot (other loot is unaffected; " +
                "injected gear rolls the pool's original rarity odds). Starts unenchanted - " +
                "enchant it at the enchanting table to choose its rarity, and re-enchant it " +
                "later to upgrade.",
                "一枚被强运笼罩的护符。佩戴时，附近掉落的魔法装备品质不会低于这枚护符" +
                "（不会提升掉落物品品质，而是取消低品质物品掉落），" +
                "并把你身上可附魔的穿戴装备加入附近掉落池，可以掉到自己穿的装备" +
                "（其他掉落不受影响；注入装备按原池品质几率 roll）。" +
                "护符初始未附魔——在附魔台附魔选择品质，之后可重新附魔升级。",
                "一枚被強運籠罩的護符。佩戴時，附近掉落的魔法裝備品質不會低於這枚護符" +
                "（不會提升掉落物品品質，而是取消低品質物品掉落），" +
                "並把你身上可附魔的穿戴裝備加入附近掉落池，可以掉到自己穿的裝備" +
                "（其他掉落不受影響；注入裝備按原池品質機率 roll）。" +
                "護符初始未附魔——在附魔台附魔選擇品質，之後可重新附魔升級。"),
            (WornPoolLine,
                "Innate: your worn equipment is added to nearby drop pools",
                "固有：你身上穿戴的装备会加入附近掉落池",
                "固有：你身上穿戴的裝備會加入附近掉落池"),
        };

        internal static void Register()
        {
            try
            {
                var localization = LocalizationManager.Instance.GetLocalization();

                foreach (var entry in Translations)
                {
                    localization.AddTranslation("English", entry.Item1, entry.Item2);
                    localization.AddTranslation("Chinese", entry.Item1, entry.Item3);
                    localization.AddTranslation("Chinese_Trad", entry.Item1, entry.Item4);
                }

                LtrLog.Info($"本地化已注册：{Translations.Length} 个条目 × 3 种语言。");
            }
            catch (Exception e)
            {
                LtrLog.Error("本地化注册失败: ", e);
            }
        }

        /// <summary>本地化 token 解析（不可用时原样返回）。</summary>
        internal static string L(string token)
        {
            var localization = Localization.instance;
            return localization != null ? localization.Localize(token) : token;
        }
    }
}
