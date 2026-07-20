using UnityEngine;
using Game.Modes.Roguelike.Archetypes.Mage;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Build.Stats;
using Game.Shared.Core;
using Game.Shared.Combat.Damage;
using Health = global::Game.Shared.Combat.Health.Health;
using Game.Shared.Gameplay.Bridges;
using Game.Shared.Gameplay.Input;

namespace Game.Modes.Roguelike.Gameplay
{
  sealed class RoguelikeSkillSystem : ISkillSystem
  {
    public static readonly RoguelikeSkillSystem Instance = new();

    SkillContext _context;

    public SkillContext Context => _context;

    public void RefreshFromBuild()
    {
      _context = MageContextBuilder.Build();
    }

    public float GetSkillDamageMult() => _context.SkillDamageMult > 0f
      ? _context.SkillDamageMult
      : RunBuildCombatHooks.GetSkillDamageMult();

    public DamageRequest BuildSkillDamageRequest(float baseDamage, GameObject caster) =>
      RunBuildCombatHooks.BuildSkillDamageRequest(baseDamage, caster);
  }

  sealed class RoguelikeArenaLayout : IArenaLayout
  {
    public static readonly RoguelikeArenaLayout Instance = new();

    public bool IsActive => CircleArenaController.IsActive;
    public Vector2 Center => CircleArenaController.Center;
    public float PathRadius => CircleArenaController.PathRadius;
    public float FullCombatRange => CircleArenaController.FullCombatRange;
    public float AngleAtPosition(Vector2 position) => CircleArenaController.AngleAtPosition(position);
    public Vector2 PositionAtAngle(float angleRadians) => CircleArenaController.PositionAtAngle(angleRadians);

    public bool ComputeChord(
      Vector2 enemyPlanar,
      Vector2 direction,
      out Vector2 start,
      out Vector2 end,
      out float dashDistance)
    {
      CircleArenaController.ComputeChord(enemyPlanar, direction, out start, out end, out dashDistance);
      return IsActive;
    }

    public Vector2 GetSpawnPointOnCircle(Vector2 hintPos) =>
      CircleArenaController.GetSpawnPointOnCircle(hintPos);
  }

  sealed class RoguelikeGameplayInputGate : IGameplayInputGate
  {
    public static readonly RoguelikeGameplayInputGate Instance = new();

    public bool BlocksPlayerInput =>
      Game.Shared.UI.KeyBindingsUI.IsOpen;
  }
}
