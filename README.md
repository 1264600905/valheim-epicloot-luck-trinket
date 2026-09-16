# Lucky Trinket & Great Fortune Trinket

A Valheim mod that adds two **Utility-slot charms** for **EpicLoot**: the **Lucky Trinket** raises the rarity floor of nearby loot, and the **Great Fortune Trinket** additionally adds your worn equipment to nearby drop pools.

> Thunderstore package: **EpicLoot_LuckTrinket**
> Localization: English / Simplified Chinese / Traditional Chinese

---

## Features

- **Rarity floor for nearby drops**: magic gear dropped near the wearer never rolls below the trinket's own rarity.
  - **It never upgrades drop quality** — low rolls are simply removed (discarded), so EpicLoot's original rarity odds stay untouched.
  - Equipment and unidentified items below the floor are discarded (they never drop).
  - Quality materials (shards, crafting materials, runestones) may still drop up to `MaterialTolerance` steps below the floor (default 1 = one step below is kept).
  - Plain materials (coins, wood, etc.) are unaffected.
- **Great Fortune Trinket - worn equipment pool injection**: your enchantable worn gear is added to nearby drop pools, so you can find your own equipment as loot.
  - Injected items take `WornPoolShare` of the pool's total weight (default 0.2 = about 20% yours / 80% original); the original loot pool is never blocked or removed.
  - Injected items roll the **pool's original rarity odds** — no clamping, no quality upgrades.
  - Per-item relative weights: held weapon x2, utility items x0.4 (ExtraSlots only), everything else x1.
  - Ammo, stackable and restricted items are skipped (they cannot be enchanted).
- **Starts unenchanted**: enchant it at the EpicLoot enchanting table to choose its rarity. This mod also keeps the trinket in the enchant list afterwards, so you can re-enchant it later to upgrade.
- **No random enchant effects**: the trinkets only carry rarity. They never roll "Bloodrage" or other random effects.
- **ExtraSlots friendly**: registered as `Utility` (same class as Void Chest). With ExtraSlots you can equip them in extra utility slots, keep several trinkets equipped, and leave the vanilla Trinket slot free for vanilla trinkets.
- **Localization**: English / Simplified Chinese / Traditional Chinese.

## Recipes

| Item | Station | Materials |
|---|---|---|
| Lucky Trinket | Workbench | Wood x10 + Bone Fragments x10 + Coins x50 |
| Great Fortune Trinket | Workbench | Lucky Trinket x1 + Black Metal x10 |

## Usage

1. Craft a Lucky Trinket (starts unenchanted: no floor yet).
2. Equip it in a Utility slot (ExtraSlots adds extra slots; the vanilla Trinket slot stays free).
3. Enchant it at the EpicLoot enchanting table and pick a target rarity (e.g. Epic / Legendary).
4. That rarity becomes the drop floor: lower-rarity gear no longer drops near you.
5. Want your own gear to drop too? Craft the Great Fortune Trinket (Lucky Trinket + 10 Black Metal) and equip it.
6. Want more? Re-enchant the trinket to pick a higher rarity (cost scales with the target rarity).

## Configuration

File: `BepInEx/config/trigger.epicloot.luckytrinket.cfg` (ConfigurationManager supported).

| Section | Key | Default | Description |
|---|---|---|---|
| General | Enabled | true | Enable the drop rarity floor |
| General | Range | 100 | Radius in meters (1-200): trinkets of wearers within this range of a drop point count |
| General | MaterialTolerance | 1 | How many rarity steps below the floor quality materials may still drop (0-5; 1 = one step below kept, 5 = keep all) |
| General | DebugLog | false | Verbose debug logging |
| General | WornPool | true | Enable worn-equipment pool injection (Great Fortune Trinket only) |
| General | WornPoolShare | 0.2 | Injected share of the pool's total weight (0.2 = about 20% yours / 80% original; 0 = off) |
| General | WornPoolWeight | 1 | Relative weight of each worn item inside the injected group |
| General | WornPoolHeldWeaponWeight | 2 | Relative weight of the held weapon inside the injected group |
| General | WornPoolUtilityWeight | 0.4 | Relative weight of utility items (ExtraSlots only) |

## Installation

