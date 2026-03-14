using UnityEngine;

/// <summary>
/// 根据 StarsManager 状态控制提示用 Mask 的显示与位置：
/// 若正在连线/已经对准/连线正在消失则用透明度隐藏 Mask；
/// 否则根据“使 diff 更小”的拖拽方向，将 Mask 放在从原点沿该方向与 Canvas 边缘的交点。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MaskHintController : MonoBehaviour
{
    [Tooltip("提供对准与连线状态的 StarsManager")]
    public StarsManager starsManager;
    [Tooltip("要控制显示与位置的 Mask（RectTransform）。不填则用本物体")]
    public RectTransform maskRect;
    [Tooltip("用于将屏幕坐标转换为 UI 坐标的 Canvas。不填则从 Mask 向上查找")]
    public Canvas canvas;

    RectTransform _rect;
    RectTransform _canvasRect;
    CanvasGroup _canvasGroup;

    void Awake()
    {
        _rect = maskRect != null ? maskRect : GetComponent<RectTransform>();
        if (_rect == null) return;
        _canvasGroup = _rect.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _rect.gameObject.AddComponent<CanvasGroup>();
        if (canvas == null)
        {
            var c = _rect.GetComponentInParent<Canvas>();
            if (c != null) canvas = c;
        }
        if (canvas != null)
            _canvasRect = canvas.transform as RectTransform;
    }

    void Update()
    {
        if (starsManager == null || _rect == null) return;

        if (starsManager.ShouldHideHintMask())
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            return;
        }

        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        Vector2 dir = starsManager.GetDragHintDirectionNormalized();
        if (dir.sqrMagnitude < 0.0001f) return;

        if (_canvasRect == null) return;

        Rect rect = _canvasRect.rect;
        Vector2 origin = rect.center;
        float t = GetRayRectEdgeT(origin, dir, rect.xMin, rect.xMax, rect.yMin, rect.yMax);
        _rect.anchoredPosition = origin + dir * t;
    }

    /// <summary>从 origin 沿方向 dir 与矩形边界的交点对应的参数 t（最小正 t），矩形为 [xMin,xMax] x [yMin,yMax]</summary>
    static float GetRayRectEdgeT(Vector2 origin, Vector2 dir, float xMin, float xMax, float yMin, float yMax)
    {
        float t = float.MaxValue;
        if (dir.x > 0.0001f) { float tx = (xMax - origin.x) / dir.x; if (tx > 0f && tx < t) t = tx; }
        if (dir.x < -0.0001f) { float tx = (xMin - origin.x) / dir.x; if (tx > 0f && tx < t) t = tx; }
        if (dir.y > 0.0001f) { float ty = (yMax - origin.y) / dir.y; if (ty > 0f && ty < t) t = ty; }
        if (dir.y < -0.0001f) { float ty = (yMin - origin.y) / dir.y; if (ty > 0f && ty < t) t = ty; }
        return t < float.MaxValue ? t : 0f;
    }
}
