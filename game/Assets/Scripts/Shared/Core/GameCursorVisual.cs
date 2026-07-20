using UnityEngine.UI;
using UnityEngine;

namespace Game.Shared.Core
{
  /// <summary>游戏内自定义鼠标指针（蓝白色，偏白）?/summary>
  public class GameCursorVisual : MonoBehaviour
  {
    const string CursorResourcePath = "UI/game_cursor";

    static GameCursorVisual s_instance;

    RectTransform _cursorRt;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_GameCursor");
      DontDestroyOnLoad(go);
      go.AddComponent<GameCursorVisual>();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      BuildCursor();
      Cursor.visible = false;
    }

    void OnDestroy()
    {
      if (s_instance == this)
      {
        s_instance = null;
        Cursor.visible = true;
      }
    }

    void Update()
    {
      if (_cursorRt != null)
        _cursorRt.position = Input.mousePosition;
    }

    void BuildCursor()
    {
      var canvasGo = new GameObject("CursorCanvas");
      canvasGo.transform.SetParent(transform, false);

      var canvas = canvasGo.AddComponent<Canvas>();
      var scaler = canvasGo.AddComponent<CanvasScaler>();
      UiFontHelper.ConfigureCanvas(canvas, scaler, 5000);
      canvasGo.AddComponent<GraphicRaycaster>().enabled = false;

      var go = new GameObject("CursorImage", typeof(RectTransform));
      go.transform.SetParent(canvasGo.transform, false);
      _cursorRt = go.GetComponent<RectTransform>();
      _cursorRt.sizeDelta = new Vector2(28f, 28f);
      _cursorRt.pivot = new Vector2(0.5f, 0.5f);

      var img = go.AddComponent<Image>();
      img.raycastTarget = false;
      img.sprite = LoadCursorSprite();
      img.color = Color.white;
    }

    static Sprite LoadCursorSprite()
    {
      var tex = Resources.Load<Texture2D>(CursorResourcePath);
      if (tex == null)
        tex = CreateFallbackCursorTexture();

      return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);
    }

    static Texture2D CreateFallbackCursorTexture()
    {
      const int size = 32;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
      {
        filterMode = FilterMode.Bilinear,
        wrapMode = TextureWrapMode.Clamp
      };

      var cx = size * 0.5f;
      var cy = size * 0.5f;
      var pixels = new Color32[size * size];

      for (int y = 0; y < size; y++)
      for (int x = 0; x < size; x++)
      {
        var dx = x - cx;
        var dy = y - cy;
        var dist = Mathf.Sqrt(dx * dx + dy * dy);
        byte a = 0;

        if (dist <= 5.5f)
          a = 255;
        else if (dist <= 8f)
          a = (byte)Mathf.Lerp(180, 0, (dist - 5.5f) / 2.5f);

        pixels[y * size + x] = new Color32(230, 245, 255, a);
      }

      tex.SetPixels32(pixels);
      tex.Apply();
      return tex;
    }
  }
}