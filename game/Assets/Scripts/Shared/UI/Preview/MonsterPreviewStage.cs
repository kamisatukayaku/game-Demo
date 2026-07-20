using System.Collections;
using UnityEngine;

using Game.Shared.Combat.Damage;
using UnityEngine.Rendering.Universal;
using Game.Shared.Core;
using Game.Shared.Enemy.Database;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.Visual;
using Game.Shared.Laser;
using Game.Shared.Projectile;
using Game.Shared.Enemy.AI;

namespace Game.UI
{
  /// <summary>
  /// 独立世界空间预览台：RenderTexture + 专用相机，复用战斗 VFX（激光粒子、蓄力、弹幕等）。
  /// </summary>
  public class MonsterPreviewStage : MonoBehaviour
  {
    public const string PreviewLayerName = "MonsterPreview";
    static readonly Vector3 StageWorldOrigin = new(5000f, 5000f, 0f);
    static readonly Color StageBackground = new(0.04f, 0.06f, 0.08f, 1f);
    static readonly Color LaserColor = new(1f, 0.42f, 0.15f, 1f);
    static readonly Color ProjectileColor = new(1f, 0.96f, 0.94f, 1f);
    const int RenderWidth = 512;
    const int RenderHeight = 320;
    const float CameraOrthoSize = 3.1f;

    Camera _camera;
    RenderTexture _renderTexture;
    Transform _stageRoot;
    Transform _dummyTarget;
    GameObject _enemyRoot;
    Transform _enemyVisual;
    EnemyMotionVisual _motionVisual;
    int _previewLayer = -1;

    /// <summary>UI 平面精灵镜像：保证与 Assets/Sprites/Enemies/Minions 素材一致的 2D 显示。</summary>
    public struct PreviewUiMirror
    {
      public RectTransform Sprite;
      public Vector2 HomeAnchoredPos;
      public float PixelsPerWorldUnit;
      public bool IsValid => Sprite != null;
    }

    public const float DefaultUiPixelsPerWorldUnit = 28f;
    public RenderTexture Texture => _renderTexture;

    public void EnsureBuilt()
    {
      if (_renderTexture != null)
        return;

      _previewLayer = LayerMask.NameToLayer(PreviewLayerName);
      if (_previewLayer < 0)
        _previewLayer = 0;

      _renderTexture = new RenderTexture(RenderWidth, RenderHeight, 16, RenderTextureFormat.ARGB32)
      {
        name = "MonsterPreviewRT",
        antiAliasing = 2
      };
      _renderTexture.Create();

      // 独立于 UI 层级，避免 Canvas 缩放/旋转影响世界空间预览。
      _stageRoot = new GameObject("PreviewStageRoot").transform;
      _stageRoot.SetParent(null, false);
      _stageRoot.position = StageWorldOrigin;
      _stageRoot.rotation = Quaternion.identity;
      _stageRoot.localScale = Vector3.one;
      SetLayer(_stageRoot.gameObject);

      var camGo = new GameObject("PreviewStageCamera");
      camGo.transform.SetParent(_stageRoot, false);
      camGo.transform.localPosition = new Vector3(0f, 0f, -10f);
      camGo.transform.localRotation = Quaternion.identity;
      _camera = camGo.AddComponent<Camera>();
      _camera.orthographic = true;
      _camera.orthographicSize = CameraOrthoSize;
      _camera.clearFlags = CameraClearFlags.SolidColor;
      _camera.backgroundColor = StageBackground;
      _camera.targetTexture = _renderTexture;
      _camera.cullingMask = 1 << _previewLayer;
      _camera.depth = -50f;
      _camera.nearClipPlane = 0.1f;
      _camera.farClipPlane = 20f;
      _camera.useOcclusionCulling = false;
      _camera.transparencySortMode = TransparencySortMode.Orthographic;
      ConfigureUrpCamera(_camera);
      SetLayer(camGo);

      var targetGo = new GameObject("PreviewTarget");
      targetGo.transform.SetParent(_stageRoot, false);
      targetGo.transform.localPosition = new Vector3(4.2f, 0f, 0f);
      _dummyTarget = targetGo.transform;
      SetLayer(targetGo);

      _motionVisual = null;
    }

