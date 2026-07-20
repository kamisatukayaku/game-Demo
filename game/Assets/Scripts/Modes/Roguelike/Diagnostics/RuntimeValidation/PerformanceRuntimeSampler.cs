using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Game.Modes.Roguelike.Diagnostics.RuntimeValidation
{
  /// <summary>Play Mode frame/GC/object pressure sampling.</summary>
  public sealed class PerformanceRuntimeSampler
  {
    readonly List<float> _frameTimesMs = new();
    long _startManagedHeap;
    int _startGoCount;
    int _peakGoCount;
    int _peakProjectiles;
    int _peakTrailRenderers;
    int _peakLineRenderers;
    int _peakParticleSystems;

    float _sampleStart;
    string _scenarioId;

    public void BeginScenario(string scenarioId)
    {
      _scenarioId = scenarioId;
      _sampleStart = Time.realtimeSinceStartup;
      _frameTimesMs.Clear();
      _startManagedHeap = Profiler.GetMonoUsedSizeLong();
      _startGoCount = CountActiveObjects();
      _peakGoCount = _startGoCount;
      _peakProjectiles = 0;
      _peakTrailRenderers = 0;
      _peakLineRenderers = 0;
      _peakParticleSystems = 0;
    }

    public void TickFrame()
    {
      var dt = Time.unscaledDeltaTime * 1000f;
      _frameTimesMs.Add(dt);
      _peakGoCount = Mathf.Max(_peakGoCount, CountActiveObjects());
      _peakProjectiles = Mathf.Max(_peakProjectiles, Object.FindObjectsByType<Game.Shared.Projectile.StraightProjectile>(FindObjectsSortMode.None).Length);
      _peakTrailRenderers = Mathf.Max(_peakTrailRenderers, Object.FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None).Length);
      _peakLineRenderers = Mathf.Max(_peakLineRenderers, Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Length);
      _peakParticleSystems = Mathf.Max(_peakParticleSystems, Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length);
    }

    public ScenarioResult EndScenario()
    {
      var duration = Time.realtimeSinceStartup - _sampleStart;
      var endHeap = Profiler.GetMonoUsedSizeLong();
      var endGo = CountActiveObjects();
      _frameTimesMs.Sort();
      var avg = Average(_frameTimesMs);
      var p95 = Percentile(_frameTimesMs, 0.95f);
      var p99 = Percentile(_frameTimesMs, 0.99f);
      var heapDelta = endHeap - _startManagedHeap;
      var goDelta = endGo - _startGoCount;
      var pass = p99 < 50f && heapDelta < 50 * 1024 * 1024 && goDelta <= 20;

      return new ScenarioResult(
        _scenarioId,
        duration,
        avg,
        p95,
        p99,
        heapDelta,
        goDelta,
        _peakGoCount,
        _peakProjectiles,
        _peakTrailRenderers,
        _peakLineRenderers,
        _peakParticleSystems,
        pass);
    }

    static int CountActiveObjects()
    {
      var all = Resources.FindObjectsOfTypeAll<GameObject>();
      var count = 0;
      foreach (var go in all)
      {
        if (go != null && go.scene.isLoaded && go.activeInHierarchy)
          count++;
      }

      return count;
    }

    static float Average(List<float> values)
    {
      if (values.Count == 0)
        return 0f;
      var sum = 0f;
      foreach (var v in values)
        sum += v;
      return sum / values.Count;
    }

    static float Percentile(List<float> sorted, float p)
    {
      if (sorted.Count == 0)
        return 0f;
      var index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * p) - 1, 0, sorted.Count - 1);
      return sorted[index];
    }

    public static string CsvHeader() =>
      "scenario,duration_sec,avg_ms,p95_ms,p99_ms,heap_delta_bytes,go_delta,peak_go,peak_projectiles,peak_trails,peak_lines,peak_particles,pass";

    public static string ToCsvRow(ScenarioResult result)
    {
      var sb = new StringBuilder();
      sb.Append(result.ScenarioId).Append(',')
        .Append(result.DurationSec.ToString("F2")).Append(',')
        .Append(result.AvgFrameMs.ToString("F2")).Append(',')
        .Append(result.P95FrameMs.ToString("F2")).Append(',')
        .Append(result.P99FrameMs.ToString("F2")).Append(',')
        .Append(result.HeapDeltaBytes).Append(',')
        .Append(result.GoDelta).Append(',')
        .Append(result.PeakGoCount).Append(',')
        .Append(result.PeakProjectiles).Append(',')
        .Append(result.PeakTrailRenderers).Append(',')
        .Append(result.PeakLineRenderers).Append(',')
        .Append(result.PeakParticleSystems).Append(',')
        .Append(result.Pass ? "PASS" : "FAIL").Append('\n');
      return sb.ToString();
    }

    public readonly struct ScenarioResult
    {
      public readonly string ScenarioId;
      public readonly float DurationSec;
      public readonly float AvgFrameMs;
      public readonly float P95FrameMs;
      public readonly float P99FrameMs;
      public readonly long HeapDeltaBytes;
      public readonly int GoDelta;
      public readonly int PeakGoCount;
      public readonly int PeakProjectiles;
      public readonly int PeakTrailRenderers;
      public readonly int PeakLineRenderers;
      public readonly int PeakParticleSystems;
      public readonly bool Pass;

      public ScenarioResult(
        string scenarioId,
        float durationSec,
        float avgFrameMs,
        float p95FrameMs,
        float p99FrameMs,
        long heapDeltaBytes,
        int goDelta,
        int peakGoCount,
        int peakProjectiles,
        int peakTrailRenderers,
        int peakLineRenderers,
        int peakParticleSystems,
        bool pass)
      {
        ScenarioId = scenarioId;
        DurationSec = durationSec;
        AvgFrameMs = avgFrameMs;
        P95FrameMs = p95FrameMs;
        P99FrameMs = p99FrameMs;
        HeapDeltaBytes = heapDeltaBytes;
        GoDelta = goDelta;
        PeakGoCount = peakGoCount;
        PeakProjectiles = peakProjectiles;
        PeakTrailRenderers = peakTrailRenderers;
        PeakLineRenderers = peakLineRenderers;
        PeakParticleSystems = peakParticleSystems;
        Pass = pass;
      }
    }
  }
}
