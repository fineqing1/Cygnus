using UnityEngine;

/// <summary>
/// 挂在子物体上：使该 RectTransform 的世界坐标与缩放不随父物体变化。
/// 在 Start 时记录当前世界位置与 lossyScale，之后每帧恢复，从而抵消父物体（如 Mask）的移动/缩放。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class IgnoreParentRectTransform : MonoBehaviour
{
    RectTransform _rect;
    Transform _parent;
    Vector3 _worldPosition;
    Vector3 _worldScale;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _parent = _rect.parent;
    }

    void Start()
    {
        if (_parent == null) return;
        _worldPosition = _rect.position;
        _worldScale = _rect.lossyScale;
    }

    void LateUpdate()
    {
        if (_parent == null) return;
        _rect.position = _worldPosition;
        Vector3 pLossy = _parent.lossyScale;
        if (pLossy.x != 0f && pLossy.y != 0f && pLossy.z != 0f)
            _rect.localScale = new Vector3(
                _worldScale.x / pLossy.x,
                _worldScale.y / pLossy.y,
                _worldScale.z / pLossy.z);
    }
}
