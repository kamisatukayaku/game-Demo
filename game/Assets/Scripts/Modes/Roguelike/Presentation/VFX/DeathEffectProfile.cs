using UnityEngine;

namespace Game.Modes.Roguelike.Presentation.VFX
{
  [CreateAssetMenu(menuName = "Game/Roguelike/VFX/Death Effect Profile")]
  public sealed class DeathEffectProfile : ScriptableObject
  {
    [Header("Flash")]
    public float flashDuration = 0.08f;
    public float flashIntensity = 1f;

    [Header("Crack")]
    public float crackDuration = 0.16f;

    [Header("Fragments")]
    public int fragmentCount = 10;
    public float fragmentSpeed = 4.8f;
    public float dissolveTime = 0.42f;

    [Header("Particles")]
    public int particleCount = 14;
  }
}
