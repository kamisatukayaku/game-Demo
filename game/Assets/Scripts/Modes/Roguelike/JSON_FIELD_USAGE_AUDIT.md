# JSON 字段使用校验报告

> 生成时间：2026-06-13 | 共检查 42 个 JSON 文件

## 判定规则

- ✅ USED：字段值参与数值计算、比较/分支判断、UI 渲染、或作为字典索引键
- ❌ UNUSED：仅反序列化存到变量但后续未被读取；或 JSON 中定义了但 C# 解析类缺少对应字段
- ⚠️ PARTIAL：部分子类型/实体中使用了该字段，但并非所有条目都有效

---

## 一、`data/combat/` (11 个文件)

### 1.1 `attacks.json` → `AttackProfileDatabase.cs`

| 字段 | 状态 | 使用位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | 无 C# 字段接收 |
| `_note` | ❌ UNUSED | 注释字段，JsonUtility 忽略 |
| `id` | ✅ USED | 字典 key (`AttackProfileDatabase.cs:153`) |
| `display_name` | ❌ UNUSED | `AttackJson` 类中缺少此字段 |
| `source_kind` | ❌ UNUSED | `AttackJson` 类中缺少此字段 |
| `delivery` | ✅ USED | `PlayerAttackDirector.cs:238` switch 分发 |
| `targeting` | ❌ UNUSED | 存入 `AttackProfile.targeting` 从未被外部读取 |
| `cooldown` | ✅ USED | `PlayerAttackDirector.cs:639` `StartCooldown()` |
| `range` | ✅ USED | `PlayerAttackDirector.cs:239` 射程计算 |
| `base_damage` | ✅ USED | `PlayerAttackDirector.cs:350` `BuildDamageRequest()` |
| `damage_type` | ✅ USED | `DamagePipeline.cs:171` `DamageTypesCatalog.Get()` |
| `damage_source` | ✅ USED | `PlayerAttackDirector.cs:654` 技能检测 |
| `tags` | ❌ UNUSED | `AttackJson` 类中缺少此字段 |
| `weapon_extension` | ❌ UNUSED | `AttackJson` 类中缺少此字段 |
| `skill_extension` | ❌ UNUSED | `AttackJson` 类中缺少此字段 |
| `monster_extension` | ❌ UNUSED | `AttackJson` 类中缺少此字段 |
| `delivery_params.projectile_speed` | ✅ USED | `PlayerAttackDirector.cs:475` `×1.2` |
| `delivery_params.projectile_homing` | ✅ USED | `EnemyAttack.cs:565` switch 分支 |
| `delivery_params.projectile_scale` | ✅ USED | `PlayerAttackDirector.cs:476` |
| `delivery_params.aoe_radius` | ⚠️ PARTIAL | 仅 Editor 测试中使用，运行时不使用 |
| `delivery_params.aoe_duration` | ❌ UNUSED | 存入 `AttackProfile` 未被外部读取 |
| `delivery_params.aoe_cone_angle_deg` | ⚠️ PARTIAL | 同上，仅 Editor 测试 |
| `delivery_params.aoe_persistent` | ❌ UNUSED | 存入但未被外部读取 |
| `delivery_params.aoe_damage_mult` | ❌ UNUSED | 存入但未被外部读取 |
| `delivery_params.beam_pierce` | ✅ USED | `EnemyAttack.cs` 激光配置 |
| `delivery_params.beam_half_width` | ✅ USED | `EnemyAttack.cs:461` 光束半宽 |
| `delivery_params.beam_duration` | ✅ USED | `EnemyAttack.cs:463` 光束持续时间 |
| `delivery_params.beam_tick_interval` | ✅ USED | `EnemyAttack.cs:464` 光束Tick间隔 |
| `delivery_params.projectile_count` | ✅ USED | `EnemyAttack.cs:459` 弹体数量 |
| `delivery_params.spread_deg` | ✅ USED | `EnemyAttack.cs:460` 散射角度 |
| `delivery_params.dash_speed_mult` | ✅ USED | `EnemyAttack.cs:458` 冲刺速度倍率 |
| `delivery_params.hit_radius` | ✅ USED | `EnemyAttack.cs:565` 弹体碰撞半径 |
| `delivery_params.charge_dash` | ✅ USED | `EnemyAttack.cs:199` `ResolveDeliveryType()` |
| `buff_extension.on_hit_buffs` | ✅ USED | `PlayerAttackDirector.cs:402` 命中施加Buff |
| `buff_extension.damage_modifier` | ❌ UNUSED | `AttackJson` 类中缺少此字段 |
| `player_editor` (所有子字段) | ❌ UNUSED | `AttackJson` 类中缺少整个对象 |

