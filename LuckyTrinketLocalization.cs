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
        internal const string InnateLuck = "$item_luckytrinket_innate_luck";

        private static readonly ValueTuple<string, string, string, string>[] Translations =
        {
            // token, English, Chinese, Chinese_Trad
            (ItemName, "Lucky Trinket", "幸运护符", "幸運護符"),
            (ItemDesc,
                "A charm touched by fortune. While equipped: grants +25 Luck, and prevents " +
                "magic gear dropped near you from rolling below this charm's own rarity " +
                "(equipment below it is discarded; materials one step below are kept). " +
                "Starts unenchanted - enchant it at the enchanting table to choose its " +
                "rarity, and re-enchant it later to upgrade. The Luck bonus is innate, " +
                "not an enchant.",
                "一枚被幸运眷顾的护符。佩戴时提供幸运 +25，并让你附近掉落的魔法装备" +
                "品质不会低于这枚护符（低于门槛的装备不再掉落；材料允许低一级保留）。" +
                "护符初始未附魔——在附魔台附魔选择品质，之后可重新附魔升级。" +
                "幸运为固有属性，不是附魔词条。",
                "一枚被幸運眷顧的護符。佩戴時提供幸運 +25，並讓你附近掉落的魔法裝備" +
                "品質不會低於這枚護符（低於門檻的裝備不再掉落；材料允許低一級保留）。" +
                "護符初始未附魔——在附魔台附魔選擇品質，之後可重新附魔升級。" +
                "幸運為固有屬性，不是附魔詞條。"),
            (InnateLuck, "Innate: +{0} Luck", "固有：幸运 +{0}", "固有：幸運 +{0}"),
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
