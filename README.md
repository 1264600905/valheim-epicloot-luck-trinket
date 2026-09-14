# Lucky Trinket

A Valheim mod that adds a **Lucky Trinket** for **EpicLoot** — a Utility-slot charm that raises the rarity floor of nearby loot.

> Localization: English / Simplified Chinese / Traditional Chinese

---

## Features

- **Innate +25 Luck** while equipped (configurable). Applied through the game's Luck value, not as an enchantment — it never takes an effect slot and cannot be re-rolled or removed by enchanting.
- **Rarity floor for nearby drops**: magic gear dropped near the wearer never rolls below the trinket's own rarity.
  - Equipment and unidentified items below the floor are **discarded** (they never drop).
  - Quality materials (shards, crafting materials, runestones) may still drop up to `MaterialTolerance` steps below the floor (default 1 = one step below is kept).
  - Plain materials (coins, wood, etc.) are unaffected.
  - EpicLoot's original rarity odds are untouched — low rolls are simply discarded, never upgraded.
- **Starts unenchanted**: enchant it at the EpicLoot enchanting table to choose its rarity. This mod also keeps the trinket in the enchant list afterwards, so you can re-enchant it later to upgrade.
- **No random enchant effects**: the trinket only carries rarity (plus its innate Luck). It never rolls "Bloodrage" or other random effects.
- **ExtraSlots friendly**: registered as `Utility` (same class as Void Chest). With ExtraSlots you can equip it in an extra utility slot, keep several trinkets equipped, and leave the vanilla Trinket slot free for vanilla trinkets.
- **Localization**: English / Simplified Chinese / Traditional Chinese.

## Recipe

| Item | Station | Materials |
|---|---|---|
| Lucky Trinket | Workbench | Wood x10 + Bone Fragments x10 + Coins x50 |

## Usage

1. Craft a Lucky Trinket (starts unenchanted: no floor, +25 Luck only).
2. Equip it in a Utility slot (ExtraSlots adds extra slots; the vanilla Trinket slot stays free).
3. Enchant it at the EpicLoot enchanting table and pick a target rarity (e.g. Epic / Legendary).
4. That rarity becomes the drop floor: lower-rarity gear no longer drops near you.
5. Want more? Re-enchant it to pick a higher rarity (cost scales with the target rarity).

## Configuration

File: `BepInEx/config/trigger.epicloot.luckytrinket.cfg` (ConfigurationManager supported).

| Section | Key | Default | Description |
|---|---|---|---|
| General | Enabled | true | Enable the drop rarity floor |
| General | Range | 100 | Radius in meters (1-200): trinkets of wearers within this range of a drop point count |
| General | LuckBonus | 25 | Innate Luck bonus (0-200; 1 point = 1%; 0 = off) |
| General | MaterialTolerance | 1 | How many rarity steps below the floor quality materials may still drop (0-5; 1 = one step below kept, 5 = keep all) |
| General | DebugLog | false | Verbose debug logging |

## Installation

