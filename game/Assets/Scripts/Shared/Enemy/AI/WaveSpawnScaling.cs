namespace Game.Shared.Enemy.AI
{
  /// <summary>波次怪物强度缩放（由模式?WaveDirector 计算后传?Spawner）?/summary>
  public struct WaveSpawnScaling
  {
    public int waveNumber;
    public float hpMult;
    public float damageMult;
    public float speedMult;
    public float dashSpeedMult;

    public static WaveSpawnScaling Identity(int wave = 1) => new()
    {
      waveNumber = wave,
      hpMult = 1f,
      damageMult = 1f,
      speedMult = 1f,
      dashSpeedMult = 1f
    };
  }
}