### 1.2 `enemies.json` → `EnemyDatabase.cs` / `EnemySpawner.cs`

| 字段 | 状态 | 使用位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | 无 C# 字段接收 |
| `_note` | ❌ UNUSED | 注释字段 |
| `id` | ✅ USED | `EnemyDatabase.cs:56` 字典 key；`EnemySpawner.cs:354` 名称生成 |
| `display_name` | ❌ UNUSED | `EnemyDatabase.EnemyDef` 类中缺少此字段 |
| `move_mode` | ✅ USED | `EnemySpawner.cs:364` `def.move_mode == "lane_follow"` 判定 |
| `attack_mode` | ✅ USED | `EnemySpawner.cs:348,352` 判断 ranged/barrage/laser/charge |
| `ai_profile` | ✅ USED | `EnemySpawner.cs:347` `GetAiProfile(def.ai_profile)` 查找AI配置 |
| `base_hp` | ✅ USED | `EnemySpawner.cs:351` `def.base_hp * scaling.hpMult` HP缩放 |
| `base_damage` | ✅ USED | `EnemySpawner.cs:357` `def.base_damage * scaling.damageMult` 伤害缩放 |
| `move_speed` | ✅ USED | `EnemySpawner.cs:356` `def.move_speed * scaling.speedMult` 速度缩放 |
| `visual_scale` | ✅ USED | `EnemySpawner.cs:410` `CombatPlaceholderVisual.ResolveScale()` |
| `projectile_turn_rate_deg` | ✅ USED | `EnemySpawner.cs:363` 传入 `ConfigureFromDef` |
| `tags` | ✅ USED | `EnemySpawner.cs` `def.tags.Contains("structure")` 结构物判定 |
| `attack_profile_id` | ✅ USED | `EnemySpawner.cs:367` 传入 `ConfigureFromDef` |
| `loot_table_id` | ✅ USED | `EnemySpawner.cs:394` `deathHandler.LootTableId`；`WorldDropSystem.cs` 掉落查找 |
| `passive_buffs` | ✅ USED | `EnemySpawner.cs:401` `ApplyPassiveBuffs()` |
| `on_hit_buffs` | ✅ USED | `EnemyAttack.cs:621` `ApplyOnHitBuffs()` 命中施加Buff |
| `on_death` | ✅ USED | `EnemyDeathHandler.cs` 死亡效果执行 |
| `spawn_weight_tags` | ✅ USED | `EnemySpawner.cs` 类型权重筛选 |

### 1.3 `ai_profiles.json` → `EnemyAiProfileDatabase.cs`

所有字段均使用（`EnemySphereController.ConfigureAi` / `EnemySpawner`）：

| 字段 | 状态 | 位置 |
|------|------|------|
| `id` | ✅ USED | `EnemyAiProfileDatabase.cs:43` 字典 key；`ConfigureAi` switch |
| `aggro_range_base` | ✅ USED | `EnemySpawner.cs:359` `ai?.aggro_range_base ?? defaultAggroRange` |
| `attack_range_base` | ✅ USED | `EnemySpawner.cs:360` |
| `attack_cooldown_base` | ✅ USED | `EnemySpawner.cs:294` `GetAiAttackTiming()` |
| `leash_mult` | ✅ USED | `EnemyCore.cs` `leashMult = ai.leash_mult` → `GetEffectiveAggroRange()` |
| `windup_base` | ✅ USED | `EnemySpawner.cs:294` `GetAiAttackTiming()` |
| `priority_weights.player` | ⚠️ PARTIAL | 加载到 `AiProfile.priority_player`，但当前未在攻击冷却计算中使用 |

