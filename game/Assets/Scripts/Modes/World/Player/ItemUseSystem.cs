using System.Collections;
using System.Collections.Generic;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Game.Shared.Combat.Events;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Health = global::Game.Shared.Combat.Health.Health;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 道具使用系统 — 挂载在 Player GameObject 上。
  ///
  /// 按下 UseItem 键时消耗当前选中槽位的道具，根据物品 effect_type 执行效果：
  ///   heal           → 直接回复玩家生命
  ///   area_damage    → 投掷至鼠标位置，对半径内敌人造成瞬间伤害（无来源伤害）
  ///   area_slow      → 投掷至鼠标位置，对半径内敌人施加减速
  ///   chain_lightning → 投掷至鼠标位置，对范围内敌人造成连锁闪电伤害
  ///   apply_buff     → 为玩家自身添加 Buff
  ///
  /// 投掷物伤害为无来源伤害（DamageRequest.Attacker = null），不受玩家攻击力加成。
  /// 不复用 PlayerAttackDirector 的任何代码。
  /// </summary>
  [DisallowMultipleComponent]
  public class ItemUseSystem : MonoBehaviour
  {
    [Header("Projectile Visual")]
    [SerializeField] float _throwSpeed = 18f;
    [SerializeField] float _throwArc = 0.3f;

    GameObject _throwVisual; // 投掷物视觉占位

    void Awake()
    {
      _throwVisual = CreateThrowVisual();
    }

    void Update()
    {
      if (!WorldRuntimeContext.IsWorldModeActive) return;

      int slot = WorldRuntimeContext.SelectedItemSlot;
      if (slot < 0) return;

      if (!GameInputBindings.WasPressed(WorldInputKeys.UseItem)) return;

      var itemId = WorldRuntimeContext.ItemSlots[slot];
      if (string.IsNullOrEmpty(itemId)) return;

      var inv = WorldManager.Instance?.Inventory;
      if (inv == null || inv.GetItemCount(itemId) <= 0) return;

      var def = WorldDatabase.GetItem(itemId);
      if (def == null || !def.IsWeapon) return;

      // 扣除道具
      if (!inv.RemoveItem(itemId, 1)) return;

      // 如用完清空选中
      if (inv.GetItemCount(itemId) <= 0)
      {
        if (WorldRuntimeContext.ItemSlots[slot] == itemId)
        {
          WorldRuntimeContext.ClearItemSlot(slot);
          WorldRuntimeContext.SelectedItemSlot = -1;
        }
      }

      // 执行效果
      switch (def.effect_type)
      {
        case "heal":
          ApplyHeal(def);
          break;
        case "area_damage":
          StartCoroutine(ThrowAndApply(def, DoAreaDamage));
          break;
        case "area_slow":
          StartCoroutine(ThrowAndApply(def, DoAreaSlow));
          break;
        case "chain_lightning":
          StartCoroutine(ThrowAndApply(def, DoChainLightning));
          break;
        case "apply_buff":
          ApplySelfBuff(def);
          break;
        default:
          Debug.LogWarning($"[ItemUseSystem] Unknown effect_type '{def.effect_type}' for '{itemId}'.");
          break;
      }
    }

    // ══════════════════════════════════════════════════════
    //  效果：治疗
    // ══════════════════════════════════════════════════════

    void ApplyHeal(WorldDatabase.ItemDef item)
    {
      var params_ = item.GetEffectParams();
      float amount = params_.TryGetValue("heal_amount", out var v) ? v : 50f;
      var health = GetComponent<Health>();
      health?.Heal(amount);
      Debug.Log($"[ItemUseSystem] Healed {amount:F0} HP.");
    }

    // ══════════════════════════════════════════════════════
    //  效果：自身 Buff
    // ══════════════════════════════════════════════════════

    void ApplySelfBuff(WorldDatabase.ItemDef item)
    {
      var params_ = item.GetEffectParams();
      string buffId = null;
      float duration = 30f;
      foreach (var p in item.effect_params ?? new WorldDatabase.EffectParamEntry[0])
      {
        if (p.key == "buff_id") buffId = p.value > 0 ? ((int)p.value).ToString() : null;
        if (p.key == "duration" && p.value > 0) duration = p.value;
      }
      if (string.IsNullOrEmpty(buffId)) return;

      var container = GetComponent<BuffContainer>();
      container?.ApplyBuff(buffId, new BuffContainer.BuffApplyContext
      {
        sourceEntity = gameObject,
        durationOverride = duration
      });
    }

    // ══════════════════════════════════════════════════════
    //  投掷物飞行 + 命中
    // ══════════════════════════════════════════════════════

    IEnumerator ThrowAndApply(WorldDatabase.ItemDef item, System.Action<Vector2, Dictionary<string, float>> onHit)
    {
      var mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
      mousePos.z = 0;
      var start = transform.position;
      var end = (Vector3)mousePos;
      var dir = (end - start).normalized;
      float dist = Vector3.Distance(start, end);
      float duration = dist / _throwSpeed;

      _throwVisual.transform.position = start;
      _throwVisual.SetActive(true);

      float elapsed = 0f;
      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        var pos = Vector3.Lerp(start, end, t);
        pos.y += Mathf.Sin(t * Mathf.PI) * _throwArc * dist;
        _throwVisual.transform.position = pos;
        yield return null;
      }

      _throwVisual.SetActive(false);
      onHit?.Invoke(end, item.GetEffectParams());
    }

    // ══════════════════════════════════════════════════════
    //  命中效果：范围伤害（无来源）
    // ══════════════════════════════════════════════════════

    void DoAreaDamage(Vector2 pos, Dictionary<string, float> p)
    {
      float damage = p.TryGetValue("damage", out var d) ? d : 50f;
      float radius = p.TryGetValue("radius", out var r) ? r : 3f;
      int hitCount = ApplyToEnemiesInRadius(pos, radius, (health) =>
      {
        var req = new DamageRequest
        {
          Base = damage,
          DamageTypeId = "physical",
          DamageSourceId = "item",
          Attacker = null // ← 无来源，不受玩家加成
        };
        DamagePipeline.Apply(req, health);
      });
      Debug.Log($"[ItemUseSystem] Area damage: {damage} dmg, radius={radius}, hit={hitCount}");
    }

    // ══════════════════════════════════════════════════════
    //  命中效果：范围减速
    // ══════════════════════════════════════════════════════

    void DoAreaSlow(Vector2 pos, Dictionary<string, float> p)
    {
      float radius = p.TryGetValue("radius", out var r2) ? r2 : 4f;
      float slowPct = p.TryGetValue("slow_percent", out var sp) ? sp : 0.5f;
      float duration = p.TryGetValue("duration", out var dur) ? dur : 5f;

      int hitCount = ApplyToEnemiesInRadius(pos, radius, (health) =>
      {
        var buff = health.GetComponent<BuffContainer>();
        buff?.ApplyBuff("debuff_slow", new BuffContainer.BuffApplyContext
        {
          sourceEntity = gameObject,
          durationOverride = duration,
          abilityId = "item_smoke_bomb"
        });
      });
      Debug.Log($"[ItemUseSystem] Area slow: {slowPct*100:F0}%, radius={radius}, duration={duration}s, hit={hitCount}");
    }

    // ══════════════════════════════════════════════════════
    //  命中效果：连锁闪电
    // ══════════════════════════════════════════════════════

    void DoChainLightning(Vector2 pos, Dictionary<string, float> p)
    {
      float damage = p.TryGetValue("damage", out var dmg) ? dmg : 35f;
      int bounceCount = p.TryGetValue("bounce_count", out var bc) ? (int)bc : 4;
      float bounceRadius = p.TryGetValue("bounce_radius", out var br) ? br : 5f;

      // 第一目标：最近敌人
      var enemies = GetEnemiesInRadius(pos, bounceRadius);
      if (enemies.Count == 0) return;

      var hitSet = new HashSet<Health>();
      var current = enemies[0].GetComponent<Health>();
      if (current == null) return;

      int bounces = 0;
      while (current != null && bounces <= bounceCount)
      {
        hitSet.Add(current);
        var req = new DamageRequest
        {
          Base = damage * (1f - bounces * 0.15f), // 每跳衰减15%
          DamageTypeId = "lightning",
          DamageSourceId = "item",
          Attacker = null
        };
        if (req.Base < 1f) req.Base = 1f;
        DamagePipeline.Apply(req, current);

        // 找下一目标（最近未命中）
        current = null;
        float bestDist = float.MaxValue;
        foreach (var e in GetEnemiesInRadius(enemies[0].transform.position, bounceRadius))
        {
          var h = e.GetComponent<Health>();
          if (h == null || hitSet.Contains(h)) continue;
          float dist = Vector2.Distance(enemies[0].transform.position, e.transform.position);
          if (dist < bestDist) { bestDist = dist; current = h; }
        }
        bounces++;
      }
      Debug.Log($"[ItemUseSystem] Chain lightning: {damage} dmg, bounced {bounces} times, hit {hitSet.Count}");
    }

    // ══════════════════════════════════════════════════════
    //  辅助：范围内敌人查询
    // ══════════════════════════════════════════════════════

    int ApplyToEnemiesInRadius(Vector2 center, float radius, System.Action<Health> action)
    {
      int count = 0;
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null) return 0;

      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null) continue;
        var health = enemy.GetComponent<Health>();
        if (health == null || health.IsDead) continue;
        action(health);
        count++;
      }
      return count;
    }

    List<GameObject> GetEnemiesInRadius(Vector2 center, float radius)
    {
      var list = new List<GameObject>();
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null) return list;
      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy != null)
        {
          var h = enemy.GetComponent<Health>();
          if (h != null && !h.IsDead) list.Add(enemy.gameObject);
        }
      }
      list.Sort((a, b) => Vector2.Distance(a.transform.position, center)
        .CompareTo(Vector2.Distance(b.transform.position, center)));
      return list;
    }

    GameObject CreateThrowVisual()
    {
      var go = new GameObject("ThrowVisual");
      go.transform.SetParent(transform);
      go.SetActive(false);
      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = CreateDotSprite();
      sr.color = new Color(1f, 0.6f, 0.2f, 0.9f);
      go.transform.localScale = Vector3.one * 0.2f;
      sr.sortingOrder = 60;
      return go;
    }

    static Sprite CreateDotSprite()
    {
      int s = 16;
      var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
      var c = new Vector2(s / 2f, s / 2f);
      for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
          tex.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), c) <= s / 2f - 1f ? Color.white : Color.clear);
      tex.filterMode = FilterMode.Bilinear;
      tex.Apply();
      return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), s);
    }
  }
}
