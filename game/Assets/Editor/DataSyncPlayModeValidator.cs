using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
  /// <summary>Play Mode 前校验 data/ 与 Resources 镜像一致（不自动复制）。</summary>
  [InitializeOnLoad]
  public static class DataSyncPlayModeValidator
  {
    const string PrefSkipOnce = "DataSyncPlayModeValidator.SkipOnce";

    static DataSyncPlayModeValidator()
    {
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
      if (state != PlayModeStateChange.ExitingEditMode)
        return;

      if (SessionState.GetBool(PrefSkipOnce, false))
      {
        SessionState.EraseBool(PrefSkipOnce);
        return;
      }

      if (DataSyncMenu.ValidateJsonMirrors(out var error))
        return;

      EditorUtility.DisplayDialog(
        "Resources 数据镜像过期",
        $"Play Mode 可能读取过期配置：\n{error}\n\n请执行 Tools → Sync Data → Resources/Data 后重试。",
        "取消进入 Play Mode");

      EditorApplication.isPlaying = false;
    }

    [MenuItem("Tools/Sync Data/Validate Mirrors (No Copy)")]
    public static void ValidateFromMenu()
    {
      if (DataSyncMenu.ValidateJsonMirrors(out var error))
      {
        Debug.Log("[DataSync] JSON mirrors are up to date.");
        return;
      }

      Debug.LogError($"[DataSync] Mirror validation failed: {error}");
    }
  }
}
