using UnityEngine;

using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.Progression;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>S11: Elite 追猎标记 + 击杀 XP 柱奖励。</summary>
  [DisallowMultipleComponent]
  public sealed class EliteHuntMarker : MonoBehaviour
  {
    const int BonusXp = 48;

    Transform _arrow;
    float _bob;
    bool _rewarded;

    public static void Attach(GameObject enemy)
    {
      if (enemy == null || enemy.GetComponent<EliteHuntMarker>() != null)
        return;

      enemy.AddComponent<EliteHuntMarker>();
    }

    void Awake()
    {
      BuildArrow();
      var health = GetComponent<Health>();
      if (health != null)
        health.Died += OnDied;
    }

    void OnDestroy()
    {
      var health = GetComponent<Health>();
      if (health != null)
        health.Died -= OnDied;
    }

    void OnDied()
    {
      if (_rewarded)
        return;

      _rewarded = true;
      LootService.SpawnXpPickup(transform.position + Vector3.up * 0.4f, ArenaDifficultyRuntime.ScaleXp(BonusXp));
    }

    void Update()
    {
      if (_arrow == null)
        return;

      _bob += Time.deltaTime * 4f;
      _arrow.localPosition = new Vector3(0f, 1.2f + Mathf.Sin(_bob) * 0.12f, 0f);
      _arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_bob * 0.5f) * 8f);
    }

    void BuildArrow()
    {
      var go = new GameObject("EliteHuntArrow");
      go.transform.SetParent(transform, false);
      _arrow = go.transform;

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = CreateArrowSprite();
      sr.color = new Color(1f, 0.82f, 0.18f, 0.95f);
      sr.sortingOrder = 120;
      go.transform.localScale = Vector3.one * 0.55f;
    }

    static Sprite CreateArrowSprite()
    {
      const int size = 16;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
      for (var y = 0; y < size; y++)
      {
        for (var x = 0; x < size; x++)
        {
          var on = x >= size / 2 - 2 && x <= size / 2 + 1 && y >= 4;
          tex.SetPixel(x, y, on ? Color.white : Color.clear);
        }
      }
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0f), 16f);
    }
  }
}
