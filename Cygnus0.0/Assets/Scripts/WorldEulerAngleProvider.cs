using UnityEngine;

/// <summary>用自身在世界坐标下的欧拉角（面向角度）更新 RotationController 的 Currentangle，不使用四元数。</summary>
public class WorldEulerAngleProvider : MonoBehaviour
{
    [Tooltip("将本物体世界欧拉角写入其 Currentangle；不填则从本物体获取")]
    [SerializeField] RotationContorller rotationController;

    void Awake()
    {
        if (rotationController == null)
            rotationController = GetComponent<RotationContorller>();
    }

    void Update()
    {
        if (rotationController == null) return;
        rotationController.Currentangle = transform.eulerAngles;
    }
}
