using UnityEngine;

/// <summary>音频管理单例，负责播放、停止等</summary>
public class AudioManager : MonoBehaviour
{
    static AudioManager _instance;

    [Header("背景音乐")]
    [SerializeField] AudioClip bgmClip;
    [Header("音效")]
    [Tooltip("音效整体音量 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] float soundEffectVolume = 1f;
    [SerializeField] AudioClip soundEffect1;
    [SerializeField] AudioClip soundEffect2;
    [SerializeField] AudioClip soundEffect3;

    AudioSource _bgmSource;
    AudioSource _sfxSource;

    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<AudioManager>();
                if (_instance == null)
                {
                    var go = new GameObject(nameof(AudioManager));
                    _instance = go.AddComponent<AudioManager>();
                }
            }
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.playOnAwake = false;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.loop = false;
        _sfxSource.playOnAwake = false;
    }

    void Start()
    {
        PlayBackgroundMusic();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>循环播放背景音乐</summary>
    public void PlayBackgroundMusic()
    {
        if (bgmClip == null) return;
        _bgmSource.clip = bgmClip;
        _bgmSource.Play();
    }

    /// <summary>停止背景音乐</summary>
    public void StopBackgroundMusic()
    {
        _bgmSource.Stop();
    }

    /// <summary>播放音效 1 一次</summary>
    public void PlaySoundEffect1()
    {
        if (soundEffect1 != null) _sfxSource.PlayOneShot(soundEffect1, soundEffectVolume);
    }

    /// <summary>播放音效 2 一次</summary>
    public void PlaySoundEffect2()
    {
        if (soundEffect2 != null) _sfxSource.PlayOneShot(soundEffect2, soundEffectVolume);
    }

    /// <summary>播放音效 3 一次</summary>
    public void PlaySoundEffect3()
    {
        if (soundEffect3 != null) _sfxSource.PlayOneShot(soundEffect3, soundEffectVolume);
    }
}
