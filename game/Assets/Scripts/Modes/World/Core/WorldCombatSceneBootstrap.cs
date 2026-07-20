using Game.Shared.Core;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Player;
using Game.Shared.UI;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// World 模式的 ICombatSceneBootstrap 实现。
  ///
  /// 在 Explore 模式下由 WorldGameMode 注册到 CombatSceneBootstrapLocator，
  /// MainScene 加载后由 CombatRoot 自动调用 InitializeCombatSystems()。
  ///
  /// 职责：
  ///   1. 实例化 WorldManager 并调用 InitWorld()（地图生成 + 全部 WorldSystem 创建）
  ///   2. 创建地图边界碰撞箱
  ///   3. 确保 World 模式 UI（HUD / 地图 / 营地悬停 / 事件 / 商人）存在
  ///   4. 相机缩放
  /// </summary>
  public sealed class WorldCombatSceneBootstrap : ICombatSceneBootstrap
  {
    public static readonly WorldCombatSceneBootstrap Instance = new();

    public void InitializeCombatSystems()
    {
      // 创建 WorldManager GameObject（如果不存在）
      var wmGo = GameObject.Find("_WorldManager");
      if (wmGo == null)
      {
        wmGo = new GameObject("_WorldManager");
        Object.DontDestroyOnLoad(wmGo);
      }

      var wm = wmGo.GetComponent<WorldManager>();
      if (wm == null)
        wm = wmGo.AddComponent<WorldManager>();

      wm.InitWorld();

      // 边界碰撞箱
      CreateBoundaryColliders(wmGo);

      // UI
      WorldHUD.EnsureExists();
      WorldMapUI.EnsureExists();
      CampInfoUI.EnsureExists();
      WorldEventUI.EnsureExists();
      MerchantUI.EnsureExists();
      InventoryUI.EnsureExists();
      WorldResultsUI.EnsureExists();
      SubtitlePopup.EnsureExists();
      InteractionHint.EnsureExists();
      ItemSlotBar.EnsureExists();

      // 相机
      CameraFollow2D.ApplyGameplayZoom(Camera.main);

      Debug.Log("[WorldCombatSceneBootstrap] World mode initialized.");
    }

    public void ApplyPlayerComponents(GameObject playerGo)
    {
      // 确保 PlayerAttackDirector 存在（WorldPlayerAttackBridge 依赖它）
      if (playerGo.GetComponent<PlayerAttackDirector>() == null)
        playerGo.AddComponent<PlayerAttackDirector>();
      // World 模式玩家攻击桥接（属性表→攻击系统、QE切换模式、R自动攻击）
      if (playerGo.GetComponent<WorldPlayerAttackBridge>() == null)
        playerGo.AddComponent<WorldPlayerAttackBridge>();
      // 道具使用系统（物品栏+投掷/回复/Buff）
      if (playerGo.GetComponent<ItemUseSystem>() == null)
        playerGo.AddComponent<ItemUseSystem>();
    }

    void CreateBoundaryColliders(GameObject parent)
    {
      const float boundsRadius = 512f;
      const float thickness = 2f;

      var boundaryGo = new GameObject("_WorldBoundary");
      boundaryGo.transform.SetParent(parent.transform, false);

      // 四个边界碰撞箱
      CreateBoundaryEdge(boundaryGo.transform, "EdgeTop",
        new Vector2(0, boundsRadius), new Vector2(boundsRadius * 2f + thickness, thickness));
      CreateBoundaryEdge(boundaryGo.transform, "EdgeBottom",
        new Vector2(0, -boundsRadius), new Vector2(boundsRadius * 2f + thickness, thickness));
      CreateBoundaryEdge(boundaryGo.transform, "EdgeLeft",
        new Vector2(-boundsRadius, 0), new Vector2(thickness, boundsRadius * 2f + thickness));
      CreateBoundaryEdge(boundaryGo.transform, "EdgeRight",
        new Vector2(boundsRadius, 0), new Vector2(thickness, boundsRadius * 2f + thickness));
    }

    static void CreateBoundaryEdge(Transform parent, string name, Vector2 center, Vector2 size)
    {
      var go = new GameObject(name);
      go.transform.SetParent(parent, false);
      go.transform.localPosition = new Vector3(center.x, center.y, 0);
      go.layer = LayerMask.NameToLayer("BlockPlayer") >= 0
        ? LayerMask.NameToLayer("BlockPlayer")
        : LayerMask.NameToLayer("Default");

      var bc = go.AddComponent<BoxCollider2D>();
      bc.size = size;
      bc.isTrigger = false;

      var rb = go.AddComponent<Rigidbody2D>();
      rb.bodyType = RigidbodyType2D.Static;
    }
  }
}