    public void SetActive(bool active) { if (_stageRoot != null) _stageRoot.gameObject.SetActive(active); }

    public void SetEnemy(EnemyDatabase.EnemyDef def)
    {
      EnsureBuilt();
      StopAllEffects();
      DestroyEnemy();

      if (def == null)
        return;

      _enemyRoot = new GameObject($"PreviewEnemy_{def.id}");
      _enemyRoot.transform.SetParent(_stageRoot, false);
      _enemyRoot.transform.localPosition = Vector3.zero;
      SetLayer(_enemyRoot);

      var scale = CombatPlaceholderVisual.ResolveScale(def.id, def.visual_scale)
        * CombatPlaceholderVisual.MinionDisplayScaleMultiplier;

      if (!CombatSpriteVisual.ApplyMinion(_enemyRoot, def.id, scale))
      {
        Debug.LogError(
          $"[MonsterPreview] Sprite not found for '{def.id}'. " +
          "Use Assets/Sprites/Enemies/Minions/ and sync via Tools → Sync Data.");
      }

      SetLayerRecursive(_enemyRoot);
      _enemyVisual = _enemyRoot.transform.Find(EntityPlaceholderVisual.DefaultChildName);
      if (_enemyVisual != null)
      {
        _enemyVisual.localRotation = Quaternion.identity;
        var sr = _enemyVisual.GetComponent<SpriteRenderer>();
        if (sr != null)
          sr.enabled = false;
      }
      _motionVisual = EnemyMotionVisual.Ensure(_enemyRoot);
      if (def.attack_mode == "laser")
        _motionVisual.EnableSlowIdleSpin(28f);
      SetLayerRecursive(_enemyRoot);
    }

    public IEnumerator PlayIdleMotion(string motion, PreviewUiMirror ui = default)
    {
      if (_enemyVisual == null) yield break;
      var t = 0f; var homeScale = _enemyVisual.localScale; var homePos = _enemyRoot.transform.localPosition; var uiHomeScale = ui.IsValid ? ui.Sprite.localScale : Vector3.one;
      while (true)
      {
        t += Time.deltaTime;
        switch (motion)
        {
          case "spin": _enemyVisual.localRotation = Quaternion.Euler(0f, 0f, -120f * t); if (ui.IsValid) ui.Sprite.localRotation = Quaternion.Euler(0f, 0f, -120f * t); break;
          case "slow_spin": { var deg = -28f * t; _enemyVisual.localRotation = Quaternion.Euler(0f, 0f, deg); if (ui.IsValid) ui.Sprite.localRotation = Quaternion.Euler(0f, 0f, deg); break; }
          case "pulse": _enemyVisual.localScale = homeScale * (1f + 0.08f * Mathf.Sin(t * 4f)); if (ui.IsValid) ui.Sprite.localScale = uiHomeScale * (1f + 0.08f * Mathf.Sin(t * 4f)); break;
          case "drift": { var offset = new Vector3(Mathf.Sin(t * 2.2f) * 0.12f, Mathf.Cos(t * 1.8f) * 0.12f, 0f); _enemyRoot.transform.localPosition = homePos + offset; if (ui.IsValid) { var uiOffset = new Vector2(offset.x, offset.y) * ui.PixelsPerWorldUnit; ui.Sprite.anchoredPosition = ui.HomeAnchoredPos + uiOffset; } break; }
          default: yield break;
        }
        yield return null;
      }
    }

