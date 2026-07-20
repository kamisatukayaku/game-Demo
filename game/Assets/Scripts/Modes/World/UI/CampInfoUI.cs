using System.Collections.Generic;
using Game.Shared.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Game.World
{
  /// <summary>
  /// 营地悬停信息框 — 鼠标移到营地附近时弹出信息面板。
  /// 检测玩家屏幕坐标与营地世界坐标的距离，在 3 单位内时触发。
  /// </summary>
  public class CampInfoUI : MonoBehaviour
  {
    static CampInfoUI s_instance;

    const float HoverWorldRadius = 3f;
    const float InfoWidth = 200f;
    const float InfoHeight = 90f;

    GameObject _tooltipGo;
    Text _titleText;
    Text _infoText;
    bool _visible;

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_CampInfoUI");
      DontDestroyOnLoad(go);
      go.AddComponent<CampInfoUI>();
    }

    void Awake()
    {
      if (s_instance != null) { Destroy(gameObject); return; }
      s_instance = this;
      Build();
    }

    void OnDestroy() { if (s_instance == this) s_instance = null; }

    void Build()
    {
      var font = UiFontHelper.GetFont();
      _tooltipGo = new GameObject("CampTooltip", typeof(RectTransform));
      _tooltipGo.transform.SetParent(transform, false);
      _tooltipGo.SetActive(false);

      var rt = _tooltipGo.GetComponent<RectTransform>();
      rt.pivot = new Vector2(0, 0);
      rt.sizeDelta = new Vector2(InfoWidth, InfoHeight);

      var img = _tooltipGo.AddComponent<Image>();
      img.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

      var outline = _tooltipGo.AddComponent<Outline>();
      outline.effectColor = new Color(0.45f, 0.75f, 1f, 0.6f);
      outline.effectDistance = new Vector2(1f, -1f);

      _titleText = MakeText(_tooltipGo.transform, "Title", "", 14, FontStyle.Bold,
        new Color(0.85f, 0.9f, 1f, 1f), new Vector2(8, -6), new Vector2(InfoWidth - 16, 22));

      _infoText = MakeText(_tooltipGo.transform, "Info", "", 12, FontStyle.Normal,
        new Color(0.65f, 0.75f, 0.85f, 1f), new Vector2(8, -32), new Vector2(InfoWidth - 16, 50));
    }

    Text MakeText(Transform parent, string name, string text, int size, FontStyle style, Color color, Vector2 pos, Vector2 wh)
    {
      var go = new GameObject(name, typeof(RectTransform));
      go.transform.SetParent(parent, false);
      var rt = go.GetComponent<RectTransform>();
      rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
      rt.pivot = new Vector2(0, 1); rt.anchoredPosition = pos; rt.sizeDelta = wh;
      var label = go.AddComponent<Text>();
      label.font = UiFontHelper.GetFont(); label.fontSize = size; label.fontStyle = style;
      label.alignment = TextAnchor.UpperLeft; label.color = color; label.text = text;
      label.raycastTarget = false; label.horizontalOverflow = HorizontalWrapMode.Wrap;
      return label;
    }

    void Update()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) { if (_visible) Hide(); return; }
      if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) DetectHover();
    }

    void DetectHover()
    {
      var cam = Camera.main;
      if (cam == null) { Hide(); return; }

      var mouseScreen = Input.mousePosition;
      var camps = WorldRuntimeContext.Camps;
      if (camps == null || camps.Count == 0) { Hide(); return; }

      foreach (var kv in camps)
      {
        var data = kv.Value;
        if (data.IsDestroyed) continue;

        var worldPos = data.WorldPosition;
        var screenPos = cam.WorldToScreenPoint(new Vector3(worldPos.x, worldPos.y, 0));
        if (screenPos.z < 0) continue;

        var dist = Vector2.Distance(
          new Vector2(mouseScreen.x, mouseScreen.y),
          new Vector2(screenPos.x, screenPos.y));

        if (dist < 60f) // screen-space threshold
        {
          Show(data, screenPos);
          return;
        }
      }

      Hide();
    }

    void Show(WorldCampData data, Vector3 screenPos)
    {
      _visible = true;
      var typeDef = WorldDatabase.GetCampType(data.CampTypeId);
      var displayName = typeDef?.display_name ?? data.CampTypeId ?? "营地";
      _titleText.text = displayName;

      // 构建编码信息
      var enemyIds = data.EnemyArchetypeIds;
      if (enemyIds == null || enemyIds.Length == 0)
        enemyIds = typeDef?.enemy_archetype_ids;
      var enemyStr = enemyIds != null && enemyIds.Length > 0
        ? string.Join(", ", enemyIds)
        : "?";

      _infoText.text = $"等级: {data.CampLevel}\n怪物种类: {enemyStr}\n坐标: ({data.WorldPosition.x:F0}, {data.WorldPosition.y:F0})";
      var rt = _tooltipGo.GetComponent<RectTransform>();
      rt.position = new Vector3(screenPos.x + 20, screenPos.y, 0);
      _tooltipGo.SetActive(true);
    }

    void Hide()
    {
      if (!_visible) return;
      _visible = false;
      _tooltipGo.SetActive(false);
    }
  }
}
