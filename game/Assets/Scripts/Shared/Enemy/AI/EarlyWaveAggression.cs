namespace Game.Shared.Enemy.AI
{
  /// <summary>前几波降低怪物攻击频率（更长前摇与冷却）?/summary>
  public static class EarlyWaveAggression
  {
    public static void GetMultipliers(int waveNumber, out float windupMult, out float cooldownMult)
    {
      switch (waveNumber)
      {
        case <= 2:
          windupMult = 1.45f;
          cooldownMult = 1.75f;
          return;
        case <= 5:
          windupMult = 1.28f;
          cooldownMult = 1.45f;
          return;
        case <= 8:
          windupMult = 1.12f;
          cooldownMult = 1.22f;
          return;
        default:
          windupMult = 1f;
          cooldownMult = 1f;
          return;
      }
    }

    public static float GetChargeCooldownMultiplier(int waveNumber)
    {
      return waveNumber switch
      {
        <= 2 => 2.5f,
        <= 4 => 2.15f,
        <= 6 => 1.85f,
        <= 8 => 1.6f,
        <= 10 => 1.4f,
        <= 12 => 1.22f,
        <= 14 => 1.1f,
        _ => 1f
      };
    }
  }
}