    public IEnumerator PlayChargeAttack(float windup, float dashSpeedMult, PreviewUiMirror ui = default)
    {
      if (_enemyRoot == null) yield break;
      _motionVisual?.UseChargeSpin();
      var start = _enemyRoot.transform.localPosition;
      var toTarget = _dummyTarget.localPosition - start; var dashDist = toTarget.magnitude;
      var dashTime = dashSpeedMult > 0f ? Mathf.Clamp(dashDist / (15f * dashSpeedMult), 0.18f, 0.58f) : 0.35f;
      yield return ChargeEnemyAttack.ExecutePreview(_enemyRoot.transform, _dummyTarget, windup, dashDist, dashTime, _previewLayer, pos => SyncUiPosition(ui, start, pos));
      _motionVisual?.ResetSpinMultiplier();
      var elapsed = 0f; var returnTime = 0.25f; var end = _enemyRoot.transform.localPosition;
      while (elapsed < returnTime) { elapsed += Time.deltaTime; var pos = Vector3.Lerp(end, start, elapsed / returnTime); _enemyRoot.transform.localPosition = pos; SyncUiPosition(ui, start, pos); yield return null; }
      _enemyRoot.transform.localPosition = start; ResetUiMirror(ui);
    }

    public IEnumerator PlayLaserAttack(float windup, string attackProfileId, PreviewUiMirror ui = default)
    {
      if (_enemyRoot == null) yield break;
      var profile = AttackProfileDatabase.Get(attackProfileId);
      var range = profile?.range ?? 7.5f; var halfWidth = profile?.beam_half_width ?? 0.22f; var previewRange = Mathf.Min(range, 5.5f);
      var fireDir = (_dummyTarget.position - _enemyRoot.transform.position); fireDir.z = 0f;
      if (fireDir.sqrMagnitude < 0.0001f) fireDir = Vector3.right; else fireDir.Normalize();
      _motionVisual?.BeginLaserCharge(windup, new Vector2(fireDir.x, fireDir.y));
      var elapsed = 0f; var homeScale = _enemyVisual != null ? _enemyVisual.localScale : Vector3.one;
      while (elapsed < windup)
      {
        elapsed += Time.deltaTime;
        if (_enemyVisual != null) { var t = elapsed / windup; _enemyVisual.localScale = homeScale * Mathf.Lerp(1f, 0.82f, t); if (ui.IsValid) ui.Sprite.localScale = Vector3.one * Mathf.Lerp(1f, 0.82f, t); }
        yield return null;
      }
      _motionVisual?.EndLaserCharge(new Vector2(fireDir.x, fireDir.y));
      if (_enemyVisual != null) _enemyVisual.localScale = homeScale;
      if (ui.IsValid) ui.Sprite.localScale = Vector3.one;
      var duration = profile?.beam_duration > 0f ? profile.beam_duration : 0.6f;
      var tick = profile?.beam_tick_interval > 0f ? profile.beam_tick_interval : 0.15f;
      var settings = LaserBeamSettings.FromProfile(LaserColor, previewRange, halfWidth, duration, tick);
      var request = DamageRequest.Direct(profile?.base_damage ?? 0f, profile?.damage_type ?? "energy", "monster", _enemyRoot);
      var attack = LaserBeamPool.Acquire();
      attack.transform.SetParent(_stageRoot, true);
      attack.Begin(_enemyRoot.transform, _dummyTarget, settings, request, null, _previewLayer);
      while (attack != null && attack.IsRunning) yield return null;
    }

