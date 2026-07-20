using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// 交互提示 — 玩家靠近交互点时显示 "按F打开商店"/"按F触发事件"。
  /// </summary>
  public class InteractionHint : MonoBehaviour
  {
    static InteractionHint s_instance;

    Text _text;
    float _checkTimer;
    const float CheckInterval = 0.3f;
    const float HintRange = 10f;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_InteractionHint");
      DontDestroyOnLoad(go);
      go.AddComponent<InteractionHint>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;

      var canvasGo = new GameObject("Canvas", typeof(RectTransform));
      canvasGo.transform.SetParent(transform, false);
      var canvas = canvasGo.AddComponent<Canvas>();
      canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      canvas.sortingOrder = 200;
      canvasGo.AddComponent<GraphicRaycaster>();

      var go = new GameObject("Text", typeof(RectTransform));
      go.transform.SetParent(canvasGo.transform, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0.5f, 0.65f); rt.anchorMax = new Vector2(0.5f, 0.65f);
      rt.sizeDelta = new Vector2(400, 36);

      _text = go.AddComponent<Text>();
      _text.font = Game.Shared.Core.UiFontHelper.GetFont();
      _text.fontSize = 18;
      _text.fontStyle = FontStyle.Bold;
      _text.alignment = TextAnchor.MiddleCenter;
      _text.color = new Color(1f, 1f, 1f, 0f);
      _text.raycastTarget = false;
    }

    void OnDestroy() { if (s_instance == this) s_instance = null; }

    void Update()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) { _text.color = new Color(1f,1f,1f,0f); return; }

      _checkTimer += Time.deltaTime;
      if (_checkTimer < CheckInterval) return;
      _checkTimer = 0f;

      var player = GameObject.FindGameObjectWithTag("Player")?.transform;
      if (player == null) { _text.color = new Color(1f,1f,1f,0f); return; }
      var playerPos = (Vector2)player.position;

      var mapMgr = WorldManager.Instance?.GetSystem<WorldMapManager>();
      if (mapMgr == null) return;

      // 检查商人
      var merchants = mapMgr.GetMarkersByType(MapMarker.MarkerType.Merchant);
      for (int i = 0; i < merchants.Count; i++)
      {
        var m = merchants[i];
        if (m.State == MapMarker.DiscoveryState.Destroyed) continue;
        if (Vector2.Distance(playerPos, m.WorldPosition) <= HintRange)
        {
          _text.text = "按 F 打开商店";
          _text.color = new Color(1f, 0.84f, 0.25f, 0.9f);
          return;
        }
      }

      // 检查事件点
      var events = mapMgr.GetMarkersByType(MapMarker.MarkerType.EventPoint);
      for (int i = 0; i < events.Count; i++)
      {
        var e = events[i];
        if (e.State == MapMarker.DiscoveryState.Destroyed) continue;
        if (Vector2.Distance(playerPos, e.WorldPosition) <= HintRange)
        {
          _text.text = "按 F 触发事件";
          _text.color = new Color(0.6f, 0.85f, 1f, 0.9f);
          return;
        }
      }

      _text.color = new Color(1f,1f,1f,0f);
    }
  }
}
