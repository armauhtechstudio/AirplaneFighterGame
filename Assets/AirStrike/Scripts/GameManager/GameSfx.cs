using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI / feedback sound effects (clips in Resources/Sfx, made by Tools/AirStrike/Generate Sound Effects).
/// Starts by itself and survives scene loads. Every UI button in each scene gets the click sound
/// (except the weapon buttons, which have their own sounds); the rest is called from the game code:
/// GameSfx.Popup(), Win(), Fail(), Star(i), Checkpoint(), Score(), Tick(), Revive(), HeartLost().
/// 2D, ignores Time.timeScale (works on the paused win / fail panels).
/// </summary>
public class GameSfx : MonoBehaviour
{
    public static float masterVolume = 1f;

    static GameSfx instance;
    AudioSource oneShot, pitched;
    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly HashSet<Button> hooked = new HashSet<Button>();
    float lastClick;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        var go = new GameObject("GameSfx");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<GameSfx>();
    }

    void Awake()
    {
        oneShot = NewSource();
        pitched = NewSource();
        SceneManager.sceneLoaded += (scene, mode) => HookButtons(null);
        HookButtons(null);
    }

    AudioSource NewSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        return s;
    }

    // ---------------------------------------------------------------- API

    public static void Click() => Play("click", 0.35f);
    public static void Popup() => Play("popup", 0.35f);
    public static void Win() => Play("win", 0.6f);
    public static void Fail() => Play("fail", 0.5f);
    public static void Checkpoint() => Play("checkpoint", 0.5f);
    public static void Score() => Play("score", 0.3f);
    public static void Tick() => Play("tick", 0.35f);
    public static void Revive() => Play("revive", 0.55f);
    public static void HeartLost() => Play("heart_lost", 0.55f);

    /// <summary>Rate Us star ding, one step higher for each star (0-based).</summary>
    public static void Star(int index)
    {
        if (instance == null) return;
        AudioClip clip = instance.Clip("star");
        if (clip == null) return;
        float[] pitches = { 1f, 1.12f, 1.26f, 1.335f, 1.5f };
        instance.pitched.pitch = pitches[Mathf.Clamp(index, 0, pitches.Length - 1)];
        instance.pitched.PlayOneShot(clip, 0.5f * masterVolume);
    }

    /// <summary>Gives every button under root (or in all loaded scenes) the click sound. Safe to call again.</summary>
    public static void HookButtons(GameObject root)
    {
        if (instance == null) return;
        Button[] buttons = root != null ? root.GetComponentsInChildren<Button>(true) : FindObjectsOfType<Button>(true);
        foreach (Button b in buttons)
        {
            if (b == null || instance.hooked.Contains(b) || IsWeaponButton(b)) continue;
            instance.hooked.Add(b);
            b.onClick.AddListener(instance.OnButtonClick);
        }
    }

    // ---------------------------------------------------------------- internals

    void OnButtonClick()
    {
        if (Time.unscaledTime - lastClick < 0.05f) return; // one click per tap even if two hooked buttons fire
        lastClick = Time.unscaledTime;
        Click();
    }

    // Fire / rocket buttons already make weapon sounds
    static bool IsWeaponButton(Button b)
    {
        for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
        {
            string m = b.onClick.GetPersistentMethodName(i);
            if (m != null && (m.Contains("Fire") || m.Contains("Shoot") || m.Contains("Missile") || m.Contains("Rocket"))) return true;
        }
        string n = b.name.ToLowerInvariant();
        return n.Contains("shoot") || n.Contains("fire") || n.Contains("rocket") || n.Contains("missile") || n.Contains("machinegun");
    }

    static void Play(string name, float volume)
    {
        if (instance == null) return;
        AudioClip clip = instance.Clip(name);
        if (clip != null) instance.oneShot.PlayOneShot(clip, volume * masterVolume);
    }

    AudioClip Clip(string name)
    {
        if (!clips.TryGetValue(name, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>("Sfx/" + name);
            clips[name] = clip;
            if (clip == null) Debug.LogWarning("[GameSfx] Resources/Sfx/" + name + " missing (Tools/AirStrike/Generate Sound Effects).");
        }
        return clip;
    }
}
