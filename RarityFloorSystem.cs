using System;
using System.Collections.Generic;
using EpicLoot;
using HarmonyLib;
using UnityEngine;

namespace LuckyTrinket
{
    /// <summary>
    /// 品质门槛核心：
    /// 1. 装备者客户端把自己的护符品质写入玩家 ZDO（多端同步，与 EpicLoot
    ///    官方 Luck/Riches 相同的广播模式）；
    /// 2. 掉落生成时读取掉落点附近所有玩家 ZDO，取最高门槛作为本次掉落上下文；
    /// 3. 掉落生成结束后，低于门槛的魔法装备（含未鉴定装备）从掉落结果中
    ///    排除——保持 EpicLoot 原有的高品质几率分布，只是低级的不再出现；
    /// 4. 幸运护符初始为未附魔物品，可在附魔台附魔并选择品质（已附魔后
    ///    也可重新附魔）；未附魔时无门槛，仅提供固有幸运加成。
    /// </summary>
    internal static class RarityFloorSystem
    {
        internal const string ZdoKey = "ltr-floor";

        /// <summary>EpicLoot 幸运值 ZDO 键（1 点 = 1%）。</summary>
        internal const string LuckZdoKey = "el-luk";

        /// <summary>兜底刷新间隔（秒），应对附魔台升级等未触发装备事件的品质变化。</summary>
        private const float AutoRefreshInterval = 5f;

        [ThreadStatic] private static Stack<int> s_floorStack;
        [ThreadStatic] private static int s_contextFloor;
        [ThreadStatic] private static List<Player> s_playerBuffer;

        private static float s_nextAutoRefresh;
        private static bool s_prefabSanitized;

        /// <summary>当前掉落上下文门槛（-1 = 无）。仅掉落生成栈内有效。</summary>
        internal static int ContextFloor =>
            s_floorStack != null && s_floorStack.Count > 0 ? s_contextFloor : -1;

        internal static void Register()
        {
            API.AddMagicItemChangedListener(OnMagicItemChanged);
            LtrLog.Info("已注册 EpicLoot MagicItemChanged 监听器。");
        }

        internal static void Unregister()
        {
            API.RemoveMagicItemChangedListener(OnMagicItemChanged);
        }

        internal static void Update()
        {
            if (Time.time < s_nextAutoRefresh)
            {
                return;
            }

            s_nextAutoRefresh = Time.time + AutoRefreshInterval;

            TrySanitizePrefabOnce();

            var player = Player.m_localPlayer;
            if (player != null)
            {
                Refresh(player);
            }
        }

        /// <summary>
        /// 运行时兜底：ObjectDB 就绪后再次清理护符 prefab 上继承的效果与属性
        /// （防止注册阶段被其它流程覆盖），只执行一次并输出诊断日志。
        /// </summary>
        private static void TrySanitizePrefabOnce()
        {
            if (s_prefabSanitized || ObjectDB.instance == null)
            {
                return;
            }

            var prefab = ObjectDB.instance.GetItemPrefab(LuckyTrinketPlugin.ItemPrefab);
            if (prefab == null)
            {
                return;
            }

            var drop = prefab.GetComponent<ItemDrop>();
            var shared = drop != null ? drop.m_itemData.m_shared : null;
            if (shared == null)
            {
                return;
            }

            LuckyTrinketItems.StripInheritedEffects(shared, "运行时兜底");
            s_prefabSanitized = true;
        }

        // ---------------------------------------------------------------
        // 掉落上下文
        // ---------------------------------------------------------------

        /// <summary>进入一次掉落生成（由 RollLootTableInternal 的 Prefix 调用）。</summary>
        internal static void EnterDropContext(Vector3? dropPoint)
        {
            s_floorStack ??= new Stack<int>();
            s_floorStack.Push(s_contextFloor);

            int floor = -1;
            try
            {
                if (dropPoint.HasValue)
                {
                    floor = ComputeNearbyFloor(dropPoint.Value);
                }
            }
            catch (Exception e)
            {
                LtrLog.Debug("ComputeNearbyFloor 异常: " + e.Message);
                floor = -1;
            }

            if (floor > s_contextFloor)
            {
                s_contextFloor = floor;
            }

            LtrLog.Debug($"进入掉落上下文: dropPoint={dropPoint}, floor={floor}, active={s_contextFloor}");
        }

