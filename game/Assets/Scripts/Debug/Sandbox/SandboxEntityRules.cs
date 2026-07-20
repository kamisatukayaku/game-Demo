using UnityEngine;

using Game.Shared.Combat;
using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.DevTools.Sandbox
{
  /// <summary>Build Sandbox 实体规则：玩家无敌、木桩怪物。</summary>
  public static class SandboxEntityRules
  {
    public const float InfiniteHp = 1_000_000_000f;

    public static void ConfigurePlayer(GameObject player)
    {
      if (player == null)
        return;

      var health = player.GetComponent<Health>();
      if (health != null)
      {
        health.Configure(InfiniteHp);
        health.SandboxIgnoreDamage = true;
        health.SandboxNeverDie = false;
      }

      if (player.GetComponent<SandboxPlayerHealthGuard>() == null)
        player.AddComponent<SandboxPlayerHealthGuard>();
    }

    public static void ConfigureTrainingDummy(GameObject enemy)
    {
      if (enemy == null)
        return;

      var health = enemy.GetComponent<Health>();
      if (health != null)
      {
        health.SandboxIgnoreDamage = false;
        health.SandboxNeverDie = true;
      }

      if (enemy.GetComponent<CombatFreezeBehaviour>() == null)
        enemy.AddComponent<CombatFreezeBehaviour>();

      if (enemy.GetComponent<SandboxTrainingDummyAnchor>() == null)
        enemy.AddComponent<SandboxTrainingDummyAnchor>();

      if (enemy.GetComponent<SandboxDummyRegen>() == null)
        enemy.AddComponent<SandboxDummyRegen>();

      var rb = enemy.GetComponent<Rigidbody2D>();
      if (rb != null)
      {
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
      }

      if (enemy.GetComponent<DamageReceiver>() == null)
        DamageReceiver.Ensure(enemy);
    }

    /// <summary>
    /// Boss 测试配置：不禁冻 AI，允许移动和释放技能。
    /// 仅设置 SandboxNeverDie 防止意外死亡。
    /// </summary>
    public static void ConfigureBossDummy(GameObject enemy)
    {
      if (enemy == null)
        return;

      var health = enemy.GetComponent<Health>();
      if (health != null)
      {
        health.SandboxIgnoreDamage = false;
        health.SandboxNeverDie = true;
      }

      // 不添加 CombatFreezeBehaviour（允许 AI 运行）
      // 不添加 TrainingDummyAnchor（允许移动）
      // 不添加 DummyRegen（Boss 有自己的血量管理）

      if (enemy.GetComponent<DamageReceiver>() == null)
        DamageReceiver.Ensure(enemy);
    }

    public static void ClearPlayer(GameObject player)
    {
      if (player == null)
        return;

      var health = player.GetComponent<Health>();
      if (health != null)
      {
        health.SandboxIgnoreDamage = false;
        health.SandboxNeverDie = false;
      }

      var guard = player.GetComponent<SandboxPlayerHealthGuard>();
      if (guard != null)
        Object.Destroy(guard);
    }
  }

  [DisallowMultipleComponent]
  sealed class SandboxTrainingDummyAnchor : MonoBehaviour
  {
    Vector3 _anchor;
    Rigidbody2D _rb;

    void Awake() => _rb = GetComponent<Rigidbody2D>();

    void OnEnable() => _anchor = transform.position;

    void LateUpdate()
    {
      if (!SandboxMode.Active)
        return;

      transform.position = _anchor;
      if (_rb == null)
        return;

      _rb.velocity = Vector2.zero;
      _rb.angularVelocity = 0f;
    }
  }

  [DisallowMultipleComponent]
  sealed class SandboxDummyRegen : MonoBehaviour
  {
    const float RegenDelaySeconds = 0.45f;

    Health _health;
    Coroutine _regenRoutine;

    void Awake() => _health = GetComponent<Health>();

    void OnEnable()
    {
      if (_health != null)
        _health.Damaged += OnDamaged;
    }

    void OnDisable()
    {
      if (_health != null)
        _health.Damaged -= OnDamaged;
    }

    void OnDamaged(float _)
    {
      if (!SandboxMode.Active || _health == null)
        return;

      if (_regenRoutine != null)
        StopCoroutine(_regenRoutine);

      _regenRoutine = StartCoroutine(RegenAfterHit());
    }

    System.Collections.IEnumerator RegenAfterHit()
    {
      yield return new WaitForSeconds(RegenDelaySeconds);

      if (!SandboxMode.Active || _health == null)
        yield break;

      var missing = _health.MaxHp - _health.CurrentHp;
      if (missing > 0f)
        _health.Heal(missing);
    }
  }

  [DisallowMultipleComponent]
  sealed class SandboxPlayerHealthGuard : MonoBehaviour
  {
    Health _health;

    void Awake() => _health = GetComponent<Health>();

    void LateUpdate()
    {
      if (!SandboxMode.Active || _health == null)
        return;

      if (_health.MaxHp < SandboxEntityRules.InfiniteHp * 0.5f)
        _health.Configure(SandboxEntityRules.InfiniteHp);

      if (_health.CurrentHp < _health.MaxHp * 0.99f)
        _health.Heal(_health.MaxHp);
    }
  }
}
