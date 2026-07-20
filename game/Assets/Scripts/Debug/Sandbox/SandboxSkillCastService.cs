using System.Collections.Generic;
using UnityEngine;

using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Progression;
using Game.Shared.Combat.Buff;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Player;
using Game.Shared.Runtime.Physics;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.DevTools.Sandbox
{
  /// <summary>
  /// 沙盒 Mage 技能：瞄准最近敌人（无敌人则自身），点击切换开/关。
  /// </summary>
  [DisallowMultipleComponent]
  public class SandboxSkillCastService : MonoBehaviour
  {
    const float SandboxDuration = 4f;

    sealed class SlotHandle
    {
      public bool Active;
      public readonly List<MageZone> MageZones = new();
      public readonly List<GameObject> Misc = new();
      public float FrostRadius;
    }

    readonly SlotHandle[] _slots = { new(), new(), new(), new() };

    PlayerAttackDirector _director;
    Transform _transform;

    public bool IsSlotActive(int index) =>
      index >= 0 && index < _slots.Length && _slots[index].Active;

    void Awake()
    {
      _director = GetComponent<PlayerAttackDirector>();
      _transform = transform;
      PlayerClassSkillDatabase.EnsureLoaded();
      AttackProfileDatabase.EnsureLoaded();
    }

    void OnDestroy() => DismissAll();

    public bool ToggleSlot(int index)
    {
      if (index < 0 || index >= _slots.Length)
        return false;

      if (_slots[index].Active)
      {
        DismissSlot(index);
        return false;
      }

      CastSlot(index);
      return _slots[index].Active;
    }

    public void DismissAll()
    {
      for (var i = 0; i < _slots.Length; i++)
        DismissSlot(i);
    }

    void CastSlot(int index)
    {
      SandboxGameplayBootstrap.EnsureInstalled();

      var theme = RunBuildState.WeaponTheme;
      if (theme != "mage")
        return;

      var slotDef = FindSlotDef(theme, index);
      if (slotDef == null)
        return;

      CastMage(index, slotDef);
    }

    static PlayerClassSkillDatabase.SkillSlotDef FindSlotDef(string theme, int index)
    {
      var set = PlayerClassSkillDatabase.Get(theme);
      if (set?.slots == null)
        return null;

      foreach (var s in set.slots)
      {
        if (s != null && s.slot == index + 1)
          return s;
      }

      return null;
    }

    void CastMage(int index, PlayerClassSkillDatabase.SkillSlotDef slot)
    {
      if (slot == null)
        return;

      if (_director == null)
        _director = GetComponent<PlayerAttackDirector>();

      var aim = ResolveAimDirection();
      var handle = _slots[index];

      switch (slot.kind)
      {
        case "attack_profile":
          if (!string.IsNullOrEmpty(slot.attack_profile_id))
            _director?.CastSkillProfile(slot.attack_profile_id, aim);
          handle.Active = false;
          break;

        case "gravity_well":
          CastGravityWell(index, slot, aim);
          break;

        case "tidal_pulse":
        case "frost_ward":
          CastTidalPulse(index, slot);
          break;

        case "fire_nova":
          PlayerSkillExecutor.ExecuteFireNova(_transform, aim, slot.base_radius,
            AttackProfileDatabase.Get(slot.attack_profile_id)?.base_damage ?? 14f,
            ResolveSandboxSkillTargetPoint(aim));
          handle.Active = false;
          break;
      }

      var mageCtrl = GetComponent<MageController>() ?? MageController.Ensure(gameObject);
      mageCtrl?.OnSkillCast(index);
    }

    public void CastOnce(int index)
    {
      if (index < 0 || index >= _slots.Length)
        return;

      if (_slots[index].Active)
        DismissSlot(index);

      CastSlot(index);
    }

    void CastGravityWell(int index, PlayerClassSkillDatabase.SkillSlotDef slot, Vector2 aim)
    {
      var ctx = MageSystemLocator.Context;
      var profile = AttackProfileDatabase.Get(slot.attack_profile_id);
      var baseDamage = profile != null ? profile.base_damage : 10f;
      var rangeMult = ctx.SkillRangeMult;
      var radius = Mathf.Max(1.5f, (slot.base_radius + ctx.SkillExplosionRadius * 0.35f) * rangeMult);
      var playerPos = GameplayPlane.Position2D(_transform);
      var center = aim.sqrMagnitude > 0.0001f
        ? playerPos + aim.normalized * 4.5f * rangeMult
        : playerPos;

      var zone = MageZone.Spawn(_transform, center, radius, SandboxDuration, baseDamage);
      var handle = _slots[index];
      handle.MageZones.Add(zone);
      handle.Active = true;
    }

    Vector2 ResolveSandboxSkillTargetPoint(Vector2 aim)
    {
      var enemy = FindNearestEnemyTransform();
      if (enemy != null)
        return GameplayPlane.Position2D(enemy);

      var origin = GameplayPlane.Position2D(_transform);
      var dir = aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
      return origin + dir * 4.5f * MageSystemLocator.Context.SkillRangeMult;
    }

    void CastTidalPulse(int index, PlayerClassSkillDatabase.SkillSlotDef slot)
    {
      var ctx = MageSystemLocator.Context;
      var radius = Mathf.Max(1.5f, slot.base_radius * ctx.SkillRangeMult);
      PlayerSkillExecutor.ExecuteTidalPulse(_transform, slot.base_radius, SandboxDuration);

      var handle = _slots[index];
      handle.FrostRadius = radius;
      handle.Active = true;
    }

    Transform ResolveEffectAnchor()
    {
      var enemy = FindNearestEnemyTransform();
      return enemy != null ? enemy : _transform;
    }

    Vector2 ResolveAimDirection()
    {
      var enemy = FindNearestEnemyTransform();
      if (enemy == null)
        return Vector2.zero;

      var origin = GameplayPlane.Position2D(_transform);
      var target = GameplayPlane.Position2D(enemy);
      var dir = target - origin;
      if (dir.sqrMagnitude < 0.0001f)
        return Vector2.zero;
      return dir.normalized;
    }

    Transform FindNearestEnemyTransform()
    {
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return null;

      Transform best = null;
      var bestDist = float.MaxValue;
      var origin = GameplayPlane.Position2D(_transform);

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

    void DismissSlot(int index)
    {
      if (index < 0 || index >= _slots.Length)
        return;

      var handle = _slots[index];
      if (!handle.Active && handle.MageZones.Count == 0 && handle.Misc.Count == 0)
        return;

      for (var i = handle.MageZones.Count - 1; i >= 0; i--)
      {
        if (handle.MageZones[i] != null)
          MageZonePool.Release(handle.MageZones[i]);
      }
      handle.MageZones.Clear();

      for (var i = handle.Misc.Count - 1; i >= 0; i--)
      {
        if (handle.Misc[i] != null)
          Destroy(handle.Misc[i]);
      }
      handle.Misc.Clear();

      if (handle.FrostRadius > 0f)
        DismissFrostWard(handle.FrostRadius);

      handle.FrostRadius = 0f;
      handle.Active = false;
    }

    void DismissFrostWard(float radius)
    {
      var playerBuffs = GetComponent<BuffContainer>();
      playerBuffs?.RemoveBuff("buff_overheal_shield");

      var center = GameplayPlane.Position2D(_transform);
      var registry = CombatRoot.EnemyRegistry;
      if (registry == null)
        return;

      foreach (var enemy in registry.GetInRange(center, radius))
      {
        if (enemy == null)
          continue;
        enemy.GetComponent<BuffContainer>()?.RemoveBuff("buff_slow_debuff");
      }
    }
  }
}
