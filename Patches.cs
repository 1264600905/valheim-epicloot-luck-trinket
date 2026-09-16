using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EpicLoot;
using EpicLoot.CraftingV2;
using EpicLoot_UnityLib;
using HarmonyLib;
using UnityEngine;

namespace LuckyTrinket
{
    /// <summary>
    /// Harmony 注入：
    /// - Humanoid.EquipItem / UnequipItem：装备变化时刷新装备者 ZDO 门槛与穿戴名单；
    /// - LootRoller.RollLootTableInternal（两个重载）：进入掉落生成时压入
    ///   附近玩家最高门槛，结束时排除低于门槛的装备掉落；
    /// - LootRoller.GetLootForLevel：强运护符把穿戴装备追加进掉落池（不屏蔽原条目）；
    /// - EnchantingUIController.GetEnchantableItems：让护符（含已附魔的）
    ///   始终出现在附魔列表，可附魔/重新附魔选择品质；
    /// - LootRoller.GetDropsForLevel / SelectDropType：掉落几率诊断日志。
    /// 全部动态查找 + 独立 try/catch，EpicLoot 升级导致签名变化时只禁用
    /// 对应功能，不影响物品注册。
    /// </summary>
    internal static class Patches
    {
        internal static void Install(Harmony harmony)
        {
            var equipPostfix = HarmonyMethodFor(nameof(EquipOrUnequip_Postfix));
            TryPatch(harmony,
                AccessTools.Method(typeof(Humanoid), "EquipItem",
                    new[] { typeof(ItemDrop.ItemData), typeof(bool) }),
                null, equipPostfix, null, "Humanoid.EquipItem");

            TryPatch(harmony,
                AccessTools.Method(typeof(Humanoid), "UnequipItem",
                    new[] { typeof(ItemDrop.ItemData), typeof(bool) }),
                null, equipPostfix, null, "Humanoid.UnequipItem");

            InstallLootContextPatches(harmony);
            InstallDropDebugPatches(harmony);

            // 护符可反复附魔：EpicLoot 原生只列出未附魔物品，这里补入已附魔护符
            TryPatch(harmony,
                AccessTools.Method(typeof(EnchantingUIController), "GetEnchantableItems"),
                null, HarmonyMethodFor(nameof(GetEnchantableItems_Postfix)), null,
                "EnchantingUIController.GetEnchantableItems");

            // 护符只保留品质：生成魔法数据时清除随机词条与装饰名
            TryPatch(harmony,
                AccessTools.Method(typeof(LootRoller), "RollMagicItem",
                    new[] { typeof(ItemRarity), typeof(ItemDrop.ItemData), typeof(float), typeof(float) }),
                null, HarmonyMethodFor(nameof(RollMagicItem_Postfix)), null,
                "LootRoller.RollMagicItem(ItemRarity, ItemData, float, float)");

            // tooltip 显示护符固有说明
            TryPatch(harmony,
                AccessTools.Method(typeof(ItemDrop.ItemData), "GetTooltip",
                    new[]
                    {
                        typeof(ItemDrop.ItemData), typeof(int), typeof(bool),
                        typeof(float), typeof(int), typeof(bool),
                    }),
                null, HarmonyMethodFor(nameof(GetTooltip_Postfix)), null,
                "ItemDrop.ItemData.GetTooltip");
        }

        private static void InstallLootContextPatches(Harmony harmony)
        {
            var prefix = HarmonyMethodFor(nameof(RollLootTableInternal_Prefix));
            var finalizer = HarmonyMethodFor(nameof(RollLootTableInternal_Finalizer));

            int patched = 0;
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(LootRoller))
                         .Where(m => m.Name == "RollLootTableInternal"))
            {
                if (TryPatch(harmony, method, prefix, null, finalizer,
                        $"LootRoller.RollLootTableInternal({Describe(method.GetParameters())})"))
                {
                    patched++;
                }
            }

            if (patched == 0)
            {
                LtrLog.Error("未注入任何 RollLootTableInternal，掉落品质下限不生效" +
                             "（EpicLoot 版本可能不兼容）。");
            }