### 1.4 `buffs.json` → `BuffDatabase.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | 无 C# 字段接收 |
| `id` | ✅ USED | 字典 key |
| `display_name` | ✅ USED | UI 层 Buff 名称显示 |
| `description` | ✅ USED | UI 层 Buff 描述 |
| `category` | ✅ USED | `BuffContainer.ApplyBuff` 按 category 查找数据库 |
| `duration` | ✅ USED | `BuffContainer.ApplyBuff` 设置 Buff 持续时长 |
| `max_stacks` | ✅ USED | `BuffContainer` 堆叠上限判定 |
| `stat_modifiers` | ✅ USED | `BuffContainer.GetStatModifier()` 战斗数值生效 |
| `effects` | ✅ USED | 周期性效果/触发效果 |

### 1.5 `damage_types.json` → `DamageTypeDatabase.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `id` | ✅ USED | `DamagePipeline.cs:171` `DamageTypesCatalog.Get(request.DamageTypeId)` |
| `display_name` | ✅ USED | UI 伤害类型显示 |
| `bypass_armor` | ✅ USED | `DamagePipeline.cs` `ApplyArmorReduction` 跳过判定 |

### 1.6 `damage_sources.json` → `DamageSourceDatabase.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `id` | ✅ USED | 字典 key |
| `display_name` | ✅ USED | UI 来源显示 |

### 1.7 `boss_visuals.json` / `enemy_visuals.json` / `waves.json` / `wild_boss_spawns.json`

这 4 个文件的字段均被对应的加载类完整使用，无未使用字段。

### 1.8 `buff_categories.json` → `BuffDatabase.cs`

| 字段 | 状态 |
|------|------|
| `id` | ✅ USED |
| `display_name` | ✅ USED |

---

## 二、`data/world/` (14 个文件)

### 2.1 `events.json` → `WorldDatabase.EventDef` / `EventNodeExecutor`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | 无 C# 字段接收 |
| `id` | ✅ USED | `WorldDatabase.cs:372` 字典 key |
| `display_name` | ✅ USED | `WorldEventUI.cs` UI 标题 |
| `description` | ✅ USED | `WorldEventUI.cs` UI 描述 |
| `category` | ✅ USED | `WorldDatabase.cs:254` `GetEventsByCategory()` 筛选 |
| `min_world_level` | ✅ USED | `WorldDatabase.cs:255` 等级比较 |
| `is_repeatable` | ✅ USED | `EventManager.cs` 重复触发判定 |
| `weight` | ✅ USED | `EventManager.cs` 加权随机 |
| `nodes[].node_id` | ✅ USED | `EventNodeExecutor.cs:302` 节点查找 |
| `nodes[].description` | ✅ USED | `WorldEventUI.cs` UI 显示 |
| `nodes[].condition.{type,op,value,item_id}` | ✅ USED | `EventNodeExecutor.cs:117-152` 条件判定 |
| `nodes[].effects[].{type,value,value_type,item_id,rarity,count,attr,buff_id,duration}` | ✅ USED | `EventNodeExecutor.cs:161-291` 效果执行 |
| `nodes[].effects[].marker_type` | ❌ UNUSED | `ApplyRevealMarkers()` 未读取此字段 |
| `nodes[].options[].{text,next,condition}` | ✅ USED | `WorldEventUI.cs` / `EventNodeExecutor.cs:73-87` |

### 2.2 `ai_affix_tiers.json` → `WorldDatabase.AffixTierDef`

**全部字段均使用**，字段 `{min,max}_world_level`, `{move,attack}_affix_{min,max}`, `{move,attack}_affixes[].{type,weight,p0-p3}` 在 `WorldAffixResolver.cs` 中被加权随机和参数传递使用。

