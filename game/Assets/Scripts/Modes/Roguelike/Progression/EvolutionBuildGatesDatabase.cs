using System;
using System.Collections.Generic;
using Game.Shared.Data;
using UnityEngine;

namespace Game.Modes.Roguelike.Progression
{
  /// <summary>
  /// Build-locked growth gates: skill path mutual exclusion and starter-scoped detached-weapon evolution routes.
  /// </summary>
  public static class EvolutionBuildGatesDatabase
  {
    static readonly Dictionary<string, BuildGateDef> s_builds = new();
    static readonly Dictionary<string, StarterEvolutionRouteDef> s_starterEvolutionRoutes = new();
    static readonly HashSet<string> s_allEvolutionIds = new()
    {
      "laser", "missile", "explosion", "pulse", "boomerang", "trail"
    };

    static bool s_loaded;

    /// <summary>Sandbox / dev tools may bypass detached evolution route filtering.</summary>
    public static bool BypassDetachedRouteFilterForDebug { get; set; }

    public static void EnsureLoaded()
    {
      if (s_loaded)
        return;
      s_loaded = true;
      s_builds.Clear();
      s_starterEvolutionRoutes.Clear();
      JsonDataLoader.TryParse("weapons/evolution_build_gates", Parse);
    }

    public static IReadOnlyList<PathDef> GetPaths(string buildId)
    {
      EnsureLoaded();
      return s_builds.TryGetValue(NormalizeStarterId(buildId), out var def) ? def.paths : Array.Empty<PathDef>();
    }

    public static IReadOnlyList<string> GetAllowedEvolutions(string starterId)
    {
      EnsureLoaded();
      return s_starterEvolutionRoutes.TryGetValue(NormalizeStarterId(starterId), out var route)
        ? route.allowed_evolutions ?? Array.Empty<string>()
        : Array.Empty<string>();
    }

    public static bool IsUpgradeBlocked(
      LevelUpChoiceDatabase.UpgradeDef upgrade,
      string buildId,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (upgrade == null || string.IsNullOrEmpty(buildId))
        return false;

      if (buildId == ArenaBuildBootstrap.Unified || ArenaBuildBootstrap.IsUnifiedBuild)
        return false;

      if (IsDetachedEvolutionBlocked(upgrade, buildId, pickStacks))
        return true;

      EnsureLoaded();
      var normalized = NormalizeStarterId(buildId);
      if (!s_builds.TryGetValue(normalized, out var build) || build.paths == null || build.paths.Length < 2)
        return false;

      var upgradePath = ResolvePathForUpgrade(upgrade, build);
      if (upgradePath < 0)
        return false;

      var lockedPath = ResolveLockedPath(build, pickStacks);
      return lockedPath >= 0 && lockedPath != upgradePath;
    }

    /// <summary>
    /// Blocks detached-weapon evolution offers that do not belong to the current starter route.
    /// Old saves with cross-route progress keep runtime weapons but receive no new nodes.
    /// </summary>
    public static bool IsDetachedEvolutionBlocked(
      LevelUpChoiceDatabase.UpgradeDef upgrade,
      string buildId,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (BypassDetachedRouteFilterForDebug || upgrade == null)
        return false;

      var evolutionId = ResolveDetachedEvolutionId(upgrade);
      if (string.IsNullOrEmpty(evolutionId))
        return false;

      EnsureLoaded();
      var allowed = GetAllowedEvolutions(buildId);
      if (allowed.Count == 0)
        return false;

      foreach (var id in allowed)
      {
        if (id == evolutionId)
          return false;
      }

      return true;
    }

    /// <summary>Weight multiplier for detached evolution offers based on active route investment.</summary>
    public static float GetDetachedEvolutionWeightMultiplier(
      LevelUpChoiceDatabase.UpgradeDef upgrade,
      string buildId,
      IReadOnlyDictionary<string, int> pickStacks)
    {
      if (upgrade == null || pickStacks == null)
        return 1f;

      var evolutionId = ResolveDetachedEvolutionId(upgrade);
      if (string.IsNullOrEmpty(evolutionId))
        return 1f;

      if (HasEvolutionPick(evolutionId, pickStacks))
        return 1.45f;

      var allowed = GetAllowedEvolutions(buildId);
      foreach (var id in allowed)
      {
        if (id != evolutionId && HasEvolutionPick(id, pickStacks))
          return 1.12f;
      }

      return 1f;
    }

