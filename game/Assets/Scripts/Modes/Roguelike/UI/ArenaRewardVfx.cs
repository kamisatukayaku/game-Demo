using UnityEngine;

namespace Game.Modes.Roguelike.UI
{
  /// <summary>Simple reward burst visuals for chests and first-kill bonuses.</summary>
  public static class ArenaRewardVfx
  {
    public static void PlayChest(Vector3 position)
    {
      SpawnBurst(position, new Color(1f, 0.88f, 0.35f, 0.9f), 18, 0.35f, 2.2f);
    }

    public static void PlayRareGolden(Vector3 position)
    {
      SpawnBurst(position, new Color(1f, 0.78f, 0.12f, 0.95f), 42, 0.42f, 3.6f);
      SpawnBurst(position + Vector3.up * 0.2f, new Color(1f, 0.95f, 0.55f, 0.75f), 24, 0.28f, 2.4f);
    }

    public static void PlayFirstKill(Vector3 position, bool isBoss)
    {
      SpawnBurst(
        position,
        isBoss ? new Color(1f, 0.55f, 0.2f, 0.95f) : new Color(0.55f, 0.95f, 1f, 0.85f),
        isBoss ? 36 : 14,
        isBoss ? 0.55f : 0.28f,
        isBoss ? 3.4f : 2f);
    }

    static void SpawnBurst(Vector3 position, Color color, int count, float size, float speed)
    {
      var go = new GameObject("ArenaRewardBurst");
      go.transform.position = position;
      var ps = go.AddComponent<ParticleSystem>();
      ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
      var main = ps.main;
      main.playOnAwake = false;
      main.loop = false;
      main.startLifetime = 0.45f;
      main.startSpeed = speed;
      main.startSize = size;
      main.startColor = color;
      main.maxParticles = count;
      var emission = ps.emission;
      emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
      var shape = ps.shape;
      shape.enabled = true;
      shape.shapeType = ParticleSystemShapeType.Circle;
      shape.radius = 0.2f;
      ps.Play(true);
      Object.Destroy(go, 1.2f);
    }
  }
}