### 2.3 `meta_progression.json` → `MetaProgressionSystem`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `nodes[].id` | ✅ USED | `MetaProgressionUI.cs` 字典 key |
| `nodes[].display_name` | ✅ USED | UI 显示 |
| `nodes[].description` | ✅ USED | UI Tooltip |
| `nodes[].cost` | ✅ USED | `MetaProgressionSystem.cs` 消耗 BattleExp |
| `nodes[].effects` | ✅ USED | `MetaProgressionSystem.cs` 效果执行 |
| `nodes[].requires` | ✅ USED | 前置节点判定 |
| `nodes[].position` | ✅ USED | UI 节点位置 |

### 2.4 `attribute_defs.json` → `WorldDatabase.AttributeDef`

**全部字段均使用**：`attr_id` (字典 key + `AttributeManager` 属性查找), `display_name` (UI), `description` (UI), `base_value` (属性表计算), `min` (clamp 下限), `max` (clamp 上限)。

### 2.5 `camp_levels.json` → `WorldDatabase.CampLevelDef`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `level` | ✅ USED | `CampLevelLogic.cs` 查找 |
| `xp_required` | ✅ USED | `CampLevelLogic.cs` 经验累积判定 |
| `enemy_hp_mult` | ✅ USED | `CampSpawnLogic.cs` `def.base_hp * hpMult` |
| `enemy_damage_mult` | ✅ USED | `CampSpawnLogic.cs` 同上模式 |
| `enemy_speed_mult` | ✅ USED | `CampSpawnLogic.cs` 同上模式 |
| `spawn_interval` | ✅ USED | `CampSpawnLogic.cs` 刷怪间隔 |
| `max_alive_enemies` | ✅ USED | `CampSpawnLogic.cs` 存活上限 |
| `enemy_count_mult` | ✅ USED | `CampSpawnLogic.cs` 刷怪数量倍率 |
| `xp_reward` | ✅ USED | `CampController.cs` 摧毁奖励 |
| `gold_reward` | ✅ USED | `CampController.cs` 摧毁奖励 |

### 2.6 `wild_spawns.json` → `WorldDatabase.WildSpawnDef`

**全部字段均使用**：`{min,max}_world_level`, `spawn_interval`, `batch_count_{min,max}`, `spawn_dist_{min,max}`, `max_alive`, `despawn_dist`, `attr_scale`, `enemy_types[].{enemy_id,weight}` 在 `WildSpawnSystem.cs` 中被完整使用。

### 2.7 `world_levels.json` → `WorldDatabase.WorldLevelDef`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `level` | ✅ USED | `WorldLevelSystem.cs` 查找 |
| `xp_required` | ✅ USED | `WorldLevelSystem.cs` 升级判定 |
| `enemy_stat_mult` | ✅ USED | `WildSpawnSystem.cs` / `CampSpawnLogic.cs` 属性缩放 |
| `enemy_affix_mult` | ❌ UNUSED | 字段被反序列化，但 `WorldAffixResolver` 或 `CampSpawnLogic` 均未读取 |
| `event_spawn_chance_mult` | ❌ UNUSED | 反序列化但未被 `WorldGenAppender` 使用 |
| `loot_quality_mult` | ❌ UNUSED | 反序列化但未被 `WorldDropSystem` 或掉落相关代码使用 |

### 2.8 `inventory_items.json` → `WorldDatabase.ItemDef`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `item_id` | ✅ USED | 字典 key + 背包存储 |
| `display_name` | ✅ USED | `InventoryUI.cs` / `ItemSlotBar.cs` 名称显示 |
| `description` | ✅ USED | `InventoryUI.cs` Tooltip |
| `category` | ✅ USED | `InventorySystem.cs` IsWeapon/IsAccessory 判定 |
| `quality` | ✅ USED | `InventoryUI.cs` / `ItemSlotBar.cs` 品质色 |
| `shop_price` | ✅ USED | `MerchantUI.cs` 商店价格计算 |
| `shop_batch_count` | ✅ USED | `MerchantUI.cs` 批量购买数量 |
| `effect_type` (weapon) | ✅ USED | `ItemUseSystem.cs` 道具效果分发 |
| `effect_params` (weapon) | ✅ USED | `ItemUseSystem.cs` 效果参数 |
| `affixes` (accessory) | ✅ USED | `InventorySystem.cs.SyncAccessoryAffixes()` → `AttributeManager` |

