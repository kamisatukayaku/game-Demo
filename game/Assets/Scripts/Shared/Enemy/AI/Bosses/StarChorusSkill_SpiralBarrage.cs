using UnityEngine;

namespace Game.Shared.Enemy.AI
{
  /// <summary>
  /// 螺旋弹幕 — 中途翻转旋转方向。Priority: 0, Cooldown: 4.5s
  /// </summary>
  public class StarChorusSkill_SpiralBarrage : BossSkillBase
  {
    const int SHOT_COUNT = 14;
    const float SHOT_INTERVAL = 0.12f;
    const float ANGLE_STEP = 22f;
    const float DAMAGE_MULT = 0.55f;

    int _shotIndex;
    float _shotTimer;
    float _baseAngle;
    int _spinSign;

    public StarChorusSkill_SpiralBarrage()
    {
      Id = MiniBossStarChorus.SKILL_SPIRAL_BARRAGE;
      Priority = 0;
      Cooldown = 4.5f;
    }

    public override bool CanTrigger(BossCore boss) => boss is MiniBossStarChorus;

    public override void OnEnter(BossCore boss)
    {
      _shotIndex = 0;
      _shotTimer = 0f;
      _spinSign = Random.value > 0.5f ? 1 : -1;
      var chorus = boss as MiniBossStarChorus;
      if (chorus == null) return;

      var toPlayer = chorus.DirToPlayer();
      _baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
      FireShot(chorus);
    }

    public override State OnUpdate(BossCore boss, float dt, float skillDt)
    {
      var chorus = boss as MiniBossStarChorus;
      if (chorus == null) return State.Completed;

      _shotTimer += dt;
      if (_shotTimer >= SHOT_INTERVAL)
      {
        _shotTimer -= SHOT_INTERVAL;
        _shotIndex++;
        if (_shotIndex < SHOT_COUNT)
          FireShot(chorus);
      }

      return _shotIndex >= SHOT_COUNT ? State.Completed : State.Running;
    }

    public override void OnExit(BossCore boss) { }

    void FireShot(MiniBossStarChorus chorus)
    {
      if (_shotIndex == SHOT_COUNT / 2)
        _spinSign *= -1;

      float angle = (_baseAngle + _spinSign * ANGLE_STEP * _shotIndex) * Mathf.Deg2Rad;
      var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
      chorus.SpawnDirectionalProjectile(dir, DAMAGE_MULT);
    }
  }
}
