#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Game.Shared.Combat.Damage;
using Game.Shared.Enemy.AI;
using Game.Shared.Enemy.Spawn;
using UnityEngine;

namespace Game.Modes.Roguelike.Combat
{
  /// <summary>Editor / Development Build boss combat diagnostics overlay.</summary>
  [DisallowMultipleComponent]
  public sealed class BossCombatDebugOverlay : MonoBehaviour
  {
    static BossCombatDebugOverlay s_instance;
    GUIStyle _labelStyle;

    public static void EnsureExists()
    {
      if (s_instance != null)
        return;

      var go = new GameObject("_BossCombatDebugOverlay");
      s_instance = go.AddComponent<BossCombatDebugOverlay>();
    }

    void Awake()
    {
      if (s_instance != null && s_instance != this)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
    }

    void OnGUI()
    {
      var boss = FindActiveBoss();
      if (boss == null)
        return;

      EnsureStyle();
      var core = boss.Core;
      var health = core?.Health;
      var ctx = boss.GetComponent<BossWaveContext>();
      var attack = core?.Attack;
      var y = 12f;
      var x = 12f;
      var line = 18f;

      Draw(x, ref y, line, $"Boss: {ctx?.BossId ?? boss.name}");
      Draw(x, ref y, line, $"Wave: {ctx?.WaveNumber ?? 0}  Phase: {boss.CurrentPhase}");
      if (health != null)
        Draw(x, ref y, line, $"HP: {health.CurrentHp:0}/{health.MaxHp:0}");
      if (attack != null)
        Draw(x, ref y, line, $"Base Dmg: {attack.AttackDamage:0.0}");
      Draw(x, ref y, line, $"Skill: {boss.ActiveSkillId ?? "idle"}");
      Draw(x, ref y, line, $"AttackInst: {boss.ActiveAttackInstanceId}");
      Draw(x, ref y, line, $"Player DPS(1s): {BossCombatDebugLog.DpsLastSecond:0.0}");
      Draw(x, ref y, line, $"QuadrantBlock: {(ArenaQuadrantBlocker.IsActive ? ArenaQuadrantBlocker.BlockedQuadrant.ToString() : "off")}");
    }

    void Draw(float x, ref float y, float line, string text)
    {
      GUI.Label(new Rect(x, y, 520f, line + 4f), text, _labelStyle);
      y += line;
    }

    void EnsureStyle()
    {
      if (_labelStyle != null)
        return;

      _labelStyle = new GUIStyle(GUI.skin.label)
      {
        fontSize = 13,
        normal = { textColor = new Color(0.95f, 0.98f, 1f, 0.92f) }
      };
    }

    static BossCore FindActiveBoss()
    {
      BossCore best = null;
      foreach (var core in Object.FindObjectsOfType<EnemyCore>())
      {
        if (core == null || core.Health == null || core.Health.IsDead)
          continue;

        var boss = core.GetComponent<BossCore>();
        if (boss != null)
          best = boss;
      }

      return best;
    }
  }
}
#endif
