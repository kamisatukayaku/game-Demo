#if UNITY_EDITOR
using System.Collections;
using System.IO;
using Game.Modes.Roguelike.Build.Runtime;
using Game.Modes.Roguelike.Combat;
using Game.Modes.Roguelike.Diagnostics.RuntimeValidation;
using Game.Modes.Roguelike.Progression;
using NUnit.Framework;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Input;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode.Roguelike.RuntimeValidation
{
  public sealed class PortfolioDemoRecordingTests
  {
    const float DurationSeconds = 80f;
    const string OutputStem = "D:/game/demo_capture/portfolio_gameplay_demo";

    [UnityTest, Timeout(150000)]
    public IEnumerator RecordPortfolioGameplayDemo()
    {
      Directory.CreateDirectory("D:/game/demo_capture");
      var starter = RingArenaPlayModeSession.ResolveStarter("unified");
      yield return RingArenaPlayModeSession.LoadMainSceneAndBootstrap(
        starter.buildId, starter.weaponTheme, 20260717);

      RuntimeValidationSettings.RestoreTimeScale();
      RingArenaPlayModeSession.BeginCombatValidation();
      ApplyPortfolioBuild();

      var recorder = StartRecorder();
      Assert.IsNotNull(recorder);
      yield return new WaitForSecondsRealtime(1f);

      var elapsed = 0f;
      var levelUpVisibleFor = 0f;
      while (elapsed < DurationSeconds)
      {
        RuntimeValidationCombatAssist.Tick();
        if (LevelUpController.IsWaiting)
        {
          levelUpVisibleFor += Time.unscaledDeltaTime;
          if (levelUpVisibleFor >= 1.15f)
          {
            LevelUpController.ValidationAutoPickFirst();
            levelUpVisibleFor = 0f;
          }
        }
        else
        {
          levelUpVisibleFor = 0f;
          ValidationBlockingUiAutoResponder.Tick();
        }

        elapsed += Time.unscaledDeltaTime;
        yield return null;
      }

      recorder.StopRecording();
      yield return new WaitForSecondsRealtime(1f);
      Assert.IsTrue(File.Exists(OutputStem + ".mp4"), "Recorder did not create the portfolio MP4.");
    }

    static RecorderController StartRecorder()
    {
      var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
      var movie = ScriptableObject.CreateInstance<MovieRecorderSettings>();
      movie.name = "Portfolio Demo 1080p";
      movie.Enabled = true;
      movie.EncoderSettings = new CoreEncoderSettings
      {
        EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High,
        Codec = CoreEncoderSettings.OutputCodec.MP4
      };
      movie.ImageInputSettings = new GameViewInputSettings
      {
        OutputWidth = 1920,
        OutputHeight = 1080
      };
      movie.OutputFile = OutputStem;

      controllerSettings.AddRecorderSettings(movie);
      controllerSettings.SetRecordModeToManual();
      // Batch-mode encoding can render substantially below the target rate. Keep
      // real timestamps so the resulting movie preserves the actual demo duration
      // instead of squeezing all captured frames into a fixed-rate two-second clip.
      controllerSettings.FrameRatePlayback = FrameRatePlayback.Variable;
      controllerSettings.CapFrameRate = false;
      controllerSettings.FrameRate = 30f;
      RecorderOptions.VerboseMode = false;

      var controller = new RecorderController(controllerSettings);
      controller.PrepareRecording();
      Assert.IsTrue(controller.StartRecording(), "Unity Recorder failed to start.");
      return controller;
    }

    static void ApplyPortfolioBuild()
    {
      Apply("foundation_primary_shot");
      Apply("foundation_dash_strike");
      Apply("dash_melee_01");
      Apply("dash_melee_02");
      Apply("foundation_detached_origin");
      Apply("evo_trail_01_short_trail");
      Apply("evo_trail_02_long_trail");
      Apply("evo_trail_03_persistent_path");
      Apply("foundation_double_projectile");
      Apply("foundation_scatter");
      Apply("num_common_damage_01");
      Apply("num_common_attack_speed_01");
    }

    static void Apply(string id)
    {
      var def = LevelUpChoiceDatabase.FindById(id);
      if (def != null)
        RunBuildState.ApplyChoice(def);
    }
  }
}
#endif
