# 激光 VFX（Shader Graph）

## 快速设置

Unity 菜单：**Game → Setup Laser VFX (Shader Graph)**

首次打开项目会自动生成 `Resources/VFX/Laser/*.mat`；若粒子仍不可见，请手动执行上述菜单。

## Shader Graph 文件

| 文件 | 用途 |
|------|------|
| `LaserParticleAdditive.shadergraph` | 方形蓄力/沿束碎片粒子 |
| `LaserBeamAdditive.shadergraph` | 光束本体（可在 Graph 中改为 Unlit Transparent Additive） |

在 Shader Graph 中建议设置：

- **Surface**：Transparent
- **Blend**：Additive
- **Render Face**：Both
- 粒子：Texture × Color，可选 Fresnel 增强边缘

编译后材质会自动被 `LaserVfxShared` 通过 `Shader Graphs/LaserParticleAdditive` 名称加载。

## 运行时材质

- `Assets/Resources/VFX/Laser/LaserParticle.mat` — 粒子
- `Assets/Resources/VFX/Laser/LaserBeam.mat` — 光束 Sprite

## 外部素材（可选）

将 PNG 放入 `Assets/Resources/VFX/Laser/`：

- `square_particle.png` — 方形粒子（8×8 白块即可）
- `beam_gradient.png` — 横向渐变（左亮右暗，橙→透明）

## 找现成素材

- [Unity Asset Store](https://assetstore.unity.com/)：`2D laser VFX`, `sci-fi beam`, `particle additive`
- [Kenney Particles](https://kenney.nl/assets/particle-pack)
- [itch.io](https://itch.io/game-assets/free/tag-vfx)：`laser beam sprite sheet`
