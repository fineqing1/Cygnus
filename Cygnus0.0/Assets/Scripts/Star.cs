using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NeighborListEntry
{
    public List<Star> list = new List<Star>();
}

public class Star : MonoBehaviour
{
    /// <summary>嵌套列表：Index 与目标对应，每个元素为该目标下的邻居 Star 列表</summary>
    public List<NeighborListEntry> neighborstarsLists = new List<NeighborListEntry>();

    [Header("弧线球面设置")]
    [Tooltip("球心世界坐标，可直接在 Inspector 输入")]
    public Vector3 sphereCenter;
    public int segmentCount = 50;
    [Tooltip("弧线起点粗细")]
    public float lineWidth = 0.02f;
    [Tooltip("弧线终点粗细（越靠近终点越粗）")]
    public float lineWidthEnd = 0.05f;

    [Header("线条外观")]
    [Tooltip("弧线使用的材质（需支持 LineRenderer/URP；不指定则线可能不可见）")]
    public Material lineMaterial;
    [Tooltip("线条亮度倍率（HDR，>1 更亮）")]
    [Min(0.1f)]
    public float lineIntensity = 2f;
    [Tooltip("弧线起点颜色")]
    public Color lineStartColor = Color.white;
    [Tooltip("弧线终点颜色（与起点不同时可形成渐变）")]
    public Color lineEndColor = Color.white;
    [Tooltip("弧线从起点到终点逐段出现的时间（秒）；0 表示立即显示")]
    [Min(0f)]
    public float lineAppearDuration = 0.5f;
    [Header("弧线终点粒子")]
    [Tooltip("是否在弧线终点创建跟随的粒子系统")]
    public bool enableTipParticles = true;
    [Tooltip("弧线终点粒子预制体（不指定则不生成终点粒子）")]
    public GameObject tipParticlePrefab;

    [Header("调试")]
    [Tooltip("开启后在 Console 输出画线详情")]
    public bool debugLog = false;
    [Tooltip("开启后在 Scene 视图绘制球心、半径与连线（仅编辑器）")]
    public bool debugGizmos = false;

    const string ArcLinesContainerName = "ArcLines";
    Coroutine _lineAppearCoroutine;

    /// <summary>当前正在播放“线条出现”动画的 Star 数量，用于禁用拖拽</summary>
    static int _lineAppearRunningCount;
    /// <summary>当前正在播放线条出现动画的 Star 数量（0 = 未在连线）</summary>
    public static int LineAppearRunningCount => _lineAppearRunningCount;
    /// <summary>任意 Star 开始播放线条出现动画时（计数从 0 变为 1）</summary>
    public static System.Action LineAppearStarted;
    /// <summary>所有线条出现动画结束时（计数变为 0）</summary>
    public static System.Action LineAppearEnded;

    /// <summary>清除本星所有弧线及终点粒子，并停止出现动画</summary>
    public void ClearArcLines()
    {
        if (_lineAppearCoroutine != null)
        {
            StopCoroutine(_lineAppearCoroutine);
            _lineAppearCoroutine = null;
            _lineAppearRunningCount--;
            if (_lineAppearRunningCount < 0) _lineAppearRunningCount = 0;
            if (_lineAppearRunningCount == 0) LineAppearEnded?.Invoke();
        }
        Transform container = transform.Find(ArcLinesContainerName);
        if (container != null)
            DestroyImmediate(container.gameObject);
    }

    List<Star> GetNeighborList(int listIndex)
    {
        if (neighborstarsLists == null || neighborstarsLists.Count == 0) return null;
        int idx = Mathf.Clamp(listIndex, 0, neighborstarsLists.Count - 1);
        var entry = neighborstarsLists[idx];
        return entry != null ? entry.list : null;
    }

