using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RotationContorller : MonoBehaviour
{
    [Header("要旋转的目标")]
    [Tooltip("要旋转的子物体，为空则旋转第一个子物体")]
    public Transform targetToRotate;

    [Header("是否可拖拽")]
    public bool isdragable = true;

    [Header("拖拽灵敏度")]
    [Tooltip("拖拽时的响应灵敏度，数值越大转得越快")]
    [Range(0.1f, 5f)]
    public float dragSensitivity = 1f;

    [Header("加速度")]
    [Tooltip("拖拽时速度累积的加速度，数值越大加速越快")]
    [Range(1f, 50f)]
    public float acceleration = 20f;

    [Header("惯性阻尼")]
    [Tooltip("松手后惯性衰减速度，数值越小惯性持续越久")]
    [Range(0.1f, 10f)]
    public float damping = 2f;

    [Header("最大速度")]
    [Tooltip("旋转速度上限，防止旋转过快")]
    [Range(50f, 500f)]
    public float maxSpeed = 200f;

    [Header("最小惯性阈值")]
    [Tooltip("速度低于此值时停止惯性旋转")]
    [Range(0.1f, 5f)]
    public float minInertiaThreshold = 0.5f;

    bool isDown = false;

    Vector2 rotationVelocity;
    Vector2 currentVelocity;

    public Vector3 Currentangle;

    public System.Action onRotationEnd;
    public System.Action onRotationStart;

    public bool IsDragging => isDown;

    public bool InputBlocked { get; set; }

    void Start()
    {
        if (targetToRotate == null && transform.childCount > 0)
        {
            targetToRotate = transform.GetChild(0);
        }

        rotationVelocity = Vector2.zero;
        currentVelocity = Vector2.zero;
    }

    void Update()
    {
        if (InputBlocked || targetToRotate == null) return;
        
        if (isdragable)
        {
            HandleInput();
            ApplyRotation();
        }
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDown = true;
            currentVelocity = Vector2.zero;
            onRotationStart?.Invoke();
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDown = false;
            rotationVelocity = currentVelocity;
            onRotationEnd?.Invoke();
        }

        if (isDown)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");

            Vector2 targetVelocity = new Vector2(-mx, my) * dragSensitivity * 100f;

            currentVelocity = Vector2.Lerp(currentVelocity, targetVelocity, acceleration * Time.deltaTime);

            float speed = currentVelocity.magnitude;
            if (speed > maxSpeed)
            {
                currentVelocity = currentVelocity.normalized * maxSpeed;
            }
        }
        else
        {
            if (rotationVelocity.magnitude > minInertiaThreshold)
            {
                rotationVelocity = Vector2.Lerp(rotationVelocity, Vector2.zero, damping * Time.deltaTime);
            }
            else
            {
                rotationVelocity = Vector2.zero;
            }
        }
    }

    void ApplyRotation()
    {
        Vector2 velocityToApply = isDown ? currentVelocity : rotationVelocity;

        if (velocityToApply.magnitude > 0.01f)
        {
            float vx = velocityToApply.x * Time.deltaTime;
            float vy = velocityToApply.y * Time.deltaTime;

            targetToRotate.Rotate(Vector3.up, vx, Space.World);
            targetToRotate.Rotate(Vector3.right, vy, Space.World);

            Currentangle = targetToRotate.eulerAngles;
        }
    }

#if UNITY_EDITOR
    void OnGUI()
    {
        if (targetToRotate == null) return;
        Rect rect = new Rect(10, 10, 500, 36);
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 24 };
        GUI.Label(rect, $"Currentangle: ({Currentangle.x:F1}, {Currentangle.y:F1}, {Currentangle.z:F1})", style);
    }
#endif
}
