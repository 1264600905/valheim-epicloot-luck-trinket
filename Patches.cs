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
    /// - Humanoid.EquipItem / UnequipItem：装备变化时刷新装备者 ZDO 门槛与幸运；
    /// - LootRoller.RollLootTableInternal（两个重载）：进入掉落生成时压入
    ///   附近玩家最高门槛，结束时排除低于门槛的装备掉落；
    /// - Multiplayer_Player_Patch...UpdateRichesAndLuck：覆盖写入含护符
    ///   固有加成的幸运值；
    /// - EnchantingUIController.GetEnchantableItems：让幸运护符（含已附魔的）
    ///   始终出现在附魔列表，可附魔/重新附魔选择品质。
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

            // 幸运注入顺序保证：EpicLoot 写完 el-luk 后立即覆盖为含护符加成的新值
            var watchEffects = AccessTools.Inner(typeof(Multiplayer_Player_Patch),
                "WatchMultiplayerMagicEffects_Player_Patch");
            var updateRiches = watchEffects == null
                ? null
                : AccessTools.Method(watchEffects, "UpdateRichesAndLuck", new[] { typeof(Player) });

            TryPatch(harmony, updateRiches, null,
                HarmonyMethodFor(nameof(UpdateRichesAndLuck_Postfix)), null,
                "Multiplayer_Player_Patch.WatchMultiplayerMagicEffects_Player_Patch.UpdateRichesAndLuck");

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

            // tooltip 显示固有幸运加成
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
        /// 让幸运护符（含已附魔的）出现在附魔台的"附魔"列表中：
        /// 新护符保持未附魔，可直接附魔并选择目标品质；已附魔护符仍可
        /// 重新附魔（重 roll 效果并提升品质），消耗按目标品质计算。
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
                    if (!RarityFloorSystem.IsOurTrinket(item))
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
            if (__result != null && RarityFloorSystem.IsOurTrinket(baseItem))
            {
                RarityFloorSystem.StripTrinketFlavor(__result);
            }
        }

        /// <summary>护符 tooltip 追加固有幸运加成（不占词条、不受附魔影响）。</summary>
        private static void GetTooltip_Postfix(ref string __result, ItemDrop.ItemData item, bool appending)
        {
            if (appending || item == null || string.IsNullOrEmpty(__result)
                || !RarityFloorSystem.IsOurTrinket(item))
            {
                return;
            }

            float bonus = LuckyTrinketPlugin.LuckBonus.Value;
            if (bonus <= 0f)
            {
                return;
            }

            string text = LuckyTrinketLocalization.L(LuckyTrinketLocalization.InnateLuck)
                .Replace("{0}", ((int)bonus).ToString());
            __result += "\n<color=#FFD24C>" + text + "</color>";
        }

        private static void RollLootTableInternal_Prefix(object[] __args)
        {
            Vector3? dropPoint = null;
            if (__args != null)
            {
                foreach (var arg in __args)
                {
                    if (arg is Vector3 point)
                    {
                        dropPoint = point;
                        break;
                    }
                }
            }

            RarityFloorSystem.EnterDropContext(dropPoint);
        }

        private static void RollLootTableInternal_Finalizer(ref List<GameObject> __result)
        {
            // 先按门槛排除低品质装备，再退出上下文
            RarityFloorSystem.FilterDroppedItems(ref __result);
            RarityFloorSystem.ExitDropContext();
        }

        /// <summary>
        /// 固有幸运：EpicLoot 官方写完 el-luk（基础幸运）后立即覆盖为
        /// "基础幸运 + 护符加成"。不修改物品词条数据，因此不占效果槽，
        /// 也不受附魔/升级操作影响；重复调用幂等，不会叠加。
        /// </summary>
        private static void UpdateRichesAndLuck_Postfix(Player player)
        {
            RarityFloorSystem.WriteLuckZdo(player);
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
