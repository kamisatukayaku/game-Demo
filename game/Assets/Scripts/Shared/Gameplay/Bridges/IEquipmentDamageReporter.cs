using Game.Shared.Gameplay.Bridges;
namespace Game.Shared.Gameplay.Bridges
{
  /// <summary>可选：装备调试器在造成伤害时回调?/summary>
  public interface IEquipmentDamageReporter
  {
    void OnDealDamage(float damage);
  }
}