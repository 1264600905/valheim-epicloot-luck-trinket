using System;
using System.Collections.Generic;
using System.Globalization;
using EpicLoot;
using HarmonyLib;
using UnityEngine;

namespace LuckyTrinket
{
    /// <summary>
    /// 品质门槛核心：
    /// 1. 装备者客户端把自己的护符品质写入玩家 ZDO（多端同步广播）；
    /// 2. 掉落生成时读取掉落点附近所有玩家 ZDO，取最高门槛作为本次掉落上下文；
    /// 3. 掉落生成结束后，低于门槛的魔法装备（含未鉴定装备）从掉落结果中
    ///    排除——只取消低品质掉落，不提升物品品质，保持 EpicLoot 原有几率分布；
    /// 4. 幸运护符初始为未附魔物品，可在附魔台附魔并选择品质（已附魔后
    ///    也可重新附魔）；未附魔时无门槛；
    /// 5. 强运护符额外提供穿戴装备注入：装备者把可附魔的穿戴装备名（prefab 名）
    ///    广播到 ZDO，掉落时按比例追加进掉落池（不屏蔽原池条目）；注入条目的
    ///    品质分布完全沿用原池，不钳制、不提升——EpicLoot 原始品质几率保持不变。
    /// </summary>
    internal static class RarityFloorSystem
    {
        internal const string ZdoKey = "ltr-floor";

        /// <summary>强运护符穿戴装备名单 ZDO 键（逗号分隔的 prefab 名）。</summary>
        internal const string WornZdoKey = "ltr-worn";

        /// <summary>兜底刷新间隔（秒），应对附魔台升级等未触发装备事件的品质变化。</summary>
        private const float AutoRefreshInterval = 5f;

        [ThreadStatic] private static Stack<int> s_floorStack;
        [ThreadStatic] private static int s_contextFloor;
        [ThreadStatic] private static Stack<Dictionary<string, float>> s_wornStack;
        [ThreadStatic] private static Dictionary<string, float> s_contextWorn;
        [ThreadStatic] private static Stack<HashSet<string>> s_injectedStack;
        [ThreadStatic] private static HashSet<string> s_contextInjected;
        [ThreadStatic] private static List<Player> s_playerBuffer;

        private static float s_nextAutoRefresh;
        private static bool s_prefabSanitized;

        /// <summary>当前掉落上下文门槛（-1 = 无）。仅掉落生成栈内有效。</summary>
        internal static int ContextFloor =>
            s_floorStack != null && s_floorStack.Count > 0 ? s_contextFloor : -1;

        /// <summary>当前掉落上下文的穿戴装备（prefab 名 → 注入权重；null/空 = 不注入）。</summary>
        private static Dictionary<string, float> ContextWorn =>
            s_wornStack != null && s_wornStack.Count > 0 ? s_contextWorn : null;

        /// <summary>本次掉落是否由穿戴装备注入（仅用于诊断日志）。</summary>
        internal static bool IsInjectedItem(string itemName) =>
            !string.IsNullOrEmpty(itemName)
            && s_contextInjected != null
            && s_contextInjected.Contains(itemName);

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

            s_wornStack ??= new Stack<Dictionary<string, float>>();
            s_wornStack.Push(s_contextWorn);

            s_injectedStack ??= new Stack<HashSet<string>>();
            s_injectedStack.Push(s_contextInjected);
            s_contextInjected = null;

            int floor = -1;
            Dictionary<string, float> worn = null;
            try
            {
                if (dropPoint.HasValue)
                {
                    floor = ComputeNearbyFloor(dropPoint.Value);
                    worn = ComputeNearbyWorn(dropPoint.Value);
                }
            }
            catch (Exception e)
            {
                LtrLog.Debug("ComputeNearbyFloor/ComputeNearbyWorn 异常: " + e.Message);
                floor = -1;
                worn = null;
            }

            if (floor > s_contextFloor)
            {
                s_contextFloor = floor;
            }