    public static string ResolveDetachedEvolutionId(LevelUpChoiceDatabase.UpgradeDef upgrade)
    {
      if (upgrade == null)
        return null;

      if (!string.IsNullOrEmpty(upgrade.mechanic_id) && s_allEvolutionIds.Contains(upgrade.mechanic_id))
        return upgrade.mechanic_id;

      if (upgrade.tags != null)
      {
        foreach (var tag in upgrade.tags)
        {
          if (s_allEvolutionIds.Contains(tag))
            return tag;
        }
      }

      if (!string.IsNullOrEmpty(upgrade.id) && upgrade.id.StartsWith("evo_", StringComparison.Ordinal))
      {
        var rest = upgrade.id.Substring(4);
        var separator = rest.IndexOf('_');
        if (separator > 0)
        {
          var candidate = rest.Substring(0, separator);
          if (s_allEvolutionIds.Contains(candidate))
            return candidate;
        }
      }

      return null;
    }

    public static string NormalizeStarterId(string buildId)
    {
      if (string.IsNullOrEmpty(buildId) || buildId == ArenaBuildBootstrap.Support)
        return ArenaBuildBootstrap.Unified;
      return buildId;
    }

    static bool HasEvolutionPick(string evolutionId, IReadOnlyDictionary<string, int> pickStacks)
    {
      if (string.IsNullOrEmpty(evolutionId) || pickStacks == null)
        return false;

      var prefix = $"evo_{evolutionId}_";
      foreach (var kv in pickStacks)
      {
        if (kv.Value <= 0 || string.IsNullOrEmpty(kv.Key))
          continue;
        if (kv.Key.StartsWith(prefix, StringComparison.Ordinal))
          return true;
      }

      return false;
    }

    public static int ResolvePathForUpgrade(LevelUpChoiceDatabase.UpgradeDef upgrade, BuildGateDef build)
    {
      if (upgrade == null || build?.paths == null)
        return -1;

      for (var i = 0; i < build.paths.Length; i++)
      {
        if (MatchesPath(upgrade, build.paths[i]))
          return i;
      }

      return -1;
    }

    public static int ResolveLockedPath(BuildGateDef build, IReadOnlyDictionary<string, int> pickStacks)
    {
      if (build?.paths == null || pickStacks == null)
        return -1;

      for (var i = 0; i < build.paths.Length; i++)
      {
        if (HasPathPick(build.paths[i], pickStacks))
          return i;
      }

      return -1;
    }

    static bool HasPathPick(PathDef path, IReadOnlyDictionary<string, int> pickStacks)
    {
      foreach (var kv in pickStacks)
      {
        if (kv.Value <= 0 || string.IsNullOrEmpty(kv.Key))
          continue;

        var stub = new LevelUpChoiceDatabase.UpgradeDef { id = kv.Key, tags = Array.Empty<string>() };
        if (MatchesPath(stub, path))
          return true;
      }

      return false;
    }

    static bool MatchesPath(LevelUpChoiceDatabase.UpgradeDef upgrade, PathDef path)
    {
      if (path?.id_prefixes != null)
      {
        foreach (var prefix in path.id_prefixes)
        {
          if (!string.IsNullOrEmpty(prefix) && upgrade.id.StartsWith(prefix, StringComparison.Ordinal))
            return true;
        }
      }

      if (path?.tags != null && upgrade.tags != null)
      {
        foreach (var tag in path.tags)
        {
          if (string.IsNullOrEmpty(tag))
            continue;
          foreach (var upgradeTag in upgrade.tags)
          {
            if (tag == upgradeTag)
              return true;
          }
        }
      }

      return false;
    }

    static void Parse(string json)
    {
      try
      {
        var root = JsonUtility.FromJson<GatesRoot>(json);
        if (root?.starter_evolution_routes != null)
        {
          foreach (var route in root.starter_evolution_routes)
          {
            if (route == null || string.IsNullOrEmpty(route.starter_id))
              continue;
            s_starterEvolutionRoutes[route.starter_id] = route;
          }
        }

        if (root?.builds == null)
          return;

        foreach (var build in root.builds)
        {
          if (build == null || string.IsNullOrEmpty(build.build_id))
            continue;
          s_builds[build.build_id] = build;
        }
      }
      catch (Exception e)
      {
        Debug.LogError($"[EvolutionBuildGatesDatabase] Parse failed: {e.Message}");
      }
    }

    [Serializable]
    sealed class GatesRoot
    {
      public StarterEvolutionRouteDef[] starter_evolution_routes;
      public BuildGateDef[] builds;
    }

    [Serializable]
    sealed class StarterEvolutionRouteDef
    {
      public string starter_id;
      public string display_name;
      public string[] allowed_evolutions;
    }

    [Serializable]
    public sealed class BuildGateDef
    {
      public string build_id;
      public PathDef[] paths;
    }

    [Serializable]
    public sealed class PathDef
    {
      public string id;
      public string display_name;
      public string[] tags;
      public string[] id_prefixes;
    }
  }
}