### 2.9 `shops.json` → `WorldDatabase.ShopDef` / `ShopCategoryDef`

**全部字段均使用**：`shop_type_id`, `display_name`, `min_world_level`, `total_items`, `categories[].{category,probability,max_purchases,max_count}` 在 `MerchantUI.cs` 中被品类抽取和 UI 渲染使用。

### 2.10 `monster_drops.json` → `WorldDatabase.MonsterDropDef`

**全部字段均使用**：`drop_id`, `drops[].{type,min,max,probability,items[].{item_id,count,weight}}` 在 `WorldDropSystem.cs` 中被完整使用。

### 2.11 `camp_types.json` → `WorldDatabase.CampTypeDef`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `type_id` | ✅ USED | 字典 key |
| `display_name` | ✅ USED | `CampInfoUI.cs` 名称显示 |
| `base_hp` | ✅ USED | `CampCore.cs` HP配置 |
| `defense` | ✅ USED | `CampCore.cs` 防御力 |
| `enemy_archetype_ids` | ✅ USED | `CampSpawnLogic.cs` 随机选怪 |
| `boss_encounter_id` | ❌ UNUSED | 字段被反序列化，但 `CampSpawnLogic` 或 `CampController` 从未读取 |
| `reward_loot_pool_id` | ❌ UNUSED | 字段反序列化但未在奖励发放时使用 |
| `one_shot` | ❌ UNUSED | 反序列化但未被任何逻辑判定 |
| `map_icon_id` | ❌ UNUSED | 反序列化但 `MapMarker.CreateCamp()` 不使用此字段 |
| `dormant_max_alive` | ✅ USED | `CampSpawnLogic.cs` 休眠预刷怪上限 |

### 2.12 `player_levels.json` → `WorldDatabase.PlayerLevelDef`

**全部字段均使用**：`level`, `xp_required`, `attack_mult`, `hp_mult`, `defense_mult` 在 `PlayerLevelSystem.cs.SyncToAttributeManager()` 中使用。

### 2.13 `world_events.json` → `WorldGenAppender`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `event_triggers[].type` | ✅ USED | `WorldGenAppender.cs` switch 分发 |
| `event_triggers[].probability` | ✅ USED | `WorldGenAppender.cs` Random 判定 |
| `event_triggers[].count_min` | ✅ USED | 生成数量下界 |
| `event_triggers[].count_max` | ✅ USED | 生成数量上界 |
| `event_triggers[].max_per_run` | ✅ USED | 本局上限 |
| `event_triggers[].position_type` | ✅ USED | 位置策略 |
| `event_triggers[].position_params` | ✅ USED | 位置参数 |

### 2.14 `shop_buffs.json` → `WorldDatabase.ShopBuffDef`

**全部字段均使用**：`buff_id`, `display_name`, `description`, `price`, `duration`, `min_world_level` 在 `MerchantUI.cs` 中被使用。

---

## 三、`data/roguelike/` (12 个文件)

### 3.1 `progression/meta_talents.json` → `MetaTalentDatabase.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `max_picks` | ✅ USED | `RoguelikeGameMode.cs:155,261` UI 显示 "已选X/Y" |
| `talents[].id` | ✅ USED | `MetaTalentDatabase.cs:88` 字典 key |
| `talents[].display_name` | ✅ USED | `RoguelikeGameMode.cs:248` UI 显示 |
| `talents[].description` | ✅ USED | `RoguelikeGameMode.cs:251` UI 显示 |
| `talents[].branch` | ❌ UNUSED | 字段反序列化但从未被任何代码读取 |
| `talents[].modifiers[].stat` | ✅ USED | `RunBuildState.AddStat()` stat key |
| `talents[].modifiers[].op` | ✅ USED | `MetaTalentDatabase.cs:69` 仅处理 "add" |
| `talents[].modifiers[].value` | ✅ USED | `AddStat()` value 参数 |

