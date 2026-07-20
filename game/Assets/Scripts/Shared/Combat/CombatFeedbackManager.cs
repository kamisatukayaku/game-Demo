using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;

using Game.Shared.Combat.Health;
using Game.Shared.UI;
using HealthComp = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Combat
{
  /// <summary>
  /// 全局战斗反馈管理器（单例）?
  /// 管理伤害数字对象池、血条创建与回收?
  /// 首次调用静?API 时自动创建，无需手动挂载?
  /// 静?API：ShowDamageNumber / ShowHealthBar / HideHealthBar
  /// </summary>
  public class CombatFeedbackManager : MonoBehaviour
  {
    [Header("Damage Number")]
    [SerializeField] int damageNumberPoolSize = 30;
    [SerializeField] float damageNumberLifetime = 1.2f;
    [SerializeField] float damageNumberFloatSpeed = 1.8f;
    [SerializeField] float damageNumberFadeStart = 0.5f;

    [Header("HealthComp Bar")]
    [SerializeField] float healthBarHideDelay = 3f;
    [SerializeField] float healthBarFadeDuration = 0.5f;
    [SerializeField] float healthBarYOffset = 0.8f;

    [Header("Debug")]
    [SerializeField] bool debugLog;

    [Header("Colors")]
    [SerializeField] Color normalDamageColor = Color.white;
    [SerializeField] Color critDamageColor = new(1f, 0.85f, 0.2f, 1f);
    [SerializeField] Color ecoDamageColor = new(0.3f, 1f, 0.5f, 1f);
    [SerializeField] Color techDamageColor = new(0.4f, 0.9f, 1f, 1f);
    [SerializeField] Color playerDamageColor = new(1f, 0.4f, 0.35f, 1f);

    static CombatFeedbackManager s_instance;

    Canvas _canvas;
    readonly Queue<DamageNumber> _damagePool = new();
    readonly List<DamageNumber> _activeDamage = new();
    readonly Dictionary<HealthComp, HealthBar> _healthBars = new();

    public static bool Exists => s_instance != null;

    // ── Auto-Create ───────────────────────────────

    /// <summary>如果单例不存在，自动在场景中创建?/summary>
    static void EnsureExists()
    {
      if (s_instance != null) return;

      var go = new GameObject("_CombatFeedbackManager");
      go.AddComponent<CombatFeedbackManager>();
      // Awake sets s_instance, so it's immediately available
    }

    // ── Static API ──────────────────────────────────

    public static void ShowDamageNumber(
      Vector3 worldPos,
      float amount,
      DamageNumberStyle style = DamageNumberStyle.Normal)
    {
      EnsureExists();
      s_instance.SpawnDamage(worldPos, amount, style);
    }

    public static void ShowDamageNumber(Vector3 worldPos, float amount, Color color)
    {
      EnsureExists();
      s_instance.SpawnDamage(worldPos, amount, color);
    }

    public static void ShowHealthBar(HealthComp health)
    {
      if (health == null) return;
      EnsureExists();
      s_instance.EnsureHealthBar(health);
    }

    public static void HideHealthBar(HealthComp health)
    {
      if (health == null || s_instance == null) return;
      s_instance.RemoveHealthBar(health);
    }

    public static void UpdateBossShieldOverlay(HealthComp bossHealth, float shieldHp, float bossMaxHp)
    {
      if (bossHealth == null || bossMaxHp <= 0.01f)
        return;

      EnsureExists();
      if (!s_instance._healthBars.TryGetValue(bossHealth, out var bar))
        return;

      var hpPercent = bossHealth.CurrentHp / bossMaxHp;
      bar.UpdateShieldOverlay(hpPercent, shieldHp, bossMaxHp);
    }

    public static Color GetDamageColor(DamageNumberStyle style)
    {
      if (s_instance == null)
        return Color.white;

      if (StreamModeSettings.Enabled)
        return StreamModeSettings.HighContrastDamageColor(style);

      return style switch
      {
        DamageNumberStyle.Crit => s_instance.critDamageColor,
        DamageNumberStyle.Eco => s_instance.ecoDamageColor,
        DamageNumberStyle.Tech => s_instance.techDamageColor,
        DamageNumberStyle.Player => s_instance.playerDamageColor,
        _ => s_instance.normalDamageColor
      };
    }

    // ── Lifecycle ───────────────────────────────────

    void Awake()
    {
      if (s_instance != null)
      {
        Destroy(gameObject);
        return;
      }
      s_instance = this;
      DontDestroyOnLoad(gameObject);
      CreateCanvas();
      PrewarmPool();
    }

    void OnDestroy()
    {
      if (s_instance == this)
        s_instance = null;
    }

    void Update()
    {
      // Clean finished damage numbers
      for (int i = _activeDamage.Count - 1; i >= 0; i--)
      {
        if (_activeDamage[i] == null || _activeDamage[i].IsFinished)
        {
          if (_activeDamage[i] != null)
            RecycleDamage(_activeDamage[i]);
          _activeDamage.RemoveAt(i);
        }
      }
    }

    void LateUpdate()
    {
      // Update health bars (in LateUpdate so entity positions are final this frame)
      var deadBars = new List<HealthComp>();

      foreach (var kv in _healthBars)
      {
        if (kv.Key == null || kv.Key.IsDead)
        {
          kv.Value?.Hide(0f);
          deadBars.Add(kv.Key);
          continue;
        }
        kv.Value.UpdateFill(kv.Key.CurrentHp / kv.Key.MaxHp);
        kv.Value.UpdatePosition(kv.Key.transform.position + Vector3.up * healthBarYOffset);
      }

      // Clean up dead references
      foreach (var h in deadBars)
        _healthBars.Remove(h);
    }

    // ── Canvas Setup ────────────────────────────────

    void CreateCanvas()
    {
      var canvasGo = new GameObject("CombatFeedbackCanvas");
      canvasGo.transform.SetParent(transform);

      // Screen Space Overlay ?standard approach for floating UI
      _canvas = canvasGo.AddComponent<Canvas>();
      _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
      _canvas.sortingOrder = 100;

      var scaler = canvasGo.AddComponent<CanvasScaler>();
      scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
      scaler.referenceResolution = new Vector2(1920, 1080);
      scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
      scaler.matchWidthOrHeight = 0.5f;

      canvasGo.AddComponent<GraphicRaycaster>();
    }

    // ── Damage Number Pool ──────────────────────────

    void PrewarmPool()
    {
      for (int i = 0; i < damageNumberPoolSize; i++)
      {
        var dn = CreateDamageNumberInstance();
        dn.gameObject.SetActive(false);
        _damagePool.Enqueue(dn);
      }
    }

    DamageNumber CreateDamageNumberInstance()
    {
      var go = new GameObject("DamageNumber");
      go.transform.SetParent(_canvas.transform, false);

      var dn = go.AddComponent<DamageNumber>();
      go.SetActive(false);
      return dn;
    }

    DamageNumber GetFromPool()
    {
      if (_damagePool.Count > 0)
        return _damagePool.Dequeue();

      var dn = CreateDamageNumberInstance();
      return dn;
    }

    void RecycleDamage(DamageNumber dn)
    {
      dn.gameObject.SetActive(false);
      _damagePool.Enqueue(dn);
    }

    void SpawnDamage(Vector3 worldPos, float amount, DamageNumberStyle style)
    {
      SpawnDamage(worldPos, amount, GetDamageColor(style));
    }

    void SpawnDamage(Vector3 worldPos, float amount, Color color)
    {
      var dn = GetFromPool();
      dn.Setup(
        worldPos,
        amount,
        StreamModeSettings.BoostColor(color),
        damageNumberLifetime,
        damageNumberFloatSpeed,
        damageNumberFadeStart);
      dn.gameObject.SetActive(true);
      _activeDamage.Add(dn);
    }

    // ── HealthComp Bar Management ───────────────────────

    void EnsureHealthBar(HealthComp health)
    {
      if (_healthBars.TryGetValue(health, out var existing))
      {
        existing.Show();
        existing.ResetHideTimer(healthBarHideDelay);
        return;
      }

      if (debugLog)
        Debug.Log($"[CombatFeedback] Creating HealthBar for {health.name} (HP: {health.CurrentHp}/{health.MaxHp})");

      var go = new GameObject($"HealthBar_{health.name}");
      go.transform.SetParent(_canvas.transform, false);

      var hb = go.AddComponent<HealthBar>();
      hb.Setup(healthBarHideDelay, healthBarFadeDuration);
      hb.UpdateFill(health.CurrentHp / health.MaxHp);
      hb.UpdatePosition(health.transform.position + Vector3.up * healthBarYOffset);
      hb.Show();

      _healthBars[health] = hb;

      health.Died += () => RemoveHealthBar(health);
    }

    void RemoveHealthBar(HealthComp health)
    {
      if (_healthBars.TryGetValue(health, out var hb))
      {
        hb.Hide(0f);
        Destroy(hb.gameObject, healthBarFadeDuration + 0.1f);
        _healthBars.Remove(health);
      }
    }
  }

  public enum DamageNumberStyle
  {
    Normal,
    Crit,
    Eco,
    Tech,
    Player
  }
}