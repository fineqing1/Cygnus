using UnityEngine;

/// <summary>
/// 在对准目标角度时首尾星的世界坐标处生成并持续更新两个红色球体（半径 0.1），用于提示玩家目标位置。
/// 需指定 StarsManager；球体在运行时创建。
/// </summary>
public class FirstLastStarHintSpheres : MonoBehaviour
{
    [Tooltip("提供当前目标与星星列表的 StarsManager")]
    public StarsManager starsManager;

    const float SphereSize = 0.1f;
    GameObject _firstSphere;
    GameObject _lastSphere;
    Material _redMaterial;

    void Start()
    {
        _redMaterial = CreateRedMaterial();
        _firstSphere = CreateHintSphere("FirstStarHint");
        _lastSphere = CreateHintSphere("LastStarHint");
    }

    void Update()
    {
        if (starsManager == null) return;
        if (!starsManager.GetAlignedFirstLastStarWorldPositions(out Vector3 firstWorld, out Vector3 lastWorld))
        {
            if (_firstSphere != null) _firstSphere.SetActive(false);
            if (_lastSphere != null) _lastSphere.SetActive(false);
            return;
        }
        if (_firstSphere != null)
        {
            _firstSphere.SetActive(true);
            _firstSphere.transform.position = firstWorld;
        }
        if (_lastSphere != null)
        {
            _lastSphere.SetActive(true);
            _lastSphere.transform.position = lastWorld;
        }
    }

    void OnDestroy()
    {
        if (_redMaterial != null) Destroy(_redMaterial);
        if (_firstSphere != null) Destroy(_firstSphere);
        if (_lastSphere != null) Destroy(_lastSphere);
    }

    static Material CreateRedMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Standard")
            ?? Shader.Find("Unlit/Color");
        if (shader == null) return null;
        Material mat = new Material(shader);
        mat.color = Color.red;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", Color.red);
        return mat;
    }

    GameObject CreateHintSphere(string name)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.localScale = Vector3.one * SphereSize;
        go.transform.SetParent(transform);
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null && _redMaterial != null)
            renderer.sharedMaterial = _redMaterial;
        if (go.TryGetComponent<Collider>(out var col))
            col.enabled = false;
        return go;
    }
}
