using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using DG.Tweening;
using TMPro;

public class ButtonAnim : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    [Header("呼吸动画设置")]
    [Tooltip("呼吸动画速率")]
    [SerializeField] private float breathingRate = 1.0f;
    [Tooltip("呼吸缩放比例")]
    [SerializeField] private float breathingScaleFactor = 1.02f;

    [Header("悬停动画设置")]
    [Tooltip("悬停颜色")]
    [SerializeField] private Color hoverColor = new Color(0.8f, 0.8f, 0.8f, 1.0f);
    [Tooltip("悬停缩放比例")]
    [SerializeField] private float hoverScaleFactor = 1.05f;

    [Header("点击动画设置")]
    [Tooltip("点击收缩比例")]
    [SerializeField] private float clickShrinkFactor = 0.9f;
    [Tooltip("点击过冲比例")]
    [SerializeField] private float clickOvershootFactor = 1.1f;
    [Tooltip("点击动画时长")]
    [SerializeField] private float clickDuration = 0.15f;

    [Header("组件引用")]
    [Tooltip("UI图片（用于UI按钮）")]
    [SerializeField] private Image targetImage;
    [Tooltip("渲染器（用于2D/3D物体）")]
    [SerializeField] private Renderer targetRenderer;
    [Tooltip("TextMeshPro文字组件")]
    [SerializeField] private TMP_Text targetText;

    [Header("泛光效果设置")]
    [Tooltip("是否启用泛光效果")]
    [SerializeField] private bool enableGlow = true;
    [Tooltip("泛光颜色")]
    [SerializeField] private Color glowColor = new Color(0f, 0.8f, 1f, 1f);
    [Tooltip("泛光内半径")]
    [SerializeField][Range(0f, 1f)] private float glowInner = 0f;
    [Tooltip("泛光外半径")]
    [SerializeField][Range(0f, 1f)] private float glowOuter = 0.5f;
    [Tooltip("泛光强度")]
    [SerializeField][Range(0f, 5f)] private float glowPower = 2.5f;
    [Tooltip("悬停时泛光强度倍数")]
    [SerializeField][Range(1f, 5f)] private float hoverGlowMultiplier = 2f;

    private Vector3 originalScale;
    private Color originalColor;
    private Coroutine breathingCoroutine;
    private Sequence currentSequence;
    private bool isUIElement;
    private Material glowMaterial;
    private float originalGlowPower;

    private static readonly int GlowColorID = Shader.PropertyToID("_GlowColor");
    private static readonly int GlowInnerID = Shader.PropertyToID("_GlowInner");
    private static readonly int GlowOuterID = Shader.PropertyToID("_GlowOuter");
    private static readonly int GlowPowerID = Shader.PropertyToID("_GlowPower");

    void Awake()
    {
        originalScale = transform.localScale;
        
        if (targetImage != null)
        {
            isUIElement = true;
            originalColor = targetImage.color;
        }
        else if (targetRenderer != null && targetRenderer.material != null)
        {
            isUIElement = false;
            originalColor = targetRenderer.material.color;
        }
        else
        {
            targetImage = GetComponent<Image>();
            if (targetImage != null)
            {
                isUIElement = true;
                originalColor = targetImage.color;
            }
            else
            {
                targetRenderer = GetComponent<Renderer>();
                if (targetRenderer != null && targetRenderer.material != null)
                {
                    isUIElement = false;
                    originalColor = targetRenderer.material.color;
                }
            }
        }

        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        InitializeGlowMaterial();
    }

    void InitializeGlowMaterial()
    {
        if (!enableGlow || targetText == null) return;
        CreateGlowMaterial();
    }

    void OnEnable()
    {
        if (gameObject.activeInHierarchy)
        {
            StopAllAnimations();
            ResetToOriginal();
            StartBreathing();
        }
    }

    void OnDisable()
    {
        StopAllAnimations();
    }

    void ResetToOriginal()
    {
        transform.localScale = originalScale;
        SetColor(originalColor);
        
        if (glowMaterial != null)
        {
            glowMaterial.SetFloat(GlowPowerID, originalGlowPower);
        }
    }

    void StartBreathing()
    {
        if (!gameObject.activeInHierarchy) return;

        if (breathingCoroutine != null)
        {
            StopCoroutine(breathingCoroutine);
            breathingCoroutine = null;
        }

        breathingCoroutine = StartCoroutine(BreathingEffect());
    }

    void StopAllAnimations()
    {
        if (currentSequence != null)
        {
            currentSequence.Kill();
            currentSequence = null;
        }

        if (breathingCoroutine != null)
        {
            StopCoroutine(breathingCoroutine);
            breathingCoroutine = null;
        }

        transform.DOKill();
        KillColorTween();
        
        if (glowMaterial != null)
        {
            DOTween.Kill(glowMaterial);
        }
    }

    void KillColorTween()
    {
        if (isUIElement && targetImage != null)
        {
            targetImage.DOKill();
        }
        else if (!isUIElement && targetRenderer != null && targetRenderer.material != null)
        {
            targetRenderer.material.DOKill();
        }
    }

    void SetColor(Color color)
    {
        if (isUIElement && targetImage != null)
        {
            targetImage.color = color;
        }
        else if (!isUIElement && targetRenderer != null && targetRenderer.material != null)
        {
            targetRenderer.material.color = color;
        }
    }

    Tweener DOColorTween(Color color, float duration)
    {
        if (isUIElement && targetImage != null)
        {
            return targetImage.DOColor(color, duration);
        }
        else if (!isUIElement && targetRenderer != null && targetRenderer.material != null)
        {
            return targetRenderer.material.DOColor(color, duration);
        }
        return null;
    }

    public void OnPointerEnter()
    {
        if (!gameObject.activeInHierarchy) return;

        StopAllAnimations();

        currentSequence = DOTween.Sequence();
        currentSequence.Append(transform.DOScale(originalScale * hoverScaleFactor, 0.2f).SetEase(Ease.OutQuad));
        
        var colorTween = DOColorTween(hoverColor, 0.2f);
        if (colorTween != null)
            currentSequence.Join(colorTween);

        if (glowMaterial != null)
        {
            float targetGlowPower = originalGlowPower * hoverGlowMultiplier;
            glowMaterial.DOFloat(targetGlowPower, GlowPowerID, 0.2f);
        }
    }

    public void OnPointerExit()
    {
        if (!gameObject.activeInHierarchy) return;

        StopAllAnimations();

        currentSequence = DOTween.Sequence();
        currentSequence.Append(transform.DOScale(originalScale, 0.2f).SetEase(Ease.OutQuad));
        
        var colorTween = DOColorTween(originalColor, 0.2f);
        if (colorTween != null)
        {
            currentSequence.Join(colorTween);
        }

        if (glowMaterial != null)
        {
            glowMaterial.DOFloat(originalGlowPower, GlowPowerID, 0.2f);
        }

        currentSequence.OnComplete(() =>
        {
            currentSequence = null;
            if (gameObject.activeInHierarchy)
                StartBreathing();
        });
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!gameObject.activeInHierarchy) return;

        StopAllAnimations();

        currentSequence = DOTween.Sequence();
        currentSequence.Append(transform.DOScale(originalScale * clickShrinkFactor, clickDuration * 0.5f).SetEase(Ease.OutQuad));
        currentSequence.Append(transform.DOScale(originalScale * clickOvershootFactor, clickDuration * 0.5f).SetEase(Ease.OutBack));
        currentSequence.Append(transform.DOScale(originalScale, clickDuration * 0.3f).SetEase(Ease.OutQuad));

        if (glowMaterial != null)
        {
            float targetGlowPower = originalGlowPower * hoverGlowMultiplier * 1.5f;
            glowMaterial.DOFloat(targetGlowPower, GlowPowerID, clickDuration * 0.5f);
            glowMaterial.DOFloat(originalGlowPower, GlowPowerID, clickDuration * 0.5f).SetDelay(clickDuration * 0.5f);
        }

        currentSequence.OnComplete(() =>
        {
            currentSequence = null;
            if (gameObject.activeInHierarchy)
                StartBreathing();
        });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnPointerEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnPointerExit();
    }

    IEnumerator BreathingEffect()
    {
        while (gameObject.activeInHierarchy)
        {
            yield return new WaitWhile(() => currentSequence != null && currentSequence.IsPlaying());

            if (!gameObject.activeInHierarchy) yield break;

            transform.DOScale(originalScale * breathingScaleFactor, breathingRate).SetEase(Ease.InOutSine);
            yield return new WaitForSeconds(breathingRate);

            yield return new WaitWhile(() => currentSequence != null && currentSequence.IsPlaying());

            if (!gameObject.activeInHierarchy) yield break;

            transform.DOScale(originalScale, breathingRate).SetEase(Ease.InOutSine);
            yield return new WaitForSeconds(breathingRate);
        }
    }

    public void SetGlowColor(Color color)
    {
        glowColor = color;
        if (glowMaterial != null)
        {
            glowMaterial.SetColor(GlowColorID, glowColor);
        }
    }

    public void SetGlowInner(float value)
    {
        glowInner = Mathf.Clamp01(value);
        if (glowMaterial != null)
        {
            glowMaterial.SetFloat(GlowInnerID, glowInner);
        }
    }

    public void SetGlowOuter(float value)
    {
        glowOuter = Mathf.Clamp01(value);
        if (glowMaterial != null)
        {
            glowMaterial.SetFloat(GlowOuterID, glowOuter);
        }
    }

    public void SetGlowPower(float value)
    {
        glowPower = Mathf.Clamp(value, 0f, 5f);
        originalGlowPower = glowPower;
        if (glowMaterial != null)
        {
            glowMaterial.SetFloat(GlowPowerID, glowPower);
        }
    }

    public void SetEnableGlow(bool enable)
    {
        enableGlow = enable;
        if (enable && targetText != null && glowMaterial == null)
        {
            InitializeGlowMaterial();
        }
        else if (!enable && glowMaterial != null)
        {
            if (targetText != null && targetText.fontMaterial == glowMaterial)
            {
                targetText.fontMaterial = targetText.fontSharedMaterial;
            }
            Destroy(glowMaterial);
            glowMaterial = null;
        }
    }

    void Reset()
    {
        targetText = GetComponent<TMP_Text>();
    }

    void OnValidate()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
        }

        if (!enableGlow)
        {
            if (glowMaterial != null)
            {
                if (targetText != null && targetText.fontMaterial == glowMaterial)
                {
                    targetText.fontMaterial = targetText.fontSharedMaterial;
                }
                DestroyImmediate(glowMaterial);
                glowMaterial = null;
            }
            return;
        }

        if (enableGlow && targetText != null && glowMaterial == null)
        {
            CreateGlowMaterial();
        }

        if (glowMaterial != null)
        {
            glowMaterial.SetColor(GlowColorID, glowColor);
            glowMaterial.SetFloat(GlowInnerID, glowInner);
            glowMaterial.SetFloat(GlowOuterID, glowOuter);
            glowMaterial.SetFloat(GlowPowerID, glowPower);
            originalGlowPower = glowPower;
        }
    }

    void CreateGlowMaterial()
    {
        if (targetText == null) return;

        glowMaterial = new Material(Shader.Find("TextMeshPro/Distance Field"));
        glowMaterial.EnableKeyword("GLOW_ON");
        
        glowMaterial.SetColor(GlowColorID, glowColor);
        glowMaterial.SetFloat(GlowInnerID, glowInner);
        glowMaterial.SetFloat(GlowOuterID, glowOuter);
        glowMaterial.SetFloat(GlowPowerID, glowPower);
        
        if (targetText.fontSharedMaterial != null)
        {
            glowMaterial.SetTexture("_MainTex", targetText.fontSharedMaterial.GetTexture("_MainTex"));
        }
        
        targetText.fontMaterial = glowMaterial;
        originalGlowPower = glowPower;
    }

    void OnDestroy()
    {
        if (glowMaterial != null)
        {
            Destroy(glowMaterial);
        }
    }
}
