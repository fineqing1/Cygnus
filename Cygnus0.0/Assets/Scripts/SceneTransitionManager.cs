using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 场景渐变过渡：先变暗再加载场景，加载完成后渐亮，便于留出加载时间。
/// 自动创建单例，跨场景保留（DontDestroyOnLoad）。
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    static SceneTransitionManager _instance;
    public static SceneTransitionManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SceneTransitionManager");
                _instance = go.AddComponent<SceneTransitionManager>();
            }
            return _instance;
        }
    }

    [Header("过渡时长（秒）")]
    [Tooltip("变暗持续时间")]
    public float fadeOutDuration = 0.5f;
    [Tooltip("变亮持续时间")]
    public float fadeInDuration = 0.5f;
    [Tooltip("遮罩颜色（通常黑色）")]
    public Color overlayColor = Color.black;

    Canvas _canvas;
    Image _overlayImage;
    bool _isTransitioning;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
    }

    void BuildOverlay()
    {
        if (_canvas != null) return;

        var go = new GameObject("TransitionOverlay");
        go.transform.SetParent(transform);

        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 32767;
        _canvas.overrideSorting = true;
        go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10;
        go.AddComponent<GraphicRaycaster>();

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var imgGo = new GameObject("Image");
        imgGo.transform.SetParent(go.transform, false);
        _overlayImage = imgGo.AddComponent<Image>();
        _overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);
        _overlayImage.raycastTarget = false;

        var imgRt = imgGo.GetComponent<RectTransform>();
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.sizeDelta = Vector2.zero;
        imgRt.anchoredPosition = Vector2.zero;

        _canvas.enabled = false;
    }

    /// <summary>
    /// 使用渐变切换场景（先变暗 → 加载 → 变亮）
    /// </summary>
    public void LoadSceneWithFade(string sceneName, float? fadeOut = null, float? fadeIn = null)
    {
        if (_isTransitioning)
            return;
        float outDur = fadeOut ?? fadeOutDuration;
        float inDur = fadeIn ?? fadeInDuration;
        StartCoroutine(TransitionRoutine(sceneName, outDur, inDur));
    }

    IEnumerator TransitionRoutine(string sceneName, float outDur, float inDur)
    {
        _isTransitioning = true;
        if (_canvas != null) _canvas.enabled = true;

        // 渐暗
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / outDur;
            if (_overlayImage != null)
                _overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, Mathf.Clamp01(t));
            yield return null;
        }
        if (_overlayImage != null)
            _overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 1f);

        yield return null;
        SceneManager.LoadScene(sceneName);
        yield return null;

        // 渐亮
        t = 1f;
        while (t > 0f)
        {
            t -= Time.deltaTime / inDur;
            if (_overlayImage != null)
                _overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, Mathf.Clamp01(t));
            yield return null;
        }
        if (_overlayImage != null)
            _overlayImage.color = new Color(overlayColor.r, overlayColor.g, overlayColor.b, 0f);

        if (_canvas != null) _canvas.enabled = false;
        _isTransitioning = false;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
