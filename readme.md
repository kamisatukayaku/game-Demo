# Geometry Roguelike Game Demo

一款使用 Unity 与 C# 开发的几何风 Roguelike 动作游戏，也是一次完整的 AI Vibe Coding 实践项目。

## 项目简介

玩家需要在环形竞技场中完成 15 波战斗，通过升级选择逐步构建武器流派，并应对近战、射击、激光、冲刺、高速追击及 Boss 等不同敌人。

项目已形成完整可玩闭环，包括模式选择、战斗、成长、武器进化、Boss 奖励、失败结算和通关界面。

## Vibe Coding 工作流

项目采用“开发者负责目标与验收，AI 负责辅助实现与验证”的协作方式：

- 开发者负责玩法方向、需求拆解、数值节奏、交互取舍与最终验收。
- 使用 Codex 辅助检索大型 Unity 工程、实现跨文件功能、定位运行时问题及执行自动化检查。
- 通过持续试玩反馈修正敌人节奏、天赋前置、碰撞体、拖尾表现和战斗性能。
- 使用编译检查、JSON 校验与 Unity PlayMode 流程降低 AI 生成代码的集成风险。

## 核心内容

- 15 波环形竞技场与 Boss 连战模式。
- 数据驱动的敌人生态、波次权重和难度成长。
- 穿透、分裂、爆炸、激光、飞弹、脉冲、回旋、轨迹及外置武器机制。
- 节点式天赋成长、前置条件校验和多阶段武器进化。
- 与几何外形匹配的多边形受击体及完整命中反馈。
- 分组持续刷怪与高频命中场景性能优化。
- 升级选择、战斗 HUD、Boss 奖励、失败及通关结算 UI。

## 技术栈

- Unity 2022.3 LTS
- C# / UGUI / 2D Physics
- JSON 数据驱动配置
- Unity Test Framework / PlayMode Validation
- Codex / AI Vibe Coding

## 运行方式

1. 使用 Unity `2022.3.62f3c1` 或兼容的 Unity 2022.3 LTS 版本打开 `game/`。
2. 等待 Package Manager 完成依赖恢复。
3. 打开 `Assets/Scenes/MainScene.unity` 并进入 Play Mode。

项目源数据位于 `data/`，Unity 运行时资源位于 `game/Assets/Resources/Data/`。
