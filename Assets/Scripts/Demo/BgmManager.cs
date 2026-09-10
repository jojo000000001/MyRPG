using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 跨场景 BGM 播放与淡入淡出切换。
/// 由 Resources/Systems/BgmManager.prefab 实例化，AudioSource 在 Prefab 上配置。
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

    private const string PrefabResourcePath = "Systems/BgmManager";
    private const string ClipSetResourcePath = "BgmClipSet";
    private const string MainMenuSceneName = "MainMenuScene";
    private const string GameplaySceneName = "SampleScene";

    public static BgmManager Instance { get; private set; }

    [SerializeField] private float crossfadeSeconds = 0.75f;
    [SerializeField] private float goblinBattleEndDelaySeconds = 3f;
    [SerializeField] private float goblinBattleProximityRadius = 12f;
    [SerializeField] private float goblinForestReleaseRadius = 18f;
    [SerializeField] private float goblinProximityCheckInterval = 0.2f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;
    [SerializeField] private BgmClipSet clipSet;
    [SerializeField] private AudioSource activeSource;
    [SerializeField] private AudioSource fadeSource;

    private Coroutine fadeRoutine;
    private Coroutine preloadRoutine;
    private Coroutine goblinBattleEndRoutine;
    private AudioSource warmupSource;
    private float goblinBattleHoldUntil = -1f;
    private float nextGoblinProximityCheckTime;
    private bool goblinProximityBattle;
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

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"BgmManager: missing prefab at Resources/{PrefabResourcePath}.prefab. Run Tools/MyRPG/Setup BGM Manager Prefab.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            StopOwnedSources();
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!ValidateSources())
            return;

        ConfigureSource(activeSource);
        ConfigureSource(fadeSource);
        EnsureWarmupSource();
        ApplyMasterVolume();

        LoadClipSet();
        preloadRoutine = StartCoroutine(PreloadAllClips());
        EnsureAudioListener();

        GameSettings.Changed -= ApplyMasterVolume;
        GameSettings.Changed += ApplyMasterVolume;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        HandleSceneBgm(SceneManager.GetActiveScene());
    }

    private void Update()
    {
        EnsureCurrentTrackKeepsPlaying();
        TickGoblinProximityMusic();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        GameSettings.Changed -= ApplyMasterVolume;

        if (Instance == this)
            Instance = null;

        CancelGoblinBattleEndDelay();
    }

    private bool ValidateSources()
    {
        if (activeSource != null && fadeSource != null)
            return true;

        Debug.LogError("BgmManager: assign activeSource and fadeSource on the prefab.", this);
        enabled = false;
        return false;
    }

    private static void ConfigureSource(AudioSource source)
    {
        if (source == null)
            return;

        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.ignoreListenerVolume = true;
    }

    private void EnsureWarmupSource()
    {
        if (warmupSource != null)
            return;

        GameObject warmupObject = new GameObject("BgmWarmup");
        warmupObject.transform.SetParent(transform, false);
        warmupSource = warmupObject.AddComponent<AudioSource>();
        ConfigureSource(warmupSource);
        warmupSource.loop = false;
        warmupSource.volume = 0f;
        warmupSource.priority = 255;
    }

    private void EnsureCurrentTrackKeepsPlaying()
    {
        if (currentTrack == BgmTrack.None || fadeRoutine != null)
            return;

        if (activeSource == null)
            return;

        activeSource.loop = true;

        if (activeSource.isPlaying)
            return;

        AudioClip clip = activeSource.clip != null ? activeSource.clip : ResolveClip(currentTrack);
        if (clip == null)
            return;

        activeSource.clip = clip;
        activeSource.volume = GetEffectiveVolume();
        activeSource.Play();
    }

    private void LoadClipSet()
    {
        if (clipSet == null)
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
            ClearCombatTracking();
            villageZoneCount = 0;
            forestZoneCount = 0;

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
        if (track == currentTrack)
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
        StopOwnedSources();
    }

    private void StopOwnedSources()
    {
        StopSource(activeSource);
        StopSource(fadeSource);
    }

    private static void StopSource(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.volume = 0f;
    }

    public static bool IsEnemyCombatActive => GoblinsInCombat.Count > 0 || DragonsInCombat.Count > 0;

    public static void NotifyGoblinEngaged(int instanceId)
    {
        if (instanceId == 0)
            return;

        if (GoblinsInCombat.Add(instanceId))
        {
            Instance?.CancelGoblinBattleEndDelay();
            Instance?.EnsureCombatClipsReady();
            RefreshGameplayMusic();
        }
    }

    public static void NotifyGoblinDisengaged(int instanceId)
    {
        if (instanceId == 0)
            return;

        if (GoblinsInCombat.Remove(instanceId))
        {
            Instance?.CancelGoblinBattleEndDelay();
            Instance?.UpdateGoblinProximityState();
            RefreshGameplayMusic();
        }
    }

    public static void NotifyGoblinDied(int instanceId)
    {
        if (instanceId == 0)
            return;

        GoblinsInCombat.Remove(instanceId);

        BgmManager manager = Instance;
        if (manager == null)
        {
            RefreshGameplayMusic();
            return;
        }

        manager.OnGoblinDied();
    }

    public static void NotifyDragonEngaged(int instanceId)
    {
        if (instanceId == 0)
            return;

        if (DragonsInCombat.Add(instanceId))
        {
            Instance?.EnsureCombatClipsReady();
            RefreshGameplayMusic();
        }
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
        BgmManager manager = Instance;
        if (manager == null)
            return;

        manager.goblinProximityBattle = false;
        manager.CancelGoblinBattleEndDelay();
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
                Instance?.EnsureCombatClipsReady();
                break;
        }

        Instance?.UpdateGoblinProximityState();
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

        Instance?.UpdateGoblinProximityState();
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

    public static void NotifyForestClearQuestChanged()
    {
        Instance?.EnsureCombatClipsReady();
        Instance?.UpdateGoblinProximityState();
        RefreshGameplayMusic();
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
        else if (manager.IsHoldingGoblinBattle())
            return;
        else if (manager.goblinProximityBattle)
            manager.Play(BgmTrack.Battle1);
        else if (villageZoneCount > 0)
            manager.Play(BgmTrack.Town);
        else if (forestZoneCount > 0)
            manager.Play(BgmTrack.Forest);
        else
            manager.StopImmediate();
    }

    private void TickGoblinProximityMusic()
    {
        if (Time.time < nextGoblinProximityCheckTime)
            return;

        nextGoblinProximityCheckTime = Time.time + Mathf.Max(0.05f, goblinProximityCheckInterval);
        bool wasNear = goblinProximityBattle;
        UpdateGoblinProximityState();
        if (goblinProximityBattle != wasNear)
            RefreshGameplayMusic();
    }

    private void UpdateGoblinProximityState()
    {
        goblinProximityBattle = ShouldKeepForestBattleByGoblinDistance();
    }

    private bool ShouldKeepForestBattleByGoblinDistance()
    {
        if (forestZoneCount <= 0)
            return false;

        Player player = Player.Resolve();
        if (player == null || player.IsDead)
            return false;

        float nearest = GetNearestLivingGoblinPlanarDistance(player.transform.position);
        if (float.IsPositiveInfinity(nearest))
            return false;

        float keepRadius = Mathf.Max(1f, goblinBattleProximityRadius);
        float releaseRadius = Mathf.Max(keepRadius, goblinForestReleaseRadius);
        return goblinProximityBattle
            ? nearest <= releaseRadius
            : nearest <= keepRadius;
    }

    private static float GetNearestLivingGoblinPlanarDistance(Vector3 origin)
    {
        float nearestSq = float.PositiveInfinity;
        Goblin[] goblins = FindObjectsOfType<Goblin>();
        for (int i = 0; i < goblins.Length; i++)
        {
            Goblin goblin = goblins[i];
            if (goblin == null || goblin.IsDead)
                continue;

            float dx = goblin.transform.position.x - origin.x;
            float dz = goblin.transform.position.z - origin.z;
            float sq = dx * dx + dz * dz;
            if (sq < nearestSq)
                nearestSq = sq;
        }

        if (float.IsPositiveInfinity(nearestSq))
            return float.PositiveInfinity;

        return Mathf.Sqrt(nearestSq);
    }

    private void OnGoblinDied()
    {
        UpdateGoblinProximityState();

        if (DragonsInCombat.Count > 0 || GoblinsInCombat.Count > 0)
        {
            CancelGoblinBattleEndDelay();
            RefreshGameplayMusic();
            return;
        }

        if (goblinProximityBattle)
        {
            CancelGoblinBattleEndDelay();
            RefreshGameplayMusic();
            return;
        }

        if (currentTrack != BgmTrack.Battle1 || goblinBattleEndDelaySeconds <= 0f)
        {
            CancelGoblinBattleEndDelay();
            RefreshGameplayMusic();
            return;
        }

        goblinBattleHoldUntil = Time.unscaledTime + goblinBattleEndDelaySeconds;
        if (goblinBattleEndRoutine != null)
            StopCoroutine(goblinBattleEndRoutine);

        goblinBattleEndRoutine = StartCoroutine(EndGoblinBattleAfterDelay());
    }

    private IEnumerator EndGoblinBattleAfterDelay()
    {
        float delay = Mathf.Max(0f, goblinBattleHoldUntil - Time.unscaledTime);
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        goblinBattleEndRoutine = null;
        goblinBattleHoldUntil = -1f;
        UpdateGoblinProximityState();
        RefreshGameplayMusic();
    }

    private void CancelGoblinBattleEndDelay()
    {
        goblinBattleHoldUntil = -1f;
        if (goblinBattleEndRoutine == null)
            return;

        StopCoroutine(goblinBattleEndRoutine);
        goblinBattleEndRoutine = null;
    }

    private bool IsHoldingGoblinBattle()
    {
        return currentTrack == BgmTrack.Battle1
            && goblinBattleHoldUntil > 0f
            && Time.unscaledTime < goblinBattleHoldUntil;
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

    private void ApplyMasterVolume()
    {
        float targetVolume = GetEffectiveVolume();
        if (fadeRoutine != null)
            return;

        if (activeSource != null)
            activeSource.volume = targetVolume;
    }

    private IEnumerator PreloadAllClips()
    {
        yield return PreloadAndWarmClip(clipSet?.battle1);
        yield return PreloadAndWarmClip(clipSet?.battle2);
        yield return PreloadAndWarmClip(clipSet?.forest);
        yield return PreloadAndWarmClip(clipSet?.town);
        yield return PreloadAndWarmClip(clipSet?.title);
        preloadRoutine = null;
    }

    private void EnsureCombatClipsReady()
    {
        TryPreload(ResolveClip(BgmTrack.Battle1));
        TryPreload(ResolveClip(BgmTrack.Battle2));
    }

    private void TryPreload(AudioClip clip)
    {
        if (clip == null || clip.loadState == AudioDataLoadState.Loaded)
            return;

        StartCoroutine(PreloadClip(clip));
    }

    private IEnumerator PreloadAndWarmClip(AudioClip clip)
    {
        yield return PreloadClip(clip);
        yield return WarmClip(clip);
    }

    private static IEnumerator PreloadClip(AudioClip clip)
    {
        if (clip == null)
            yield break;

        if (clip.loadState == AudioDataLoadState.Loaded)
            yield break;

        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        while (clip.loadState == AudioDataLoadState.Loading)
            yield return null;
    }

    private IEnumerator WarmClip(AudioClip clip)
    {
        if (clip == null || warmupSource == null)
            yield break;

        if (clip.loadState != AudioDataLoadState.Loaded)
            yield break;

        warmupSource.Stop();
        warmupSource.clip = clip;
        warmupSource.volume = 0f;
        warmupSource.Play();
        yield return null;
        warmupSource.Stop();
    }

    private IEnumerator CrossfadeTo(AudioClip nextClip)
    {
        yield return PreloadClip(nextClip);
        yield return new WaitForEndOfFrame();

        if (activeSource == null || fadeSource == null)
        {
            fadeRoutine = null;
            yield break;
        }

        AudioSource incoming = fadeSource;
        AudioSource outgoing = activeSource;

        incoming.Stop();
        incoming.volume = 0f;
        incoming.loop = true;
        if (incoming.clip != nextClip)
            incoming.clip = nextClip;

        double startAt = AudioSettings.dspTime + 0.04;
        incoming.PlayScheduled(startAt);

        float duration = Mathf.Max(0.01f, crossfadeSeconds);
        float elapsed = 0f;
        float fromVolume = outgoing.isPlaying ? outgoing.volume : 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float targetVolume = GetEffectiveVolume();
            incoming.volume = Mathf.Lerp(0f, targetVolume, t);
            outgoing.volume = Mathf.Lerp(fromVolume, 0f, t);
            yield return null;
        }

        outgoing.Stop();
        outgoing.volume = 0f;
        incoming.volume = GetEffectiveVolume();

        activeSource = incoming;
        fadeSource = outgoing;
        fadeRoutine = null;
    }
}
