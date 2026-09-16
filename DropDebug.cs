using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BepInEx.Configuration;
using EpicLoot;
using HarmonyLib;
using UnityEngine;

namespace LuckyTrinket
{
    /// <summary>
    /// 掉落几率诊断（仅在 DebugLog 打开时输出）：
    /// 1. 敌人掉落数量表：0/1/2 件的权重与实际几率（GetDropsForLevel）；
    /// 2. 掉落分类权重：装备/碎片石/未鉴定/材料（EpicLoot Balance 配置，反射读取）；
    /// 3. 每个掉落实际选中的分类与物品名（SelectDropType）；
    /// 4. 本次实际 roll 出的件数，以及过滤前后每件掉落的具体物品与剔除原因。
    /// </summary>
    internal static class DropDebug
    {
        [ThreadStatic] private static Stack<int> s_countStack;
        [ThreadStatic] private static int s_contextCount;
        [ThreadStatic] private static Stack<bool> s_countItemsStack;
        [ThreadStatic] private static bool s_countItems;

        private static float s_baselineAncientPercent = -1f;
        private static int s_sessionKeptEquip;
        private static int s_sessionKeptEquipAncient;
        private static int s_sessionKeptInjectedEquip;

        private static bool _ratiosResolved;
        private static FieldInfo _fItem;
        private static FieldInfo _fShard;
        private static FieldInfo _fUnidentified;
        private static FieldInfo _fMaterials;
        private static FieldInfo _fGlobal;

        internal static bool Enabled =>
            LuckyTrinketPlugin.DebugLog != null && LuckyTrinketPlugin.DebugLog.Value;

        /// <summary>当前掉落上下文内 SelectDropType 被调用的次数（= 实际 roll 出的掉落件数）。</summary>
        internal static int ContextCount =>
            s_countStack != null && s_countStack.Count > 0 ? s_contextCount : 0;

        internal static void EnterContext(bool countItems)
        {
            s_countStack ??= new Stack<int>();
            s_countStack.Push(s_contextCount);
            s_contextCount = 0;

            s_countItemsStack ??= new Stack<bool>();
            s_countItemsStack.Push(s_countItems);
            s_countItems = countItems;
        }

        internal static void ExitContext()
        {
            if (s_countItemsStack != null && s_countItemsStack.Count > 0)
            {
                s_countItems = s_countItemsStack.Pop();
            }

            if (s_countStack != null && s_countStack.Count > 0)
            {
                s_contextCount = s_countStack.Pop();
            }
        }

        /// <summary>记录本次注入的原池 Ancient 期望（用于累计统计对比）。</summary>
        internal static void SetBaselineAncientPercent(float percent)
        {
            s_baselineAncientPercent = percent;
        }

        /// <summary>保留掉落的累计统计（仅装备；只在单表上下文计数）。</summary>
        internal static void OnItemKept(GameObject gameObject)
        {
            if (!s_countItems)
            {
                return;
            }

            try
            {
                var drop = gameObject != null ? gameObject.GetComponent<ItemDrop>() : null;
                var data = drop != null ? drop.m_itemData : null;
                if (data == null || !data.IsEquipable())
                {
                    return;
                }

                var magic = data.GetMagicItem();
                bool ancient = magic != null && (int)magic.Rarity >= (int)ItemRarity.Ancient;
                bool injected = RarityFloorSystem.IsInjectedItem(data.m_dropPrefab?.name);

                s_sessionKeptEquip++;
                if (ancient)
                {
                    s_sessionKeptEquipAncient++;
                }

                if (injected)
                {
                    s_sessionKeptInjectedEquip++;
                }
            }
            catch
            {
                // 统计失败不影响掉落
            }
        }

        /// <summary>累计统计：保留装备的 Ancient 占比 vs 原版池期望。</summary>
        internal static void LogCumulative()
        {
            if (!Enabled || !s_countItems || s_sessionKeptEquip <= 0)
            {
                return;
            }

            float pct = s_sessionKeptEquipAncient * 100f / s_sessionKeptEquip;
            string baseline = s_baselineAncientPercent >= 0f
                ? $"原版池期望≈{s_baselineAncientPercent:0.#}%"
                : "原版池期望=?";
            LtrLog.Debug($"本局累计: 保留装备 {s_sessionKeptEquip} 件，Ancient {s_sessionKeptEquipAncient} 件" +
                         $"（{pct:0.#}%，{baseline}），其中穿戴注入 {s_sessionKeptInjectedEquip} 件");
        }

