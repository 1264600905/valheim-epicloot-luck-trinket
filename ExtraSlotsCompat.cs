using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace LuckyTrinket
{
    /// <summary>
    /// ExtraSlots 软依赖（全反射，未安装时自动跳过）：
    /// 提供 ExtraSlots 装备槽（含额外 Utility/「饰品」槽）中的物品列表，
    /// 供门槛/幸运检测使用。
    /// 护符本体已按 Utility（ExtraSlots 中文标签「饰品」）注册，
    /// 可直接放入 ExtraSlots 的额外饰品槽、多份共存，不占用游戏原版「护符」位。
    /// ExtraSlots 自带 EpicLoot 兼容（equipment provider），额外槽物品会被
    /// EpicLoot 的魔法效果统计自动包含。
    /// </summary>
    internal static class ExtraSlotsCompat
    {
        private static bool _resolved;
        private static MethodInfo _getEquipmentSlotsItems;

        private static bool Resolve()
        {
            if (_resolved)
            {
                return _getEquipmentSlotsItems != null;
            }

            _resolved = true;
            var apiType = AccessTools.TypeByName("ExtraSlots.API");
            if (apiType == null)
            {
                LtrLog.Debug("未检测到 ExtraSlots.API。");
                return false;
            }

            _getEquipmentSlotsItems = AccessTools.Method(apiType, "GetEquipmentSlotsItems");
            if (_getEquipmentSlotsItems == null)
            {
                LtrLog.Warn("检测到 ExtraSlots 但 API 方法缺失，跳过额外槽检测。");
            }

            return _getEquipmentSlotsItems != null;
        }

        /// <summary>ExtraSlots 所有装备槽中的物品（含额外 Utility/饰品槽）；不可用时返回 null。</summary>
        internal static List<ItemDrop.ItemData> GetEquippedSlotItems()
        {
            if (!Resolve())
            {
                return null;
            }

            try
            {
                return _getEquipmentSlotsItems.Invoke(null, null) as List<ItemDrop.ItemData>;
            }
            catch (Exception e)
            {
                LtrLog.Debug("ExtraSlots.GetEquipmentSlotsItems 调用失败: " + e.Message);
                return null;
            }
        }
    }
}
