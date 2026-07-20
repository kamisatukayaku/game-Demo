using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

using Game.Shared.Core;
using Game.Shared.Player;
using Game.Shared.UI;

namespace Game.Modes.Roguelike.Gameplay
{
  /// <summary>MainScene 加载后立即修正遗留 3D Visual、相机与遮罩，并输出诊断日志。</summary>
  public static class MainSceneStartupFix
  {
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
      var scene = SceneManager.GetActiveScene();
      if (scene.name != "MainScene")
        return;

      GameSceneTransitionCurtain.DismissIfVisible();
      FixCamera();
      FixPlayerVisual();
      LogOverlayCanvases();
    }

    static void FixCamera()
    {
      var camera = Camera.main;
      if (camera == null)
      {
        Debug.LogError("[MainSceneStartupFix] Camera.main is null.");
        return;
      }

      camera.enabled = true;
      camera.orthographic = true;
      camera.clearFlags = CameraClearFlags.SolidColor;
      camera.backgroundColor = new Color(0.12f, 0.13f, 0.18f, 1f);
      camera.allowHDR = false;
      camera.depth = -1;
      CameraFollow2D.ApplyGameplayZoom(camera);

      var urp = camera.GetComponent<UniversalAdditionalCameraData>();
      if (urp == null)
        urp = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
      urp.renderType = CameraRenderType.Base;
      urp.renderPostProcessing = false;
      urp.antialiasing = AntialiasingMode.None;
      urp.allowHDROutput = false;
      urp.SetRenderer(0);

      Debug.Log(
        $"[MainSceneStartupFix] Camera ready pos={camera.transform.position} size={camera.orthographicSize} bg={camera.backgroundColor}");
    }

    static void FixPlayerVisual()
    {
      var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player");
      if (player == null)
      {
        Debug.LogError("[MainSceneStartupFix] Player not found.");
        return;
      }

      foreach (var meshRenderer in player.GetComponentsInChildren<MeshRenderer>(true))
        meshRenderer.enabled = false;

      PlayerSpriteVisual.EnsureOnPlayer(player);

      foreach (var renderer in Object.FindObjectsOfType<SpriteRenderer>())
        SpriteMaterialUtility.ApplyUnlit(renderer);

      Debug.Log("[MainSceneStartupFix] Player legacy mesh disabled; sprites refreshed.");
    }

    static void LogOverlayCanvases()
    {
      foreach (var canvas in Object.FindObjectsOfType<Canvas>())
      {
        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay)
          continue;

        Debug.Log(
          $"[MainSceneStartupFix] Overlay canvas '{canvas.name}' order={canvas.sortingOrder} active={canvas.gameObject.activeInHierarchy}");
      }
    }
  }
}
