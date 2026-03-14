using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class StarsListEntry
{
    public List<Star> list = new List<Star>();
}

public class StarsManager : MonoBehaviour
{
    // 非单例，随场景卸载而销毁，不跨场景保存

    [Tooltip("嵌套列表：Index 与 targetIndex 对应，每个元素为该目标下的 Star 列表")]
    [SerializeField] List<StarsListEntry> starsLists = new List<StarsListEntry>();

    [Header("目标角度与亮度")]
    [Tooltip("多个目标角度，由 targetIndex 选取当前使用的项")]
    [SerializeField] List<Vector3> targetangles = new List<Vector3>();
    [Tooltip("当前使用的目标角度在 targetangles 中的索引")]
    [SerializeField] int targetIndex = 0;
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
    [Header("首尾星亮度")]
    [Tooltip("HDR 发射色（亮度由角度差计算，色相由此颜色决定）")]
    [ColorUsage(true, true)]
    [SerializeField] Color emissionColor = Color.white;
    [Header("首尾星缩放")]
    [Tooltip("角度差最大时首尾星的缩放")]
    [SerializeField] float firstLastStarScaleMin = 0.1f;
    [Tooltip("完全对准时首尾星的缩放")]
    [SerializeField] float firstLastStarScaleMax = 0.3f;
    [Header("首尾星材质")]
    [Tooltip("首尾星使用的材质（例如 glow）")]
    [SerializeField] Material firstLastStarMaterial;
    [Header("非首尾星")]
    [Tooltip("非首尾星使用的材质（不指定则仅通过亮度设为最暗）")]
    [SerializeField] Material nonFirstLastStarMaterial;
    [Tooltip("targetIndex 超出列表范围时切换到此场景。必须已在 File -> Build Settings 中加入该场景，否则会报错。")]
    [SerializeField] string outOfRangeSceneName = "Start";
    [Tooltip("对准后平滑旋转到目标角度的时长（秒）")]
    [SerializeField] float alignRotationDuration = 0.5f;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly string[] EmissionKeywords = { "_EMISSION", "EMISSION" };

    bool wasAligned;
    Coroutine _alignRotationCoroutine;

    void Start()
    {
        if (angleSource == null && rotationController != null)
            angleSource = rotationController.transform;
        // 进入场景时按当前 targetIndex 刷新星星材质与亮度
        ApplyNewTargetBrightness();
    }

    void OnEnable()
    {
        if (rotationController != null)
        {
            rotationController.onRotationEnd += OnRotationEnd;
            rotationController.onRotationStart += OnRotationStart;
        }
        Star.LineAppearStarted += OnLineAppearStarted;
        Star.LineAppearEnded += OnLineAppearEnded;
    }

    void OnDisable()
    {
        if (rotationController != null)
        {
            rotationController.onRotationEnd -= OnRotationEnd;
            rotationController.onRotationStart -= OnRotationStart;
        }
        Star.LineAppearStarted -= OnLineAppearStarted;
        Star.LineAppearEnded -= OnLineAppearEnded;
    }

    void OnRotationStart()
    {
        AudioManager.Instance?.PlaySoundEffect1();
        List<Star> stars = GetCurrentStars();
        if (stars == null) return;
        foreach (Star star in stars)
        {
            if (star != null)
                star.ClearArcLines();
        }
    }

    void OnLineAppearStarted()
    {
        if (rotationController != null)
            rotationController.isdragable = false;
    }

    void OnLineAppearEnded()
    {
        AudioManager.Instance?.PlaySoundEffect3();
        if (rotationController != null)
            rotationController.isdragable = true;
        StartCoroutine(AdvanceTargetIndexAfterDelay(1f));
    }

    IEnumerator AdvanceTargetIndexAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (targetangles == null || targetangles.Count == 0) yield break;

        List<Star> oldStars = GetCurrentStars();
        if (oldStars == null || oldStars.Count == 0)
        {
            if (targetIndex + 1 >= targetangles.Count && !string.IsNullOrEmpty(outOfRangeSceneName))
            {
                SceneManager.LoadScene(outOfRangeSceneName);
                yield break;
            }
            targetIndex = (targetIndex + 1) % targetangles.Count;
            ApplyNewTargetBrightness();
            yield break;
        }

