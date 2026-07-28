using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景 BGM 播放与淡入淡出切换。
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public sealed class BgmManager : MonoBehaviour
{
    public enum BgmTrack
    {
        None,
        Title,
        Town,
        Forest,
        Battle1,
        Battle2
    }

    private const string ClipSetResourcePath = "BgmClipSet";
    private const string MainMenuSceneName = "MainMenuScene";
    private const string GameplaySceneName = "SampleScene";

    public static BgmManager Instance { get; private set; }

    [SerializeField] private float crossfadeSeconds = 0.75f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;

    private AudioSource activeSource;
    private AudioSource fadeSource;
    private BgmClipSet clipSet;
    private Coroutine fadeRoutine;
    private BgmTrack currentTrack = BgmTrack.None;

    private static readonly HashSet<int> GoblinsInCombat = new HashSet<int>();
    private static readonly HashSet<int> DragonsInCombat = new HashSet<int>();
    private static int villageZoneCount;
    private static int forestZoneCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureExists()
    {
        if (Instance != null)
            return;

        var host = new GameObject(nameof(BgmManager));
        host.AddComponent<BgmManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        activeSource = CreateSource("BgmActive");
        fadeSource = CreateSource("BgmFade");

        LoadClipSet();
        EnsureAudioListener();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        HandleSceneBgm(SceneManager.GetActiveScene());
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (Instance == this)
            Instance = null;
    }

    private void LoadClipSet()
    {
        clipSet = Resources.Load<BgmClipSet>(ClipSetResourcePath);
        if (clipSet == null)
            clipSet = ScriptableObject.CreateInstance<BgmClipSet>();

        if (clipSet.title == null)
            clipSet.title = Resources.Load<AudioClip>("BGM/Title");

        if (clipSet.town == null)
            clipSet.town = Resources.Load<AudioClip>("BGM/Town");

        if (clipSet.forest == null)
            clipSet.forest = Resources.Load<AudioClip>("BGM/Forest");

        if (clipSet.battle1 == null)
            clipSet.battle1 = Resources.Load<AudioClip>("BGM/Battle1");

        if (clipSet.battle2 == null)
            clipSet.battle2 = Resources.Load<AudioClip>("BGM/Battle2");

        if (clipSet.title == null || clipSet.town == null || clipSet.forest == null || clipSet.battle1 == null || clipSet.battle2 == null)
            Debug.LogWarning("BgmManager: BGM clips missing. Expected Resources/BGM/Title, Town, Forest, Battle1, Battle2.", this);
    }

    private void EnsureAudioListener()
    {
        if (FindObjectOfType<AudioListener>() != null)
            return;

        gameObject.AddComponent<AudioListener>();
    }

    private static AudioSource CreateSource(string sourceName)
    {
        var sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(Instance.transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 0;
        return source;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HandleSceneBgm(scene);
    }

    private void HandleSceneBgm(Scene scene)
    {
        if (!scene.IsValid())
            return;

        EnsureAudioListener();

        if (scene.name == MainMenuSceneName)
        {
            ClearCombatTracking();
            villageZoneCount = 0;
            forestZoneCount = 0;
            Play(BgmTrack.Title);
            return;
        }

        if (scene.name == GameplaySceneName)
        {
            if (currentTrack == BgmTrack.Title)
                StopImmediate();

            StartCoroutine(RefreshGameplayMusicNextFrame());
        }
    }

    private IEnumerator RefreshGameplayMusicNextFrame()
    {
        yield return null;
        RefreshGameplayMusic();
    }

    public void Play(BgmTrack track)
    {
        if (track == currentTrack && track != BgmTrack.None && activeSource != null && activeSource.isPlaying)
            return;

        AudioClip clip = ResolveClip(track);
        if (track != BgmTrack.None && clip == null)
        {
            Debug.LogWarning($"BgmManager: missing clip for {track}.", this);
            return;
        }

        currentTrack = track;

        if (track == BgmTrack.None)
        {
            StopImmediate();
            return;
        }

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(CrossfadeTo(clip));
    }

    public void StopImmediate()
    {
        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        currentTrack = BgmTrack.None;

        if (activeSource != null)
        {
            activeSource.Stop();
            activeSource.clip = null;
            activeSource.volume = 0f;
        }

        if (fadeSource != null)
        {
            fadeSource.Stop();
            fadeSource.clip = null;
            fadeSource.volume = 0f;
        }
    }

    public static void NotifyGoblinEngaged(int instanceId)
    {
        if (instanceId == 0)
            return;

        if (GoblinsInCombat.Add(instanceId))
            RefreshGameplayMusic();
    }

    public static void NotifyGoblinDisengaged(int instanceId)
    {
        if (instanceId == 0)
            return;

        if (GoblinsInCombat.Remove(instanceId))
            RefreshGameplayMusic();
    }

    public static void NotifyDragonEngaged(int instanceId)
    {
        if (instanceId == 0)
            return;

        if (DragonsInCombat.Add(instanceId))
            RefreshGameplayMusic();
    }

    public static void NotifyDragonDisengaged(int instanceId)
    {
        if (instanceId == 0)
            return;

        if (DragonsInCombat.Remove(instanceId))
            RefreshGameplayMusic();
    }

    public static void ClearCombatTracking()
    {
        GoblinsInCombat.Clear();
        DragonsInCombat.Clear();
    }

    public static void NotifyAreaEntered(AreaBgmZone.AreaKind kind)
    {
        switch (kind)
        {
            case AreaBgmZone.AreaKind.Village:
                villageZoneCount++;
                break;
            case AreaBgmZone.AreaKind.Forest:
                forestZoneCount++;
                break;
        }

        RefreshGameplayMusic();
    }

    public static void NotifyAreaExited(AreaBgmZone.AreaKind kind)
    {
        switch (kind)
        {
            case AreaBgmZone.AreaKind.Village:
                villageZoneCount = Mathf.Max(0, villageZoneCount - 1);
                break;
            case AreaBgmZone.AreaKind.Forest:
                forestZoneCount = Mathf.Max(0, forestZoneCount - 1);
                break;
        }

        RefreshGameplayMusic();
    }

    public static void NotifyVillageEntered()
    {
        NotifyAreaEntered(AreaBgmZone.AreaKind.Village);
    }

    public static void NotifyVillageExited()
    {
        NotifyAreaExited(AreaBgmZone.AreaKind.Village);
    }

    public static void RefreshGameplayMusic()
    {
        BgmManager manager = Instance;
        if (manager == null)
            return;

        if (DragonsInCombat.Count > 0)
            manager.Play(BgmTrack.Battle2);
        else if (GoblinsInCombat.Count > 0)
            manager.Play(BgmTrack.Battle1);
        else if (villageZoneCount > 0)
            manager.Play(BgmTrack.Town);
        else if (forestZoneCount > 0)
            manager.Play(BgmTrack.Forest);
        else
            manager.StopImmediate();
    }

    private AudioClip ResolveClip(BgmTrack track)
    {
        if (clipSet == null)
            return null;

        switch (track)
        {
            case BgmTrack.Title:
                return clipSet.title;
            case BgmTrack.Town:
                return clipSet.town;
            case BgmTrack.Forest:
                return clipSet.forest;
            case BgmTrack.Battle1:
                return clipSet.battle1;
            case BgmTrack.Battle2:
                return clipSet.battle2;
            default:
                return null;
        }
    }

    private float GetEffectiveVolume()
    {
        return volume * GameSettings.MasterVolume;
    }

    private IEnumerator CrossfadeTo(AudioClip nextClip)
    {
        float duration = Mathf.Max(0.01f, crossfadeSeconds);
        float elapsed = 0f;
        float targetVolume = GetEffectiveVolume();

        fadeSource.clip = nextClip;
        fadeSource.volume = 0f;
        fadeSource.Play();

        float fromVolume = activeSource.isPlaying ? activeSource.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            fadeSource.volume = Mathf.Lerp(0f, targetVolume, t);
            activeSource.volume = Mathf.Lerp(fromVolume, 0f, t);
            yield return null;
        }

        activeSource.Stop();
        activeSource.clip = fadeSource.clip;
        activeSource.volume = fadeSource.volume;
        activeSource.Play();

        fadeSource.Stop();
        fadeSource.clip = null;
        fadeSource.volume = 0f;

        fadeRoutine = null;
    }
}
