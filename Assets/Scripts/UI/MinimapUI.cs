using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小地图 / 地图：
/// - 右上角常驻一张小地图，内容是把 layer 为 Ground 的物体（墙体、地面）用一台正交相机渲染成贴图；
///   Ground 层上再叠存档点、可传送的门、以及玩家自己的小方块；
///   贴图本身是整张地图，小地图只显示「以角色为中心的一小块」——靠 RawImage 的 uvRect 窗口取景，
///   窗口跟着角色跑，走到地图边缘时会贴边（不会看到地图外面）；
/// - 大小由 cornerViewWorldHeight（纵向显示多少世界单位）定，调小了就更「近」；
/// - 按 Tab 显示 / 隐藏右上角的小地图；
/// - 按 M 暂停游戏并打开大地图，再按 M 或 Esc 关闭并恢复。
///
/// 地图范围是运行时按 Ground 层物体的包围盒算的，所以换场景不用改参数；
/// 相机和 RenderTexture 也是运行时建的，场景里不会多出多余的东西。
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Header("右上角小地图")]
    [SerializeField] private GameObject cornerRoot;
    [SerializeField] private RawImage cornerMap;
    [Tooltip("小地图宽度（高度按地图长宽比自动算）")]
    [SerializeField] private float cornerWidth = 380f;
    [Tooltip("小地图边框留白（和 Map 的 inset 对应）")]
    [SerializeField] private float cornerPadding = 5f;

    [Tooltip("小地图只显示以角色为中心的一小块：这里填纵向要显示多少世界单位（0 = 显示整张地图）。" +
             "用世界单位而不是地图比例，是因为四个场景地图大小差很多、共用同一个 Canvas 预制体")]
    [SerializeField] private float cornerViewWorldHeight = 16f;

    [Header("M 键大地图")]
    [SerializeField] private GameObject fullRoot;
    [SerializeField] private RawImage fullMap;
    [Tooltip("大地图最多占屏幕宽 / 高的比例")]
    [SerializeField] private float fullWidthRatio = 0.86f;
    [SerializeField] private float fullHeightRatio = 0.72f;

    [Header("按键")]
    [SerializeField] private KeyCode mapKey = KeyCode.M;
    [SerializeField] private KeyCode toggleKey = KeyCode.Tab;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("地图内容")]
    [Tooltip("要画进地图的图层名（墙体和地面的图层）")]
    [SerializeField] private string groundLayerName = "Ground";

    [SerializeField] private float iconSizeCorner = 9f;
    [SerializeField] private float iconSizeFull = 16f;
    [SerializeField] private float playerSizeCorner = 11f;
    [SerializeField] private float playerSizeFull = 18f;

    [Tooltip("地图底色。地形（墙/地面）本身几乎是黑的，底色用浅色才分得出来")]
    [SerializeField] private Color backgroundColor = new Color(0.76f, 0.8f, 0.88f, 1f);
    [SerializeField] private Color checkpointColor = new Color(1f, 0.84f, 0.35f, 1f);
    [SerializeField] private Color doorColor = new Color(0.42f, 0.85f, 1f, 1f);
    [SerializeField] private Color playerColor = new Color(1f, 0.36f, 0.36f, 1f);

    [Header("相机 / 贴图")]
    [Tooltip("地图贴图的宽度，越高越清晰")]
    [SerializeField] private int textureWidth = 1024;
    [Tooltip("地图四周留白比例")]
    [SerializeField] private float boundsPadding = 1.06f;
    [Tooltip("地图相机的远裁剪面，够长就行")]
    [SerializeField] private float cameraFarClip = 100f;

    [Header("大地图缩放 / 拖动")]
    [Tooltip("滚轮每一格缩放多少（比例）")]
    [SerializeField] private float zoomStep = 0.18f;
    [Tooltip("最小倍数（1 = 整张地图）")]
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 8f;

    /// <summary>大地图现在开着没有。</summary>
    public bool IsMapOpen => mapOpen;
    /// <summary>右上角小地图现在是不是显示着。</summary>
    public bool IsCornerVisible => cornerRoot != null && cornerRoot.activeSelf;

    private Bounds mapBounds;
    private bool mapReady;

    private Camera mapCamera;
    private GameObject mapCameraObject;
    private RenderTexture mapTexture;

    private readonly List<MapIcon> cornerIcons = new List<MapIcon>();
    private readonly List<MapIcon> fullIcons = new List<MapIcon>();

    private MapIcon cornerPlayerIcon;
    private MapIcon fullPlayerIcon;

    // 大地图看的是整张贴图的哪一块：zoom = 1 就是全图，中心点用 0..1 的地图坐标
    private float fullZoom = 1f;
    private Vector2 fullViewCenter = new Vector2(0.5f, 0.5f);

    private bool draggingMap;
    private Vector3 lastMousePosition;

    /// <summary>地图上的一个图标：控件 + 它在图上的相对位置（0..1）。</summary>
    private class MapIcon
    {
        public RectTransform rect;
        public Vector2 normalized;
    }

    private bool mapOpen;
    private bool pausedByUs;
    private bool cornerEnabledByUser = true;

    #region 生命周期

    private void Awake()
    {
        BuildMap();

        // 玩家去开别的菜单时，把地图收掉（不然会卡在暂停里）
        UI_SelfManagedPanel.OnOtherMenuOpened += CloseMap;
    }

    private void OnDestroy()
    {
        UI_SelfManagedPanel.OnOtherMenuOpened -= CloseMap;

        ReleaseMap();
    }

    private void Update()
    {
        HandleInput();
        UpdateMarkers();
        ApplyCornerVisibility();
    }

    #endregion

    #region 搭建地图

    private void BuildMap()
    {
        if (!TryGetGroundBounds(out mapBounds))
        {
            Debug.LogWarning($"小地图：场景里没找到 layer「{groundLayerName}」上的东西，地图先关掉了。", this);
            HideAll();
            return;
        }

        int layer = LayerMask.NameToLayer(groundLayerName);

        // 长宽比按地图包围盒来，贴图和相机的取景才对得上
        float aspect = Mathf.Max(0.05f, mapBounds.size.x / Mathf.Max(0.05f, mapBounds.size.y));
        int height = Mathf.Max(16, Mathf.RoundToInt(textureWidth / aspect));

        mapTexture = new RenderTexture(textureWidth, height, 16)
        {
            name = "MinimapTexture",
            filterMode = FilterMode.Bilinear
        };
        mapTexture.Create();

        GameObject cameraGo = new GameObject("MinimapCamera");
        // 不挂在 Canvas 下面：CanvasScaler 会缩放并移动画布，子物体会被一起带跑，相机的世界坐标就错了
        mapCameraObject = cameraGo;

        mapCamera = cameraGo.AddComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.clearFlags = CameraClearFlags.SolidColor;
        mapCamera.backgroundColor = backgroundColor;
        mapCamera.cullingMask = layer >= 0 ? (1 << layer) : 0;
        mapCamera.depth = -100;
        mapCamera.allowHDR = false;
        mapCamera.allowMSAA = false;
        mapCamera.useOcclusionCulling = false;
        mapCamera.nearClipPlane = 0.1f;
        mapCamera.farClipPlane = cameraFarClip;
        mapCamera.targetTexture = mapTexture;

        mapCamera.transform.position = new Vector3(mapBounds.center.x, mapBounds.center.y, -10f);
        // aspect 是按包围盒算的，所以竖直方向够高，横向也正好够宽
        mapCamera.orthographicSize = mapBounds.extents.y * boundsPadding;

        if (cornerMap != null)
        {
            cornerMap.texture = mapTexture;
            SetSize(cornerMap.rectTransform, cornerWidth, cornerWidth / aspect);

            // 小地图只显示角色周围一小块 —— 窗口之外的图标（存档点 / 门）要裁掉，
            // 不然它们会飘到小地图框外面去（大地图的 Map 上也有一模一样的这个组件）
            if (cornerMap.GetComponent<RectMask2D>() == null)
                cornerMap.gameObject.AddComponent<RectMask2D>();

            // 先按地图中心算一次；等 UpdateMarkers 拿到玩家再跟着玩家跑
            cornerMap.uvRect = CornerUv(new Vector2(0.5f, 0.5f));

            // 边框（Corner）跟着地图的长宽比一起变，不然地图会缩在框里或者溢出
            if (cornerRoot != null)
            {
                RectTransform rootRect = cornerRoot.GetComponent<RectTransform>();

                if (rootRect != null)
                    SetSize(rootRect, cornerWidth + cornerPadding * 2f, cornerWidth / aspect + cornerPadding * 2f);
            }
        }

        if (fullMap != null)
        {
            fullMap.texture = mapTexture;

            Vector2 size = FitFullMap(aspect);
            SetSize(fullMap.rectTransform, size.x, size.y);
        }

        BuildMarkers(cornerMap != null ? cornerMap.rectTransform : null, iconSizeCorner, playerSizeCorner, cornerIcons, out cornerPlayerIcon);
        BuildMarkers(fullMap != null ? fullMap.rectTransform : null, iconSizeFull, playerSizeFull, fullIcons, out fullPlayerIcon);

        ApplyFullMapView();

        if (fullRoot != null) fullRoot.SetActive(false);

        mapReady = true;

        ApplyCornerVisibility();
    }

    /// <summary>ground 图层上所有渲染物 / 碰撞体的总包围盒 = 地图范围。</summary>
    private bool TryGetGroundBounds(out Bounds _bounds)
    {
        _bounds = new Bounds();
        int layer = LayerMask.NameToLayer(groundLayerName);

        if (layer < 0)
            return false;

        bool has = false;
        UnityEngine.SceneManagement.Scene scene = gameObject.scene;

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t.gameObject.layer != layer)
                    continue;

                foreach (Renderer r in t.GetComponents<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (!has) { _bounds = r.bounds; has = true; } else _bounds.Encapsulate(r.bounds);
                }

                foreach (Collider2D c in t.GetComponents<Collider2D>())
                {
                    if (!c.enabled) continue;
                    if (!has) { _bounds = c.bounds; has = true; } else _bounds.Encapsulate(c.bounds);
                }
            }
        }

        return has;
    }

    private Vector2 FitFullMap(float _aspect)
    {
        RectTransform parent = fullMap != null ? fullMap.rectTransform.parent as RectTransform : null;

        float maxWidth = parent != null && parent.rect.width > 1f ? parent.rect.width * fullWidthRatio : 1200f;
        float maxHeight = parent != null && parent.rect.height > 1f ? parent.rect.height * fullHeightRatio : 640f;

        float width = maxWidth;
        float height = width / _aspect;

        if (height > maxHeight)
        {
            height = maxHeight;
            width = height * _aspect;
        }

        return new Vector2(width, height);
    }

    private static void SetSize(RectTransform _rect, float _width, float _height)
    {
        if (_rect == null)
            return;

        _rect.sizeDelta = new Vector2(_width, _height);
    }

    #endregion

    #region 图标

    private void BuildMarkers(RectTransform _mapRect, float _iconSize, float _playerSize, List<MapIcon> _list, out MapIcon _playerIcon)
    {
        _playerIcon = null;

        if (_mapRect == null)
            return;

        foreach (Checkpoint checkpoint in FindObjectsOfType<Checkpoint>(true))
        {
            if (checkpoint == null) continue;
            _list.Add(CreateIcon(_mapRect, _iconSize, checkpointColor, "Checkpoint", checkpoint.transform.position));
        }

        foreach (SceneDoor door in FindObjectsOfType<SceneDoor>(true))
        {
            if (door == null) continue;
            _list.Add(CreateIcon(_mapRect, _iconSize, doorColor, "Door", door.transform.position));
        }

        _playerIcon = CreateIcon(_mapRect, _playerSize, playerColor, "Player", Vector3.zero);

        if (_playerIcon != null)
        {
            _playerIcon.rect.SetAsLastSibling();
            _list.Add(_playerIcon);
        }
    }

    private MapIcon CreateIcon(RectTransform _parent, float _size, Color _color, string _name, Vector3 _worldPosition)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform), typeof(Image));
        go.layer = 5;

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(_size, _size);

        Image image = go.GetComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;

        return new MapIcon { rect = rect, normalized = WorldToNormalized(_worldPosition) };
    }

    /// <summary>世界坐标 → 0..1 的地图坐标。</summary>
    private Vector2 WorldToNormalized(Vector3 _worldPosition)
    {
        return new Vector2(Mathf.InverseLerp(mapBounds.min.x, mapBounds.max.x, _worldPosition.x),
                           Mathf.InverseLerp(mapBounds.min.y, mapBounds.max.y, _worldPosition.y));
    }

    /// <summary>0..1 的地图坐标 → 某张地图上的 anchoredPosition。_uv 是当前看的是贴图的哪一块。</summary>
    private static Vector2 NormalizedToMapPoint(Vector2 _normalized, RectTransform _mapRect, Rect _uv)
    {
        float u = (_normalized.x - _uv.x) / Mathf.Max(0.0001f, _uv.width);
        float v = (_normalized.y - _uv.y) / Mathf.Max(0.0001f, _uv.height);

        return new Vector2((u - 0.5f) * _mapRect.rect.width, (v - 0.5f) * _mapRect.rect.height);
    }

    private void UpdateMarkers()
    {
        if (!mapReady)
            return;

        Player player = PlayerManager.instance != null ? PlayerManager.instance.player : null;

        if (player == null)
            return;

        Vector2 playerPosition = WorldToNormalized(player.transform.position);
        bool visible = !player.stats.isDead;

        if (cornerPlayerIcon != null) cornerPlayerIcon.normalized = playerPosition;
        if (fullPlayerIcon != null) fullPlayerIcon.normalized = playerPosition;

        // 右上角小地图只看角色周围一小块，窗口跟着角色跑
        Rect cornerUv = CornerUv(playerPosition);

        if (cornerMap != null)
            cornerMap.uvRect = cornerUv;

        ApplyIcons(cornerIcons, cornerMap != null ? cornerMap.rectTransform : null, cornerUv, visible);
        ApplyIcons(fullIcons, fullMap != null ? fullMap.rectTransform : null, CurrentFullUv(), visible);
    }

    private void ApplyIcons(List<MapIcon> _icons, RectTransform _mapRect, Rect _uv, bool _playerVisible)
    {
        if (_mapRect == null)
            return;

        foreach (MapIcon icon in _icons)
        {
            if (icon == null || icon.rect == null)
                continue;

            bool isPlayer = icon == cornerPlayerIcon || icon == fullPlayerIcon;

            if (isPlayer)
            {
                if (icon.rect.gameObject.activeSelf != _playerVisible)
                    icon.rect.gameObject.SetActive(_playerVisible);

                if (!_playerVisible)
                    continue;
            }

            icon.rect.anchoredPosition = NormalizedToMapPoint(icon.normalized, _mapRect, _uv);
        }
    }

    /// <summary>大地图现在看的是贴图的哪一块。窗口是正方形，和贴图长宽比一致，所以不会拉伸。</summary>
    /// <summary>
    /// 右上角小地图现在看的是贴图的哪一块：以角色为中心的一小块。
    /// 和大地图一样用正方形窗口 —— 小地图框本身就是按地图长宽比做的，正方形窗口放进去正好不拉伸。
    /// 走到地图边缘时窗口贴边（角色图标就不再居中），这样不会看到地图外面去。
    /// </summary>
    private Rect CornerUv(Vector2 _playerNormalized)
    {
        float size = 1f;

        if (cornerViewWorldHeight > 0f && mapBounds.size.y > 0.01f)
            size = Mathf.Clamp01(cornerViewWorldHeight / mapBounds.size.y);

        if (size >= 1f)
            return new Rect(0f, 0f, 1f, 1f);

        float x = Mathf.Clamp(_playerNormalized.x - size * 0.5f, 0f, 1f - size);
        float y = Mathf.Clamp(_playerNormalized.y - size * 0.5f, 0f, 1f - size);

        return new Rect(x, y, size, size);
    }

    private Rect CurrentFullUv()
    {
        float size = 1f / Mathf.Max(1f, fullZoom);
        float x = Mathf.Clamp(fullViewCenter.x - size * 0.5f, 0f, 1f - size);
        float y = Mathf.Clamp(fullViewCenter.y - size * 0.5f, 0f, 1f - size);

        return new Rect(x, y, size, size);
    }

    /// <summary>把当前的缩放 / 平移应用到大地图，并重新摆一遍图标。</summary>
    private void ApplyFullMapView()
    {
        if (fullMap == null)
            return;

        Rect uv = CurrentFullUv();
        fullMap.uvRect = uv;

        // 夹取之后中心点会变，写回去，下一次拖动才跟手
        fullViewCenter = new Vector2(uv.x + uv.width * 0.5f, uv.y + uv.height * 0.5f);

        UpdateMarkers();
    }

    #endregion

    #region 输入

    private void HandleInput()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            cornerEnabledByUser = !cornerEnabledByUser;
            ApplyCornerVisibility();
        }

        if (Input.GetKeyDown(mapKey))
        {
            if (mapOpen)
            {
                CloseMap();
            }
            else if (Time.timeScale > 0f)
            {
                // 菜单 / 对话已经把游戏暂停了就不去抢，也不叠一层暂停
                OpenMap();
            }
        }

        if (mapOpen && Input.GetKeyDown(closeKey))
            CloseMap();

        if (mapOpen)
            HandleMapZoomAndDrag();
    }

    /// <summary>大地图：滚轮以鼠标为中心缩放，按住左键拖动。</summary>
    private void HandleMapZoomAndDrag()
    {
        if (fullMap == null)
            return;

        RectTransform rect = fullMap.rectTransform;
        Vector2 local;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, Input.mousePosition, GetUiCamera(), out local))
            return;

        Rect area = rect.rect;
        bool overMap = area.Contains(local);

        // 滚轮缩放：鼠标指着的那一点，缩放前后停在同一位置
        float scroll = Input.mouseScrollDelta.y;

        if (overMap && Mathf.Abs(scroll) > 0.01f)
        {
            float su = local.x / area.width + 0.5f;
            float sv = local.y / area.height + 0.5f;

            Rect uv = CurrentFullUv();
            Vector2 focus = new Vector2(uv.x + su * uv.width, uv.y + sv * uv.height);

            fullZoom = Mathf.Clamp(fullZoom * (1f + scroll * zoomStep), minZoom, maxZoom);

            float size = 1f / Mathf.Max(1f, fullZoom);
            fullViewCenter = focus + new Vector2((0.5f - su) * size, (0.5f - sv) * size);

            ApplyFullMapView();
        }

        // 按住左键拖地图
        if (Input.GetMouseButtonDown(0) && overMap)
        {
            draggingMap = true;
            lastMousePosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            draggingMap = false;
        }

        if (draggingMap && Input.GetMouseButton(0))
        {
            Vector3 delta = Input.mousePosition - lastMousePosition;
            lastMousePosition = Input.mousePosition;

            Rect uv = CurrentFullUv();
            Vector2 move = new Vector2(delta.x / area.width, delta.y / area.height) * uv.width;

            // 地图跟着鼠标走：拖向右，看的窗口就往左移
            fullViewCenter -= move;

            ApplyFullMapView();
        }
    }

    private Camera GetUiCamera()
    {
        if (fullMap == null)
            return null;

        Canvas canvas = fullMap.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }

    private void OpenMap()
    {
        if (!mapReady)
            return;

        mapOpen = true;

        if (fullRoot != null)
            fullRoot.SetActive(true);

        // 每次打开都先回到整张地图
        fullZoom = 1f;
        fullViewCenter = new Vector2(0.5f, 0.5f);
        draggingMap = false;
        ApplyFullMapView();

        ApplyCornerVisibility();

        if (GameManager.instance != null)
        {
            GameManager.instance.PauseGame(true);
            pausedByUs = true;
        }
    }

    /// <summary>关掉大地图；玩家去开别的菜单时也会被调用。</summary>
    public void CloseMap()
    {
        if (!mapOpen)
            return;

        mapOpen = false;

        if (fullRoot != null)
            fullRoot.SetActive(false);


        if (pausedByUs && GameManager.instance != null)
            GameManager.instance.PauseGame(false);

        pausedByUs = false;

        // 先取消暂停再刷新：小地图的显示条件里带着「没被暂停」
        ApplyCornerVisibility();
    }

    private void ApplyCornerVisibility()
    {
        if (cornerRoot == null)
            return;

        // 玩家按 Tab 关掉、开着大地图、或者游戏被暂停（菜单 / 对话）时都不显示
        bool visible = cornerEnabledByUser && !mapOpen && Time.timeScale > 0f;

        if (cornerRoot.activeSelf != visible)
            cornerRoot.SetActive(visible);
    }

    private void HideAll()
    {
        if (cornerRoot != null) cornerRoot.SetActive(false);
        if (fullRoot != null) fullRoot.SetActive(false);
    }

    private void ReleaseMap()
    {
        if (mapCameraObject != null)
        {
            Destroy(mapCameraObject);
            mapCameraObject = null;
        }

        if (mapTexture != null)
        {
            mapTexture.Release();
            Destroy(mapTexture);
            mapTexture = null;
        }
    }

    #endregion
}
