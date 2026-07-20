using UnityEngine;

using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Core;
using Game.Shared.Runtime.Physics;
using Game.Shared.UI;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Gameplay.Input;
namespace Game.Shared.Player
{
  /// <summary>
  /// 2.5D 玩家移动：输入与位移?Vector2（XY 平面），?EntityPhysicsBody + Physics2D ?FixedUpdate 驱动?
  /// </summary>
  [DefaultExecutionOrder(-100)]
  [DisallowMultipleComponent]
  public class PlayerSphereController : MonoBehaviour
  {
    [SerializeField] float moveSpeed = 8f;

    EntityPhysicsBody _physics;
    Health _health;
    IPlayerHitStunGate _hitStunGate;
    IPlayerMovementInputGate _movementInputGate;

    float _baseMoveSpeed;
    float _equipSpeedBonus;
    float _buildMoveSpeedMult = 1f;
    float _killMoveSpeedBonus;

    public float CurrentMoveSpeed =>
      (_baseMoveSpeed + _equipSpeedBonus) * _buildMoveSpeedMult * (1f + _killMoveSpeedBonus);

    /// <summary>?RunBuildApplier 注入构筑移速倍率?/summary>
    public void SetBuildMoveSpeedMultipliers(float moveSpeedMult, float killMoveSpeedBonus)
    {
      _buildMoveSpeedMult = moveSpeedMult > 0f ? moveSpeedMult : 1f;
      _killMoveSpeedBonus = killMoveSpeedBonus;
    }

    void Awake()
    {
      _baseMoveSpeed = moveSpeed;
      _physics = GetComponent<EntityPhysicsBody>();
      if (_physics == null)
        _physics = EntityPhysicsBody.EnsurePlayer(gameObject);
    }

    void Start()
    {
      _health = GetComponent<Health>();
      _hitStunGate = GetComponent<IPlayerHitStunGate>();
      _movementInputGate = GetComponent<IPlayerMovementInputGate>();
    }

    void FixedUpdate()
    {
      if (_physics == null)
        return;

      if (_health != null && _health.IsDead)
        return;

      if (GameplayInputGateLocator.BlocksPlayerInput)
        return;

      if (_hitStunGate != null && _hitStunGate.IsInHitStun)
        return;

      if (_movementInputGate != null && _movementInputGate.BlocksMovementInput)
        return;

      var input = GameInputBindings.ReadMoveVector();
      if (input.sqrMagnitude < 0.0001f)
        return;

      _physics.QueuePlanarMove(input.normalized * (CurrentMoveSpeed * Time.fixedDeltaTime));
    }

    public void ApplyEquipmentSpeedBoost(float bonus)
    {
      _equipSpeedBonus = bonus;
    }
  }
}