1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Install [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
3. Install [EpicLoot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/).
4. Drop `LuckyTrinket.dll` into `BepInEx/plugins/LuckyTrinket/`.
5. Optional: [ExtraSlots](https://thunderstore.io/c/valheim/p/shudnal/ExtraSlots/) for extra utility slots.

## Dependencies

- BepInExPack Valheim 5.4.2350+
- Jotunn 2.30.0+
- EpicLoot 0.14.5+
- Optional: ExtraSlots

## Repository

https://github.com/1264600905/valheim-epicloot-luck-trinket

## Build

```powershell
cd LuckyTrinket
dotnet build .\LuckyTrinket.csproj -c Release
```

Output: `BepInEx/plugins/LuckyTrinket/LuckyTrinket.dll` (output path is preconfigured).

## Notes

- The floor is evaluated at drop-generation time (monster drops and chests both use the same path). EpicLoot's rarity odds are preserved — low results are discarded rather than upgraded.
- Low-rarity items are still generated in memory once and then discarded. Skipping generation entirely would require patching EpicLoot's internals (IL), which is brittle across updates.
- Worn-equipment lists are broadcast through the player ZDO (`ltr-worn`), so the feature works in both singleplayer and multiplayer; nearby wearers' lists are unioned.
- Uninstalling the mod leaves the trinket items inert; character data is unaffected.

---

## 简体中文

一个 Valheim 模组：为 **EpicLoot** 增加两枚 **Utility（饰品）槽位护符**：**幸运护符** 提升附近掉落的品质下限，**强运护符** 额外把你身上穿戴的装备加入附近掉落池。

### 功能

- **掉落品质下限**：佩戴者附近掉落的魔法装备品质不会低于护符自身品质。
  - **不会提升掉落物品品质**——低品质结果被直接取消（丢弃），EpicLoot 原有品质几率完全保留；
  - 低于门槛的**装备与未鉴定装备**不再掉落；
  - 带品质的**材料**（碎片石/魔法材料/符文）允许低于门槛 `MaterialTolerance` 级保留（默认 1 = 低一级也保留）；
  - 普通材料（金币、木材等）不受影响。
- **强运护符·穿戴装备注入**：把你身上**可附魔**的穿戴装备加入附近掉落池，可以掉到自己穿的装备。
  - 注入组占掉落池总权重的 `WornPoolShare`（默认 0.2 ≈ 你的装备 20%、原版池 80%）；**原掉落池条目不受影响、不被屏蔽**；
  - 注入装备按**原池品质几率** roll，不钳制、不提升品质；
  - 组内相对权重：手持武器 ×2、饰品 ×0.4（仅安装 ExtraSlots 时）、其余 ×1；
  - 弹药、可堆叠、受限物品会被跳过（它们不能附魔）。
- **初始未附魔**：在附魔台附魔时自选品质；本模组会把护符保留在附魔列表中，之后可随时重新附魔升级。
- **不带随机词条**：护符只有品质，不会出现"血怒"之类的随机效果。
- **ExtraSlots 兼容**：注册为 `Utility` 类（与虚空宝箱同类）。装了 ExtraSlots 可放进额外槽位、多枚共存，**不占用游戏原版「护符」槽**。
- **本地化**：English / 简体中文 / 繁體中文。

### 配方

| 物品 | 工作台 | 材料 |
|---|---|---|
| 幸运护符 | 工作台 | 木头×10 + 骨头碎片×10 + 金币×50 |
| 强运护符 | 工作台 | 幸运护符×1 + 黑金属×10 |

### 使用

1. 制作幸运护符（初始未附魔：暂无门槛）。
2. 装备到 Utility 槽（装 ExtraSlots 后额外槽位可用，原版「护符」槽保持空闲）。
3. 去 EpicLoot 附魔台附魔，选择目标品质（如 Epic / Legendary）。
4. 该品质即门槛：低于它的装备不再在附近掉落。
5. 想让自己的装备也能掉？制作强运护符（幸运护符 + 黑金属×10）并装备。
6. 想继续提升？重新附魔选更高品质即可（消耗按目标品质计算）。

### 配置

文件：`BepInEx/config/trigger.epicloot.luckytrinket.cfg`（支持 ConfigurationManager）。

| 配置 | 默认 | 说明 |
|---|---|---|
| Enabled | true | 启用掉落品质下限 |
| Range | 100 | 生效半径（米，1-200） |
| MaterialTolerance | 1 | 材料允许低于门槛多少级保留（0-5） |
| DebugLog | false | 调试日志 |
| WornPool | true | 启用穿戴装备注入（仅强运护符） |
| WornPoolShare | 0.2 | 注入组占掉落池总权重的比例（0.2 ≈ 我们 20%、原版 80%；0 = 关闭） |
| WornPoolWeight | 1 | 注入组内每件装备的相对权重 |
| WornPoolHeldWeaponWeight | 2 | 注入组内手持武器的相对权重 |
| WornPoolUtilityWeight | 0.4 | 注入组内饰品（Utility）的相对权重（仅 ExtraSlots） |

---

## 繁體中文

一個 Valheim 模組：為 **EpicLoot** 增加兩枚 **Utility（飾品）槽位護符**：**幸運護符** 提升附近掉落的品質下限，**強運護符** 額外把你身上穿戴的裝備加入附近掉落池。

### 功能

- **掉落品質下限**：佩戴者附近掉落的魔法裝備品質不會低於護符自身品質。
  - **不會提升掉落物品品質**——低品質結果被直接取消（丟棄），EpicLoot 原有品質機率完全保留；
  - 低於門檻的**裝備與未鑑定裝備**不再掉落；
  - 帶品質的**材料**（碎片石/魔法材料/符文）允許低於門檻 `MaterialTolerance` 級保留（預設 1 = 低一級也保留）；
  - 普通材料（金幣、木材等）不受影響。
- **強運護符·穿戴裝備注入**：把你身上**可附魔**的穿戴裝備加入附近掉落池，可以掉到自己穿的裝備。
  - 注入組佔掉落池總權重的 `WornPoolShare`（預設 0.2 ≈ 你的裝備 20%、原版池 80%）；**原掉落池條目不受影響、不被屏蔽**；
  - 注入裝備按**原池品質機率** roll，不鉗制、不提升品質；
  - 組內相對權重：手持武器 ×2、飾品 ×0.4（僅安裝 ExtraSlots 時）、其餘 ×1；
  - 彈藥、可堆疊、受限物品會被跳過（它們不能附魔）。
- **初始未附魔**：在附魔台附魔時自選品質；本模組會把護符保留在附魔列表中，之後可隨時重新附魔升級。
- **不帶隨機詞條**：護符只有品質，不會出現「血怒」之類的隨機效果。
- **ExtraSlots 相容**：註冊為 `Utility` 類（與虛空寶箱同類）。裝了 ExtraSlots 可放進額外槽位、多枚共存，**不佔用遊戲原版「護符」槽**。
- **本地化**：English / 简体中文 / 繁體中文。

### 配方

| 物品 | 工作台 | 材料 |
|---|---|---|
| 幸運護符 | 工作台 | 木頭×10 + 骨頭碎片×10 + 金幣×50 |
| 強運護符 | 工作台 | 幸運護符×1 + 黑金屬×10 |

### 使用

1. 製作幸運護符（初始未附魔：暫無門檻）。
2. 裝備到 Utility 槽（裝 ExtraSlots 後額外槽位可用，原版「護符」槽保持空閒）。
3. 去 EpicLoot 附魔台附魔，選擇目標品質（如 Epic / Legendary）。
4. 該品質即門檻：低於它的裝備不再在附近掉落。
5. 想讓自己的裝備也能掉？製作強運護符（幸運護符 + 黑金屬×10）並裝備。
6. 想繼續提升？重新附魔選更高品質即可（消耗按目標品質計算）。

### 配置

檔案：`BepInEx/config/trigger.epicloot.luckytrinket.cfg`（支援 ConfigurationManager）。

| 設定 | 預設 | 說明 |
|---|---|---|
| Enabled | true | 啟用掉落品質下限 |
| Range | 100 | 生效半徑（公尺，1-200） |
| MaterialTolerance | 1 | 材料允許低於門檻多少級保留（0-5） |
| DebugLog | false | 除錯日誌 |
| WornPool | true | 啟用穿戴裝備注入（僅強運護符） |
| WornPoolShare | 0.2 | 注入組佔掉落池總權重的比例（0.2 ≈ 我們 20%、原版 80%；0 = 關閉） |
| WornPoolWeight | 1 | 注入組內每件裝備的相對權重 |
| WornPoolHeldWeaponWeight | 2 | 注入組內手持武器的相對權重 |
| WornPoolUtilityWeight | 0.4 | 注入組內飾品（Utility）的相對權重（僅 ExtraSlots） |
