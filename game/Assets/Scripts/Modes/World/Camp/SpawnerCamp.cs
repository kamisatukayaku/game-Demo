using Game.Shared.Combat;
using Game.Shared.Core;
using Game.Shared.Enemy.Spawn;
using Game.Shared.Enemy.Visual;
using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 敌人生成营地动画组件 — 纯代码驱动，不依赖预制动画资源。
  ///
  /// 工作方式：
  ///   - 使用 LineRenderer 绘制正方形厚边框（静止不动）
  ///   - 使用 SpriteRenderer 绘制纯色矩形核心块
  ///   - 通过有限状态机驱动六阶段动画循环：
  ///     充电 → 分裂 → 释放 → 消散 → 预览 → 恢复
  ///   - 最终调用 EnemySpawner.SpawnEnemy() 生成真实敌人
  /// </summary>
  [AddComponentMenu("World/Spawner Camp")]
  public class SpawnerCamp : MonoBehaviour
  {
    // ══════════════════════════════════════════════════════
    //  Inspector 配置
    // ══════════════════════════════════════════════════════

    [Header("外框 (Frame)")]
    [SerializeField] [Range(0.5f, 8f)] float _frameHalfSize = 2.5f;
    [SerializeField] [Range(0.02f, 0.6f)] float _frameThickness = 0.15f;
    [SerializeField] Color _frameColor = new(0.30f, 0.36f, 0.42f, 1f);

    [Header("核心块 (Core Block)")]
    [SerializeField] [Range(0.2f, 4f)] float _baseWidth = 1.0f;
    [SerializeField] [Range(0.2f, 4f)] float _baseHeight = 1.0f;
    [SerializeField] Color _coreColor = new(0.10f, 0.55f, 0.85f, 1f);
    [SerializeField] [Range(1f, 3f)] float _maxChargeScale = 1.8f;

    [Header("动画时长 (秒)")]
    [SerializeField] float _chargeDuration = 2.0f;
    [SerializeField] float _splitDuration = 0.5f;
    [SerializeField] float _releaseDuration = 0.4f;
    [SerializeField] float _dissipateDuration = 0.35f;
    [SerializeField] float _previewDuration = 0.45f;
    [SerializeField] float _recoverDuration = 0.35f;

    [Header("脉冲效果")]
    [SerializeField] [Range(0f, 0.2f)] float _pulseAmplitude = 0.04f;
    [SerializeField] [Range(0f, 12f)] float _pulseFrequency = 4.5f;

    [Header("敌人预览与生成")]
    [SerializeField] string[] _enemyPool = new[]
    {
      "mob_tri_01", "mob_square_01",
      "mob_hex_01", "mob_pent_01",
      "mob_star4_01"
    };
    [SerializeField] [Range(0.3f, 3f)] float _previewDiameter = 1.0f;

    [Header("调试")]
    [SerializeField] bool _debugLog;

    // ══════════════════════════════════════════════════════
    //  FSM 状态枚举
    // ══════════════════════════════════════════════════════

    enum CampAnimState
    {
      /// <summary>待机充能 — 核心矩形缓慢放大 + 脉冲</summary>
      Charging,
      /// <summary>分裂 — 核心沿水平/竖直分裂为两个子矩形</summary>
      Splitting,
      /// <summary>释放 — 一个子矩形移向边框，另一个返回中心</summary>
      Releasing,
      /// <summary>消散 — 释放块缩小淡出，同时敌人预览出现在边框外</summary>
      Dissipating,
      /// <summary>预览 — 敌人精灵从 scale=0 放大至 1</summary>
      EnemyPreview,
      /// <summary>恢复 — 核心块返回中心并恢复基础尺寸</summary>
      Recovering
    }

    CampAnimState _state = CampAnimState.Charging;
    float _stateTimer;

    // ══════════════════════════════════════════════════════
    //  运行时数据
    // ══════════════════════════════════════════════════════

    enum SplitAxis { Horizontal, Vertical }
    SplitAxis _splitAxis;

    // 哪个子矩形被选为释放块
    enum WhichBlock { A, B }
    WhichBlock _releaseBlock;

    string _nextEnemyId;
    Game.Shared.Enemy.Spawn.EnemySpawner _enemySpawner;

    // ══════════════════════════════════════════════════════
    //  渲染组件
    // ══════════════════════════════════════════════════════

    LineRenderer _frameLine;
    SpriteRenderer _blockA;
    SpriteRenderer _blockB;
    SpriteRenderer _previewSr;
    GameObject _previewGo;

    Texture2D _whiteTex;
    Sprite _whiteSprite;

    /// <summary>组件是否已正确初始化</summary>
    bool _ready;

    // ══════════════════════════════════════════════════════
    //  Unity 生命周期
    // ══════════════════════════════════════════════════════

    void Awake()
    {
      CreateWhiteTexture();
      BuildFrame();
      BuildBlocks();
      BuildPreview();
    }

    void Start()
    {
      _enemySpawner = FindObjectOfType<Game.Shared.Enemy.Spawn.EnemySpawner>();
      if (_enemySpawner == null && _debugLog)
        Debug.LogWarning("[SpawnerCamp] 未在场景中找到 EnemySpawner。将无法生成敌人。");

      PickNextEnemy();
      ResetForNewCycle();

      _ready = true;
      _state = CampAnimState.Charging;
      _stateTimer = 0f;
    }

    void Update()
    {
      if (!_ready) return;

      _stateTimer += Time.deltaTime;

      switch (_state)
      {
        case CampAnimState.Charging:      StepCharging();      break;
        case CampAnimState.Splitting:     StepSplitting();     break;
        case CampAnimState.Releasing:     StepReleasing();     break;
        case CampAnimState.Dissipating:   StepDissipating();   break;
        case CampAnimState.EnemyPreview:  StepEnemyPreview();  break;
        case CampAnimState.Recovering:    StepRecovering();    break;
      }
    }

    void OnDestroy()
    {
      if (_whiteTex != null) Destroy(_whiteTex);
      if (_whiteSprite != null) Destroy(_whiteSprite);
    }

    // ══════════════════════════════════════════════════════
    //  初始化 — 渲染组件
    // ══════════════════════════════════════════════════════

    void CreateWhiteTexture()
    {
      _whiteTex = new Texture2D(1, 1);
      _whiteTex.SetPixel(0, 0, Color.white);
      _whiteTex.filterMode = FilterMode.Point;
      _whiteTex.Apply();

      _whiteSprite = Sprite.Create(
        _whiteTex,
        new Rect(0, 0, 1, 1),
        new Vector2(0.5f, 0.5f),
        1f); // 1 PPU → localScale 直接匹配世界单位
      _whiteSprite.name = "White1x1";
    }

    // ── 外框 (LineRenderer) ───────────────────────────

    void BuildFrame()
    {
      _frameLine = gameObject.AddComponent<LineRenderer>();
      _frameLine.positionCount = 5;
      _frameLine.useWorldSpace = false;
      _frameLine.loop = false;
      _frameLine.startWidth = _frameThickness;
      _frameLine.endWidth = _frameThickness;
      _frameLine.startColor = _frameColor;
      _frameLine.endColor = _frameColor;
      _frameLine.numCapVertices = 2;
      _frameLine.numCornerVertices = 2;
      _frameLine.sortingOrder = 4;

      var mat = new Material(Shader.Find("Sprites/Default"));
      if (mat != null) _frameLine.sharedMaterial = mat;

      RefreshFrame();
    }

    void RefreshFrame()
    {
      float h = _frameHalfSize;
      _frameLine.SetPosition(0, new Vector3(-h, -h, 0));
      _frameLine.SetPosition(1, new Vector3( h, -h, 0));
      _frameLine.SetPosition(2, new Vector3( h,  h, 0));
      _frameLine.SetPosition(3, new Vector3(-h,  h, 0));
      _frameLine.SetPosition(4, new Vector3(-h, -h, 0));
    }

    // ── 核心块 (SpriteRenderer × 2) ──────────────────

    void BuildBlocks()
    {
      _blockA = MakeBlock("BlockA");
      _blockB = MakeBlock("BlockB");
    }

    SpriteRenderer MakeBlock(string name)
    {
      var go = new GameObject(name);
      go.transform.SetParent(transform, false);
      go.transform.localPosition = Vector3.zero;

      var sr = go.AddComponent<SpriteRenderer>();
      sr.sprite = _whiteSprite;
      sr.color = _coreColor;
      sr.sortingOrder = 5;
      return sr;
    }

    // ── 敌人预览 (SpriteRenderer) ────────────────────

    void BuildPreview()
    {
      _previewGo = new GameObject("EnemyPreview");
      _previewGo.transform.SetParent(transform, false);
      _previewGo.transform.localPosition = Vector3.zero;

      _previewSr = _previewGo.AddComponent<SpriteRenderer>();
      _previewSr.sortingOrder = 7;
      _previewSr.color = Color.white;
      _previewGo.SetActive(false);
    }

    // ══════════════════════════════════════════════════════
    //  初始化 — 数据
    // ══════════════════════════════════════════════════════

    void PickNextEnemy()
    {
      if (_enemyPool == null || _enemyPool.Length == 0)
      {
        _nextEnemyId = "mob_tri_01";
        return;
      }
      _nextEnemyId = _enemyPool[Random.Range(0, _enemyPool.Length)];
    }

    /// <summary>重置两个块到中心重叠的初始状态</summary>
    void ResetForNewCycle()
    {
      _blockA.gameObject.SetActive(true);
      _blockB.gameObject.SetActive(true);
      _blockA.transform.localPosition = Vector3.zero;
      _blockB.transform.localPosition = Vector3.zero;
      _blockA.transform.localScale = new Vector3(_baseWidth, _baseHeight, 1);
      _blockB.transform.localScale = new Vector3(_baseWidth, _baseHeight, 1);
      _blockA.color = _coreColor;
      _blockB.color = _coreColor;
      SetAlpha(_blockA, 1f);
      SetAlpha(_blockB, 1f);
    }

    // ══════════════════════════════════════════════════════
    //  块位置辅助
    // ══════════════════════════════════════════════════════

    static void PlaceBlock(SpriteRenderer sr, Vector2 center, float w, float h)
    {
      sr.transform.localPosition = new Vector3(center.x, center.y, 0);
      sr.transform.localScale = new Vector3(w, h, 1);
    }

    static void SetAlpha(SpriteRenderer sr, float a)
    {
      var c = sr.color;
      c.a = Mathf.Clamp01(a);
      sr.color = c;
    }

    // ══════════════════════════════════════════════════════
    //  SmoothStep 辅助
    // ══════════════════════════════════════════════════════

    static float Smooth01(float t)
    {
      t = Mathf.Clamp01(t);
      return t * t * (3f - 2f * t);
    }

    // ══════════════════════════════════════════════════════
    //  FSM 状态转移
    // ══════════════════════════════════════════════════════

    void Goto(CampAnimState next)
    {
      if (_debugLog)
        Debug.Log($"[SpawnerCamp] {_state} → {next}");

      _state = next;
      _stateTimer = 0f;

      switch (next)
      {
        case CampAnimState.Splitting:     OnSplitEnter();     break;
        case CampAnimState.Releasing:     OnReleaseEnter();   break;
        case CampAnimState.Dissipating:   OnDissipateEnter(); break;
        case CampAnimState.EnemyPreview:  OnPreviewEnter();   break;
        case CampAnimState.Recovering:    OnRecoverEnter();   break;
      }
    }

    // ══════════════════════════════════════════════════════
    //  阶段 1 — 待机充能
    // ══════════════════════════════════════════════════════

    void StepCharging()
    {
      float t = Mathf.Clamp01(_stateTimer / _chargeDuration);
      float s = Smooth01(t);

      float chargeScale = 1f + s * (_maxChargeScale - 1f);
      float pulse = 1f + Mathf.Sin(Time.time * _pulseFrequency) * _pulseAmplitude;
      float finalScale = chargeScale * pulse;

      float w = _baseWidth * finalScale;
      float h = _baseHeight * finalScale;

      PlaceBlock(_blockA, Vector2.zero, w, h);
      PlaceBlock(_blockB, Vector2.zero, w, h);

      // 亮度脉冲
      float bright = 1f + Mathf.Sin(Time.time * _pulseFrequency * 0.7f) * 0.06f;
      _blockA.color = _coreColor * bright;
      _blockB.color = _coreColor * bright;

      if (t >= 1f)
        Goto(CampAnimState.Splitting);
    }

    // ══════════════════════════════════════════════════════
    //  阶段 2 — 随机分裂
    // ══════════════════════════════════════════════════════

    void OnSplitEnter()
    {
      _splitAxis = Random.value < 0.5f
        ? SplitAxis.Horizontal
        : SplitAxis.Vertical;

      // 两个块都可见，重叠在中心
      _blockA.gameObject.SetActive(true);
      _blockB.gameObject.SetActive(true);
      SetAlpha(_blockA, 1f);
      SetAlpha(_blockB, 1f);
      _blockA.color = _coreColor;
      _blockB.color = _coreColor;

      float fullSize = _maxChargeScale;
      float sw = _baseWidth * fullSize;
      float sh = _baseHeight * fullSize;
      PlaceBlock(_blockA, Vector2.zero, sw, sh);
      PlaceBlock(_blockB, Vector2.zero, sw, sh);
    }

    void StepSplitting()
    {
      float t = Mathf.Clamp01(_stateTimer / _splitDuration);
      float s = Smooth01(t);

      float fullSize = _maxChargeScale;
      float sw = _baseWidth * fullSize;
      float sh = _baseHeight * fullSize;

      // 每个子块占约 45%（留 10% 间隙）
      float childW = sw * 0.45f;
      float childH = sh * 0.45f;

      float maxOffset = _frameHalfSize * 0.6f;
      float offset = s * maxOffset;

      if (_splitAxis == SplitAxis.Horizontal)
      {
        PlaceBlock(_blockA, new Vector2(-offset, 0), childW, sh);
        PlaceBlock(_blockB, new Vector2( offset, 0), childW, sh);
      }
      else
      {
        PlaceBlock(_blockA, new Vector2(0,  offset), sw, childH);
        PlaceBlock(_blockB, new Vector2(0, -offset), sw, childH);
      }

      if (t >= 1f)
        Goto(CampAnimState.Releasing);
    }

    // ══════════════════════════════════════════════════════
    //  阶段 3 — 随机释放
    // ══════════════════════════════════════════════════════

    void OnReleaseEnter()
    {
      _releaseBlock = Random.value < 0.5f ? WhichBlock.A : WhichBlock.B;
    }

    void StepReleasing()
    {
      float t = Mathf.Clamp01(_stateTimer / _releaseDuration);
      float s = Smooth01(t);

      float fullSize = _maxChargeScale;
      float sw = _baseWidth * fullSize;
      float sh = _baseHeight * fullSize;
      float childW = sw * 0.45f;
      float childH = sh * 0.45f;
      float maxOffset = _frameHalfSize * 0.6f;

      float releaseExtra = s * (_frameHalfSize * 0.80f - maxOffset);
      float coreReturn = 1f - s; // 1 → 0

      if (_splitAxis == SplitAxis.Horizontal)
      {
        bool releaseLeft = _releaseBlock == WhichBlock.A;
        float releaseDir = releaseLeft ? -1f : 1f;
        float releaseX = releaseDir * (maxOffset + releaseExtra);
        float coreX = (releaseLeft ? 1f : -1f) * maxOffset * coreReturn;

        if (_releaseBlock == WhichBlock.A)
        {
          PlaceBlock(_blockA, new Vector2(releaseX, 0), childW, sh);
          PlaceBlock(_blockB, new Vector2(coreX, 0), childW, sh);
        }
        else
        {
          PlaceBlock(_blockA, new Vector2(coreX, 0), childW, sh);
          PlaceBlock(_blockB, new Vector2(releaseX, 0), childW, sh);
        }
      }
      else // Vertical
      {
        bool releaseTop = _releaseBlock == WhichBlock.A;
        float releaseDir = releaseTop ? 1f : -1f;
        float releaseY = releaseDir * (maxOffset + releaseExtra);
        float coreY = (releaseTop ? -1f : 1f) * maxOffset * coreReturn;

        if (_releaseBlock == WhichBlock.A)
        {
          PlaceBlock(_blockA, new Vector2(0, releaseY), sw, childH);
          PlaceBlock(_blockB, new Vector2(0, coreY), sw, childH);
        }
        else
        {
          PlaceBlock(_blockA, new Vector2(0, coreY), sw, childH);
          PlaceBlock(_blockB, new Vector2(0, releaseY), sw, childH);
        }
      }

      if (t >= 1f)
        Goto(CampAnimState.Dissipating);
    }

    // ══════════════════════════════════════════════════════
    //  阶段 4 — 消散 + 敌人预览出现
    // ══════════════════════════════════════════════════════

    void OnDissipateEnter()
    {
      // blocks 已就位，无需额外操作
    }

    void StepDissipating()
    {
      float t = Mathf.Clamp01(_stateTimer / _dissipateDuration);
      float s = Smooth01(t);

      float fullSize = _maxChargeScale;
      float sw = _baseWidth * fullSize;
      float sh = _baseHeight * fullSize;
      float childW = sw * 0.45f;
      float childH = sh * 0.45f;

      SpriteRenderer releaseSr = _releaseBlock == WhichBlock.A ? _blockA : _blockB;
      SpriteRenderer coreSr   = _releaseBlock == WhichBlock.A ? _blockB : _blockA;
      Vector3 releasePos = releaseSr.transform.localPosition;

      // 释放块：缩小 + 淡出
      float fade = 1f - s;
      PlaceBlock(releaseSr, releasePos, childW * fade, childH * fade);
      SetAlpha(releaseSr, fade);

      // 核心块：停留在中心，略膨胀以示能量转移
      float coreScale = 1f + s * 0.15f;
      PlaceBlock(coreSr, Vector2.zero, childW * coreScale, childH * coreScale);
      SetAlpha(coreSr, 1f);

      // 在消散中段（~40%）显示敌人预览
      if (s >= 0.35f && !_previewGo.activeSelf)
        RevealPreview();

      if (t >= 1f)
      {
        releaseSr.gameObject.SetActive(false);
        Goto(CampAnimState.EnemyPreview);
      }
    }

    // ══════════════════════════════════════════════════════
    //  敌人预览
    // ══════════════════════════════════════════════════════

    void RevealPreview()
    {
      // 决定预览位置：释放块方向 + 边框外侧
      Vector3 dir;
      if (_splitAxis == SplitAxis.Horizontal)
        dir = new Vector3(_releaseBlock == WhichBlock.A ? -1f : 1f, 0f, 0f);
      else
        dir = new Vector3(0f, _releaseBlock == WhichBlock.A ? 1f : -1f, 0f);

      _previewGo.transform.localPosition = dir * (_frameHalfSize + _frameThickness + 0.25f);

      // 加载敌人精灵
      var sprite = CombatSpriteVisual.LoadMinion(_nextEnemyId);
      if (sprite != null)
      {
        _previewSr.sprite = sprite;
        _previewSr.color = Color.white;

        float extent = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        float s = _previewDiameter / Mathf.Max(0.01f, extent);
        _previewGo.transform.localScale = new Vector3(s, s, 1);
      }
      else
      {
        // 回退：白色方块 + 颜色
        _previewSr.sprite = _whiteSprite;
        _previewSr.color = new Color(0.9f, 0.3f, 0.2f, 1f);
        _previewGo.transform.localScale = new Vector3(_previewDiameter, _previewDiameter, 1);
      }

      // 从 0 开始
      _previewGo.transform.localScale = Vector3.zero;
      _previewGo.SetActive(true);
    }

    void OnPreviewEnter()
    {
      if (!_previewGo.activeSelf)
        RevealPreview();
    }

    void StepEnemyPreview()
    {
      float t = Mathf.Clamp01(_stateTimer / _previewDuration);

      // 快速放大 + 尾部过冲（弹性感）
      float scale;
      if (t < 0.75f)
      {
        scale = t / 0.75f; // 0→1 in first 75%
      }
      else
      {
        float overshootT = (t - 0.75f) / 0.25f; // 0→1
        scale = 1f + 0.18f * (1f - overshootT) * Mathf.Sin(overshootT * Mathf.PI * 3f);
      }

      // 获取目标缩放值并应用
      if (_previewSr.sprite != null && _previewSr.sprite != _whiteSprite)
      {
        float extent = Mathf.Max(_previewSr.sprite.bounds.size.x, _previewSr.sprite.bounds.size.y);
        float baseS = _previewDiameter / Mathf.Max(0.01f, extent);
        float s = baseS * scale;
        _previewGo.transform.localScale = new Vector3(s, s, 1);
      }
      else
      {
        _previewGo.transform.localScale = Vector3.one * (_previewDiameter * scale);
      }

      if (t >= 1f)
      {
        // 生成真实敌人
        SpawnAndCleanup();
      }
    }

    // ══════════════════════════════════════════════════════
    //  阶段 5 — 敌人生成（瞬时）
    // ══════════════════════════════════════════════════════

    void SpawnAndCleanup()
    {
      // 关闭预览
      _previewGo.SetActive(false);

      // 生成敌人
      if (_enemySpawner != null)
      {
        Vector3 spawnPos = _previewGo.transform.localPosition;
        // 再向外偏移一点，避免卡边框
        if (_splitAxis == SplitAxis.Horizontal)
          spawnPos.x += (spawnPos.x > 0 ? 1f : -1f) * 0.3f;
        else
          spawnPos.y += (spawnPos.y > 0 ? 1f : -1f) * 0.3f;

        spawnPos += transform.position; // 转世界坐标

        var enemy = _enemySpawner.SpawnEnemy(_nextEnemyId, spawnPos);

        if (_debugLog)
          Debug.Log($"[SpawnerCamp] 生成 '{_nextEnemyId}' 于 {spawnPos}" +
            (enemy != null ? "" : " (失败)"));
      }
      else if (_debugLog)
      {
        Debug.LogWarning("[SpawnerCamp] 未连接 EnemySpawner，跳过生成");
      }

      // 选择下一个敌人
      PickNextEnemy();

      // 进入恢复阶段
      Goto(CampAnimState.Recovering);
    }

    // ══════════════════════════════════════════════════════
    //  阶段 6 — 营地恢复
    // ══════════════════════════════════════════════════════

    void OnRecoverEnter()
    {
      // 释放块可能被隐藏了，恢复其可见
      SpriteRenderer releaseSr = _releaseBlock == WhichBlock.A ? _blockA : _blockB;
      releaseSr.gameObject.SetActive(true);
      SetAlpha(releaseSr, 0f); // 从透明开始弹入
    }

    void StepRecovering()
    {
      float t = Mathf.Clamp01(_stateTimer / _recoverDuration);
      float s = Smooth01(t);

      SpriteRenderer releaseSr = _releaseBlock == WhichBlock.A ? _blockA : _blockB;
      SpriteRenderer coreSr   = _releaseBlock == WhichBlock.A ? _blockB : _blockA;

      // 核心块：从当前尺寸平滑回到基础尺寸
      float fullSize = _maxChargeScale;
      float childW = _baseWidth * fullSize * 0.45f;
      float childH = _baseHeight * fullSize * 0.45f;

      float coreW = Mathf.Lerp(childW * 1.15f, _baseWidth,  s);
      float coreH = Mathf.Lerp(childH * 1.15f, _baseHeight, s);

      // 轻微微动（缓震效果）
      float jiggleX = Mathf.Sin(s * Mathf.PI * 2.5f) * (1f - s) * 0.04f;
      float jiggleY = Mathf.Cos(s * Mathf.PI * 2.5f + 0.7f) * (1f - s) * 0.04f;

      PlaceBlock(coreSr, new Vector2(jiggleX, jiggleY), coreW, coreH);
      coreSr.color = _coreColor;

      // 释放块：从透明淡入 + 聚回中心
      float releaseAlpha = s;
      float releaseW = Mathf.Lerp(childW * 0.3f, _baseWidth,  s);
      float releaseH = Mathf.Lerp(childH * 0.3f, _baseHeight, s);
      float releaseJiggleX = Mathf.Cos(s * Mathf.PI * 2f) * (1f - s) * 0.06f;
      float releaseJiggleY = Mathf.Sin(s * Mathf.PI * 2f + 0.5f) * (1f - s) * 0.06f;

      PlaceBlock(releaseSr, new Vector2(releaseJiggleX, releaseJiggleY), releaseW, releaseH);
      SetAlpha(releaseSr, releaseAlpha);
      releaseSr.color = _coreColor;

      if (t >= 1f)
      {
        ResetForNewCycle();
        Goto(CampAnimState.Charging);
      }
    }

    // ══════════════════════════════════════════════════════
    //  公开控制接口
    // ══════════════════════════════════════════════════════

    /// <summary>重置并重新开始动画</summary>
    public void Restart()
    {
      ResetForNewCycle();
      _state = CampAnimState.Charging;
      _stateTimer = 0f;
      _previewGo.SetActive(false);
    }

    /// <summary>立即暂停动画循环</summary>
    public void Pause()
    {
      enabled = false;
    }

    /// <summary>恢复动画循环</summary>
    public void Resume()
    {
      enabled = true;
    }

    /// <summary>外部注入敌人池</summary>
    public void SetEnemyPool(string[] pool)
    {
      _enemyPool = pool;
      PickNextEnemy();
    }

    /// <summary>获取当前阶段名称（调试用）</summary>
    public string GetStateName() => _state.ToString();
  }
}