    public IEnumerator PlayBarrageAttack(EnemyDatabase.EnemyDef def, float windup, PreviewUiMirror ui = default)
    {
      if (_enemyRoot == null) yield break;
      _motionVisual?.PlayRangedAttackPulse(windup);
      var elapsed = 0f; var uiHomeScale = ui.IsValid ? ui.Sprite.localScale : Vector3.one;
      while (elapsed < windup) { elapsed += Time.deltaTime; if (ui.IsValid) { var s = 1f + 0.12f * Mathf.Sin(elapsed / windup * Mathf.PI); ui.Sprite.localScale = uiHomeScale * s; } yield return null; }
      if (ui.IsValid) ui.Sprite.localScale = uiHomeScale;
      var profile = AttackProfileDatabase.Get(def.attack_profile_id);
      var count = Mathf.Max(1, profile?.projectile_count ?? 3); var spread = profile?.spread_deg ?? 12f;
      var speed = profile?.projectile_speed ?? 8f; var scale = profile?.projectile_scale ?? 0.28f;
      var range = profile?.range ?? 6f; var previewRange = Mathf.Min(range, 5f);
      var origin = _enemyRoot.transform.position + Vector3.right * 0.45f;
      var baseDir = (_dummyTarget.position - origin).normalized;
      if (baseDir.sqrMagnitude < 0.0001f) baseDir = Vector3.right;
      var request = DamageRequest.Direct(profile?.base_damage ?? def.base_damage, profile?.damage_type ?? "kinetic", "monster", _enemyRoot);
      var baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;
      for (var i = 0; i < count; i++)
      {
        var t = count <= 1 ? 0f : i / (float)(count - 1) - 0.5f;
        var angle = baseAngle + t * spread; var rad = angle * Mathf.Deg2Rad;
        var dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
        var proj = EnemyTriangleProjectile.SpawnDirectional(origin, dir, request, speed, scale, ProjectileColor, previewRange);
        if (proj != null)
        {
          proj.SetOffscreenCulling(false);
          proj.transform.SetParent(_stageRoot, true);
          SetLayerRecursive(proj.gameObject);
        }
      }
      yield return new WaitForSeconds(0.85f);
    }

    public void StopAllEffects()
    {
      StopAllCoroutines(); _motionVisual?.CancelLaserCharge(); _motionVisual?.ResetSpinMultiplier();
      if (_stageRoot == null) return;
      foreach (var beam in _stageRoot.GetComponentsInChildren<LaserEnemyAttack>(true)) if (beam != null) beam.Stop();
      foreach (var legacy in _stageRoot.GetComponentsInChildren<LaserBeamVisual>(true)) if (legacy != null) Destroy(legacy.gameObject);
      foreach (var proj in _stageRoot.GetComponentsInChildren<StraightProjectile>(true)) if (proj != null) Destroy(proj.gameObject);
    }

    public void DisposeStage() { StopAllEffects(); DestroyEnemy(); if (_renderTexture != null) { _renderTexture.Release(); Destroy(_renderTexture); _renderTexture = null; } if (_stageRoot != null) { Destroy(_stageRoot.gameObject); _stageRoot = null; } _camera = null; _dummyTarget = null; }
    void DestroyEnemy() { if (_enemyRoot != null) { Destroy(_enemyRoot); _enemyRoot = null; } _enemyVisual = null; _motionVisual = null; }
    void SetLayer(GameObject go) { if (go != null && _previewLayer >= 0) go.layer = _previewLayer; }
    void SetLayerRecursive(GameObject go) { if (go == null) return; SetLayer(go); foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject); }
    static void ConfigureUrpCamera(Camera camera)
    {
      if (camera == null) return;
      var urp = camera.GetComponent<UniversalAdditionalCameraData>();
      if (urp == null) urp = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
      urp.renderType = CameraRenderType.Base; urp.renderPostProcessing = false; urp.antialiasing = AntialiasingMode.None; urp.SetRenderer(0);
    }
    static void SyncUiPosition(PreviewUiMirror ui, Vector3 homeWorld, Vector3 currentWorld) { if (!ui.IsValid) return; var delta = currentWorld - homeWorld; ui.Sprite.anchoredPosition = ui.HomeAnchoredPos + new Vector2(delta.x, delta.y) * ui.PixelsPerWorldUnit; }
    static void ResetUiMirror(PreviewUiMirror ui) { if (!ui.IsValid) return; ui.Sprite.anchoredPosition = ui.HomeAnchoredPos; ui.Sprite.localRotation = Quaternion.identity; ui.Sprite.localScale = Vector3.one; }
  }
}