            if (worn != null && worn.Count > 0)
            {
                s_contextWorn = worn;
            }

            LtrLog.Debug($"进入掉落上下文: dropPoint={dropPoint}, floor={floor}, " +
                         $"worn={worn?.Count ?? 0}, active={s_contextFloor}");
            if (worn != null && worn.Count > 0)
            {
                LtrLog.Debug($"  穿戴装备名单({worn.Count}): {DescribeWorn(worn)}");
            }
        }

        /// <summary>退出一次掉落生成（由 RollLootTableInternal 的 Finalizer 调用）。</summary>
        internal static void ExitDropContext()
        {
            if (s_injectedStack != null && s_injectedStack.Count > 0)
            {
                s_contextInjected = s_injectedStack.Pop();
            }

            if (s_wornStack != null && s_wornStack.Count > 0)
            {
                s_contextWorn = s_wornStack.Pop();
            }

            if (s_floorStack == null || s_floorStack.Count == 0)
            {
                return;
            }

            s_contextFloor = s_floorStack.Pop();
        }

        /// <summary>
        /// 掉落生成结束时调用：低于当前门槛的魔法装备（含未鉴定装备）从结果中
        /// 移除并销毁。材料、金币、碎片、符文等非装备不受影响。
        /// （穿戴装备只做注入，不在这里屏蔽原池条目。）
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
                LtrLog.Debug($"掉落无门槛（附近无装备护符的玩家），跳过过滤" +
                             $"（{items.Count} 个物品）。");
                return;
            }

            DropDebug.LogRolledCount();

            int removed = 0;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var gameObject = items[i];
                if (gameObject == null)
                {
                    items.RemoveAt(i);
                    continue;
                }

                bool excludeByFloor = ShouldExclude(gameObject, floor);
                if (excludeByFloor)
                {
                    if (LuckyTrinketPlugin.DebugLog.Value)
                    {
                        LtrLog.Debug($"剔除(门槛<{floor}): {DropDebug.Describe(gameObject)}");
                    }

                    DestroyLoot(gameObject);
                    items.RemoveAt(i);
                    removed++;
                }
                else
                {
                    DropDebug.OnItemKept(gameObject);
                    if (LuckyTrinketPlugin.DebugLog.Value)
                    {
                        LtrLog.Debug($"保留掉落: {DropDebug.Describe(gameObject)}");
                    }
                }
            }

            if (LuckyTrinketPlugin.DebugLog.Value)
            {
                var kept = new List<string>();
                foreach (var gameObject in items)
                {
                    kept.Add(DropDebug.Describe(gameObject));
                }

                LtrLog.Debug($"过滤结果: roll={DropDebug.ContextCount}, 剔除门槛={removed}, " +
                             $"保留={items.Count}");
                LtrLog.Debug($"最终掉落: {(kept.Count > 0 ? string.Join("; ", kept) : "（空）")}");
                DropDebug.LogCumulative();
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

        /// <summary>
        /// 强运护符：把附近佩戴者的穿戴装备按比例追加进本次掉落池（由
        /// LootRoller.GetLootForLevel 的 Postfix 调用）。
        /// 只做追加，不修改/屏蔽原池条目；注入组总权重 = 原池总权重 ×
        /// WornPoolShare/(1-Share)，组内按相对权重分配；注入条目的品质分布
        /// 完全沿用原池（不钳制、不提升），低于门槛的注入装备与其它掉落一样被门槛剔除。
        /// </summary>
        internal static void InjectWornItems(ref LootDrop[] loot)
        {
            if (!LuckyTrinketPlugin.Enabled.Value || !LuckyTrinketPlugin.WornPool.Value)
            {
                return;
            }

            var worn = ContextWorn;
            if (worn == null || worn.Count == 0)
            {
                return;
            }

            float share = Mathf.Clamp(LuckyTrinketPlugin.WornPoolShare.Value, 0f, 0.95f);
            if (share <= 0f)
            {
                return;
            }

            var existing = new HashSet<string>(StringComparer.Ordinal);
            float originalTotal = 0f;
            if (loot != null)
            {
                foreach (var entry in loot)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(entry.Item))
                    {
                        existing.Add(entry.Item);
                    }

                    originalTotal += Math.Max(0f, entry.Weight);
                }
            }

            // 注入组内相对权重（手持武器/饰品/默认），排除池内已有条目
            float relativeTotal = 0f;
            var candidates = new List<KeyValuePair<string, float>>();
            foreach (var pair in worn)
            {
                if (string.IsNullOrEmpty(pair.Key) || existing.Contains(pair.Key))
                {
                    continue;
                }

                float relative = pair.Value > 0f ? pair.Value : LuckyTrinketPlugin.WornPoolWeight.Value;
                candidates.Add(new KeyValuePair<string, float>(pair.Key, relative));
                relativeTotal += relative;
            }

            if (candidates.Count == 0 || relativeTotal <= 0f)
            {
                return;
            }

            // 按目标占比缩放：注入总权重 = 原池总权重 × share/(1-share)
            float injectedTotal = originalTotal > 0f
                ? originalTotal * share / (1f - share)
                : relativeTotal;
            float scale = injectedTotal / relativeTotal;

            var rarity = BuildInjectedRarity(loot);
            var injected = new List<LootDrop>();
            foreach (var candidate in candidates)
            {
                injected.Add(new LootDrop
                {
                    Item = candidate.Key,
                    Weight = Math.Max(0.0001f, candidate.Value * scale),
                    Rarity = rarity,
                });
            }

            int originalLength = loot?.Length ?? 0;
            var combined = new LootDrop[originalLength + injected.Count];
            if (loot != null)
            {
                Array.Copy(loot, combined, originalLength);
            }

            for (int i = 0; i < injected.Count; i++)
            {
                combined[originalLength + i] = injected[i];
            }

            loot = combined;

            // 记录本次注入的条目名，供掉落分类日志标注
            s_contextInjected ??= new HashSet<string>(StringComparer.Ordinal);
            foreach (var drop in injected)
            {
                s_contextInjected.Add(drop.Item);
            }

            if (LuckyTrinketPlugin.DebugLog.Value)
            {
                var parts = new List<string>();
                foreach (var drop in injected)
                {
                    parts.Add($"{drop.Item}({drop.Weight.ToString("0.###", CultureInfo.InvariantCulture)})");
                }

                float effectiveShare = originalTotal > 0f
                    ? injectedTotal / (originalTotal + injectedTotal)
                    : 1f;
                LtrLog.Debug($"注入掉落池: 追加 {injected.Count} 件穿戴装备" +
                             $"（原池总权重={originalTotal.ToString("0.###", CultureInfo.InvariantCulture)}, " +
                             $"注入总权重={injectedTotal.ToString("0.###", CultureInfo.InvariantCulture)}, " +
                             $"占比={effectiveShare * 100f:0.#}%）: " +
                             string.Join(", ", parts));

                float ancientPct = AncientPercent(rarity);
                DropDebug.SetBaselineAncientPercent(ancientPct);
                LtrLog.Debug($"  品质分布（原池=注入，未钳制）=[{JoinWeights(rarity)}] Ancient≈{ancientPct:0.#}%");
            }
        }

        /// <summary>Ancient（含更高档）权重占总权重的百分比。</summary>
        private static float AncientPercent(float[] weights)
        {
            if (weights == null || weights.Length == 0)
            {
                return 0f;
            }

            float total = 0f;
            float ancient = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                float weight = Math.Max(0f, weights[i]);
                total += weight;
                if (i >= (int)ItemRarity.Ancient)
                {
                    ancient += weight;
                }
            }

            return total > 0f ? ancient / total * 100f : 0f;
        }

        private static string JoinWeights(float[] weights)
        {
            if (weights == null)
            {
                return "无";
            }

            var parts = new List<string>(weights.Length);
            foreach (var weight in weights)
            {
                parts.Add(weight.ToString("0.###", CultureInfo.InvariantCulture));
            }

            return string.Join(",", parts);
        }

        /// <summary>穿戴名单的日志文本：名字(权重)，按名字排序。</summary>
        private static string DescribeWorn(Dictionary<string, float> worn)
        {
            if (worn == null || worn.Count == 0)
            {
                return "";
            }

            var parts = new List<string>();
            foreach (var pair in worn)
            {
                parts.Add($"{pair.Key}({pair.Value.ToString("0.##", CultureInfo.InvariantCulture)})");
            }

            parts.Sort(StringComparer.Ordinal);
            return string.Join(", ", parts);
        }

        /// <summary>
        /// 注入条目的品质权重：完全复制池内第一个带品质分布的条目——不钳制、不提升，
        /// 保持 EpicLoot 原始品质几率不变。
        /// </summary>
        private static float[] BuildInjectedRarity(LootDrop[] pool)
        {
            int count = Rarities.Count;
            var result = new float[count];

            float[] source = null;
            if (pool != null)
            {
                foreach (var entry in pool)
                {
                    if (entry?.Rarity != null && entry.Rarity.Length == count)
                    {
                        source = entry.Rarity;
                        break;
                    }
                }
            }

            if (source != null)
            {
                Array.Copy(source, result, count);
            }
            else
            {
                // 池内没有品质分布（纯材料表等）：与原版一致，按最低档 Magic 处理
                result[0] = 1f;
            }

            return result;
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

        /// <summary>
        /// 掉落点附近所有强运护符佩戴者的穿戴装备并集（prefab 名 → 权重；
        /// null = 无人佩戴，不注入）。名单经玩家 ZDO 广播，联机时由各客户端自行写入。
        /// </summary>
        private static Dictionary<string, float> ComputeNearbyWorn(Vector3 point)
        {
            if (!LuckyTrinketPlugin.Enabled.Value || !LuckyTrinketPlugin.WornPool.Value)
            {
                return null;
            }

            var players = s_playerBuffer ?? (s_playerBuffer = new List<Player>());
            players.Clear();
            Player.GetPlayersInRange(point, LuckyTrinketPlugin.Range.Value, players);

            Dictionary<string, float> worn = null;
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

                string list = zdo.GetString(WornZdoKey, "");
                if (string.IsNullOrEmpty(list))
                {
                    continue;
                }

                worn ??= new Dictionary<string, float>(StringComparer.Ordinal);
                foreach (var entry in list.Split(','))
                {
                    if (string.IsNullOrEmpty(entry))
                    {
                        continue;
                    }

                    string name = entry;
                    float weight = LuckyTrinketPlugin.WornPoolWeight.Value;

                    int sep = entry.LastIndexOf(':');
                    if (sep > 0
                        && float.TryParse(entry.Substring(sep + 1), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out float parsed))
                    {
                        name = entry.Substring(0, sep);
                        weight = parsed;
                    }

                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }

                    // 多佩戴者同名装备取最大权重
                    if (!worn.TryGetValue(name, out float current) || weight > current)
                    {
                        worn[name] = weight;
                    }
                }
            }

            return worn;
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

                string worn = ComputeWornList(player);
                if (zdo.GetString(WornZdoKey, "") != worn)
                {
                    zdo.Set(WornZdoKey, worn);
                    LtrLog.Debug($"装备者穿戴名单 ZDO 更新: [{worn}]");
                }

                SanitizeOwnedTrinkets(player);
            }
            catch (Exception e)
            {
                LtrLog.Error("刷新玩家门槛失败: ", e);
            }
        }

        /// <summary>
        /// 清除护符魔法数据中的随机词条与装饰名，只保留品质。
        /// 护符的效果是固有的（门槛/穿戴注入），不参与 EpicLoot 随机词条。
        /// </summary>
        internal static void StripTrinketFlavor(MagicItem magicItem)
        {
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
                if (!IsAnyTrinket(item) || !item.IsMagic(out var magicItem) || magicItem == null)
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

        private static int ComputePlayerFloor(Player player)
        {
            int best = -1;
            foreach (var item in CollectEquippedItems(player))
            {
                if (!IsAnyTrinket(item))
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

        /// <summary>
        /// 强运护符佩戴者的穿戴装备 prefab 名列表（逗号分隔、排序去重）。
        /// 未佩戴强运护符时返回空串，ZDO 名单清空 → 掉落恢复原池子。
        /// </summary>
        private static string ComputeWornList(Player player)
        {
            if (!IsGreatTrinketEquipped(player))
            {
                return "";
            }

            float defaultWeight = LuckyTrinketPlugin.WornPoolWeight.Value;
            float heldWeaponWeight = LuckyTrinketPlugin.WornPoolHeldWeaponWeight.Value;
            float utilityWeight = LuckyTrinketPlugin.WornPoolUtilityWeight.Value;
            bool extraSlotsAvailable = ExtraSlotsCompat.IsAvailable;

            // 手持武器（GetCurrentWeapon：右手武器，否则左手武器，排除火把）
            var heldWeapon = player.GetCurrentWeapon();

            var entries = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in CollectEquippedItems(player))
            {
                if (item == null || IsAnyTrinket(item))
                {
                    continue;
                }

                // 跳过不能附魔的物品（弹药、可堆叠物、受限物品等），
                // 这类物品没有品质，注入后只会作为普通物品掉落
                if (!global::EpicLoot.EpicLoot.CanBeMagicItem(item))
                {
                    continue;
                }

                var prefab = item.m_dropPrefab;
                if (prefab == null || string.IsNullOrEmpty(prefab.name))
                {
                    continue;
                }

                if (!seen.Add(prefab.name))
                {
                    continue;
                }

                float weight = defaultWeight;
                if (heldWeapon != null && ReferenceEquals(item, heldWeapon))
                {
                    weight = heldWeaponWeight;
                }
                else if (extraSlotsAvailable
                         && item.m_shared != null
                         && item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Utility)
                {
                    // 饰品权重仅在安装 ExtraSlots 时生效（否则用默认权重）
                    weight = utilityWeight;
                }

                entries.Add($"{prefab.name}:{weight.ToString("0.###", CultureInfo.InvariantCulture)}");
            }

            entries.Sort(StringComparer.Ordinal);
            return string.Join(",", entries);
        }

        // ---------------------------------------------------------------
        // 工具
        // ---------------------------------------------------------------

        /// <summary>玩家当前是否装备着强运护符（用于穿戴装备注入判定）。</summary>
        internal static bool IsGreatTrinketEquipped(Player player)
        {
            return IsTrinketEquipped(player, IsGreatTrinket);
        }

        private static bool IsTrinketEquipped(Player player, Func<ItemDrop.ItemData, bool> predicate)
        {
            if (player == null)
            {
                return false;
            }

            foreach (var item in CollectEquippedItems(player))
            {
                if (predicate(item))
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

        /// <summary>是否为幸运护符物品。</summary>
        internal static bool IsLuckyTrinket(ItemDrop.ItemData item)
        {
            return IsTrinketPrefab(item, LuckyTrinketPlugin.ItemPrefab);
        }

        /// <summary>是否为强运护符物品。</summary>
        internal static bool IsGreatTrinket(ItemDrop.ItemData item)
        {
            return IsTrinketPrefab(item, LuckyTrinketPlugin.GreatItemPrefab);
        }

        /// <summary>是否为任意护符物品（幸运/强运）。</summary>
        internal static bool IsAnyTrinket(ItemDrop.ItemData item)
        {
            return IsLuckyTrinket(item) || IsGreatTrinket(item);
        }

        private static bool IsTrinketPrefab(ItemDrop.ItemData item, string target)
        {
            var prefab = item?.m_dropPrefab;
            if (prefab == null)
            {
                return false;
            }

            string name = prefab.name;
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
                if (!IsAnyTrinket(item))
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
