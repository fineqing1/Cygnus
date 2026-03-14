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

        Vector3 center = sphereCenter;

        if (debugLog)
        {
            int validCount = 0;
            foreach (Star n in neighborstars) if (n != null) validCount++;
            Debug.Log($"[Star.ConnectOtherStars] {name} | 球心={center} | 邻居数={neighborstars.Count} (有效={validCount}) | segmentCount={segmentCount}");
        }

        ClearArcLines();

        GameObject containerGo = new GameObject(ArcLinesContainerName);
        containerGo.transform.SetParent(transform, worldPositionStays: false);

        var arcDataList = new List<ArcLineData>();

        foreach (Star neighbor in neighborstars)
        {
            if (neighbor == null)
            {
                if (debugLog) Debug.LogWarning($"[Star.ConnectOtherStars] {name} 的邻居列表中存在 null，已跳过");
                continue;
            }

            GameObject lineGo = new GameObject($"ArcTo_{neighbor.name}");
            lineGo.transform.SetParent(containerGo.transform, worldPositionStays: false);

            LineRenderer lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidthEnd;
            if (lineMaterial != null)
                lr.material = lineMaterial;
            lr.startColor = lineStartColor * lineIntensity;
            lr.endColor = lineEndColor * lineIntensity;

            float distA = Vector3.Distance(center, transform.position);
            float distB = Vector3.Distance(center, neighbor.transform.position);
            Vector3 dirA = (transform.position - center).normalized;
            Vector3 dirB = (neighbor.transform.position - center).normalized;

            if (debugLog)
            {
                float centralAngle = Vector3.Angle(dirA, dirB);
                Debug.Log($"[Star.ConnectOtherStars] {name} -> {neighbor.name} | 球心={center} | 本星距球心={distA:F3} | 邻居距球心={distB:F3} | 圆心角={centralAngle:F1}°");
            }

            int segs = Mathf.Max(1, segmentCount);
            int totalPoints = segs + 1;

            if (lineAppearDuration <= 0f)
            {
                lr.positionCount = totalPoints;
                for (int i = 0; i < totalPoints; i++)
                {
                    float t = i / (float)segs;
                    Vector3 pos = GetArcPointAt(center, dirA, dirB, distA, distB, t);
                    lr.SetPosition(i, pos);
                }
                if (enableTipParticles && tipParticlePrefab != null)
                {
                    Vector3 endPos = GetArcPointAt(center, dirA, dirB, distA, distB, 1f);
                    Vector3 tangent = GetArcTangentAt(center, dirA, dirB, distA, distB, 1f);
                    CreateTipParticleSystem(lineGo.transform, endPos, tangent);
                }
            }
            else
            {
                lr.positionCount = 2;
                lr.SetPosition(0, transform.position);
                float t1 = 1f / segs;
                Vector3 tipPos = GetArcPointAt(center, dirA, dirB, distA, distB, t1);
                lr.SetPosition(1, tipPos);
                Transform tipParticles = null;
                if (enableTipParticles && tipParticlePrefab != null)
                {
                    Vector3 tangent = GetArcTangentAt(center, dirA, dirB, distA, distB, t1);
                    tipParticles = CreateTipParticleSystem(lineGo.transform, tipPos, tangent).transform;
                }
                arcDataList.Add(new ArcLineData { lr = lr, center = center, dirA = dirA, dirB = dirB, distA = distA, distB = distB, tipParticles = tipParticles });
            }
        }

        if (arcDataList.Count > 0)
        {
            _lineAppearRunningCount++;
            if (_lineAppearRunningCount == 1) LineAppearStarted?.Invoke();
            _lineAppearCoroutine = StartCoroutine(AnimateLinesAppear(arcDataList));
        }
    }

    struct ArcLineData
    {
        public LineRenderer lr;
        public Vector3 center;
        public Vector3 dirA, dirB;
        public float distA, distB;
        public Transform tipParticles;
    }

    /// <summary>弧线参数 t∈[0,1] 处的位置：沿圆心角 Slerp 方向，半径在 distA 与 distB 间线性插值，两端落在两星上</summary>
    static Vector3 GetArcPointAt(Vector3 center, Vector3 dirA, Vector3 dirB, float distA, float distB, float t)
    {
        Vector3 dir = Vector3.Slerp(dirA, dirB, t);
        float r = Mathf.Lerp(distA, distB, t);
        return center + dir * r;
    }

    /// <summary>弧线参数 t 处的切线方向（世界空间，沿绘制方向）</summary>
    static Vector3 GetArcTangentAt(Vector3 center, Vector3 dirA, Vector3 dirB, float distA, float distB, float t)
    {
        float dt = 0.01f;
        float t2 = Mathf.Clamp01(t + dt);
        Vector3 p1 = GetArcPointAt(center, dirA, dirB, distA, distB, t);
        Vector3 p2 = GetArcPointAt(center, dirA, dirB, distA, distB, t2);
        Vector3 tangent = p2 - p1;
        if (tangent.sqrMagnitude < 0.0001f) return dirB;
        return tangent.normalized;
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

    System.Collections.IEnumerator AnimateLinesAppear(List<ArcLineData> arcDataList)
    {
        float elapsed = 0f;
        int segs = Mathf.Max(1, segmentCount);
        int totalPoints = segs + 1;
        while (elapsed < lineAppearDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lineAppearDuration);
            int showCount = 2 + (int)((totalPoints - 2) * progress);
            if (showCount > totalPoints) showCount = totalPoints;

            foreach (var data in arcDataList)
            {
                if (data.lr == null) continue;
                data.lr.positionCount = showCount;
                Vector3 prevPos = data.center;
                Vector3 endPos = data.center;
                for (int i = 0; i < showCount; i++)
                {
                    float t = i / (float)segs;
                    prevPos = endPos;
                    endPos = GetArcPointAt(data.center, data.dirA, data.dirB, data.distA, data.distB, t);
                    data.lr.SetPosition(i, endPos);
                }
                if (data.tipParticles != null)
                {
                    data.tipParticles.position = endPos;
                    Vector3 tangent = (endPos - prevPos).normalized;
                    if (tangent.sqrMagnitude >= 0.0001f)
                        data.tipParticles.rotation = Quaternion.LookRotation(tangent);
                }
            }
            yield return null;
        }

        foreach (var data in arcDataList)
        {
            if (data.lr == null) continue;
            data.lr.positionCount = totalPoints;
            Vector3 prevPos = data.center;
            Vector3 endPos = data.center;
            for (int i = 0; i < totalPoints; i++)
            {
                float t = i / (float)segs;
                prevPos = endPos;
                endPos = GetArcPointAt(data.center, data.dirA, data.dirB, data.distA, data.distB, t);
                data.lr.SetPosition(i, endPos);
            }
            if (data.tipParticles != null)
            {
                data.tipParticles.position = endPos;
                Vector3 tangent = GetArcTangentAt(data.center, data.dirA, data.dirB, data.distA, data.distB, 1f);
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

        Vector3 center = sphereCenter;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, 0.15f);

        List<Star> neighbors = GetNeighborList(0);
        if (neighbors == null) return;
        foreach (Star neighbor in neighbors)
        {
            if (neighbor == null) continue;

            float distA = Vector3.Distance(center, transform.position);
            float distB = Vector3.Distance(center, neighbor.transform.position);
            Vector3 dirA = (transform.position - center).normalized;
            Vector3 dirB = (neighbor.transform.position - center).normalized;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(center, transform.position);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(center, neighbor.transform.position);
            Gizmos.color = Color.white;
            int segs = Mathf.Max(4, segmentCount);
            for (int i = 0; i < segs; i++)
            {
                float t0 = i / (float)segs;
                float t1 = (i + 1) / (float)segs;
                Vector3 p0 = GetArcPointAt(center, dirA, dirB, distA, distB, t0);
                Vector3 p1 = GetArcPointAt(center, dirA, dirB, distA, distB, t1);
                Gizmos.DrawLine(p0, p1);
            }
        }
    }
#endif
}
