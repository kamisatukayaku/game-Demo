namespace Game.Shared.Stats
{
  public static class BuildStatWriterLocator
  {
    static IBuildStatWriter s_writer = NullBuildStatWriter.Instance;

    public static IBuildStatWriter Writer => s_writer;

    public static void Register(IBuildStatWriter writer)
    {
      s_writer = writer ?? NullBuildStatWriter.Instance;
    }

    public static void Clear()
    {
      s_writer = NullBuildStatWriter.Instance;
    }
  }
}