    public void ConnectOtherStars(int listIndex)
    {
        List<Star> neighborstars = GetNeighborList(listIndex);
        if (neighborstars == null) return;

        if (debugLog)
        {
            int validCount = 0;
            foreach (Star n in neighborstars) if (n != null) validCount++;
            Debug.Log($"[Star.ConnectOtherStars] {name} | 邻居数={neighborstars.Count} (有效={validCount}) | segmentCount={segmentCount}");
        }

        ClearArcLines();

        GameObject containerGo = new GameObject(ArcLinesContainerName);
        containerGo.transform.SetParent(transform, worldPositionStays: false);

        var lineDataList = new List<LineData>();

        foreach (Star neighbor in neighborstars)
        {
            if (neighbor == null)
            {
                if (debugLog) Debug.LogWarning($"[Star.ConnectOtherStars] {name} 的邻居列表中存在 null，已跳过");
                continue;
            }

            GameObject lineGo = new GameObject($"LineTo_{neighbor.name}");
            lineGo.transform.SetParent(containerGo.transform, worldPositionStays: false);

            LineRenderer lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidthEnd;
            if (lineMaterial != null)
                lr.material = lineMaterial;
            lr.startColor = lineStartColor * lineIntensity;
            lr.endColor = lineEndColor * lineIntensity;

            Vector3 startPos = transform.position;
            Vector3 endPos = neighbor.transform.position;
            int segs = Mathf.Max(1, segmentCount);
            int totalPoints = segs + 1;

            if (lineAppearDuration <= 0f)
            {
                lr.positionCount = totalPoints;
                for (int i = 0; i < totalPoints; i++)
                {
                    float t = i / (float)segs;
                    lr.SetPosition(i, Vector3.Lerp(startPos, endPos, t));
                }
                if (enableTipParticles && tipParticlePrefab != null)
                {
                    Vector3 tangent = (endPos - startPos).normalized;
                    if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;
                    CreateTipParticleSystem(lineGo.transform, endPos, tangent);
                }
            }
            else
            {
                lr.positionCount = 2;
                lr.SetPosition(0, startPos);
                float t1 = 1f / segs;
                Vector3 tipPos = Vector3.Lerp(startPos, endPos, t1);
                lr.SetPosition(1, tipPos);
                Transform tipParticles = null;
                if (enableTipParticles && tipParticlePrefab != null)
                {
                    Vector3 tangent = (endPos - startPos).normalized;
                    if (tangent.sqrMagnitude < 0.0001f) tangent = Vector3.forward;
                    tipParticles = CreateTipParticleSystem(lineGo.transform, tipPos, tangent).transform;
                }
                lineDataList.Add(new LineData { lr = lr, startPos = startPos, endPos = endPos, tipParticles = tipParticles });
            }
        }

        if (lineDataList.Count > 0)
        {
            _lineAppearRunningCount++;
            if (_lineAppearRunningCount == 1) LineAppearStarted?.Invoke();
            _lineAppearCoroutine = StartCoroutine(AnimateLinesAppear(lineDataList));
        }
    }

    struct LineData
    {
        public LineRenderer lr;
        public Vector3 startPos, endPos;
        public Transform tipParticles;
    }

    GameObject CreateTipParticleSystem(Transform parent, Vector3 worldPos, Vector3 tangent)
    {
        GameObject go = Instantiate(tipParticlePrefab, parent);
        go.transform.SetParent(parent, worldPositionStays: true);
        go.transform.position = worldPos;
        if (tangent.sqrMagnitude >= 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(tangent);
        go.name = "ArcTipParticles";
        var ps = go.GetComponentInChildren<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
        }
        return go;
    }

    System.Collections.IEnumerator AnimateLinesAppear(List<LineData> lineDataList)
    {
        float elapsed = 0f;
        int segs = Mathf.Max(1, segmentCount);
        int totalPoints = segs + 1;
        Vector3 tangentFallback = Vector3.forward;
        while (elapsed < lineAppearDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lineAppearDuration);
            int showCount = 2 + (int)((totalPoints - 2) * progress);
            if (showCount > totalPoints) showCount = totalPoints;

            foreach (var data in lineDataList)
            {
                if (data.lr == null) continue;
                data.lr.positionCount = showCount;
                Vector3 lastPos = data.startPos;
                for (int i = 0; i < showCount; i++)
                {
                    float t = i / (float)segs;
                    lastPos = Vector3.Lerp(data.startPos, data.endPos, t);
                    data.lr.SetPosition(i, lastPos);
                }
                if (data.tipParticles != null)
                {
                    data.tipParticles.position = lastPos;
                    Vector3 tangent = (data.endPos - data.startPos).normalized;
                    if (tangent.sqrMagnitude >= 0.0001f)
                        data.tipParticles.rotation = Quaternion.LookRotation(tangent);
                    else
                        data.tipParticles.rotation = Quaternion.LookRotation(tangentFallback);
                }
            }
            yield return null;
        }

        foreach (var data in lineDataList)
        {
            if (data.lr == null) continue;
            data.lr.positionCount = totalPoints;
            for (int i = 0; i < totalPoints; i++)
            {
                float t = i / (float)segs;
                data.lr.SetPosition(i, Vector3.Lerp(data.startPos, data.endPos, t));
            }
            if (data.tipParticles != null)
            {
                data.tipParticles.position = data.endPos;
                Vector3 tangent = (data.endPos - data.startPos).normalized;
                if (tangent.sqrMagnitude >= 0.0001f)
                    data.tipParticles.rotation = Quaternion.LookRotation(tangent);
            }
        }
        _lineAppearCoroutine = null;
        _lineAppearRunningCount--;
        if (_lineAppearRunningCount < 0) _lineAppearRunningCount = 0;
        if (_lineAppearRunningCount == 0) LineAppearEnded?.Invoke();
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!debugGizmos) return;

        List<Star> neighbors = GetNeighborList(0);
        if (neighbors == null) return;
        Gizmos.color = Color.white;
        foreach (Star neighbor in neighbors)
        {
            if (neighbor == null) continue;
            Gizmos.DrawLine(transform.position, neighbor.transform.position);
        }
    }
#endif
}
