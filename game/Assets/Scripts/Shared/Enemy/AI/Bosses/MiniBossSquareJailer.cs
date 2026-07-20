using System.Collections.Generic;
using UnityEngine;
using Game.Shared.Combat;
using Game.Shared.Combat.Damage;
using Game.Shared.Core;
using Health = Game.Shared.Combat.Health.Health;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// Mini-Boss W18 — 四角柱牢笼召唤。
  /// </summary>
  [DisallowMultipleComponent]
  public class MiniBossSquareJailer : BossCore
  {
    public const string SKILL_CORNER_PILLARS = "corner_pillars";

    const float NORMAL_SPEED = 2.3f;
    const float IDEAL_DIST_MIN = 4f;
    const float IDEAL_DIST_MAX = 6f;
    const string PILLAR_ENEMY_ID = "mini_boss_square_jailer_pillar";

    EnemyMovement _movement;
    EnemyAttack _attackComp;
    readonly List<BossPart> _pillars = new();

    public float MoveSpeed => _movement != null ? _movement.MoveSpeed : NORMAL_SPEED;
    public float AttackDmg => _attackComp != null ? _attackComp.AttackDamage : 6f;

    public int LivingPillarCount
    {
      get
      {
        int count = 0;
        for (int i = _pillars.Count - 1; i >= 0; i--)
        {
          var part = _pillars[i];
          if (part == null || part.IsDestroyed)
            _pillars.RemoveAt(i);
          else
            count++;
        }
        return count;
      }
    }

    public Vector2 GetPlayerPos()
    {
      var t = Core?.ChaseTarget;
      if (t != null) return GameplayPlane.Position2D(t);
      var go = GameObject.FindGameObjectWithTag("Player");
      return go != null ? GameplayPlane.Position2D(go.transform) : Vector2.zero;
    }

    public Vector2 DirToPlayer()
    {
      var d = GetPlayerPos() - Position;
      return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.right;
    }

    public void MoveInDir(Vector2 dir, float speed, float dt)
    {
      if (dir.sqrMagnitude < 0.0001f) return;
      transform.position += (Vector3)(dir.normalized * speed * dt);
    }

    public DamageRequest BuildReq(float dmg, string type = "physical", string src = "monster")
      => DamageRequest.Direct(dmg, type, src, gameObject);

    public void MeleeHit(float dmgMult = 1f, string type = "physical")
    {
      var t = Core?.ChaseTarget;
      if (t == null)
        return;

      BossContactDamage.ApplyPlayerMeleeHit(
        gameObject, t, AttackDmg * dmgMult, type, "boss_melee",
        attackInstanceId: ActiveAttackInstanceId);
    }

    public void SummonCornerPillars(float radius)
    {
      ClearPillars();
      var center = GetPlayerPos();
      var offsets = new[]
      {
        new Vector2(radius, radius),
        new Vector2(-radius, radius),
        new Vector2(-radius, -radius),
        new Vector2(radius, -radius)
      };

      foreach (var offset in offsets)
      {
        var worldPos = center + offset;
        var part = SpawnPart(
          PILLAR_ENEMY_ID,
          worldPos - Position,
          BossPart.DamageMode.Independent,
          BossPart.MovementMode.None,
          visualScale: 1.1f,
          hpMult: 0.2f);
        if (part == null) continue;
        part.transform.position = new Vector3(worldPos.x, worldPos.y, part.transform.position.z);
        _pillars.Add(part);
      }
    }

    public void ClearPillars()
    {
      for (int i = _pillars.Count - 1; i >= 0; i--)
      {
        if (_pillars[i] != null)
          DestroyPart(_pillars[i]);
      }
      _pillars.Clear();
    }

    protected override void Awake()
    {
      base.Awake();
      _movement = GetComponent<EnemyMovement>();
      _attackComp = GetComponent<EnemyAttack>();
    }

    protected override void OnBossStart()
    {
      RegisterSkill(new SquareJailerSkill_CornerPillars());
      RegisterSkill(new SquareJailerSkill_Charge());
    }

    protected override void OnBossUpdate(float dt) { }

    protected override void OnPassiveUpdate(float dt)
    {
      if (ActiveSkill == null)
        HandleIdleMovement(dt);
    }

    public override void OnPartDestroyed(BossPart part) => _pillars.Remove(part);

    protected override Transform SelectAttackOrigin() => transform;

    void HandleIdleMovement(float dt)
    {
      float dist = Vector2.Distance(Position, GetPlayerPos());
      if (dist < IDEAL_DIST_MIN)
        MoveInDir((Position - GetPlayerPos()).normalized, NORMAL_SPEED, dt);
      else if (dist > IDEAL_DIST_MAX)
        MoveInDir(DirToPlayer(), NORMAL_SPEED, dt);
    }
  }
}
