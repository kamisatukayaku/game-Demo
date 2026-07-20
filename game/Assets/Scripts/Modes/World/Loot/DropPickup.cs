using UnityEngine;

namespace Game.World
{
  /// <summary>
  /// 世界空间掉落物 — 挂载在动态创建的 GameObject 上。
  ///
  /// 表现：
  ///   - 金币：金色小圆点
  ///   - 物品：带光芒特效的圆点，颜色由物品品质决定
  ///
  /// 拾取行为：
  ///   - 玩家靠近到 pickRadius 时，掉落物变透明并消失
  ///   - 同时触发 OnPickup 回调更新背包/货币系统
  ///
  /// 生命周期：
  ///   - 创建时从死亡位置随机偏移弹出
  ///   - 短暂等待后开始检测玩家距离
  ///   - 拾取或超时后自动销毁
  /// </summary>
  public class DropPickup : MonoBehaviour
  {
    // ══════════════════════════════════════════════════════
    //  类型定义
    // ══════════════════════════════════════════════════════

    public enum PickupType
    {
      Gold,   // 金币
      Item    // 道具/饰品
    }

    // ══════════════════════════════════════════════════════
    //  公开数据（由生成方设置）
    // ══════════════════════════════════════════════════════

    public PickupType Type { get; private set; }
    public int GoldAmount { get; private set; }
    public string ItemId { get; private set; }
    public int ItemCount { get; private set; }
    public WorldDatabase.ItemQuality Quality { get; private set; }

    /// <summary>拾取回调（由 WorldDropSystem 绑定）</summary>
    public System.Action<DropPickup> OnPickup;

    // ══════════════════════════════════════════════════════
    //  配置常量
    // ══════════════════════════════════════════════════════

    const float PickRadius = 1.5f;
    const float PickupDelay = 0.4f;       // 生成后短暂不可拾取
    const float FadeDuration = 0.15f;      // 拾取时淡出时间
    const float LifetimeSeconds = 60f;     // 超时自动销毁
    const float PopDistance = 0.8f;        // 弹出距离
    const float FloatAmplitude = 0.10f;    // 浮动幅度
    const float FloatFrequency = 2.5f;     // 浮动频率
    const float DotSize = 0.22f;           // 圆点大小

    // ══════════════════════════════════════════════════════
    //  内部状态
    // ══════════════════════════════════════════════════════

    SpriteRenderer _spriteRenderer;
    float _spawnTime;
    float _baseY;
    Vector3 _popVelocity;
    bool _pickingUp;
    float _fadeTimer;
    Transform _playerTransform;
    Color _baseColor;

    // ══════════════════════════════════════════════════════
    //  初始化
    // ══════════════════════════════════════════════════════

    /// <summary>设置为金币掉落物。</summary>
    public void SetupGold(int amount)
    {
      Type = PickupType.Gold;
      GoldAmount = amount;
      Quality = WorldDatabase.ItemQuality.Common;
    }

    /// <summary>设置为物品掉落物。</summary>
    public void SetupItem(string itemId, int count, WorldDatabase.ItemQuality quality)
    {
      Type = PickupType.Item;
      ItemId = itemId;
      ItemCount = count;
      Quality = quality;
    }

    void Awake()
    {
      _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
      _spriteRenderer.sprite = CreateCircleSprite();
      _spriteRenderer.sortingOrder = 50;

      _spawnTime = Time.time;
    }

    void Start()
    {
      _baseY = transform.position.y;

      // 小角度随机弹出
      var angle = Random.Range(0f, Mathf.PI * 2f);
      _popVelocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * PopDistance;

      // 查找玩家
      var playerGo = GameObject.FindGameObjectWithTag("Player");
      if (playerGo != null)
        _playerTransform = playerGo.transform;

      ApplyAppearance();
    }

    // ══════════════════════════════════════════════════════
    //  每帧更新
    // ══════════════════════════════════════════════════════

    void Update()
    {
      if (_pickingUp)
      {
        UpdatePickupFade();
        return;
      }

      var elapsed = Time.time - _spawnTime;

      // 弹出动画（前 0.3 秒）
      if (elapsed < 0.3f)
      {
        var t = elapsed / 0.3f;
        var offset = _popVelocity * (1f - t) * t; // ease out
        var pos = transform.position;
        pos.x += offset.x * Time.deltaTime * 3f;
        pos.y += offset.y * Time.deltaTime * 3f;
        transform.position = pos;
      }

      // 浮动动画
      var floatOffset = Mathf.Sin(elapsed * FloatFrequency) * FloatAmplitude;
      var currentPos = transform.position;
      currentPos.y = _baseY + floatOffset;
      transform.position = currentPos;

      // 超时销毁
      if (elapsed > LifetimeSeconds)
      {
        Destroy(gameObject);
        return;
      }

      // 拾取检测（延迟后开始）
      if (elapsed > PickupDelay && _playerTransform != null)
      {
        var dist = Vector3.Distance(transform.position, _playerTransform.position);
        if (dist < PickRadius)
          StartPickup();
      }
    }

    // ══════════════════════════════════════════════════════
    //  表现
    // ══════════════════════════════════════════════════════

    void ApplyAppearance()
    {
      if (_spriteRenderer == null) return;

      switch (Type)
      {
        case PickupType.Gold:
          _baseColor = new Color(1f, 0.84f, 0.2f, 1f); // 金色
          transform.localScale = Vector3.one * DotSize;
          break;

        case PickupType.Item:
          _baseColor = WorldDatabase.QualityColor(Quality);
          transform.localScale = Vector3.one * (DotSize * 1.3f); // 物品稍大
          break;
      }

      _spriteRenderer.color = _baseColor;
    }

    // ══════════════════════════════════════════════════════
    //  拾取逻辑
    // ══════════════════════════════════════════════════════

    void StartPickup()
    {
      _pickingUp = true;
      _fadeTimer = 0f;
    }

    void UpdatePickupFade()
    {
      _fadeTimer += Time.deltaTime;
      var progress = Mathf.Clamp01(_fadeTimer / FadeDuration);

      // 向玩家靠近
      if (_playerTransform != null)
        transform.position = Vector3.Lerp(transform.position, _playerTransform.position, progress * 0.5f);

      // 淡出
      var c = _baseColor;
      c.a = 1f - progress;
      if (_spriteRenderer != null)
        _spriteRenderer.color = c;

      // 缩小
      transform.localScale = Vector3.one * DotSize * (1f - progress * 0.6f);

      if (progress >= 1f)
      {
        OnPickup?.Invoke(this);
        Destroy(gameObject);
      }
    }

    // ══════════════════════════════════════════════════════
    //  辅助
    // ══════════════════════════════════════════════════════

    /// <summary>程序化生成圆形 Sprite（避免依赖外部资源）。</summary>
    static Sprite CreateCircleSprite()
    {
      var size = 32;
      var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
      var center = new Vector2(size / 2f, size / 2f);
      var radius = size / 2f - 1f;

      for (int y = 0; y < size; y++)
      {
        for (int x = 0; x < size; x++)
        {
          var dist = Vector2.Distance(new Vector2(x, y), center);
          if (dist <= radius)
          {
            // 柔光边缘
            var edgeSoftness = 2f;
            var alpha = 1f;
            if (dist > radius - edgeSoftness)
              alpha = (radius - dist) / edgeSoftness;
            tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
          }
          else
          {
            tex.SetPixel(x, y, Color.clear);
          }
        }
      }
      tex.filterMode = FilterMode.Bilinear;
      tex.Apply();

      return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
  }
}