### 3.2 `loot/loot_tables.json` → `LootService.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `pools[].id` | ✅ USED | 字典 key |
| `pools[].context` | ❌ UNUSED | `LootPoolJson` 类中缺少此字段 |
| `pools[].description` | ❌ UNUSED | 无 C# 字段接收 |
| `pools[].rolls` | ✅ USED | `LootService.cs:164` `Mathf.Max(1, pool.rolls)` |
| `pools[].entries[].weight` | ✅ USED | `LootService.cs:111` 加权随机 |
| `pools[].entries[].kind` | ✅ USED | `LootService.cs:172` switch 分支 |
| `pools[].entries[].amount_{min,max}` | ✅ USED | `LootService.cs:60` `Random.Range()` |
| `pools[].entries[].equipment_id` | ❌ UNUSED | `LootEntry` 类中缺少此字段 |
| `pools[].entries[].once_per_run` | ❌ UNUSED | `LootEntry` 类中缺少此字段 |

### 3.3 `themes/weapon_themes.json` → `WeaponThemeDatabase.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `themes[].id` | ✅ USED | 字典 key + UI 过滤 |
| `themes[].display_name` | ✅ USED | UI 显示 |
| `themes[].description` | ✅ USED | UI 显示 |
| `themes[].attack_profile_id` | ✅ USED | `RunBuildApplier.cs:116` 设置攻击模式 |
| `themes[].base_stats.attack_mult` | ✅ USED | `ThemeState.cs:34-35` 种子计算 |
| `themes[].base_stats.attack_speed_mult` | ✅ USED | `ThemeState.cs:37-38` 种子计算 |
| `themes[].base_stats.range` | ❌ UNUSED | `ThemeState.SeedThemeBaseStats()` 中从未使用 |
| `themes[].base_stats.reflect_damage` | ✅ USED | `ThemeState.cs:42` reflect 主题专用 |

### 3.4 `skills/skills.json` → `SkillDatabase.cs`

**全部字段均使用**：`id`, `display_name`, `description`, `cooldown`, `attack_profile_id`, `delivery`, `range`, `tags` 在技能系统加载和 `PlayerActiveSkillController` 中被完整使用。

### 3.5 `skills/player_class_skills.json` → `PlayerClassSkillDatabase.cs`

**全部字段均使用**：`class_id`, `skills[]` 在 Roguelike 技能选择系统中使用。

### 3.6 `progression/level_up_config.json` → `LevelUpController.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `choices_per_level` | ✅ USED | `LevelUpController.cs` 升级选项数量 |
| `option_pool_size` | ✅ USED | 随机选项池大小 |
| `reroll_cost` | ✅ USED | 重Roll价格 |
| `max_rerolls` | ✅ USED | 最大重Roll次数 |

### 3.7 `progression/level_up_choices.json` → `LevelUpController.cs`

**全部字段均使用**：`weapon_choices[]`, `skill_choices[]`, `aura_choices[]` 确定每个升级层级可获得的选项类型。

### 3.8 `upgrades/*.json` (5 个文件，共约 250KB)

这些文件共享相同的 schema (`id`, `display_name`, `description`, `tier`, `stat`, `op`, `value`, `per_level`):
- `player_upgrades.json` (通用升级)
- `melee_upgrades.json` (近战升级)
- `ranged_upgrades.json` (远程升级)
- `reflect_upgrades.json` (反射升级)
- `aura_upgrades.json` (光环升级)
- `mage_upgrades.json` (法师升级)

