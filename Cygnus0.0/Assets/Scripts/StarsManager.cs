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

    [Header("连线消散")]
    [Tooltip("连线完成后停顿多久再开始让线消失（秒）")]
    [SerializeField] [Min(0f)] float delayBeforeLineFadeOut = 1f;
    [Tooltip("连线淡出时间（秒）；0 表示立即清除，不淡出")]
    [SerializeField] [Min(0f)] float lineFadeOutDuration = 2f;
    [Tooltip("淡出时是否同时将线条粗细渐变为 0")]
    [SerializeField] bool lineFadeOutWidthShrink = true;

    static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly string[] EmissionKeywords = { "_EMISSION", "EMISSION" };

    bool wasAligned;
    Coroutine _alignRotationCoroutine;
    Coroutine _lineFadeOutCoroutine;
    /// <summary>AdvanceTargetIndexAfterDelay 内连线淡出阶段为 true</summary>
    bool _advanceFadeOutRunning;

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

        if (lineFadeOutDuration <= 0f)
        {
            foreach (Star star in stars)
            {
                if (star != null)
                    star.ClearArcLines();
            }
            return;
        }

        if (_lineFadeOutCoroutine != null)
        {
            StopCoroutine(_lineFadeOutCoroutine);
            _lineFadeOutCoroutine = null;
            foreach (Star star in stars)
            {
                if (star != null)
                    star.ClearArcLines();
            }
            return; // 中断当前淡出并立即清除，便于下次拖拽能重新触发淡出
        }

        var lineData = CollectLineDataFromStars(stars);
        if (lineData.Count == 0) return;

        _lineFadeOutCoroutine = StartCoroutine(FadeOutLinesThenClear(lineData, stars, lineFadeOutDuration, lineFadeOutWidthShrink));
    }

    /// <summary>从当前星星列表中收集所有弧线的 LineRenderer 及初始颜色、线宽，用于淡出</summary>
    static List<(LineRenderer lr, Color startColor, Color endColor, float startWidth, float endWidth)> CollectLineDataFromStars(List<Star> stars)
    {
        var list = new List<(LineRenderer lr, Color startColor, Color endColor, float startWidth, float endWidth)>();
        if (stars == null) return list;
        foreach (Star star in stars)
        {
            if (star == null) continue;
            Transform container = star.transform.Find("ArcLines");
            if (container == null) continue;
            foreach (Transform child in container)
            {
                LineRenderer lr = child.GetComponent<LineRenderer>();
                if (lr == null) continue;
                list.Add((lr, lr.startColor, lr.endColor, lr.startWidth, lr.endWidth));
            }
        }
        return list;
    }

    /// <summary>在 duration 秒内将连线透明度（及可选线宽）渐变为 0，然后清除所有弧线</summary>
    IEnumerator FadeOutLinesThenClear(
        List<(LineRenderer lr, Color startColor, Color endColor, float startWidth, float endWidth)> lineData,
        List<Star> stars,
        float duration,
        bool widthShrink)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            foreach (var d in lineData)
            {
                if (d.lr == null) continue;
                float sa = Mathf.Lerp(d.startColor.a, 0f, t);
                float ea = Mathf.Lerp(d.endColor.a, 0f, t);
                d.lr.startColor = new Color(d.startColor.r, d.startColor.g, d.startColor.b, sa);
                d.lr.endColor = new Color(d.endColor.r, d.endColor.g, d.endColor.b, ea);
                if (widthShrink)
                {
                    d.lr.startWidth = Mathf.Lerp(d.startWidth, 0f, t);
                    d.lr.endWidth = Mathf.Lerp(d.endWidth, 0f, t);
                }
            }
            yield return null;
        }

        foreach (var d in lineData)
        {
            if (d.lr != null)
            {
                d.lr.startColor = new Color(d.startColor.r, d.startColor.g, d.startColor.b, 0f);
                d.lr.endColor = new Color(d.endColor.r, d.endColor.g, d.endColor.b, 0f);
                if (widthShrink)
                {
                    d.lr.startWidth = 0f;
                    d.lr.endWidth = 0f;
                }
            }
        }

        if (stars != null)
        {
            foreach (Star star in stars)
            {
                if (star != null)
                    star.ClearArcLines();
            }
        }

        _lineFadeOutCoroutine = null;
    }

    void OnLineAppearStarted()
    {
        AudioManager.Instance?.PlaySoundEffect3();
        if (rotationController != null)
            rotationController.isdragable = false;
    }

    void OnLineAppearEnded()
    {
        if (rotationController != null)
            rotationController.isdragable = true;
        StartCoroutine(AdvanceTargetIndexAfterDelay(delayBeforeLineFadeOut));
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

        _advanceFadeOutRunning = true;
        // 缓存当前所有弧线的初始颜色与线宽，用于在 lineFadeOutDuration 内渐变透明（及可选线宽收缩）后再清除
        var lineData = new List<(LineRenderer lr, Color startColor, Color endColor, float startWidth, float endWidth)>();
        foreach (Star star in oldStars)
        {
            if (star == null) continue;
            Transform container = star.transform.Find("ArcLines");
            if (container == null) continue;
            foreach (Transform child in container)
            {
                var lr = child.GetComponent<LineRenderer>();
                if (lr == null) continue;
                lineData.Add((lr, lr.startColor, lr.endColor, lr.startWidth, lr.endWidth));
            }
        }

        float fadeDuration = Mathf.Max(0f, lineFadeOutDuration);
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            foreach (var d in lineData)
            {
                if (d.lr == null) continue;
                float sa = Mathf.Lerp(d.startColor.a, 0f, t);
                float ea = Mathf.Lerp(d.endColor.a, 0f, t);
                d.lr.startColor = new Color(d.startColor.r, d.startColor.g, d.startColor.b, sa);
                d.lr.endColor = new Color(d.endColor.r, d.endColor.g, d.endColor.b, ea);
                if (lineFadeOutWidthShrink)
                {
                    d.lr.startWidth = Mathf.Lerp(d.startWidth, 0f, t);
                    d.lr.endWidth = Mathf.Lerp(d.endWidth, 0f, t);
                }
            }
            yield return null;
        }
        foreach (var d in lineData)
        {
            if (d.lr == null) continue;
            d.lr.startColor = new Color(d.startColor.r, d.startColor.g, d.startColor.b, 0f);
            d.lr.endColor = new Color(d.endColor.r, d.endColor.g, d.endColor.b, 0f);
            if (lineFadeOutWidthShrink)
            {
                d.lr.startWidth = 0f;
                d.lr.endWidth = 0f;
            }
        }

        _advanceFadeOutRunning = false;
        AudioManager.Instance?.PlaySoundEffect2();

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

    /// <summary>正在连线、已经对准、或连线正在消失时应隐藏提示用 Mask</summary>
    public bool ShouldHideHintMask()
    {
        if (Star.LineAppearRunningCount > 0) return true;
        if (_lineFadeOutCoroutine != null || _advanceFadeOutRunning) return true;
        if (!rotationController.IsDragging)
        {
            float diff = GetAngleDiff(GetCurrentAngle(), GetCurrentTargetAngle());
            if (diff <= angleTolerance && wasAligned) return true;
        }
        return false;
    }

    /// <summary>使 diff 更小的拖拽方向（屏幕二维，已归一化）。沿视角球面 S² 上当前到目标的最短弧（大圆弧）的切线方向，投影到屏幕；与 RotationController 的映射一致。</summary>
    public Vector2 GetDragHintDirectionNormalized()
    {
        if (rotationController == null || rotationController.targetToRotate == null) return Vector2.zero;
        Transform t = rotationController.targetToRotate;
        Vector3 currentView = t.forward;
        Vector3 targetView = Quaternion.Euler(GetCurrentTargetAngle()) * Vector3.forward;
        currentView.Normalize();
        targetView.Normalize();

        float dot = Vector3.Dot(currentView, targetView);
        if (dot >= 0.9999f) return Vector2.zero;
        Vector3 tangent = targetView - dot * currentView;
        float len = tangent.magnitude;
        if (len < 1e-6f) return Vector2.zero;
        tangent /= len;

        Vector3 up = Vector3.up;
        Vector3 right = Vector3.right;
        Vector3 eY = Vector3.Cross(up, currentView);
        if (eY.sqrMagnitude < 1e-8f) eY = Vector3.Cross(currentView, right);
        eY.Normalize();
        Vector3 eX = Vector3.Cross(currentView, eY);
        eX.Normalize();

        float dy = Vector3.Dot(tangent, eY);
        float dx = Vector3.Dot(tangent, eX);
        Vector2 v = new Vector2(-dy, dx);
        if (v.sqrMagnitude < 0.0001f) return Vector2.zero;
        return v.normalized;
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

    /// <summary>计算对准目标角度时，首星与尾星的世界坐标。返回是否有效（至少有一颗星且 rotationController 可用）。</summary>
    public bool GetAlignedFirstLastStarWorldPositions(out Vector3 firstWorld, out Vector3 lastWorld)
    {
        firstWorld = Vector3.zero;
        lastWorld = Vector3.zero;
        if (rotationController == null || rotationController.targetToRotate == null) return false;
        List<Star> stars = GetCurrentStars();
        if (stars == null || stars.Count == 0) return false;
        Transform target = rotationController.targetToRotate;
        Quaternion targetRot = Quaternion.Euler(GetCurrentTargetAngle());
        Star first = stars[0];
        if (first != null && first.transform != null)
        {
            Vector3 offsetLocal = Quaternion.Inverse(target.rotation) * (first.transform.position - target.position);
            firstWorld = target.position + targetRot * offsetLocal;
        }
        if (stars.Count > 1)
        {
            Star last = stars[stars.Count - 1];
            if (last != null && last.transform != null)
            {
                Vector3 offsetLocal = Quaternion.Inverse(target.rotation) * (last.transform.position - target.position);
                lastWorld = target.position + targetRot * offsetLocal;
            }
        }
        else
            lastWorld = firstWorld;
        return true;
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
