using UnityEngine;
using Game.Shared.Gameplay.Bridges;
using Health = global::Game.Shared.Combat.Health.Health;

namespace Game.World
{
  /// <summary>
  /// 营地核心实体 ?可被攻击和摧毁的营地中心?
  ///
  /// 职责?
  ///   1. 拥有 Health 组件，可接收伤害
  ///   2. 被摧毁时：掉落饰品、触发事件、影响世界等纀"
  ///   3. 提供视觉反馈（受击闪色、摧毁特效）
  ///
  /// 挂载方式?
  ///   作为 CampController 的子 GameObject，挂?CampCore 组件?
  ///   需要手动添?Collider2D（CircleCollider2D 推荐）让弹体/近战可命中?
  ///
  /// 摧毁流程?
  ///   Health.Died ?OnCoreDestroyed
  ///     ?LootService.Roll(lootPoolId) ?生成掉落牀"
  ///     ?WorldEventBus.FireCampDestroyed ?触发事件系统
  ///     ?WorldRuntimeContext.ModifyWorldLevel(-delta)
  ///     ?CampController ?TransitionTo(Destroyed)
  /// </summary>
  [RequireComponent(typeof(Health))]
  public class CampCore : MonoBehaviour
  {
    // ══════════════════════════════════════════════════════
    //  Inspector
    // ══════════════════════════════════════════════════════

    [Header("Visual")]
    [SerializeField] Color _coreColor = new(0.8f, 0.2f, 0.1f, 1f); // 红褐艀"
    [SerializeField] float _coreSize = 1.5f;
    [SerializeField] bool _debugLog;

    [Header("Destroy")]
    [SerializeField] float _destroyDelay = 0.5f;

    // ══════════════════════════════════════════════════════
    //  内部引用
    // ══════════════════════════════════════════════════════

    Health _health;
    CampController _controller;
    SpriteRenderer _visual;

    // 配置（由 CampController.Initialize 设置?
    string _campId;
    string _lootPoolId;
    float _worldLevelDropOnDestroy; // 摧毁时世界等级下降量

    public bool IsDestroyed => _health != null && _health.IsDead;

    // ══════════════════════════════════════════════════════
    //  Unity 生命周期
    // ══════════════════════════════════════════════════════

    void Awake()
    {
      _health = GetComponent<Health>();
      CreateVisual();
      SetupCollider();
    }

    void Start()
    {
      // 从父节点获取 CampController 引用
      _controller = GetComponentInParent<CampController>();

      if (_health != null)
        _health.Died += OnCoreDestroyed;
    }

    void OnDestroy()
    {
      if (_health != null)
        _health.Died -= OnCoreDestroyed;
    }

    // ══════════════════════════════════════════════════════
    //  初始匀"
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// ?CampController 调用，配置营地核心参数?
    /// </summary>
    /// <param name="campId">营地唯一实例 ID</param>
    /// <param name="maxHp">核心最?HP（来自配表或计算?/param>
    /// <param name="lootPoolId">摧毁时使用的掉落?ID</param>
    /// <param name="worldLevelDrop">摧毁时世界等级下降量</param>
    public void InitializeCampCore(
      string campId,
      float maxHp,
      string lootPoolId,
      float worldLevelDrop)
    {
      _campId = campId;
      _lootPoolId = lootPoolId;
      _worldLevelDropOnDestroy = worldLevelDrop;

      if (_health != null)
      {
        _health.Configure(maxHp);
      }

      gameObject.name = $"CampCore_{campId}";

      if (_debugLog)
        Debug.Log($"[CampCore] Initialized '{campId}' HP={maxHp} loot={lootPoolId}");
    }

    // ══════════════════════════════════════════════════════
    //  摧毁逻辑
    // ══════════════════════════════════════════════════════

    /// <summary>
    /// 营地核心被摧毁时的处理?
    /// ?Health.Died 事件触发?
    /// </summary>
    void OnCoreDestroyed()
    {
      if (_debugLog)
        Debug.Log($"[CampCore] Camp '{_campId}' core destroyed!");

      // Step 1: 注册?WorldRuntimeContext（标记营地已摧毁?
      WorldRuntimeContext.UnregisterCamp(_campId);

      // Step 2: 掉落饰品/奖励
      DropLoot();

      // Step 3: 世界等级影响?WorldLevelSystem 通过 CampDestroyed 事件自动处理

      // Step 4: 广播世界事件
      var campData = new WorldCampData
      {
        CampId = _campId,
        IsDestroyed = true,
        LootPoolId = _lootPoolId
      };
      WorldEventBus.FireCampDestroyed(_campId, campData);

      // Step 5: 通知控制器切换状态"
      if (_controller != null)
      {
        _controller.OnCoreDestroyed();
      }

      // Step 6: 视觉销毀"
      Invoke(nameof(DestroyVisual), _destroyDelay);
    }

    /// <summary>
    /// 掉落饰品和奖励?
    /// 复用 LootService 进行掉落计算和拾取物生成?
    /// </summary>
    void DropLoot()
    {
      if (string.IsNullOrEmpty(_lootPoolId))
      {
        if (_debugLog)
          Debug.LogWarning($"[CampCore] No loot pool specified for camp '{_campId}'.");
        return;
      }

      try
      {
        LootGrantServiceLocator.Service.GrantLootPoolAtPosition(transform.position, _lootPoolId);

        if (_debugLog)
          Debug.Log($"[CampCore] Camp '{_campId}' granted loot pool '{_lootPoolId}'.");
      }
      catch (System.Exception e)
      {
        Debug.LogError($"[CampCore] Loot roll failed for pool '{_lootPoolId}': {e.Message}");
      }
    }

    // ══════════════════════════════════════════════════════
    //  视觉
    // ══════════════════════════════════════════════════════

    void CreateVisual()
    {
      _visual = gameObject.AddComponent<SpriteRenderer>();

      // 创建纯色方形 sprite（与项目极简几何美术一致）
      var tex = new Texture2D(32, 32);
      var pixels = new Color[32 * 32];
      for (int i = 0; i < pixels.Length; i++)
        pixels[i] = _coreColor;
      tex.SetPixels(pixels);
      tex.filterMode = FilterMode.Point;
      tex.Apply();

      var pivot = new Vector2(0.5f, 0.5f);
      _visual.sprite = Sprite.Create(
        tex,
        new Rect(0, 0, 32, 32),
        pivot,
        32f / _coreSize);

      _visual.sortingOrder = 1;
    }

    void SetupCollider()
    {
      // 圆形碰撞体（与项?Physics2D 兼容?
      var col = gameObject.AddComponent<CircleCollider2D>();
      col.radius = _coreSize * 0.5f;
      col.isTrigger = false; // 弹体/近战通过非触发碰撞检测命与"
    }

    void DestroyVisual()
    {
      if (_visual != null)
      {
        // 渐变消失
        var c = _visual.color;
        c.a = 0f;
        _visual.color = c;
      }

      // 摧毁碰撞体（避免死后继续阻挡弹体?
      var col = GetComponent<Collider2D>();
      if (col != null)
        col.enabled = false;
    }

    // ══════════════════════════════════════════════════════
    //  查询
    // ══════════════════════════════════════════════════════

    /// <summary>当前 HP 百分比（0~1?/summary>
    public float HpPercent => _health != null ? _health.HpPercent : 0f;

    /// <summary>当前 HP</summary>
    public float CurrentHp => _health != null ? _health.CurrentHp : 0f;

    /// <summary>最?HP</summary>
    public float MaxHp => _health != null ? _health.MaxHp : 1f;
  }
}
