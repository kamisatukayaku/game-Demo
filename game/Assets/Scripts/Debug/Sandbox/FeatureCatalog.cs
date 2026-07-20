using System.Collections.Generic;

namespace Game.DevTools.Sandbox
{
  public static class FeatureCatalog
  {
    public static readonly IReadOnlyList<FeatureExecutionRecord> MageFeatures = new[]
    {
      Record("mage", "time_stop", "TimeStop"),
      Record("mage", "element_burst", "ElementBurst"),
      Record("mage", "element_melt", "ElementMelt"),
      Record("mage", "element_overload", "ElementOverload"),
      Record("mage", "cooldown_reset", "CooldownReset"),
      Record("mage", "gravity_well", "GravityWell"),
      Record("mage", "time_dilation_field", "TimeDilationField"),
      Record("mage", "fire_nova", "FireNova"),
    };

    public static readonly IReadOnlyList<FeatureExecutionRecord> RangeFeatures = new[]
    {
      Record("range", "pierce", "Pierce"),
      Record("range", "split", "Split"),
      Record("range", "chain", "Chain"),
      Record("range", "homing", "Homing"),
      Record("range", "explosion", "Explosion"),
      Record("range", "attack", "Attack"),
    };

    static FeatureExecutionRecord Record(string archetype, string id, string display) => new()
    {
      Archetype = archetype,
      FeatureId = id,
      DisplayName = display,
      Executed = false
    };
  }
}
