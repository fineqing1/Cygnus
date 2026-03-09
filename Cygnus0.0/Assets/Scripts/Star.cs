using System.Collections.Generic;
using UnityEngine;

public class Star : MonoBehaviour
{
    /// <summary>相邻星体引用列表</summary>
    public List<Star> neighborstars = new List<Star>();

    [Header("弧线球面设置")]
    [Tooltip("不指定时使用父物体（oldparent）坐标作为球心")]
    public Transform sphereCenter;
    public float sphereRadius = 5f;
    public int segmentCount = 50;
    public float lineWidth = 0.2f;

    const string ArcLinesContainerName = "ArcLines";

    public void ConnectOtherStars()
    {
        // 清除旧的弧线
        Transform container = transform.Find(ArcLinesContainerName);
        if (container != null)
        {
            DestroyImmediate(container.gameObject);
        }

        GameObject containerGo = new GameObject(ArcLinesContainerName);
        containerGo.transform.SetParent(transform, worldPositionStays: false);

        Transform centerTransform = sphereCenter != null ? sphereCenter : transform.parent;
        Vector3 center = centerTransform != null ? centerTransform.position : Vector3.zero;

        foreach (Star neighbor in neighborstars)
        {
            if (neighbor == null) continue;

            GameObject lineGo = new GameObject($"ArcTo_{neighbor.name}");
            lineGo.transform.SetParent(containerGo.transform, worldPositionStays: false);

            LineRenderer lr = lineGo.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;

            Vector3 dirA = (transform.position - center).normalized;
            Vector3 dirB = (neighbor.transform.position - center).normalized;

            lr.positionCount = segmentCount + 1;
            for (int i = 0; i <= segmentCount; i++)
            {
                float t = i / (float)segmentCount;
                Vector3 dir = Vector3.Slerp(dirA, dirB, t);
                Vector3 pos = center + dir * sphereRadius;
                lr.SetPosition(i, pos);
            }
        }
    }
}
