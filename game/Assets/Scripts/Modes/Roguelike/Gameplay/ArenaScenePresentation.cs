using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

using Game.Modes.Roguelike.Combat;
using Game.Shared.Core;
using Game.Shared.Player;
using Game.Shared.Runtime;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.Gameplay
{
  /// <summary>MainScene 进局后修正相机、2D 光照与 Sprite 材质，避免白屏/空场景。</summary>
  public static class ArenaScenePresentation
  {
    static readonly Color GameplayBackground = new(0.12f, 0.13f, 0.18f, 1f);

    static bool s_applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void HookSceneUnload() => SceneManager.sceneUnloaded += OnSceneUnloaded;

    static void OnSceneUnloaded(Scene scene)
    {
      if (scene.name == "MainScene")
        s_applied = false;
    }

    public static void Apply(GameObject player = null)
    {
      if (GameSessionConfig.SelectedMode != GameSessionConfig.GameMode.Arena)
        return;

      if (player == null)
        player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");

      if (!s_applied)
      {
        FixMainCamera(player);
        FixUrpCamera(Camera.main);
        DisableBrokenGlobalLight2D();
        GameSceneTransitionCurtain.DismissIfVisible();
        s_applied = true;
      }

      FixAllSpriteMaterials();
      if (player != null)
        FixPlayerSprites(player);
    }

    static void FixMainCamera(GameObject player)
    {
      var camera = Camera.main;
      if (camera == null)
        return;

      camera.enabled = true;
      camera.orthographic = true;
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = GameplayBackground;
      camera.allowHDR = false;
      camera.depth = -1;
      CameraFollow2D.ApplyGameplayZoom(camera);

      var follow = camera.GetComponent<CameraFollow2D>();
      if (player != null)
      {
        if (follow != null)
          follow.SetTarget(player.transform);

        camera.transform.position = player.transform.position + new Vector3(0f, 0f, -10f);
      }
    }

    static void FixUrpCamera(Camera camera)
    {
      if (camera == null)
        return;

      var urp = camera.GetComponent<UniversalAdditionalCameraData>();
      if (urp == null)
        urp = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();

      urp.renderType = CameraRenderType.Base;
      urp.renderPostProcessing = false;
      urp.antialiasing = AntialiasingMode.None;
      urp.allowHDROutput = false;
      urp.SetRenderer(0);
    }

    static void DisableBrokenGlobalLight2D()
    {
      var globalLight = GameObject.Find("Global Light 2D");
      if (globalLight != null)
        globalLight.SetActive(false);
    }

    static void FixAllSpriteMaterials()
    {
      foreach (var renderer in Object.FindObjectsOfType<SpriteRenderer>())
        SpriteMaterialUtility.ApplyUnlit(renderer);
    }

    static void FixPlayerSprites(GameObject player)
    {
      if (player == null)
        return;

      var visual = player.GetComponent<PlayerSpriteVisual>();
      if (visual == null)
        PlayerSpriteVisual.EnsureOnPlayer(player);

      foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
        SpriteMaterialUtility.ApplyUnlit(renderer);

      var meshRenderer = player.transform.Find("Visual")?.GetComponent<MeshRenderer>();
      if (meshRenderer != null)
        meshRenderer.enabled = false;
    }
  }
}
