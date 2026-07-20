using UnityEngine;

using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
namespace Game.Shared.Player
{
  public enum PlayerAttackMode
  {
    Hybrid,
    MeleeOnly,
    RangedSingle,
    RangedRapid,
    Laser
  }

  /// <summary>
  /// 玩家攻击：按住绑定键（默认鼠标左键）朝鼠标方向攻击；?<see cref="PlayerAttackDirector"/> 执行?
  /// </summary>
  [DisallowMultipleComponent]
  [RequireComponent(typeof(PlayerAttackDirector))]
  public class PlayerAutoAttack : MonoBehaviour
  {
    [Header("Mode (legacy display / gizmos)")]
    [SerializeField] PlayerAttackMode attackMode = PlayerAttackMode.Hybrid;

    [Header("Hybrid fallback (no weapon equipped)")]
    [SerializeField] float hybridMeleeRange = 1.6f;
    [SerializeField] string hybridRangedProfileId = "weapon_starter_bolt";

    [Header("Debug")]
    [SerializeField] bool debugLogAttacks;

    PlayerAttackDirector _director;

    public PlayerAttackMode CurrentMode => attackMode;

    void Awake()
    {
      _director = GetComponent<PlayerAttackDirector>();
      if (_director == null)
        _director = gameObject.AddComponent<PlayerAttackDirector>();

      _director.BindLegacyHost(this);
      _director.SetHybridFallback(hybridMeleeRange, hybridRangedProfileId);

      if (GetComponent<Health>() == null)
        Debug.LogError("[PlayerAutoAttack] No Health component on Player!", this);

      DamageReceiver.Ensure(gameObject);
    }

    void Update()
    {
      if (_director != null)
        _director.Tick();
    }

    public void SetEquipmentModifiers(
      float attackMult,
      float attackSpeedMult,
      float rangeAdd,
      float critChance,
      float critDamage,
      float lifesteal,
      float cooldownMult,
      float attackFlatAdd = 0f)
    {
      EnsureDirector();
      _director.SetEquipmentModifiers(
        attackMult, attackSpeedMult, rangeAdd,
        critChance, critDamage, lifesteal, cooldownMult, attackFlatAdd);
    }

    /// <summary>根据武器 attack_profile_id 切换攻击 profile?/summary>
    void EnsureDirector()
    {
      if (_director != null)
        return;

      _director = GetComponent<PlayerAttackDirector>();
      if (_director == null)
        _director = gameObject.AddComponent<PlayerAttackDirector>();

      _director.BindLegacyHost(this);
    }

    public void SetAttackModeFromProfile(string profileId)
    {
      if (string.IsNullOrEmpty(profileId))
      {
        Debug.LogWarning("[PlayerAutoAttack] SetAttackModeFromProfile: empty profileId.");
        return;
      }

      EnsureDirector();

      var profile = AttackProfileDatabase.Get(profileId);
      attackMode = InferLegacyMode(profileId, profile?.delivery);

      _director.SetAttackProfile(profileId);
      _director.SetHybridFallback(
        profile?.range > 0f ? profile.range : hybridMeleeRange,
        hybridRangedProfileId);

      if (debugLogAttacks)
        Debug.Log($"[PlayerAutoAttack] profile='{profileId}' delivery={profile?.delivery} mode={attackMode}");
    }

    static PlayerAttackMode InferLegacyMode(string profileId, string delivery)
    {
      if (!string.IsNullOrEmpty(delivery))
      {
        return delivery switch
        {
          "melee" => PlayerAttackMode.MeleeOnly,
          "projectile" when profileId.Contains("rapid") || profileId.Contains("gatling")
            => PlayerAttackMode.RangedRapid,
          "projectile" => PlayerAttackMode.RangedSingle,
          "beam" => PlayerAttackMode.Laser,
          _ => PlayerAttackMode.Hybrid
        };
      }

      return profileId switch
      {
        "weapon_theme_melee" or "weapon_starter_melee" or "weapon_eco_vine_whip" or "weapon_cursed_melee_slam" or "weapon_theme_reflect" => PlayerAttackMode.MeleeOnly,
        "weapon_theme_ranged" or "weapon_starter_bolt" or "weapon_tech_shock_bolt" or "weapon_spore_launcher"
          or "weapon_legendary_storm" or "weapon_cursed_heavy_pulse" => PlayerAttackMode.RangedSingle,
        "weapon_starter_rapid" or "weapon_rare_gatling" => PlayerAttackMode.RangedRapid,
        "weapon_starter_laser" or "weapon_epic_plasma_beam" or "weapon_epic_railgun"
          or "weapon_legendary_void_beam" => PlayerAttackMode.Laser,
        "weapon_theme_mage" or "skill_mage_arcane_bolt" => PlayerAttackMode.RangedSingle,
        "weapon_theme_aura" => PlayerAttackMode.RangedSingle,
        _ => PlayerAttackMode.Hybrid
      };
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
      var director = GetComponent<PlayerAttackDirector>();
      var range = director != null ? director.GetGizmoRange() : hybridMeleeRange;
      Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.35f);
      Gizmos.DrawWireSphere(transform.position, range);
    }
#endif
  }
}