        // 缓存当前所有弧线的初始颜色，用于在 fadeDuration 内渐变透明后再清除
        var lineData = new List<(LineRenderer lr, Color startColor, Color endColor)>();
        foreach (Star star in oldStars)
        {
            if (star == null) continue;
            Transform container = star.transform.Find("ArcLines");
            if (container == null) continue;
            foreach (Transform child in container)
            {
                var lr = child.GetComponent<LineRenderer>();
                if (lr == null) continue;
                Color s = lr.startColor, e = lr.endColor;
                lineData.Add((lr, s, e));
            }
        }

        float elapsed = 0f;
        const float fadeDuration = 1.1f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            foreach (var d in lineData)
            {
                if (d.lr == null) continue;
                // 仅渐变 alpha：从原始 alpha 过渡到 0，使线条在 fadeDuration 内逐渐透明
                Color s = d.startColor;
                Color e = d.endColor;
                float sa = Mathf.Lerp(s.a, 0f, t);
                float ea = Mathf.Lerp(e.a, 0f, t);
                d.lr.startColor = new Color(s.r, s.g, s.b, sa);
                d.lr.endColor = new Color(e.r, e.g, e.b, ea);
            }
            yield return null;
        }
        foreach (var d in lineData)
        {
            if (d.lr == null) continue;
            Color s = d.startColor;
            Color e = d.endColor;
            d.lr.startColor = new Color(s.r, s.g, s.b, 0f);
            d.lr.endColor  = new Color(e.r, e.g, e.b, 0f);
        }

        // 切换前将上一组首尾星设为 0.02 大小和 glow2 材质
        const float oldFirstLastStarScale = 0.02f;
        if (oldStars.Count > 0 && oldStars[0] != null)
        {
            SetStarMaterial(oldStars[0], nonFirstLastStarMaterial);
            SetStarScale(oldStars[0], oldFirstLastStarScale);
        }
        if (oldStars.Count > 1 && oldStars[oldStars.Count - 1] != null)
        {
            SetStarMaterial(oldStars[oldStars.Count - 1], nonFirstLastStarMaterial);
            SetStarScale(oldStars[oldStars.Count - 1], oldFirstLastStarScale);
        }

        foreach (Star star in oldStars)
        {
            if (star != null)
                star.ClearArcLines();
        }

        if (targetIndex + 1 >= targetangles.Count && !string.IsNullOrEmpty(outOfRangeSceneName))
        {
            SceneManager.LoadScene(outOfRangeSceneName);
            yield break;
        }
        targetIndex = (targetIndex + 1) % targetangles.Count;
        ApplyNewTargetBrightness();
    }

    void ApplyNewTargetBrightness()
    {
        List<Star> stars = GetCurrentStars();
        if (stars == null || stars.Count == 0) return;
        if (rotationController == null && angleSource == null) return;

        float brightness = GetCurrentBrightness();
        float scale = GetFirstLastStarScale();
        const float minBrightness = 0f;

        // 首星：glow 材质 + 目标亮度/缩放
        SetStarMaterial(stars[0], firstLastStarMaterial);
        SetStarEmissionBrightness(stars[0], brightness);
        SetStarScale(stars[0], scale);
        // 中间星：glow2 材质 + 最暗亮度
        for (int i = 1; i < stars.Count - 1; i++)
        {
            if (stars[i] != null)
            {
                SetStarMaterial(stars[i], nonFirstLastStarMaterial);
                SetStarEmissionBrightness(stars[i], minBrightness);
            }
        }
        // 尾星：glow 材质 + 目标亮度/缩放
        if (stars.Count > 1)
        {
            SetStarMaterial(stars[stars.Count - 1], firstLastStarMaterial);
            SetStarEmissionBrightness(stars[stars.Count - 1], brightness);
            SetStarScale(stars[stars.Count - 1], scale);
        }
    }

    void Update()
    {
        SetFirstLastStarBrightnessByAngleDiff();
        TryTriggerDrawWhenAligned();
    }

    /// <summary>每帧检测：未拖拽且 diff 在容差内时触发一次画线（不依赖松手事件）</summary>
    void TryTriggerDrawWhenAligned()
    {
        List<Star> stars = GetCurrentStars();
        if (rotationController == null || stars == null || stars.Count == 0) return;
        if (rotationController.IsDragging)
        {
            wasAligned = false;
            return;
        }
        float diff = GetAngleDiff(GetCurrentAngle(), GetCurrentTargetAngle());
        bool isAligned = diff <= angleTolerance;
        if (isAligned && !wasAligned)
        {
            AudioManager.Instance?.PlaySoundEffect2();
            StartSmoothRotateToTargetAngle();
            wasAligned = true;
        }
        else if (!isAligned)
            wasAligned = false;
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        float brightness = GetCurrentBrightness();
        GUI.Label(new Rect(10, 80,400, 28), $"HDR 亮度: {brightness:F2}", style);
        if (rotationController == null) return;
        float diff = GetAngleDiff(GetCurrentAngle(), GetCurrentTargetAngle());
        GUI.Label(new Rect(10, 52, 400, 28), $"diff: {diff:F1}", style);
    }
