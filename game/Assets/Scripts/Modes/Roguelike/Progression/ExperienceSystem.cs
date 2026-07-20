using UnityEngine;
using Game.Modes.Roguelike.Build.Apply;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Progression;
using Game.Modes.Roguelike.Loot;
using Game.Modes.Roguelike.UI;
using Game.Shared.Gameplay;
using Game.Shared.Gameplay.Events;
using Game.Shared.Runtime;
namespace Game.Modes.Roguelike.Progression
{
  /// <summary>
  /// 全局经验系统。升级所需经验? + 10x + x²（x 为当前等级）?
  /// </summary>
  public class ExperienceSystem : MonoBehaviour
  {
    [SerializeField] int startXp;
    [SerializeField] bool debugLog;

    static ExperienceSystem s_instance;

    int _totalXp;
    int _level = 1;
    EventListenerHandle _enemyKilledHandle;

    public static int TotalXp => s_instance != null ? s_instance._totalXp : 0;
    public static int Level => s_instance != null ? s_instance._level : 1;
    public static bool Exists => s_instance != null;

    public static event System.Action<int, int> XpChanged;
    public static event System.Action<int, int> LevelUp;

    public static int XpStepForLevel(int currentLevel)
    {
      LevelUpChoiceDatabase.EnsureLoaded();
      var curve = LevelUpChoiceDatabase.Curve;
      var levelIndex = Mathf.Max(0, currentLevel - 1);
      var baseXp = Mathf.Max(1, curve.xp_base);
      var growth = Mathf.Max(1f, curve.xp_growth);
      return Mathf.Max(1, Mathf.RoundToInt(baseXp * Mathf.Pow(growth, levelIndex)));
    }

    public static void EnsureExists()
    {
      if (s_instance != null) return;
      var go = new GameObject("_ExperienceSystem");
      go.AddComponent<ExperienceSystem>();
    }

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }

      s_instance = this;
      DontDestroyOnLoad(gameObject);
      _totalXp = startXp;
      RecalculateLevel();
      _enemyKilledHandle = GameEventBus.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
    }

    void OnDestroy()
    {
      if (_enemyKilledHandle.Valid)
        GameEventBus.Unsubscribe(_enemyKilledHandle);

      if (s_instance == this)
        s_instance = null;
    }

    void OnEnemyKilled(EnemyKilledEvent evt)
    {
      if (GameSessionConfig.IsBossRush)
        return;

      if (!IsPlayerAttribution(evt.Killer))
        return;

      var xpBonus = PlayerBuildModifiers.Current.xpOnKillBonus;
      if (xpBonus > 0f)
        Gain(Mathf.RoundToInt(xpBonus));
    }

    static bool IsPlayerAttribution(GameObject killer) =>
      PlayerCombatAttribution.IsPlayerOrOwned(killer);

    public static void Gain(int amount)
    {
      if (s_instance == null || amount <= 0)
        return;

      var mult = RunBuildCombatHooks.GetExperienceGainMultiplier();
      var gained = Mathf.Max(1, Mathf.RoundToInt(amount * mult));

      var oldLevel = s_instance._level;
      s_instance._totalXp += gained;
      s_instance.RecalculateLevel();
      XpChanged?.Invoke(s_instance._totalXp, s_instance._level);

      if (s_instance.debugLog)
        Debug.Log($"[ExperienceSystem] +{gained} XP ?total={s_instance._totalXp}, Lv.{s_instance._level}");

      if (s_instance._level > oldLevel)
      {
        if (s_instance.debugLog)
          Debug.Log($"[ExperienceSystem] Level up! {oldLevel} {s_instance._level}");

        LevelUp?.Invoke(oldLevel, s_instance._level);
        GameEventBus.Publish(new LevelUpEvent(oldLevel, s_instance._level, s_instance._totalXp));
      }
    }

    public static int XpRequiredForLevel(int level)
    {
      if (level <= 1)
        return 0;

      var total = 0;
      for (int lv = 1; lv < level; lv++)
        total += XpStepForLevel(lv);

      return total;
    }

    public static int XpToNextLevel()
    {
      if (s_instance == null)
        return 0;

      var nextThreshold = XpRequiredForLevel(s_instance._level + 1);
      return Mathf.Max(0, nextThreshold - s_instance._totalXp);
    }

    void RecalculateLevel()
    {
      Game.Modes.Roguelike.Progression.LevelUpChoiceDatabase.EnsureLoaded();
      var maxLevel = Game.Modes.Roguelike.Progression.LevelUpChoiceDatabase.Curve.max_level;
      var level = 1;
      while (level < maxLevel && _totalXp >= XpRequiredForLevel(level + 1))
        level++;
      _level = level;
    }

    public static int XpIntoCurrentLevel()
    {
      if (s_instance == null)
        return 0;

      return s_instance._totalXp - XpRequiredForLevel(s_instance._level);
    }

    public static int XpNeededForNextLevel()
    {
      if (s_instance == null)
        return 1;

      Game.Modes.Roguelike.Progression.LevelUpChoiceDatabase.EnsureLoaded();
      var maxLevel = Game.Modes.Roguelike.Progression.LevelUpChoiceDatabase.Curve.max_level;
      if (s_instance._level >= maxLevel)
        return 1;

      return Mathf.Max(1, XpStepForLevel(s_instance._level));
    }

    public static float GetLevelProgress01()
    {
      if (s_instance == null)
        return 0f;

      Game.Modes.Roguelike.Progression.LevelUpChoiceDatabase.EnsureLoaded();
      if (s_instance._level >= Game.Modes.Roguelike.Progression.LevelUpChoiceDatabase.Curve.max_level)
        return 1f;

      var into = XpIntoCurrentLevel();
      var needed = XpNeededForNextLevel();
      return Mathf.Clamp01(into / (float)needed);
    }

    public static void ResetToDefault()
    {
      if (s_instance == null)
        return;

      s_instance._totalXp = s_instance.startXp;
      s_instance.RecalculateLevel();
      XpChanged?.Invoke(s_instance._totalXp, s_instance._level);
    }

    public static void GrantBonusXp(int amount) => Gain(amount);
  }
}