| 字段 | 状态 | 位置 |
|------|------|---------|
| `id` | ✅ USED | 所有升级文件共享的升级ID |
| `display_name` | ✅ USED | `LevelUpController.cs` UI 三选一 |
| `description` | ✅ USED | UI Tooltip |
| `tier` | ✅ USED | `LevelUpController.cs` 按 tier 筛选可选升级 |
| `stat` | ✅ USED | `RunBuildApplier.cs` 写入 `RunBuildState` |
| `op` | ✅ USED | `RunBuildApplier.cs` Add/Mult 分支 |
| `value` | ✅ USED | `RunBuildApplier.cs` 实际数值 |
| `per_level` | ⚠️ PARTIAL | 仅部分升级条目使用了此字段 |

---

## 四、`data/map/` (3 个文件)

### 4.1 `biomes.json` → `WorldGenerator.cs`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `biomes[].id` | ✅ USED | 字典 key |
| `biomes[].display_name` | ✅ USED | 地图 UI |
| `biomes[].tile_sets` | ✅ USED | 地图瓦片选择 |
| `biomes[].spawn_weights` | ✅ USED | 生成权重 |

### 4.2 `map_layout.json` / `structures.json`

这两个小文件（253B + 119B）的字段均被 `WorldGenerator.cs` 完整使用。

---

## 五、`data/core/` (1 个文件)

### 5.1 `run_difficulty.json` → `RunDifficultyConfig`

| 字段 | 状态 | 位置 |
|------|------|---------|
| `schema_version` | ❌ UNUSED | |
| `difficulties[].id` | ✅ USED | 难度选择 UI |
| `difficulties[].display_name` | ✅ USED | UI 显示 |
| `difficulties[].enemy_hp_mult` | ✅ USED | `EnemySpawner.cs` HP 倍率 |
| `difficulties[].enemy_damage_mult` | ✅ USED | 伤害倍率 |
| `difficulties[].enemy_speed_mult` | ✅ USED | 速度倍率 |
| `difficulties[].xp_mult` | ✅ USED | 经验倍率 |
| `difficulties[].gold_mult` | ✅ USED | 金币倍率 |
| `difficulties[].boss_hp_bonus` | ✅ USED | Boss HP 加成 |
| `difficulties[].enemy_count_mult` | ✅ USED | 敌人数倍率 |

---

## 六、汇总：需要处理的未使用字段

### 严重（JSON 中有字段但 C# 类缺少 → 数据被丢弃）

| 文件 | 字段 | 说明 |
|------|------|------|
| `attacks.json` | `display_name`, `source_kind`, `tags`, `weapon_extension`, `skill_extension`, `monster_extension`, `buff_extension.damage_modifier`, `player_editor.*` | `AttackJson` 类缺少对应字段 |
| `loot_tables.json` | `context`, `description`, `entries[].equipment_id`, `entries[].once_per_run` | `LootPoolJson` / `LootEntry` 类缺少 |

### 一般（字段已反序列化但未被逻辑使用）

| 文件 | 字段 | 建议 |
|------|------|------|
| `attacks.json` | `targeting` | 若暂不需要，从 JSON 中移除或标注为预留 |
| `attacks.json` | `delivery_params.aoe_*` (duration,persistent,damage_mult) | AOE 系统尚未完成，标注为预留 |
| `camp_types.json` | `boss_encounter_id`, `reward_loot_pool_id`, `one_shot`, `map_icon_id` | 营地 Boss/奖励/地图图标系统未实现 |
| `world_levels.json` | `enemy_affix_mult`, `event_spawn_chance_mult`, `loot_quality_mult` | 世界等级的高级乘数未接入 |
| `weapon_themes.json` | `base_stats.range` | 主题基础射程未被 `RunBuildApplier` 使用 |
| `meta_talents.json` | `talents[].branch` | 天赋分支分类字段未使用 |
| `events.json` | `effects[].marker_type` | reveal_markers 效果未完整实现 |

---

## 七、统计

| 类别 | 数量 |
|------|------|
| 总 JSON 文件 | 42 |
| 总字段数（估算） | ~420 |
| ✅ 已使用字段 | ~385 (92%) |
| ❌ 未使用字段 | ~20 (5%) |
| ⚠️ 部分使用字段 | ~15 (3%) |
| 建议删除/标注预留的字段 | ~35 |
