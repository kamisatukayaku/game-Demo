using UnityEngine;

using HealthComp = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Combat
{
  /// <summary>
  /// 伤害显示桥接器。监?HealthComp.Damaged，自动调?CombatFeedbackManager
  /// 弹出伤害数字并显?更新血条。挂载在需要战斗反馈的实体（Player / Enemy）根物体上?
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(HealthComp))]
  public class DamageDisplay : MonoBehaviour
  {
    [Header("Damage Number")]
    [SerializeField] bool showDamageNumbers = true;
    [SerializeField] DamageNumberStyle numberStyle = DamageNumberStyle.Normal;
    [SerializeField] float numberYOffset = 0.6f;

    [Header("HealthComp Bar")]
    [SerializeField] bool showHealthBar = true;

    public bool ShowHealthBar
    {
      get => showHealthBar;
      set => showHealthBar = value;
    }

    [Header("Debug")]
    [SerializeField] bool debugLog;

    HealthComp _health;

    public DamageNumberStyle NumberStyle
    {
      get => numberStyle;
      set => numberStyle = value;
    }

    void Awake()
    {
      _health = GetComponent<HealthComp>();
      _health.Damaged += OnDamaged;
      _health.Died += OnDied;
    }

    void OnDestroy()
    {
      if (_health != null)
      {
        _health.Damaged -= OnDamaged;
        _health.Died -= OnDied;
      }
    }

    void OnDamaged(float amount)
    {
      if (debugLog)
        Debug.Log($"[DamageDisplay] {name} took {amount:F1} dmg | showDmgNumbers={showDamageNumbers} showBar={showHealthBar} | CBFM.exists={CombatFeedbackManager.Exists} | HP={_health.CurrentHp}/{_health.MaxHp}");

      if (showDamageNumbers)
      {
        CombatFeedbackManager.ShowDamageNumber(
          transform.position + Vector3.up * numberYOffset,
          amount,
          numberStyle);
      }

      if (showHealthBar)
      {
        CombatFeedbackManager.ShowHealthBar(_health);
      }
    }

    void OnDied()
    {
      if (debugLog)
        Debug.Log($"[DamageDisplay] {name} died.");

      if (showHealthBar)
      {
        CombatFeedbackManager.HideHealthBar(_health);
      }
    }
  }
}