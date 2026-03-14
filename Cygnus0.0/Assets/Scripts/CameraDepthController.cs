using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraDepthController : MonoBehaviour
{
    [Header("目标摄像机")]
    [Tooltip("要控制的摄像机，为空则使用主摄像机")]
    public Camera targetCamera;

    [Header("景深控制方式")]
    public DepthControlMode controlMode = DepthControlMode.FOV;

    [Header("FOV设置")]
    [Tooltip("最小FOV值")]
    [Range(10f, 90f)]
    public float minFOV = 30f;

    [Tooltip("最大FOV值")]
    [Range(30f, 120f)]
    public float maxFOV = 90f;

    [Tooltip("默认FOV值")]
    [Range(10f, 120f)]
    public float defaultFOV = 60f;

    [Header("距离设置")]
    [Tooltip("最小距离")]
    public float minDistance = 2f;

    [Tooltip("最大距离")]
    public float maxDistance = 20f;

    [Tooltip("默认距离")]
    public float defaultDistance = 10f;

    [Header("滚轮灵敏度")]
    [Tooltip("滚轮滚动灵敏度")]
    [Range(0.1f, 5f)]
    public float scrollSensitivity = 1f;

    [Header("加速度")]
    [Tooltip("速度累积加速度，数值越大加速越快")]
    [Range(5f, 100f)]
    public float acceleration = 30f;

    [Header("惯性阻尼")]
    [Tooltip("松手后惯性衰减速度，数值越小惯性持续越久")]
    [Range(0.1f, 10f)]
    public float damping = 3f;

    [Header("最大速度")]
    [Tooltip("最大变化速度，防止变化过快")]
    [Range(10f, 200f)]
    public float maxSpeed = 80f;

    [Header("最小惯性阈值")]
    [Tooltip("速度低于此值时停止惯性")]
    [Range(0.1f, 5f)]
    public float minInertiaThreshold = 0.5f;

    float currentValue;
    float currentVelocity;
    bool isScrolling;
    bool isInitialized;

    public enum DepthControlMode
    {
        FOV,
        Distance
    }

    void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera != null)
        {
            if (controlMode == DepthControlMode.FOV)
            {
                currentValue = targetCamera.fieldOfView;
            }
            else
            {
                currentValue = -transform.localPosition.z;
            }
            isInitialized = true;
        }
    }

    void Update()
    {
        if (!isInitialized || targetCamera == null) return;

        HandleInput();
        ApplyDamping();
        ApplyValue();
    }

    void HandleInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) > 0.001f)
        {
            isScrolling = true;

            float targetVelocity = scroll * scrollSensitivity * 500f;

            currentVelocity = Mathf.Lerp(currentVelocity, targetVelocity, acceleration * Time.deltaTime);

            currentVelocity = Mathf.Clamp(currentVelocity, -maxSpeed, maxSpeed);
        }
        else
        {
            isScrolling = false;
        }
    }

    void ApplyDamping()
    {
        if (!isScrolling)
        {
            if (Mathf.Abs(currentVelocity) > minInertiaThreshold)
            {
                currentVelocity = Mathf.Lerp(currentVelocity, 0f, damping * Time.deltaTime);
            }
            else
            {
                currentVelocity = 0f;
            }
        }
    }

    void ApplyValue()
    {
        float delta = currentVelocity * Time.deltaTime;

        if (controlMode == DepthControlMode.FOV)
        {
            currentValue -= delta;
            currentValue = Mathf.Clamp(currentValue, minFOV, maxFOV);

            if (currentValue <= minFOV && currentVelocity < 0)
            {
                currentVelocity = 0f;
            }
            else if (currentValue >= maxFOV && currentVelocity > 0)
            {
                currentVelocity = 0f;
            }

            targetCamera.fieldOfView = currentValue;
        }
        else
        {
            currentValue += delta;
            currentValue = Mathf.Clamp(currentValue, minDistance, maxDistance);

            if (currentValue <= minDistance && currentVelocity > 0)
            {
                currentVelocity = 0f;
            }
            else if (currentValue >= maxDistance && currentVelocity < 0)
            {
                currentVelocity = 0f;
            }

            transform.localPosition = new Vector3(
                transform.localPosition.x,
                transform.localPosition.y,
                -currentValue
            );
        }
    }

    public void ResetToDefault()
    {
        if (controlMode == DepthControlMode.FOV)
        {
            currentValue = defaultFOV;
            targetCamera.fieldOfView = currentValue;
        }
        else
        {
            currentValue = defaultDistance;
            transform.localPosition = new Vector3(
                transform.localPosition.x,
                transform.localPosition.y,
                -currentValue
            );
        }
        currentVelocity = 0f;
    }

    public float GetCurrentValue()
    {
        return currentValue;
    }

    public float GetVelocity()
    {
        return currentVelocity;
    }

    public bool IsScrolling => isScrolling;
}
