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
    [SerializeField] float maxBrightness = 10f;
    [Tooltip("角度差在此范围内视为对准，给满亮度")]
    [SerializeField] float angleTolerance = 10f;
    [Tooltip("角度差超过此值亮度为 0")]
    [SerializeField] float maxAngleDiff = 180f;
    [Tooltip("发射色作为色相/色调，实际 HDR 强度由角度差计算，与左上角显示的亮度一致")]
    [SerializeField] Color emissionColor = Color.white;
    [Header("首尾星缩放")]
    [Tooltip("角度差最大时首尾星的缩放")]
    [SerializeField] float firstLastStarScaleMin = 0.1f;
    [Tooltip("完全对准时首尾星的缩放")]
    [SerializeField] float firstLastStarScaleMax = 0.3f;

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
        Star.LineAppearStarted += OnLineAppearStarted;
        Star.LineAppearEnded += OnLineAppearEnded;
    }

    void OnDisable()
    {
        if (rotationController != null)
            rotationController.onRotationEnd -= OnRotationEnd;
        Star.LineAppearStarted -= OnLineAppearStarted;
        Star.LineAppearEnded -= OnLineAppearEnded;
    }

    void OnLineAppearStarted()
    {
        if (rotationController != null)
            rotationController.isdragable = false;
    }

    void OnLineAppearEnded()
    {
        if (rotationController != null)
            rotationController.isdragable = true;
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
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        float brightness = GetCurrentBrightness();
        GUI.Label(new Rect(10, 80,400, 28), $"HDR 亮度: {brightness:F2}", style);
        if (rotationController == null) return;
        float diff = GetAngleDiff(GetCurrentAngle(), targetangle);
        GUI.Label(new Rect(10, 52, 400, 28), $"diff: {diff:F1}", style);
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

    /// <summary>将角度归一化到 [0, 360)，使 360° 与 0° 视为同一角度</summary>
    static float NormalizeAngle360(float angle)
    {
        angle = angle % 360f;
        if (angle < 0f) angle += 360f;
        return angle;
    }

    float GetAngleDiff(Vector3 current, Vector3 target)
    {
        float cx = NormalizeAngle360(current.x), cy = NormalizeAngle360(current.y), cz = NormalizeAngle360(current.z);
        float tx = NormalizeAngle360(target.x), ty = NormalizeAngle360(target.y), tz = NormalizeAngle360(target.z);
        float dx = Mathf.Abs(Mathf.DeltaAngle(cx, tx));
        float dy = Mathf.Abs(Mathf.DeltaAngle(cy, ty));
        float dz = Mathf.Abs(Mathf.DeltaAngle(cz, tz));
        return new Vector3(dx, dy, dz).magnitude;
    }

    Vector3 GetCurrentAngle()
    {
        if (angleSource != null) return angleSource.eulerAngles;
        if (rotationController != null) return rotationController.Currentangle;
        return Vector3.zero;
    }

    /// <summary>根据当前角度与 targetangle 的差值计算应有的 HDR 亮度（与 SetFirstLastStarBrightnessByAngleDiff 一致）</summary>
    public float GetCurrentBrightness()
    {
        if (rotationController == null && angleSource == null) return 0f;
        float diff = GetAngleDiff(GetCurrentAngle(), targetangle);
        float t;
        if (diff <= angleTolerance)
            t = 1f;
        else if (diff >= maxAngleDiff || maxAngleDiff <= angleTolerance)
            t = 0f;
        else
        {
            // 归一化到 [0,1]，再用 sqrt 使越接近目标亮度变化速率越大（近目标时对角度更敏感）
            float x = (diff - angleTolerance) / (maxAngleDiff - angleTolerance);
            t = Mathf.Max(0f, 1f - Mathf.Sqrt(Mathf.Clamp01(x)));
        }
        return Mathf.Clamp(t * maxBrightness, 0f, 10f);
    }

    /// <summary>根据当前角度与 targetangle 的差值计算首尾星缩放 [min, max]，使用与亮度相同的 sqrt 曲线（越接近目标变化越快）</summary>
    public float GetFirstLastStarScale()
    {
        if (rotationController == null && angleSource == null)
            return firstLastStarScaleMin;
        float diff = GetAngleDiff(GetCurrentAngle(), targetangle);
        float t;
        if (diff <= angleTolerance)
            t = 1f;
        else if (diff >= maxAngleDiff || maxAngleDiff <= angleTolerance)
            t = 0f;
        else
        {
            float x = (diff - angleTolerance) / (maxAngleDiff - angleTolerance);
            t = Mathf.Max(0f, 1f - Mathf.Sqrt(Mathf.Clamp01(x)));
        }
        return Mathf.Lerp(firstLastStarScaleMin, firstLastStarScaleMax, t);
    }

    /// <summary>根据当前角度与 targetangle 的差值，设置列表第一个和最后一个 Star 的亮度与缩放；差值越小越亮、越大。</summary>
    public void SetFirstLastStarBrightnessByAngleDiff()
    {
        if (stars == null || stars.Count == 0) return;
        if (rotationController == null && angleSource == null) return;

        float brightness = GetCurrentBrightness();
        float scale = GetFirstLastStarScale();
        SetStarEmissionBrightness(stars[0], brightness);
        SetStarScale(stars[0], scale);
        if (stars.Count > 1)
        {
            SetStarEmissionBrightness(stars[stars.Count - 1], brightness);
            SetStarScale(stars[stars.Count - 1], scale);
        }
    }

    void SetStarScale(Star star, float scale)
    {
        if (star == null) return;
        star.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.1f, 0.3f);
    }

    void SetStarEmissionBrightness(Star star, float brightness)
    {
        if (star == null) return;
        Renderer r = star.GetComponentInChildren<Renderer>();
        if (r == null || r.sharedMaterial == null) return;

        Material mat = r.material;
        // 用 emissionColor 仅作色相（归一化），强度完全由 brightness 决定，使材质面板 HDR 亮度与界面显示一致
        float maxChannel = Mathf.Max(emissionColor.r, emissionColor.g, emissionColor.b);
        Color tint = maxChannel > 0.0001f ? emissionColor / maxChannel : Color.white;
        Color color = tint * brightness;

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
