using System;
using System.IO;
using BepInEx;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace LuckyTrinket
{
    /// <summary>
    /// 注册两枚护符物品：克隆原版 Trinket（饰品槽），工作台配方合成。
    /// 幸运护符（金色）提供掉落品质下限；强运护符（紫色，幸运护符 + 黑金属）
    /// 额外提供穿戴装备注入。护符初始为未附魔物品，在 EpicLoot 附魔台
    /// 附魔时选择品质（本 mod 会让已附魔的护符也能重新附魔）。
    /// </summary>
    internal static class LuckyTrinketItems
    {
        private const string BasePrefab = "TrinketBronzeHealth";
        private static bool _registered;

        internal static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += RegisterItem;
            LtrLog.Info($"已订阅 PrefabManager.OnVanillaPrefabsAvailable（克隆基础物品 {BasePrefab}）");
        }

        private static void RegisterItem()
        {
            if (_registered)
            {
                return;
            }

            try
            {
                RegisterTrinket(
                    LuckyTrinketPlugin.ItemPrefab,
                    new Color(1f, 0.82f, 0.25f, 1f),
                    200,
                    LuckyTrinketLocalization.ItemName,
                    LuckyTrinketLocalization.ItemDesc,
                    new[]
                    {
                        new RequirementConfig("Wood", 10),
                        new RequirementConfig("BoneFragments", 10),
                        new RequirementConfig("Coins", 50),
                    });

                RegisterTrinket(
                    LuckyTrinketPlugin.GreatItemPrefab,
                    new Color(0.85f, 0.2f, 0.85f, 1f),
                    500,
                    LuckyTrinketLocalization.GreatItemName,
                    LuckyTrinketLocalization.GreatItemDesc,
                    new[]
                    {
                        new RequirementConfig(LuckyTrinketPlugin.ItemPrefab, 1),
                        new RequirementConfig("BlackMetal", 10),
                    });

                _registered = true;
            }
            catch (Exception e)
            {
                LtrLog.Error("注册护符失败: ", e);
            }
        }

        /// <summary>注册单个护符：克隆基础饰品、写入本地化与属性、工作台配方。</summary>
        private static void RegisterTrinket(string prefabName, Color tint, int value,
            string nameToken, string descToken, RequirementConfig[] requirements)
        {
            var config = new ItemConfig
            {
                Name = prefabName,
                Description = prefabName,
                CraftingStation = "piece_workbench",
                MinStationLevel = 1,
                Amount = 1,
                Requirements = requirements,
                Weight = 1.5f,
            };

            var item = new CustomItem(prefabName, BasePrefab, config);
            if (item.ItemPrefab == null)
            {
                LtrLog.Error($"[{prefabName}] CustomItem 克隆 {BasePrefab} 失败。");
                return;
            }

            Sanitize(item, prefabName, tint, value, nameToken, descToken);
            ItemManager.Instance.AddItem(item);

            var recipe = item.Recipe;
            LtrLog.Info($"[{prefabName}] 物品与配方注册完成" +
                        $"（工作台={recipe?.Recipe?.m_craftingStation?.name ?? "null"}, " +
                        $"材料={requirements.Length} 项）。");

            ExportIconForModPackaging(item.ItemDrop.m_itemData.m_shared, prefabName);
        }

        /// <summary>
        /// 导出游戏内护符图标为 PNG（用于模组发布 icon.png）。
        /// 统一输出到插件目录 LuckyTrinket/，避免在 plugins 下多建文件夹。
        /// </summary>
        private static void ExportIconForModPackaging(ItemDrop.ItemData.SharedData shared, string prefabName)
        {
            try
            {
                var icon = shared?.m_icons != null && shared.m_icons.Length > 0
                    ? shared.m_icons[0]
                    : null;
                var texture = icon != null ? icon.texture : null;
                if (texture == null)
                {
                    LtrLog.Warn("护符图标导出跳过：纹理为空。");
                    return;
                }

                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0)
                {
                    LtrLog.Warn("护符图标导出失败：PNG 数据为空。");
                    return;
                }

                string dir = Path.Combine(Paths.PluginPath, LuckyTrinketPlugin.ItemPrefab);
                Directory.CreateDirectory(dir);
                string fileName = prefabName == LuckyTrinketPlugin.ItemPrefab
                    ? "icon-export.png"
                    : $"icon-export-{prefabName}.png";
                string path = Path.Combine(dir, fileName);
                File.WriteAllBytes(path, png);
                LtrLog.Info($"护符图标已导出: {path}（{texture.width}x{texture.height}, {png.Length} bytes）");
            }
            catch (Exception e)
            {
                LtrLog.Warn("护符图标导出失败: " + e.Message);
            }
        }

        private static void Sanitize(CustomItem item, string prefabName, Color tint, int value,
            string nameToken, string descToken)
        {
            var drop = item.ItemDrop;
            if (drop == null)
            {
                LtrLog.Error($"[{prefabName}] 没有 ItemDrop 组件。");
                return;
            }

            var shared = drop.m_itemData.m_shared;
            // Utility（ExtraSlots 中文标签「饰品」）——像虚空宝箱一样可放入额外饰品槽、多份共存，
            // 不占用游戏唯一的「护符」（Trinket）位，原版饰品不受影响。
            shared.m_itemType = ItemDrop.ItemData.ItemType.Utility;
            shared.m_maxStackSize = 1;
            shared.m_maxQuality = 1;
            shared.m_weight = 1.5f;
            shared.m_value = value;
            shared.m_teleportable = true;
            shared.m_questItem = false;

            // 直接写入本地化 token，游戏 tooltip / 背包按当前语言解析
            shared.m_name = nameToken;
            shared.m_description = descToken;

            // 彻底清除从基础饰品继承的一切效果/属性，护符只保留"品质 + 固有幸运"逻辑
            StripInheritedEffects(shared, "注册");

            RecolorIcons(shared, tint);
            RecolorRenderers(item.ItemPrefab, tint);

            LtrLog.Info($"[{prefabName}] 属性: type={shared.m_itemType}, " +
                        $"weight={shared.m_weight}, icons={shared.m_icons?.Length ?? 0}, name={shared.m_name}");
        }

        /// <summary>
        /// 清除 SharedData 上从基础饰品（TrinketBronzeHealth）继承的所有效果与属性，
        /// 包括状态效果、附加 tooltip、肾上腺素（游戏内中文译名"血怒"）与各种修饰符。
        /// 幂等，可重复调用（注册时 + 游戏运行时兜底）。
        /// </summary>
        internal static void StripInheritedEffects(ItemDrop.ItemData.SharedData shared, string context)
        {
            if (shared == null)
            {
                return;
            }

            LtrLog.Info($"[{context}] 清理前: " +
                        $"equipSE={NameOf(shared.m_equipStatusEffect)}, " +
                        $"setSE={NameOf(shared.m_setStatusEffect)}, " +
                        $"attackSE={NameOf(shared.m_attackStatusEffect)}, " +
                        $"consumeSE={NameOf(shared.m_consumeStatusEffect)}, " +
                        $"perfectBlockSE={NameOf(shared.m_perfectBlockStatusEffect)}, " +
                        $"fullAdrenalineSE={NameOf(shared.m_fullAdrenalineSE)}, " +
                        $"appendToolTip={NameOf(shared.m_appendToolTip)}, " +
                        $"maxAdrenaline={shared.m_maxAdrenaline}, " +
                        $"blockAdrenaline={shared.m_blockAdrenaline}, " +
                        $"perfectBlockAdrenaline={shared.m_perfectBlockAdrenaline}");

            // 状态效果
            shared.m_equipStatusEffect = null;
            shared.m_setStatusEffect = null;
            shared.m_attackStatusEffect = null;
            shared.m_consumeStatusEffect = null;
            shared.m_perfectBlockStatusEffect = null;
            shared.m_fullAdrenalineSE = null;

            // 附加 tooltip（会追加显示其它物品的效果说明）
            shared.m_appendToolTip = null;

            // 套装
            shared.m_setName = "";
            shared.m_setSize = 0;

            // 肾上腺素（游戏内显示为"血怒"）
            shared.m_maxAdrenaline = 0f;
            shared.m_blockAdrenaline = 0f;
            shared.m_perfectBlockAdrenaline = 0f;

            // 属性修饰
            shared.m_eitrRegenModifier = 0f;
            shared.m_movementModifier = 0f;
            shared.m_homeItemsStaminaModifier = 0f;
            shared.m_heatResistanceModifier = 0f;
            shared.m_jumpStaminaModifier = 0f;
            shared.m_attackStaminaModifier = 0f;
            shared.m_blockStaminaModifier = 0f;
            shared.m_dodgeStaminaModifier = 0f;
            shared.m_swimStaminaModifier = 0f;
            shared.m_sneakStaminaModifier = 0f;
            shared.m_runStaminaModifier = 0f;
            shared.m_iceSkates = false;
            shared.m_iceShoes = false;

            // 食物属性（护符不是食物）
            shared.m_food = 0f;
            shared.m_foodStamina = 0f;
            shared.m_foodEitr = 0f;
            shared.m_foodBurnTime = 0f;
            shared.m_foodRegen = 0f;
            shared.m_isDrink = false;
        }

        private static string NameOf(UnityEngine.Object obj)
        {
            return obj == null ? "null" : obj.name;
        }

        private static void RecolorIcons(ItemDrop.ItemData.SharedData shared, Color tint)
        {
            if (shared.m_icons == null || shared.m_icons.Length == 0)
            {
                LtrLog.Warn($"[{LuckyTrinketPlugin.ItemPrefab}] m_icons 为空，跳过图标染色。");
                return;
            }

            for (int i = 0; i < shared.m_icons.Length; i++)
            {
                shared.m_icons[i] = RecolorSprite(shared.m_icons[i], tint);
            }
        }

        private static Sprite RecolorSprite(Sprite src, Color tint)
        {
            if (src == null)
            {
                return null;
            }

            var srcTex = src.texture;
            if (srcTex == null)
            {
                return src;
            }

            // 图标可能来自 SpriteAtlas：必须按 textureRect 读取子区域
            var rect = src.textureRect;
            int rw = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int rh = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            int tw = Mathf.Max(1, srcTex.width);
            int th = Mathf.Max(1, srcTex.height);

            var rt = RenderTexture.GetTemporary(tw, th, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;

            try
            {
                Graphics.Blit(srcTex, rt);
                RenderTexture.active = rt;

                var tex = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(rect.x, rect.y, rw, rh), 0, 0);
                tex.Apply();

                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    var p = px[i];
                    if (p.a == 0)
                    {
                        continue;
                    }

                    // 保留原像素亮度层次，替换为目标金色
                    float v = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) / 255f;
                    v = Mathf.Clamp01(0.2f + 0.8f * v);

                    px[i] = new Color32(
                        (byte)(tint.r * 255f * v),
                        (byte)(tint.g * 255f * v),
                        (byte)(tint.b * 255f * v),
                        p.a);
                }

                tex.SetPixels32(px);
                tex.Apply();

                return Sprite.Create(tex, new Rect(0, 0, rw, rh), new Vector2(0.5f, 0.5f),
                    src.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            finally
            {
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static void RecolorRenderers(GameObject root, Color tint)
        {
            int count = 0;

            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.materials;
                foreach (var mat in materials)
                {
                    if (mat == null)
                    {
                        continue;
                    }

                    if (mat.HasProperty("_Color"))
                    {
                        mat.SetColor("_Color", tint);
                        count++;
                    }

                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", tint * 0.35f);
                    }
                }
            }

            LtrLog.Info($"[{LuckyTrinketPlugin.ItemPrefab}] 模型材质染色: {count} 处。");
        }
    }
}