        /// <summary>单张掉落表开始：打印表名/等级与四类掉落权重。</summary>
        internal static void LogContextStart(string objectName, int level, Vector3? point)
        {
            if (!Enabled)
            {
                return;
            }

            LtrLog.Debug($"掉落开始: 表={objectName}, 等级={level}, 点={point}");
            LtrLog.Debug($"  分类权重（EpicLoot Balance）: {GetRatios()}");
        }

        /// <summary>敌人掉落数量表：0/1/2 件各自的几率。</summary>
        internal static void LogCountTable(LootTable table, int level,
            List<KeyValuePair<int, float>> drops)
        {
            if (!Enabled || drops == null || drops.Count == 0)
            {
                return;
            }

            float total = 0f;
            foreach (var drop in drops)
            {
                total += Math.Max(0f, drop.Value);
            }

            var sb = new StringBuilder();
            sb.Append($"掉落数量几率: 表={table?.Object}, 等级={level}");
            foreach (var drop in drops)
            {
                float pct = total > 0f ? Math.Max(0f, drop.Value) / total * 100f : 0f;
                sb.Append($", {drop.Key}件={pct:0.#}%");
            }

            LtrLog.Debug(sb.ToString());
        }

        /// <summary>每个掉落的分类 roll 结果（穿戴注入的条目会标注）。</summary>
        internal static void OnDropTypeSelected(LootDrop lootDrop, bool isShardDrop, LootDropType type,
            bool injected = false)
        {
            s_contextCount++;

            if (!Enabled)
            {
                return;
            }

            LtrLog.Debug($"  掉落分类: {lootDrop?.Item ?? "?"} → {TypeName(type)}" +
                         (injected ? "（穿戴注入）" : "") +
                         (isShardDrop ? "（固定碎片石，不参与分类 roll）" : ""));
        }

        /// <summary>本次上下文实际 roll 出的件数（在过滤前调用）。</summary>
        internal static void LogRolledCount()
        {
            if (Enabled)
            {
                LtrLog.Debug($"本次实际 roll 出 {ContextCount} 件掉落，开始过滤。");
            }
        }

        /// <summary>掉落物详情（名称/类型/品质/未鉴定/prefab）。</summary>
        internal static string Describe(GameObject gameObject)
        {
            try
            {
                var item = gameObject != null ? gameObject.GetComponent<ItemDrop>() : null;
                var data = item != null ? item.m_itemData : null;
                if (data == null)
                {
                    return "?";
                }

                var magic = data.GetMagicItem();
                return $"{data.m_shared?.m_name} [类型={data.m_shared?.m_itemType}, " +
                       $"品质={magic?.Rarity}, 未鉴定={magic?.IsUnidentified}, " +
                       $"prefab={data.m_dropPrefab?.name}]";
            }
            catch (Exception e)
            {
                return "?（" + e.Message + "）";
            }
        }

        internal static string TypeName(LootDropType type)
        {
            switch (type)
            {
                case LootDropType.Item: return "装备/物品";
                case LootDropType.ShardStone: return "碎片石";
                case LootDropType.Unidentified: return "未鉴定装备";
                case LootDropType.Materials: return "材料";
                default: return type.ToString();
            }
        }

        /// <summary>EpicLoot Balance 的四类掉落权重（ELConfig 为 internal，走反射）。</summary>
        private static string GetRatios()
        {
            try
            {
                ResolveRatios();
                return $"装备={Ratio(_fItem)}, 碎片石={Ratio(_fShard)}, " +
                       $"未鉴定={Ratio(_fUnidentified)}, 材料={Ratio(_fMaterials)}, " +
                       $"全局掉率={Ratio(_fGlobal)}";
            }
            catch (Exception e)
            {
                return "读取失败: " + e.Message;
            }
        }

        private static void ResolveRatios()
        {
            if (_ratiosResolved)
            {
                return;
            }

            _ratiosResolved = true;
            var type = AccessTools.TypeByName("EpicLoot.Config.ELConfig");
            if (type == null)
            {
                return;
            }

            _fItem = AccessTools.Field(type, "ItemDropRatio");
            _fShard = AccessTools.Field(type, "ShardStoneDropRatio");
            _fUnidentified = AccessTools.Field(type, "ItemsUnidentifiedDropRatio");
            _fMaterials = AccessTools.Field(type, "MaterialsDropRatio");
            _fGlobal = AccessTools.Field(type, "GlobalDropRateModifier");
        }

        private static string Ratio(FieldInfo field)
        {
            if (field == null)
            {
                return "?";
            }

            var entry = field.GetValue(null) as ConfigEntry<float>;
            return entry != null ? entry.Value.ToString("0.###") : "?";
        }
    }
}
