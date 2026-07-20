using System;
using System.Collections.Generic;
using Game.Shared.Core;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>A9: Central arena obstacles at W10/W15.</summary>
  [DisallowMultipleComponent]
  public sealed class ArenaObstacleController : MonoBehaviour
  {
    static ArenaObstacleController s_instance;
    static readonly List<ObstacleDef> s_layouts = new();

    readonly List<GameObject> _active = new();
    Transform _root;
    float _rotationSpeed;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;
      var go = new GameObject("_ArenaObstacleController");
      DontDestroyOnLoad(go);
      s_instance = go.AddComponent<ArenaObstacleController>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      _root = new GameObject("ObstacleRoot").transform;
      _root.SetParent(transform, false);
      LoadLayouts();
      WaveDirector.WaveCompleted += OnWaveCompleted;
    }

    void OnDestroy()
    {
      WaveDirector.WaveCompleted -= OnWaveCompleted;
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      if (_rotationSpeed > 0f)
        _root.Rotate(0f, 0f, _rotationSpeed * Time.deltaTime);
    }

    static void LoadLayouts()
    {
      if (s_layouts.Count > 0)
        return;
      JsonDataLoader.TryParse("combat/arena_obstacles", json =>
      {
        try
        {
          var root = JsonUtility.FromJson<ObstacleRoot>(json);
          if (root?.obstacles != null)
            s_layouts.AddRange(root.obstacles);
        }
        catch (Exception e)
        {
          Debug.LogError($"[ArenaObstacleController] Parse failed: {e.Message}");
        }
      });
    }

    public static void ResetForNewRun()
    {
      if (s_instance != null)
        s_instance.ClearActive();
    }

    void OnWaveCompleted(int wave)
    {
      foreach (var layout in s_layouts)
      {
        if (layout != null && layout.wave == wave)
          ApplyLayout(layout);
      }
    }

    void ApplyLayout(ObstacleDef layout)
    {
      ClearActive();
      _rotationSpeed = layout.layout_id == "rotating_bar" ? 28f : 0f;
      var anchor = ArenaTerrainPlacement.GetPlayerPlanarPosition();
      _root.position = GameplayPlane.ToWorld(anchor, _root.position.z);

      switch (layout.layout_id)
      {
        case "cross_pillars":
          SpawnPillar(Vector2.up * 4f, new Vector2(0.9f, 3.2f));
          SpawnPillar(Vector2.down * 4f, new Vector2(0.9f, 3.2f));
          SpawnPillar(Vector2.left * 4f, new Vector2(3.2f, 0.9f));
          SpawnPillar(Vector2.right * 4f, new Vector2(3.2f, 0.9f));
          break;
        case "rotating_bar":
          SpawnPillar(Vector2.zero, new Vector2(7.5f, 0.75f));
          SpawnPillar(Vector2.zero, new Vector2(0.75f, 7.5f));
          break;
        case "broken_islands":
          SpawnPillar(new Vector2(-2.5f, 1.8f), new Vector2(2.2f, 2.2f));
          SpawnPillar(new Vector2(2.2f, -1.5f), new Vector2(2f, 2f));
          SpawnPillar(new Vector2(0.5f, 2.8f), new Vector2(1.6f, 1.6f));
          break;
      }
    }

    void SpawnPillar(Vector2 offset, Vector2 size)
    {
      var go = new GameObject("ArenaObstacle");
      go.transform.SetParent(_root, false);
      go.transform.localPosition = new Vector3(offset.x, offset.y, -0.02f);
      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = CreateBlockSprite();
      sr.color = new Color(0.55f, 0.62f, 0.72f, 0.88f);
      sr.sortingOrder = 6;
      go.transform.localScale = new Vector3(size.x, size.y, 1f);
      _active.Add(go);
    }

    void ClearActive()
    {
      foreach (var go in _active)
      {
        if (go != null)
          Destroy(go);
      }
      _active.Clear();
      _rotationSpeed = 0f;
      _root.localRotation = Quaternion.identity;
    }

    static Sprite CreateBlockSprite()
    {
      var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
      var pixels = new Color[16];
      for (var i = 0; i < pixels.Length; i++)
        pixels[i] = Color.white;
      tex.SetPixels(pixels);
      tex.Apply(false, true);
      return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    [Serializable]
    sealed class ObstacleRoot
    {
      public ObstacleDef[] obstacles;
    }

    [Serializable]
    sealed class ObstacleDef
    {
      public int wave;
      public string layout_id;
      public string display_name;
    }
  }
}