            // 强运护符：把穿戴装备追加进掉落池（LootRoller 选池入口）
            TryPatch(harmony,
                AccessTools.Method(typeof(LootRoller), "GetLootForLevel"),
                null, HarmonyMethodFor(nameof(GetLootForLevel_Postfix)), null,
                "LootRoller.GetLootForLevel");
        }

        /// <summary>
        /// 掉落几率诊断（仅 DebugLog 打开时输出）：
        /// - GetDropsForLevel：敌人掉落数量表（0/1/2 件几率）；
        /// - SelectDropType：每个掉落实际选中的分类（装备/碎片石/未鉴定/材料）。
        /// </summary>
        private static void InstallDropDebugPatches(Harmony harmony)
        {
            TryPatch(harmony,
                AccessTools.Method(typeof(LootRoller), "GetDropsForLevel"),
                null, HarmonyMethodFor(nameof(GetDropsForLevel_Postfix)), null,
                "LootRoller.GetDropsForLevel");

            TryPatch(harmony,
                AccessTools.Method(typeof(LootRoller), "SelectDropType"),
                null, HarmonyMethodFor(nameof(SelectDropType_Postfix)), null,
                "LootRoller.SelectDropType");
        }

        // ---------------------------------------------------------------
        // 注入体
        // ---------------------------------------------------------------

        private static void EquipOrUnequip_Postfix(Humanoid __instance)
        {
            if (__instance is Player player)
            {
                RarityFloorSystem.Refresh(player);
            }
        }

        /// <summary>
        /// 让护符（含已附魔的）出现在附魔台的"附魔"列表中：
        /// 新护符保持未附魔，可直接附魔并选择目标品质；已附魔护符仍可
        /// 重新附魔（重新选择品质），消耗按目标品质计算。
        /// </summary>
        private static void GetEnchantableItems_Postfix(ref List<InventoryItemListElement> __result)
        {
            try
            {
                if (__result == null)
                {
                    return;
                }

                var allItems = InventoryManagement.Instance?.GetAllItems();
                if (allItems == null)
                {
                    return;
                }

                foreach (var item in allItems)
                {
                    if (!RarityFloorSystem.IsAnyTrinket(item))
                    {
                        continue;
                    }

                    if (__result.Any(element => element.Item == item))
                    {
                        continue;
                    }

                    __result.Add(new InventoryItemListElement { Item = item });
                }
            }
            catch (Exception e)
            {
                LtrLog.Debug("补充护符到附魔列表异常: " + e.Message);
            }
        }

        /// <summary>护符生成魔法数据时不带随机词条与装饰名（只保留品质）。</summary>
        private static void RollMagicItem_Postfix(ItemDrop.ItemData baseItem, ref MagicItem __result)
        {
            if (__result != null && RarityFloorSystem.IsAnyTrinket(baseItem))
            {
                RarityFloorSystem.StripTrinketFlavor(__result);
            }
        }

        /// <summary>
        /// 护符 tooltip 追加固有说明：强运护符显示穿戴装备注入说明
        /// （固有属性，不占词条）。
        /// </summary>
        private static void GetTooltip_Postfix(ref string __result, ItemDrop.ItemData item, bool appending)
        {
            if (appending || item == null || string.IsNullOrEmpty(__result)
                || !RarityFloorSystem.IsAnyTrinket(item))
            {
                return;
            }

            if (RarityFloorSystem.IsGreatTrinket(item))
            {
                string wornText = LuckyTrinketLocalization.L(LuckyTrinketLocalization.WornPoolLine);
                __result += "\n<color=#C77DFF>" + wornText + "</color>";
            }
        }

        private static void RollLootTableInternal_Prefix(object[] __args)
        {
            Vector3? dropPoint = null;
            string objectName = null;
            int level = 0;
            bool singleTable = false;

            if (__args != null)
            {
                // 单表重载 args[0] 是 LootTable；集合重载只是逐表调用单表重载，
                // 诊断日志只在单表重载打印，避免重复。
                singleTable = __args.Length > 0 && __args[0] is LootTable;

                foreach (var arg in __args)
                {
                    if (arg is Vector3 point)
                    {
                        dropPoint = point;
                    }
                    else if (arg is string name)
                    {
                        objectName = name;
                    }
                    else if (arg is int lvl)
                    {
                        level = lvl;
                    }
                }
            }

            DropDebug.EnterContext(singleTable);
            RarityFloorSystem.EnterDropContext(dropPoint);

            if (singleTable)
            {
                DropDebug.LogContextStart(objectName, level, dropPoint);
            }
        }

        private static void RollLootTableInternal_Finalizer(ref List<GameObject> __result)
        {
            // 先按门槛过滤（内部输出诊断），再退出上下文
            RarityFloorSystem.FilterDroppedItems(ref __result);
            RarityFloorSystem.ExitDropContext();
            DropDebug.ExitContext();
        }

        /// <summary>强运护符：把穿戴装备追加进本次掉落池（不屏蔽原池条目）。</summary>
        private static void GetLootForLevel_Postfix(ref LootDrop[] __result)
        {
            RarityFloorSystem.InjectWornItems(ref __result);
        }

        /// <summary>敌人掉落数量表：0/1/2 件的几率。</summary>
        private static void GetDropsForLevel_Postfix(LootTable lootTable, int level,
            ref List<KeyValuePair<int, float>> __result)
        {
            DropDebug.LogCountTable(lootTable, level, __result);
        }

        /// <summary>每个掉落实际选中的分类与物品名（穿戴注入的会标注）。</summary>
        private static void SelectDropType_Postfix(LootDrop lootDrop, bool isShardDrop,
            ref LootDropType __result)
        {
            DropDebug.OnDropTypeSelected(lootDrop, isShardDrop, __result,
                RarityFloorSystem.IsInjectedItem(lootDrop?.Item));
        }

        // ---------------------------------------------------------------
        // 工具
        // ---------------------------------------------------------------

        private static bool TryPatch(Harmony harmony, MethodBase target, HarmonyMethod prefix,
            HarmonyMethod postfix, HarmonyMethod finalizer, string label)
        {
            if (target == null)
            {
                LtrLog.Warn($"未找到目标方法，跳过: {label}");
                return false;
            }

            try
            {
                harmony.Patch(target, prefix: prefix, postfix: postfix, finalizer: finalizer);
                LtrLog.Info($"已注入: {label}");
                return true;
            }
            catch (Exception e)
            {
                LtrLog.Error($"注入失败 {label}: ", e);
                return false;
            }
        }

        private static HarmonyMethod HarmonyMethodFor(string name)
        {
            var method = AccessTools.Method(typeof(Patches), name);
            if (method == null)
            {
                throw new MissingMethodException($"Patches.{name} 不存在");
            }

            return new HarmonyMethod(method);
        }

        private static string Describe(ParameterInfo[] parameters)
        {
            return string.Join(", ", parameters.Select(p => p.ParameterType.Name));
        }
    }
}