        /// <summary>退出一次掉落生成（由 RollLootTableInternal 的 Finalizer 调用）。</summary>
        internal static void ExitDropContext()
        {
            if (s_floorStack == null || s_floorStack.Count == 0)
            {
                return;
            }

            s_contextFloor = s_floorStack.Pop();
        }

        /// <summary>
        /// 掉落生成结束时调用：把低于当前门槛的魔法装备（含未鉴定装备）
        /// 从结果中移除并销毁。材料、金币、碎片、符文等非装备不受影响。
        /// </summary>
        internal static void FilterDroppedItems(ref List<GameObject> items)
        {
            if (!LuckyTrinketPlugin.Enabled.Value || items == null || items.Count == 0)
            {
                return;
            }

            int floor = ContextFloor;
            if (floor < 0)
            {
                LtrLog.Debug($"掉落无门槛（附近无装备护符的玩家），跳过过滤（{items.Count} 个物品）。");
                return;
            }

            int removed = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var gameObject = items[i];
                if (gameObject == null)
                {
                    items.RemoveAt(i);
                    continue;
                }

                bool exclude = ShouldExclude(gameObject, floor);
                if (exclude)
                {
                    DestroyLoot(gameObject);
                    items.RemoveAt(i);
                    removed++;
                }
                else if (LuckyTrinketPlugin.DebugLog.Value)
                {
                    // 诊断：保留物品详情（确认没有低品质漏过）
                    var drop = gameObject.GetComponent<ItemDrop>();
                    var item = drop != null ? drop.m_itemData : null;
                    var magicItem = item != null ? item.GetMagicItem() : null;
                    LtrLog.Debug($"保留掉落: {item?.m_shared?.m_name ?? "?"}, " +
                                 $"type={item?.m_shared?.m_itemType}, equipable={item?.IsEquipable()}, " +
                                 $"rarity={magicItem?.Rarity}, unidentified={magicItem?.IsUnidentified}, " +
                                 $"prefab={item?.m_dropPrefab?.name}");
                }
            }

            if (removed > 0)
            {
                LtrLog.Debug($"门槛 {floor}：排除 {removed} 个低于门槛的掉落" +
                             $"（剩余 {items.Count} 个）。");
            }
        }

        private static bool ShouldExclude(GameObject gameObject, int floor)
        {
            try
            {
                var drop = gameObject.GetComponent<ItemDrop>();
                var item = drop?.m_itemData;
                if (item == null)
                {
                    return false;
                }

                var magicItem = item.GetMagicItem();
                bool unidentified = magicItem != null
                    && (magicItem.IsUnidentified
                        || (item.m_dropPrefab != null
                            && item.m_dropPrefab.name.EndsWith("_Unidentified", StringComparison.Ordinal)));

                // 装备与未鉴定装备：始终过滤
                if (item.IsEquipable() || unidentified)
                {
                    return magicItem != null && (int)magicItem.Rarity < floor;
                }

                // 带品质的材料（碎片石/魔法材料/符文）：
                // 允许低于门槛 MaterialTolerance 级的材料保留（默认低一级也保留）
                int rarity = -1;
                if (API.TryGetRarity(item, ref rarity))
                {
                    int materialFloor = Math.Max(0, floor - LuckyTrinketPlugin.MaterialTolerance.Value);
                    return rarity < materialFloor;
                }

                // 普通无品质材料（金币、木材等）：不过滤
                return false;
            }
            catch (Exception e)
            {
                LtrLog.Debug("ShouldExclude 异常: " + e.Message);
                return false;
            }
        }

        private static void DestroyLoot(GameObject gameObject)
        {
            if (ZNetScene.instance != null)
            {
                ZNetScene.instance.Destroy(gameObject);
            }
            else
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }

        /// <summary>掉落点附近所有装备者护符门槛的最大值（-1 = 无）。</summary>
        private static int ComputeNearbyFloor(Vector3 point)
        {
            if (!LuckyTrinketPlugin.Enabled.Value)
            {
                return -1;
            }

            var players = s_playerBuffer ?? (s_playerBuffer = new List<Player>());
            players.Clear();
            Player.GetPlayersInRange(point, LuckyTrinketPlugin.Range.Value, players);

            int best = -1;
            foreach (var player in players)
            {
                if (player == null)
                {
                    continue;
                }

                var zdo = GetZdo(player);
                if (zdo == null)
                {
                    continue;
                }

                int value = zdo.GetInt(ZdoKey, -1);
                if (value > best)
                {
                    best = value;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------
        // 装备者门槛广播
        // ---------------------------------------------------------------

        /// <summary>重新计算本地玩家的门槛并写入自己的玩家 ZDO（仅装备者客户端执行）。</summary>
        internal static void Refresh(Player player)
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            try
            {
                var zdo = GetZdo(player);
                if (zdo == null)
                {
                    return;
                }

                int floor = ComputePlayerFloor(player);
                if (zdo.GetInt(ZdoKey, -1) != floor)
                {
                    zdo.Set(ZdoKey, floor);
                    LtrLog.Debug($"装备者门槛 ZDO 更新: {floor}");
                }

                WriteLuckZdo(player, zdo);
                SanitizeOwnedTrinkets(player);
            }
            catch (Exception e)
            {
                LtrLog.Error("刷新玩家门槛失败: ", e);
            }
        }

        /// <summary>
        /// 清除护符魔法数据中的随机词条与装饰名，只保留品质。
        /// 护符的效果是固有的（幸运 + 门槛），不参与 EpicLoot 随机词条。
        /// </summary>
        internal static void StripTrinketFlavor(MagicItem magicItem)
        {
            if (magicItem == null)
            {
                return;
            }

            var effects = magicItem.Effects;
            if (effects != null && effects.Count > 0)
            {
                effects.Clear();
            }

            magicItem.DisplayName = null;
        }

        /// <summary>清理玩家背包/装备中所有护符的随机词条（含旧存档数据）。</summary>
        private static void SanitizeOwnedTrinkets(Player player)
        {
            var inventory = player.GetInventory();
            if (inventory == null)
            {
                return;
            }

            foreach (var item in inventory.GetAllItems())
            {
                if (!IsOurTrinket(item) || !item.IsMagic(out var magicItem) || magicItem == null)
                {
                    continue;
                }

                bool hasEffects = magicItem.Effects != null && magicItem.Effects.Count > 0;
                bool hasDisplayName = !string.IsNullOrEmpty(magicItem.DisplayName);
                if (!hasEffects && !hasDisplayName)
                {
                    continue;
                }

                StripTrinketFlavor(magicItem);
                item.SaveMagicItem(magicItem);
                LtrLog.Debug("已清除护符的随机词条/装饰名（只保留品质）。");
            }
        }

        /// <summary>
        /// 把 el-luk 写为"基础幸运 + 护符固有加成"。
        /// EpicLoot 官方在装备变化时写 el-luk；本方法在其后覆盖（幂等），
        /// 保证固有 +LuckBonus 始终计入且不会重复叠加。
        /// </summary>
        internal static void WriteLuckZdo(Player player, ZDO zdo = null)
        {
            if (player == null || player != Player.m_localPlayer)
            {
                return;
            }

            try
            {
                zdo ??= GetZdo(player);
                if (zdo == null)
                {
                    return;
                }

                float baseLuck = ComputeBaseLuck(player);
                float bonus = IsLuckyTrinketEquipped(player) ? LuckyTrinketPlugin.LuckBonus.Value : 0f;
                int target = (int)(baseLuck + bonus);

                if (zdo.GetInt(LuckZdoKey, 0) != target)
                {
                    zdo.Set(LuckZdoKey, target);
                    LtrLog.Debug($"幸运 ZDO 更新: {target}（基础 {baseLuck} + 护符 {bonus}）");
                }
            }
            catch (Exception e)
            {
                LtrLog.Debug("写入幸运 ZDO 异常: " + e.Message);
            }
        }

        /// <summary>玩家所有已装备魔法物品提供的幸运效果总和（与 EpicLoot 官方统计一致）。</summary>
        private static float ComputeBaseLuck(Player player)
        {
            try
            {
                float sum = 0f;
                foreach (var effect in player.GetAllActiveMagicEffects(MagicEffectType.Luck))
                {
                    sum += effect.EffectValue;
                }

                return sum;
            }
            catch (Exception e)
            {
                // 极端情况下（EpicLoot 版本差异）降级为直接遍历已装备物品
                LtrLog.Debug("GetAllActiveMagicEffects 异常，降级遍历装备: " + e.Message);

                float sum = 0f;
                foreach (var item in CollectEquippedItems(player))
                {
                    if (!item.IsMagic(out var magicItem) || magicItem == null)
                    {
                        continue;
                    }

                    foreach (var effect in magicItem.GetEffects(MagicEffectType.Luck, true))
                    {
                        sum += effect.EffectValue;
                    }
                }

                return sum;
            }
        }

        private static int ComputePlayerFloor(Player player)
        {
            int best = -1;
            foreach (var item in CollectEquippedItems(player))
            {
                if (!IsOurTrinket(item))
                {
                    continue;
                }

                if (!item.IsMagic(out var magicItem) || magicItem == null)
                {
                    continue;
                }

                int rarity = (int)magicItem.Rarity;
                if (rarity > best)
                {
                    best = rarity;
                }
            }

            return best;
        }

        // ---------------------------------------------------------------
        // 工具
        // ---------------------------------------------------------------

        /// <summary>玩家当前是否装备着幸运护符（用于固有幸运加成判定）。</summary>
        internal static bool IsLuckyTrinketEquipped(Player player)
        {
            if (player == null)
            {
                return false;
            }

            foreach (var item in CollectEquippedItems(player))
            {
                if (IsOurTrinket(item))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 玩家所有已装备物品：原生装备槽（含 Trinket）+ ExtraSlots 额外槽/自定义槽（如安装）。
        /// </summary>
        private static List<ItemDrop.ItemData> CollectEquippedItems(Player player)
        {
            var items = new List<ItemDrop.ItemData>();

            var inventory = player.GetInventory();
            if (inventory != null)
            {
                items.AddRange(inventory.GetEquippedItems());
            }

            var extra = ExtraSlotsCompat.GetEquippedSlotItems();
            if (extra != null)
            {
                foreach (var item in extra)
                {
                    if (item != null && !items.Contains(item))
                    {
                        items.Add(item);
                    }
                }
            }

            return items;
        }

        internal static bool IsOurTrinket(ItemDrop.ItemData item)
        {
            var prefab = item?.m_dropPrefab;
            if (prefab == null)
            {
                return false;
            }

            string name = prefab.name;
            string target = LuckyTrinketPlugin.ItemPrefab;
            return name == target || name.StartsWith(target + "(Clone)", StringComparison.Ordinal);
        }

        private static ZDO GetZdo(Component component)
        {
            if (component == null)
            {
                return null;
            }

            var nview = component.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid())
            {
                return null;
            }

            return nview.GetZDO();
        }

        private static void OnMagicItemChanged(ItemDrop.ItemData item, string reason)
        {
            try
            {
                if (!IsOurTrinket(item))
                {
                    return;
                }

                var player = Player.m_localPlayer;
                if (player == null)
                {
                    return;
                }

                if (!player.GetInventory().GetEquippedItems().Contains(item))
                {
                    return;
                }

                LtrLog.Debug($"护符魔法数据变化（{reason}），刷新门槛。");
                Refresh(player);
            }
            catch (Exception e)
            {
                LtrLog.Debug("OnMagicItemChanged 异常: " + e.Message);
            }
        }
    }
}
