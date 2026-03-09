using System.Collections.Generic;
using UnityEngine;

public class StarsManager : MonoBehaviour
{
    // 非单例，随场景卸载而销毁，不跨场景保存

    [SerializeField] List<Star> stars = new List<Star>(); // 可在 Inspector 中拖拽赋值，序列化保存

    [Header("目标角度与亮度")]
    public Vector3 targetangle;
    [Tooltip("用于拖拽、松手逻辑与对准判断")]
    [SerializeField] RotationContorller rotationController;
    [Tooltip("不填则用 rotationController 所在物体；填了则直接读其世界欧拉角，保证拖拽时亮度实时变化")]
    [SerializeField] Transform angleSource;
    [Tooltip("角度差超过此值亮度为 0；完全对准时亮度为该值")]
    [SerializeField] float maxBrightness = 6f;
    [Tooltip("角度差在此范围内视为对准，给满亮度")]
    [SerializeField] float angleTolerance = 10f;
    [Tooltip("角度差超过此值亮度为 0")]
    [SerializeField] float maxAngleDiff = 180f;
    [Tooltip("HDR 发射色（亮度由角度差计算）")]
    [SerializeField] Color emissionColor = Color.white;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // URP Lit 使用 _EmissionColor；URP Unlit 使用 _BaseColor（无 Emission）
    static readonly string[] EmissionKeywords = { "_EMISSION", "EMISSION" };

    bool wasAligned;

    void Start()
    {
        if (angleSource == null && rotationController != null)
            angleSource = rotationController.transform;
    }

    void OnEnable()
    {
        if (rotationController != null)
            rotationController.onRotationEnd += OnRotationEnd;
    }

    void OnDisable()
    {
        if (rotationController != null)
            rotationController.onRotationEnd -= OnRotationEnd;
    }

    void Update()
    {
        SetFirstLastStarBrightnessByAngleDiff();
        TryTriggerDrawWhenAligned();
    }

    /// <summary>每帧检测：未拖拽且 diff 在容差内时触发一次画线（不依赖松手事件）</summary>
    void TryTriggerDrawWhenAligned()
    {
        if (rotationController == null || stars == null || stars.Count == 0) return;
        if (rotationController.IsDragging)
        {
            wasAligned = false;
            return;
        }
        float diff = GetAngleDiff(GetCurrentAngle(), targetangle);
        bool isAligned = diff <= angleTolerance;
        if (isAligned && !wasAligned)
        {
            foreach (Star star in stars)
            {
                if (star != null)
                    star.ConnectOtherStars();
            }
            wasAligned = true;
        }
        else if (!isAligned)
            wasAligned = false;
    }

    void OnGUI()
    {
        if (rotationController == null) return;
        float diff = GetAngleDiff(GetCurrentAngle(), targetangle);
        Rect rect = new Rect(10, 52, 400, 28);
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        GUI.Label(rect, $"diff: {diff:F1}", style);
    }

    /// <summary>仅松手时调用：若在容差内则触发所有 Star 绘制连线</summary>
    void OnRotationEnd()
    {
        if (stars == null || stars.Count == 0 || rotationController == null) return;
        float diff = GetAngleDiff(GetCurrentAngle(), targetangle);
        if (diff > angleTolerance) return;
        foreach (Star star in stars)
        {
            if (star != null)
                star.ConnectOtherStars();
        }
    }

    float GetAngleDiff(Vector3 current, Vector3 target)
    {
        float dx = Mathf.Abs(Mathf.DeltaAngle(current.x, target.x));
        float dy = Mathf.Abs(Mathf.DeltaAngle(current.y, target.y));
        float dz = Mathf.Abs(Mathf.DeltaAngle(current.z, target.z));
        return new Vector3(dx, dy, dz).magnitude;
    }

    Vector3 GetCurrentAngle()
    {
        if (angleSource != null) return angleSource.eulerAngles;
        if (rotationController != null) return rotationController.Currentangle;
        return Vector3.zero;
    }

    /// <summary>根据当前角度与 targetangle 的差值，设置列表第一个和最后一个 Star 的 Material 的 HDR 亮度；差值越小越亮，越大越暗，最低为 0。</summary>
    public void SetFirstLastStarBrightnessByAngleDiff()
    {
        if (stars == null || stars.Count == 0) return;
        if (rotationController == null && angleSource == null) return;

        float diff = GetAngleDiff(GetCurrentAngle(), targetangle);
        float t;
        if (diff <= angleTolerance)
            t = 1f;
        else if (diff >= maxAngleDiff || maxAngleDiff <= angleTolerance)
            t = 0f;
        else
            t = Mathf.Max(0f, 1f - (diff - angleTolerance) / (maxAngleDiff - angleTolerance));
        float brightness = Mathf.Clamp(t * maxBrightness, 0f, 6f);

        SetStarEmissionBrightness(stars[0], brightness);
        if (stars.Count > 1)
            SetStarEmissionBrightness(stars[stars.Count - 1], brightness);
    }

    void SetStarEmissionBrightness(Star star, float brightness)
    {
        if (star == null) return;
        Renderer r = star.GetComponentInChildren<Renderer>();
        if (r == null || r.sharedMaterial == null) return;

        Material mat = r.material;
        Color color = emissionColor * brightness;

        if (mat.HasProperty(EmissionColorId))
        {
            // URP Lit：用 Emission 控制亮度
            foreach (string kw in EmissionKeywords)
                mat.EnableKeyword(kw);
            mat.SetColor(EmissionColorId, color);
        }
        else if (mat.HasProperty(BaseColorId))
        {
            // URP Unlit：用 Base Color 控制亮度（无光照，直接显示该颜色）
            mat.SetColor(BaseColorId, color);
        }
    }
}