1. Install [BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Install [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
3. Install [EpicLoot](https://thunderstore.io/c/valheim/p/RandyKnapp/EpicLoot/).
4. Drop `LuckyTrinket.dll` into `BepInEx/plugins/LuckyTrinket/`.
5. Optional: [ExtraSlots](https://thunderstore.io/c/valheim/p/shudnal/ExtraSlots/) for extra utility slots.

## Dependencies

- BepInExPack Valheim 5.4.2350+
- Jotunn 2.30.0+
- EpicLoot 0.14.4+
- Optional: ExtraSlots

## Repository

https://github.com/1264600905/valheim-lucky-trinket

## Build

```powershell
cd LuckyTrinket
dotnet build .\LuckyTrinket.csproj -c Release
```

Output: `BepInEx/plugins/LuckyTrinket/LuckyTrinket.dll` (output path is preconfigured).

## Notes

- The floor is evaluated at drop-generation time (monster drops and chests both use the same path). EpicLoot's rarity odds are preserved — low results are discarded rather than upgraded.
- Low-rarity items are still generated in memory once and then discarded. Skipping generation entirely would require patching EpicLoot's internals (IL), which is brittle across updates.
- Uninstalling the mod leaves the trinket item inert; character data is unaffected.

---

## 简体中文

一个 Valheim 模组：为 **EpicLoot** 增加一枚 **幸运护符**——Utility 槽位饰品，提升附近掉落的品质下限。

### 功能

- **固有幸运 +25**：佩戴时生效（可配置）。走游戏幸运值链路，**不是附魔词条**——不占效果槽，附魔/重铸都无法移除或覆盖。
- **掉落品质下限**：佩戴者附近掉落的魔法装备品质不会低于护符自身品质。
  - 低于门槛的**装备与未鉴定装备**直接不掉落；
  - 带品质的**材料**（碎片石/魔法材料/符文）允许低于门槛 `MaterialTolerance` 级保留（默认 1 = 低一级也保留）；
  - 普通材料（金币、木材等）不受影响；
  - **EpicLoot 原有品质几率完全保留**——低品质结果被丢弃，而不是升级。
- **初始未附魔**：在附魔台附魔时自选品质；本模组会把护符保留在附魔列表中，之后可随时重新附魔升级。
- **不带随机词条**：护符只有品质（和固有幸运），不会出现"血怒"之类的随机效果。
- **ExtraSlots 兼容**：注册为 `Utility` 类（与虚空宝箱同类）。装了 ExtraSlots 可放进额外槽位、多枚共存，**不占用游戏原版「护符」槽**。
- **本地化**：English / 简体中文 / 繁體中文。

### 配方

| 物品 | 工作台 | 材料 |
|---|---|---|
| 幸运护符 | 工作台 | 木头×10 + 骨头碎片×10 + 金币×50 |

### 使用

1. 制作幸运护符（初始未附魔：无门槛，仅有 +25 幸运）。
2. 装备到 Utility 槽（装 ExtraSlots 后额外槽位可用，原版「护符」槽保持空闲）。
3. 去 EpicLoot 附魔台附魔，选择目标品质（如 Epic / Legendary）。
4. 该品质即门槛：低于它的装备不再在附近掉落。
5. 想继续提升？重新附魔选更高品质即可（消耗按目标品质计算）。

### 配置

文件：`BepInEx/config/trigger.epicloot.luckytrinket.cfg`（支持 ConfigurationManager）。

| 配置 | 默认 | 说明 |
|---|---|---|
| Enabled | true | 启用掉落品质下限 |
| Range | 100 | 生效半径（米，1-200） |
| LuckBonus | 25 | 固有幸运加成（0-200，1 点 = 1%） |
| MaterialTolerance | 1 | 材料允许低于门槛多少级保留（0-5） |
| DebugLog | false | 调试日志 |

---

## 繁體中文

一個 Valheim 模組：為 **EpicLoot** 增加一枚 **幸運護符**——Utility 槽位飾品，提升附近掉落的品質下限。

### 功能

- **固有幸運 +25**：佩戴時生效（可設定）。走遊戲幸運值鏈路，**不是附魔詞條**——不佔效果槽，附魔/重鑄都無法移除或覆蓋。
- **掉落品質下限**：佩戴者附近掉落的魔法裝備品質不會低於護符自身品質。
  - 低於門檻的**裝備與未鑑定裝備**直接不掉落；
  - 帶品質的**材料**（碎片石/魔法材料/符文）允許低於門檻 `MaterialTolerance` 級保留（預設 1 = 低一級也保留）；
  - 普通材料（金幣、木材等）不受影響；
  - **EpicLoot 原有品質機率完全保留**——低品質結果被丟棄，而不是升級。
- **初始未附魔**：在附魔台附魔時自選品質；本模組會把護符保留在附魔列表中，之後可隨時重新附魔升級。
- **不帶隨機詞條**：護符只有品質（和固有幸運），不會出現「血怒」之類的隨機效果。
- **ExtraSlots 相容**：註冊為 `Utility` 類（與虛空寶箱同類）。裝了 ExtraSlots 可放進額外槽位、多枚共存，**不佔用遊戲原版「護符」槽**。
- **本地化**：English / 简体中文 / 繁體中文。

### 配方

| 物品 | 工作台 | 材料 |
|---|---|---|
| 幸運護符 | 工作台 | 木頭×10 + 骨頭碎片×10 + 金幣×50 |

### 使用

1. 製作幸運護符（初始未附魔：無門檻，僅有 +25 幸運）。
2. 裝備到 Utility 槽（裝 ExtraSlots 後額外槽位可用，原版「護符」槽保持空閒）。
3. 去 EpicLoot 附魔台附魔，選擇目標品質（如 Epic / Legendary）。
4. 該品質即門檻：低於它的裝備不再在附近掉落。
5. 想繼續提升？重新附魔選更高品質即可（消耗按目標品質計算）。
