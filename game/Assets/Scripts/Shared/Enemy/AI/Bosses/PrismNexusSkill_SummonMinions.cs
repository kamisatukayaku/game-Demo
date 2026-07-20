using UnityEngine;
using Game.Shared.Enemy.Spawn;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 技能 7 — 召唤小怪。
  /// P1: 在玩家周围中远距离召唤随机种类小怪
  /// P2: 召唤数量提高
  /// 一阶段无效。
  /// Priority: 6, Cooldown: 15s
  /// </summary>
  public class PrismNexusSkill_SummonMinions : BossSkillBase
  {
    const float CAST_TIME    = 0.4f;
    const float SPAWN_DIST   = 6f;

    // 可配置的召唤敌人 ID 列表（使用现有 minion 精灵）
    static readonly string[] MINION_IDS = {
      "mob_tri_01", "mob_square_01", "mob_pent_01",
      "mob_star4_01", "mob_hex_01", "mob_star5_01"
    };

    int   Count => GamePhase >= 2 ? 5 : 3;
    int   GamePhase;
    float _elapsed;
    bool  _spawned;

    public PrismNexusSkill_SummonMinions()
    {
      Id = FinalBossPrismNexus.SK_SUMMON; Priority = 6; Cooldown = 15f;
    }

    public override bool CanTrigger(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      return nexus != null && !nexus.IsSkillLocked
        && nexus.GamePhase >= 1;
    }

    public override void OnEnter(BossCore boss)
    {
      var nexus = boss as FinalBossPrismNexus;
      GamePhase = nexus != null ? nexus.GamePhase : 1;
      _elapsed  = 0f;
      _spawned  = false;
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      _elapsed += dt;
      if (!_spawned && _elapsed >= CAST_TIME)
      {
        _spawned = true;
        var nexus = boss as FinalBossPrismNexus;
        if (nexus != null) SpawnMinions(nexus);
      }
      return _elapsed >= CAST_TIME + 0.4f ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void SpawnMinions(FinalBossPrismNexus nexus)
    {
      var spawner = Object.FindObjectOfType<EnemySpawner>();
      if (spawner == null) return;

      var playerPos = nexus.GetPlayerPos();

      for (int i = 0; i < Count; i++)
      {
        float angle = (360f / Count) * i + Random.Range(-15f, 15f);
        var pos = playerPos + new Vector2(
          Mathf.Cos(angle * Mathf.Deg2Rad),
          Mathf.Sin(angle * Mathf.Deg2Rad)) * (SPAWN_DIST + Random.Range(-1f, 1f));

        // ── 召唤前淡入动画 ──
        string id = MINION_IDS[Random.Range(0, MINION_IDS.Length)];
        var previewSprite = Resources.Load<Sprite>("Sprites/Enemies/Minions/" + id);
        if (previewSprite != null)
          ShowSummonFadeIn(new Vector3(pos.x, pos.y, 0f), previewSprite, Vector3.one * 1.3f, 0.5f);

        spawner.SpawnEnemy(id, pos);
      }
    }
  }
}
