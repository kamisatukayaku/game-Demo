# Data 配置索引

本目录只存放数据配置。分类以“谁拥有这份数据”为准，不按文件大小或临时功能分类。

## 目录结构

```text
data/
├─ combat/                 共享战斗数据
│  ├─ ai_profiles.json
│  ├─ attacks.json
│  ├─ enemies.json
│  ├─ waves.json
│  ├─ buffs.json
│  ├─ buff_categories.json
│  ├─ damage_types.json
│  ├─ damage_sources.json
│  ├─ enemy_visuals.json
│  ├─ boss_visuals.json
│  └─ wild_boss_spawns.json
├─ core/                   跨模式核心规则
│  └─ run_difficulty.json
├─ roguelike/              Ring Arena / Roguelike 专属数据
│  ├─ progression/         升级规则、Meta、开局偏好
│  ├─ skills/              技能目录与技能槽
│  ├─ loot/                Roguelike 掉落池
│  ├─ themes/              旧主题兼容配置
│  ├─ upgrades/            机制升级与数值升级
│  └─ weapons/             脱手武器定义与机制成长线
└─ world/                  Exploration / World 专属数据
```

## Roguelike 子目录

```text
roguelike/
├─ progression/
│  ├─ level_up_config.json
│  ├─ level_up_choices.json
│  ├─ meta_talents.json
│  └─ starter_biases.json
├─ skills/
│  ├─ skills.json
│  └─ player_class_skills.json
├─ loot/
│  └─ loot_tables.json
├─ themes/
│  └─ weapon_themes.json
├─ upgrades/
│  ├─ mage_upgrades.json
│  ├─ ranged_upgrades.json
│  └─ numeric/
└─ weapons/
   ├─ detached_weapons.json
   └─ evolutions/
```

## 分类规则

- `combat/`：怪物、攻击、伤害、Buff、波次及战斗视觉数据。
- `core/`：多个玩法模式共同使用的规则。
- `roguelike/`：Ring Arena 的升级、技能、掉落和脱手武器数据。
- `world/`：探索世界的营地、事件、商店、生成和永久成长数据。
- `weapons/evolutions/` 是机制成长线的数据源，当前保留但暂不接入运行时。
- `upgrades/numeric/` 是数值成长线的数据源，当前保留但暂不接入运行时。

## 路径约定

Roguelike 使用 `JsonDataLoader` 时传入相对 `data/roguelike/` 的无扩展名路径，例如：

```text
progression/level_up_config
skills/player_class_skills
themes/weapon_themes
weapons/detached_weapons
```

World 与 Combat 仍保留现有固定路径，避免无必要地扩大迁移范围。
