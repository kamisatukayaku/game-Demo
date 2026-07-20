namespace Game.Shared.Enemy.AI
{
  public enum EnemyAttackKind { Melee, Ranged }

  public enum EnemyDeliveryType { Melee, Projectile, Beam, ChargeDash }

  public enum EnemyAiStyle { MeleeChase, RangedKite, RangedSniper, BruteTank, LaneFollow, Stationary, FastWisp }

  public enum EnemyOrigin { Wild, Camp }
}
