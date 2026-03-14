using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 挂在 UI 元素上：鼠标点在此物体 RectTransform 区域内即跳转到指定场景。
/// 在 Inspector 中通过 RectTransform 自由调整位置和大小即可定义点击区域。
/// 需要同物体上有 Image（可设为透明），并勾选 Raycast Target。
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class ClickAreaSceneLoader : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("点击后要加载的场景名称（需已加入 Build Settings）")]
    public string sceneName = "Bigball";

    [Header("渐变过渡（留出加载时间）")]
    [Tooltip("变暗时长（秒）")]
    public float fadeOutDuration = 0.5f;
    [Tooltip("变亮时长（秒）")]
    public float fadeInDuration = 0.5f;

    public void OnPointerClick(PointerEventData eventData)
    {
        AudioManager.Instance?.PlaySoundEffect1();
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("ClickAreaSceneLoader: sceneName 未设置。");
            return;
        }
        SceneTransitionManager.Instance.LoadSceneWithFade(sceneName, fadeOutDuration, fadeInDuration);
    }
}
