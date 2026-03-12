using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class RotationContorller : MonoBehaviour
{
    public bool isdragable;//后续用if判断
    bool isDown=false;
    public float rotateSpeed=10;
    Transform tempParent;//临时父对象
    Transform oldparent;//原始父对象

    /// <summary>当前面向角度（世界坐标欧拉角，度），由 WorldEulerAngleProvider 等外部脚本写入</summary>
    public Vector3 Currentangle;

    /// <summary>松手时触发（仅此时可据此判断是否对准并绘制星体连线）</summary>
    public System.Action onRotationEnd;
    /// <summary>开始拖拽时触发（如用于清除线条）</summary>
    public System.Action onRotationStart;

    /// <summary>是否正在拖拽，供 StarsManager 判断“松手且对准”时画线</summary>
    public bool IsDragging => isDown;

    /// <summary>为 true 时忽略拖拽输入（如线条出现动画期间）</summary>
    public bool InputBlocked { get; set; }

    private void Awake()
    {
        tempParent = new GameObject("TempParent").transform;
    }

    void Start()
    {
        oldparent = transform.parent;
        tempParent.position = oldparent == null ? transform.position : oldparent.position;
        tempParent.rotation = Quaternion.identity;
    }

    // Update is called once per frame
    void Update()
    {
        if (InputBlocked) return;
        if (isdragable)
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDown = true;
                transform.parent = tempParent;
                onRotationStart?.Invoke();
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDown = false;
                transform.parent = oldparent;
                tempParent.rotation = Quaternion.identity;
                // 松手瞬间用当前世界欧拉角更新，确保 OnRotationEnd 里读到正确值（不依赖 Update 顺序）
                Currentangle = transform.eulerAngles;
                onRotationEnd?.Invoke();
            }

            if (isDown)
            {
                float mx = Input.GetAxis("Mouse X");
                float my = Input.GetAxis("Mouse Y");
                Quaternion qx = Quaternion.AngleAxis(-mx * rotateSpeed, Vector3.up);
                Quaternion qy = Quaternion.AngleAxis(my * rotateSpeed, Vector3.right);
                tempParent.rotation = tempParent.rotation * qx * qy;
            }
        }
    }

    void OnGUI()
    {
        Rect rect = new Rect(10, 10, 500, 36);
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 24 };
        GUI.Label(rect, $"Currentangle: ({Currentangle.x:F1}, {Currentangle.y:F1}, {Currentangle.z:F1})", style);
    }
}
