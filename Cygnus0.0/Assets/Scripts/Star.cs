using System.Collections.Generic;
using UnityEngine;

public class Star : MonoBehaviour
{
    /// <summary>相邻星体引用列表</summary>
    public List<Star> neighborstars = new List<Star>();

    [Header("弧线球面设置")]
    [Tooltip("球心世界坐标，可直接在 Inspector 输入")]
    public Vector3 sphereCenter;
    public int segmentCount = 50;
    public float lineWidth = 0.05f;

    [Header("线条外观")]
    [Tooltip("弧线使用的材质（需支持 LineRenderer/URP；不指定则线可能不可见）")]
    public Material lineMaterial;
    [Tooltip("弧线起点颜色")]
    public Color lineStartColor = Color.white;
    [Tooltip("弧线终点颜色（与起点不同时可形成渐变）")]
    public Color lineEndColor = Color.white;
    [Tooltip("弧线从起点到终点逐段出现的时间（秒）；0 表示立即显示")]
    [Min(0f)]
    public float lineAppearDuration = 0.5f;

    [Header("调试")]
    [Tooltip("开启后在 Console 输出画线详情")]
    public bool debugLog = false;
    [Tooltip("开启后在 Scene 视图绘制球心、半径与连线（仅编辑器）")]
    public bool debugGizmos = false;

    const string ArcLinesContainerName = "ArcLines";
    Coroutine _lineAppearCoroutine;

    /// <summary>当前正在播放“线条出现”动画的 Star 数量，用于禁用拖拽</summary>
    static int _lineAppearRunningCount;
    /// <summary>任意 Star 开始播放线条出现动画时（计数从 0 变为 1）</summary>
    public static System.Action LineAppearStarted;
    /// <summary>所有线条出现动画结束时（计数变为 0）</summary>
    public static System.Action LineAppearEnded;

    public void ConnectOtherStars()
    {
        Vector3 center = sphereCenter;

        if (debugLog)
        {
            int validCount = 0;
            foreach (Star n in neighborstars) if (n != null) validCount++;
            Debug.Log($"[Star.ConnectOtherStars] {name} | 球心={center} | 邻居数={neighborstars.Count} (有效={validCount}) | segmentCount={segmentCount}");
        }

        // 清除旧的弧线并停止未完成的出现动画
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
            lr.endWidth = lineWidth;
            if (lineMaterial != null)
                lr.material = lineMaterial;
            lr.startColor = lineStartColor;
            lr.endColor = lineEndColor;

            float distA = Vector3.Distance(center, transform.position);
            float distB = Vector3.Distance(center, neighbor.transform.position);
            float radius = Mathf.Max(distA, distB);

            if (debugLog)
            {
                Debug.Log($"[Star.ConnectOtherStars] {name} -> {neighbor.name} | 球心={center} | 本星距球心={distA:F3} | 邻居距球心={distB:F3} | 使用半径={radius:F3}");
            }

            Vector3 dirA = (transform.position - center).normalized;
            Vector3 dirB = (neighbor.transform.position - center).normalized;

            if (lineAppearDuration <= 0f)
            {
                lr.positionCount = segmentCount + 1;
                for (int i = 0; i <= segmentCount; i++)
                {
                    float t = i / (float)segmentCount;
                    Vector3 dir = Vector3.Slerp(dirA, dirB, t);
                    lr.SetPosition(i, center + dir * radius);
                }
            }
            else
            {
                lr.positionCount = 2;
                lr.SetPosition(0, center + dirA * radius);
                lr.SetPosition(1, center + dirB * radius);
                arcDataList.Add(new ArcLineData { lr = lr, center = center, dirA = dirA, dirB = dirB, radius = radius });
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
        public float radius;
    }

    System.Collections.IEnumerator AnimateLinesAppear(List<ArcLineData> arcDataList)
    {
        float elapsed = 0f;
        int totalPoints = segmentCount + 1;

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
                float denom = (showCount > 1) ? (showCount - 1) : 1f;
                for (int i = 0; i < showCount; i++)
                {
                    float t = i / denom;
                    Vector3 dir = Vector3.Slerp(data.dirA, data.dirB, t);
                    data.lr.SetPosition(i, data.center + dir * data.radius);
                }
            }
            yield return null;
        }

        foreach (var data in arcDataList)
        {
            if (data.lr == null) continue;
            data.lr.positionCount = totalPoints;
            for (int i = 0; i < totalPoints; i++)
            {
                float t = i / (float)(totalPoints - 1);
                Vector3 dir = Vector3.Slerp(data.dirA, data.dirB, t);
                data.lr.SetPosition(i, data.center + dir * data.radius);
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

        foreach (Star neighbor in neighborstars)
        {
            if (neighbor == null) continue;

            float distA = Vector3.Distance(center, transform.position);
            float distB = Vector3.Distance(center, neighbor.transform.position);
            float radius = Mathf.Max(distA, distB);

            Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
            Gizmos.DrawWireSphere(center, radius);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(center, transform.position);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(center, neighbor.transform.position);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, neighbor.transform.position);
        }
    }
#endif
}
