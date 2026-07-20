namespace Game.Shared.Stats
{
  sealed class NullBuildStatWriter : IBuildStatWriter
  {
    public static readonly NullBuildStatWriter Instance = new();
    NullBuildStatWriter() { }
    public void AddStat(string key, float value) { }
  }
}