#endif

    /// <summary>仅松手时调用：若在容差内则触发所有 Star 绘制连线</summary>
    void OnRotationEnd()
    {
        List<Star> stars = GetCurrentStars();
        if (stars == null || stars.Count == 0 || rotationController == null) return;
        float diff = GetAngleDiff(GetCurrentAngle(), GetCurrentTargetAngle());
        if (diff > angleTolerance) return;
        AudioManager.Instance?.PlaySoundEffect2();
        StartSmoothRotateToTargetAngle();
    }

    /// <summary>对准后创建临时父节点，在 alignRotationDuration 内将 targetToRotate 平滑旋转到当前 targetangle</summary>
    void StartSmoothRotateToTargetAngle()
    {
        if (rotationController == null || rotationController.targetToRotate == null || alignRotationDuration <= 0f) return;
        if (_alignRotationCoroutine != null) StopCoroutine(_alignRotationCoroutine);
        _alignRotationCoroutine = StartCoroutine(SmoothRotateToTargetAngle());
    }

    IEnumerator SmoothRotateToTargetAngle()
    {
        if (rotationController != null)
            rotationController.isdragable = false;

        Transform target = rotationController.targetToRotate;
        Transform originalParent = target.parent;
        GameObject tempGo = new GameObject("TempRotationParent");
        tempGo.transform.position = target.position;
        tempGo.transform.rotation = Quaternion.identity;

        target.SetParent(tempGo.transform, worldPositionStays: true);
        Quaternion startWorld = target.rotation;
        Quaternion endWorld = Quaternion.Euler(GetCurrentTargetAngle());

        float elapsed = 0f;
        while (elapsed < alignRotationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / alignRotationDuration);
            Quaternion tempRot = Quaternion.Slerp(Quaternion.identity, endWorld * Quaternion.Inverse(startWorld), t);
            tempGo.transform.rotation = tempRot;
            yield return null;
        }

        target.rotation = endWorld;
        target.SetParent(originalParent, worldPositionStays: true);
        Destroy(tempGo);

        if (rotationController != null)
            rotationController.isdragable = true;

        List<Star> stars = GetCurrentStars();
        if (stars != null)
        {
            foreach (Star star in stars)
            {
                if (star != null)
                    star.ConnectOtherStars(targetIndex);
            }
        }
        _alignRotationCoroutine = null;
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

    /// <summary>当前朝向角度，必须来自实际被旋转的物体（targetToRotate），否则 diff 不会随旋转变化。</summary>
    Vector3 GetCurrentAngle()
    {
        if (rotationController != null && rotationController.targetToRotate != null)
            return rotationController.targetToRotate.eulerAngles;
        if (angleSource != null) return angleSource.eulerAngles;
        if (rotationController != null) return rotationController.Currentangle;
        return Vector3.zero;
    }

    /// <summary>targetIndex 超出列表范围时加载 outOfRangeSceneName，返回 false</summary>
    bool ValidateTargetIndex()
    {
        if (targetangles == null || targetangles.Count == 0 || targetIndex < 0 || targetIndex >= targetangles.Count)
        {
            if (!string.IsNullOrEmpty(outOfRangeSceneName))
                SceneManager.LoadScene(outOfRangeSceneName);
            return false;
        }
        return true;
    }

    /// <summary>返回当前选中的目标角度（由 targetIndex 决定）</summary>
    Vector3 GetCurrentTargetAngle()
    {
        if (!ValidateTargetIndex()) return Vector3.zero;
        return targetangles[targetIndex];
    }

    /// <summary>返回当前 targetIndex 对应的 Star 列表</summary>
    List<Star> GetCurrentStars()
    {
        if (!ValidateTargetIndex()) return null;
        if (starsLists == null || starsLists.Count == 0) return null;
        int idx = Mathf.Clamp(targetIndex, 0, starsLists.Count - 1);
        var entry = starsLists[idx];
        return entry != null ? entry.list : null;
    }

    /// <summary>根据当前角度与 targetangle 的差值计算应有的 HDR 亮度（与 SetFirstLastStarBrightnessByAngleDiff 一致）</summary>
    public float GetCurrentBrightness()
    {
        if (rotationController == null && angleSource == null) return 0f;
        float diff = GetAngleDiff(GetCurrentAngle(), GetCurrentTargetAngle());
        float t;
        if (diff <= angleTolerance)
            t = 1f;
        else if (diff >= maxAngleDiff || maxAngleDiff <= angleTolerance)
            t = 0f;
        else
        {
            float x = (diff - angleTolerance) / (maxAngleDiff - angleTolerance);
            t = Mathf.Max(0f, 1f - Mathf.Clamp01(x));
        }
        return Mathf.Clamp(t * maxBrightness, 0f, 10f);
    }

    /// <summary>根据当前角度与 targetangle 的差值计算首尾星缩放 [min, max]，与亮度一致为线性插值</summary>
    public float GetFirstLastStarScale()
    {
        if (rotationController == null && angleSource == null)
            return firstLastStarScaleMin;
        float diff = GetAngleDiff(GetCurrentAngle(), GetCurrentTargetAngle());
        float t;
        if (diff <= angleTolerance)
            t = 1f;
        else if (diff >= maxAngleDiff || maxAngleDiff <= angleTolerance)
            t = 0f;
        else
        {
            float x = (diff - angleTolerance) / (maxAngleDiff - angleTolerance);
            t = Mathf.Max(0f, 1f - Mathf.Clamp01(x));
        }
        return Mathf.Lerp(firstLastStarScaleMin, firstLastStarScaleMax, t);
    }

    /// <summary>根据当前角度与 targetangle 的差值，设置列表第一个和最后一个 Star 的亮度与缩放；差值越小越亮、越大。</summary>
    public void SetFirstLastStarBrightnessByAngleDiff()
    {
        List<Star> stars = GetCurrentStars();
        if (stars == null || stars.Count == 0) return;
        if (rotationController == null && angleSource == null) return;

        float brightness = GetCurrentBrightness();
        float scale = GetFirstLastStarScale();
        // 材质仅在进入场景和 targetIndex 变化时刷新，此处只更新亮度与缩放
        SetStarEmissionBrightness(stars[0], brightness);
        SetStarScale(stars[0], scale);
        if (stars.Count > 1)
        {
            SetStarEmissionBrightness(stars[stars.Count - 1], brightness);
            SetStarScale(stars[stars.Count - 1], scale);
        }
    }

    void SetStarMaterial(Star star, Material mat)
    {
        if (star == null || mat == null) return;
        Renderer r = star.GetComponentInChildren<Renderer>();
        if (r == null) return;
        r.material = mat;
    }

    void SetStarScale(Star star, float scale)
    {
        if (star == null) return;
        star.transform.localScale = Vector3.one * Mathf.Clamp(scale, 0.02f, 0.3f);
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
            foreach (string kw in EmissionKeywords)
                mat.EnableKeyword(kw);
            mat.SetColor(EmissionColorId, color);
        }
        else if (mat.HasProperty(BaseColorId))
        {
            mat.SetColor(BaseColorId, color);
        }
    }
}
