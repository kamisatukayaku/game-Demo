using UnityEngine;



using Game.Modes.Roguelike.Loot;

using Game.Modes.Roguelike.Progression;

using Game.Modes.Roguelike.UI;

using Game.Shared.Gameplay;

using Game.Shared.Gameplay.Events;



namespace Game.Modes.Roguelike.Combat

{

  /// <summary>A13: 金色游荡怪视觉 + 击杀奖励（进化加速或大额 XP）。</summary>

  [DisallowMultipleComponent]

  public sealed class RareGoldenMarker : MonoBehaviour

  {

    const int LargeXp = 165;



    Transform _halo;

    float _pulse;

    bool _rewarded;

    EventListenerHandle _killedHandle;



    void Awake()

    {

      BuildHalo();

      TintGold();

      _killedHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);

    }



    void OnDestroy()

    {

      if (_killedHandle.Valid)

        GameEventBus.Unsubscribe(_killedHandle);

    }



    void Update()

    {

      if (_halo == null)

        return;



      _pulse += Time.deltaTime * 2.6f;

      var scale = 1.15f + Mathf.Sin(_pulse) * 0.08f;

      _halo.localScale = Vector3.one * scale;

      _halo.rotation = Quaternion.Euler(0f, 0f, _pulse * 18f);

    }



    void OnEnemyKilled(EnemyKilledEvent evt)

    {

      if (_rewarded || evt.Enemy == null || evt.Enemy != gameObject)

        return;



      if (!PlayerCombatAttribution.IsPlayerOrOwned(evt.Killer))

        return;



      _rewarded = true;

      GrantReward(evt.Position);

    }



    void GrantReward(Vector3 position)

    {

      if (Random.value < 0.5f)

      {

        var boost = Mathf.Max(12, ExperienceSystem.XpToNextLevel());

        ExperienceSystem.Gain(boost);

        ArenaMomentUI.ShowBanner("金色游荡怪 — 进化加速！", new Color(1f, 0.88f, 0.35f, 1f));

        RunTimelineRecorder.Record("金色游荡怪", "进化加速");

      }

      else

      {

        var xp = ArenaDifficultyRuntime.ScaleXp(LargeXp);

        LootService.SpawnXpPickup(position + Vector3.up * 0.35f, xp);

        LootService.SpawnXpPickup(position + Vector3.left * 0.45f, Mathf.RoundToInt(xp * 0.55f));

        ArenaMomentUI.ShowBanner("金色游荡怪 — 大额经验！", new Color(1f, 0.82f, 0.28f, 1f));

        RunTimelineRecorder.Record("金色游荡怪", "大额经验");

      }



      ArenaRewardVfx.PlayRareGolden(position);

    }



    void BuildHalo()

    {

      var go = new GameObject("RareGoldenHalo");

      go.transform.SetParent(transform, false);

      go.transform.localPosition = new Vector3(0f, 0.05f, 0f);

      _halo = go.transform;



      var sr = go.AddComponent<SpriteRenderer>();

      sr.sprite = CreateRingSprite();

      sr.color = new Color(1f, 0.82f, 0.18f, 0.82f);

      sr.sortingOrder = 118;

      go.transform.localScale = Vector3.one * 1.35f;

    }



    void TintGold()

    {

      foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())

      {

        if (renderer.gameObject.name == "RareGoldenHalo")

          continue;



        var c = renderer.color;

        renderer.color = new Color(

          Mathf.Lerp(c.r, 1f, 0.55f),

          Mathf.Lerp(c.g, 0.82f, 0.55f),

          Mathf.Lerp(c.b, 0.18f, 0.45f),

          c.a);

      }

    }



    static Sprite CreateRingSprite()

    {

      const int size = 32;

      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

      var center = new Vector2(size * 0.5f, size * 0.5f);

      for (var y = 0; y < size; y++)

      {

        for (var x = 0; x < size; x++)

        {

          var dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size * 0.5f);

          var ring = dist > 0.62f && dist < 0.92f;

          tex.SetPixel(x, y, ring ? Color.white : Color.clear);

        }

      }

      tex.Apply();

      return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);

    }

  }

}


