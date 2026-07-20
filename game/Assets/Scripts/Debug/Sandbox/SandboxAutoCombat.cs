using UnityEngine;

using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Player;
using Game.Shared.Runtime.Physics;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.DevTools.Sandbox
{
  /// <summary>沙盒自动战斗：索敌、瞄准、攻击、施法（真实 Runtime，非模拟）。</summary>
  [DisallowMultipleComponent]
  public class SandboxAutoCombat : MonoBehaviour
  {
    [SerializeField] bool autoCombatEnabled = true;
    [SerializeField] bool autoCastSkills = false;
    [SerializeField] float attackInterval = 0.45f;
    [SerializeField] float skillCastInterval = 1.8f;

    PlayerAttackDirector _director;
    PlayerActiveSkillController _skills;
    float _attackTimer;
    float _skillTimer;
    int _skillSlot;

    public bool AutoCastSkills
    {
      get => autoCastSkills;
      set => autoCastSkills = value;
    }

    public bool AutoCombatEnabled
    {
      get => autoCombatEnabled;
      set => autoCombatEnabled = value;
    }

    void Awake()
    {
      _director = GetComponent<PlayerAttackDirector>();
      _skills = GetComponent<PlayerActiveSkillController>();
    }

    void Update()
    {
      if (!autoCombatEnabled || _director == null)
        return;

      var target = FindNearestEnemy(transform);
      if (target == null)
        return;

      var dir = AimAt(target);
      TickAttack(dir);
      TickSkills(dir);
    }

    Vector2 AimAt(Transform target)
    {
      var pos = GameplayPlane.Position2D(transform);
      var tpos = GameplayPlane.Position2D(target);
      var dir = tpos - pos;
      if (dir.sqrMagnitude < 0.0001f)
        dir = Vector2.right;
      return dir.normalized;
    }

    void TickAttack(Vector2 dir)
    {
      _attackTimer -= Time.deltaTime;
      if (_attackTimer > 0f)
        return;

      _attackTimer = attackInterval;
      _director.SandboxExecuteAttack(dir);
    }

    void TickSkills(Vector2 dir)
    {
      if (!autoCastSkills)
        return;

      var theme = RunBuildState.WeaponTheme;
      if (theme != "mage")
        return;

      _skillTimer -= Time.deltaTime;
      if (_skillTimer > 0f)
        return;

      _skillTimer = skillCastInterval;

      var skillService = GetComponent<SandboxSkillCastService>();
      if (skillService != null)
      {
        skillService.CastOnce(_skillSlot);
        _skillSlot = (_skillSlot + 1) % 4;
        return;
      }

      _skills?.SandboxTryCastSlot(_skillSlot);
      _skillSlot = (_skillSlot + 1) % 4;
    }

    static Transform FindNearestEnemy(Transform originTransform)
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return null;

      Transform best = null;
      var bestDist = float.MaxValue;
      var origin = GameplayPlane.Position2D(originTransform);

      foreach (var enemy in registry.AllEnemies)
      {
        if (enemy == null)
          continue;

        var health = enemy.GetComponent<Health>();
        if (health != null && health.IsDead)
          continue;

        var dist = Vector2.SqrMagnitude(GameplayPlane.Position2D(enemy.transform) - origin);
        if (dist < bestDist)
        {
          bestDist = dist;
          best = enemy.transform;
        }
      }

      return best;
    }
  }
}
