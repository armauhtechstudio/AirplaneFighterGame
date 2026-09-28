using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SplashScript : MonoBehaviour
{
    [Tooltip("Assign the Image component that has Image Type set to Filled.")]
    public Image loadingBar;

    [Tooltip("Time in seconds to wait before loading the next scene.")]
    public float loadTime = 8f;

    [Header("Logo Intro (punchy pop-in)")]
    [Tooltip("The Logo's RectTransform.")]
    public RectTransform logo;
    public float logoIntroDuration = 0.45f;
    public float logoIntroOvershoot = 1.18f;

    [Header("Logo Idle (rhythmic recoil kick)")]
    public float idleKickInterval = 1.4f;
    public float idleKickStrength = 0.09f;
    public float idleKickDuration = 0.18f;

    [Header("Loading Text Animation")]
    [Tooltip("The \"Loading...\" Text. Its dots will animate with a punch.")]
    public Text loadingText;
    public float dotsInterval = 0.35f;

    [Header("Loading Bar (tick-by-tick, like chambering rounds)")]
    [Tooltip("How many discrete 'shots' fill the bar, instead of one smooth sweep.")]
    public int loadTicks = 18;
    [Tooltip("Muzzle-flash tint the bar flashes to on every tick.")]
    public Color tickFlashColor = Color.white;
    [Tooltip("Optional: a soft click/shot sound played on every tick.")]
    public AudioSource audioSource;
    public AudioClip[] tickSounds;
    [Range(0f, 1f)] public float tickVolume = 0.5f;

    Vector3 logoBaseScale = Vector3.one;
    Color barBaseColor = Color.white;
    string loadingBaseLabel = "Loading";

    private void Start()
    {
        if (loadingBar != null)
        {
            loadingBar.fillAmount = 0f;
            barBaseColor = loadingBar.color;
        }

        if (logo != null)
        {
            logoBaseScale = logo.localScale;
            logo.localScale = Vector3.zero;
            StartCoroutine(LogoIntroThenIdle());
        }

        if (loadingText != null)
        {
            if (!string.IsNullOrEmpty(loadingText.text))
            {
                loadingBaseLabel = loadingText.text.TrimEnd('.');
            }
            StartCoroutine(AnimateLoadingDots());
        }

        StartCoroutine(LoadNextScene());
    }

    // Elastic-feeling overshoot: shoots past the target then settles back, like recoil.
    static float EaseOutBack(float t, float overshoot)
    {
        float c1 = (overshoot - 1f) * 2f + 1f;
        float c3 = c1 + 1f;
        t -= 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    IEnumerator LogoIntroThenIdle()
    {
        // Punchy pop-in — snaps in fast and overshoots before settling, like a round hitting home.
        float t = 0f;
        while (t < logoIntroDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / logoIntroDuration);
            logo.localScale = logoBaseScale * EaseOutBack(p, logoIntroOvershoot);
            yield return null;
        }
        logo.localScale = logoBaseScale;

        // Idle: a subtle rhythmic "kick" every interval, like a slow satisfying heartbeat/recoil pulse.
        while (true)
        {
            yield return new WaitForSeconds(idleKickInterval);
            yield return StartCoroutine(KickPulse());
        }
    }

    IEnumerator KickPulse()
    {
        float t = 0f;
        while (t < idleKickDuration)
        {
            t += Time.deltaTime;
            float p = t / idleKickDuration;
            // Quick punch out, softer settle back — asymmetric like an impulse, not a smooth sine.
            float kick = Mathf.Sin(p * Mathf.PI) * idleKickStrength * (1f - p * 0.3f);
            logo.localScale = logoBaseScale * (1f + kick);
            yield return null;
        }
        logo.localScale = logoBaseScale;
    }

    IEnumerator AnimateLoadingDots()
    {
        RectTransform textRect = loadingText.rectTransform;
        Vector3 baseScale = textRect.localScale;
        int dotCount = 0;
        while (true)
        {
            loadingText.text = loadingBaseLabel + new string('.', dotCount);
            dotCount = (dotCount + 1) % 4;

            // Tiny punch on every dot change so it feels alive, not just text-swapping.
            float t = 0f;
            float punchDuration = 0.12f;
            while (t < punchDuration)
            {
                t += Time.deltaTime;
                float p = t / punchDuration;
                textRect.localScale = baseScale * (1f + (1f - p) * 0.08f);
                yield return null;
            }
            textRect.localScale = baseScale;

            yield return new WaitForSeconds(dotsInterval);
        }
    }

    private IEnumerator LoadNextScene()
    {
        int ticks = Mathf.Max(1, loadTicks);
        float tickDuration = loadTime / ticks;

        for (int i = 1; i <= ticks; i++)
        {
            float targetFill = (float)i / ticks;
            yield return StartCoroutine(FillTick(targetFill, tickDuration));
        }

        if (loadingBar != null)
        {
            loadingBar.fillAmount = 1f;
            loadingBar.color = barBaseColor;
        }

        // Load the next scene in the build index
        AdsManager.Instance.ShowAppOpen();
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        SceneManager.LoadScene(nextSceneIndex);
    }

    // Each tick snaps the bar forward with a tiny overshoot + a bright muzzle-flash tint that fades
    // back out — like a round being chambered — instead of one continuous linear sweep.
    IEnumerator FillTick(float targetFill, float duration)
    {
        if (loadingBar == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        float startFill = loadingBar.fillAmount;
        PlayTickSound();

        float snapDuration = duration * 0.35f;
        float t = 0f;
        while (t < snapDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / snapDuration);
            loadingBar.fillAmount = Mathf.Lerp(startFill, targetFill, EaseOutBack(p, 1.4f));
            loadingBar.color = Color.Lerp(tickFlashColor, barBaseColor, p);
            yield return null;
        }
        loadingBar.fillAmount = targetFill;

        float rest = duration - snapDuration;
        float fadeT = 0f;
        while (fadeT < rest)
        {
            fadeT += Time.deltaTime;
            loadingBar.color = Color.Lerp(tickFlashColor, barBaseColor, Mathf.Clamp01(fadeT / Mathf.Max(0.01f, rest)));
            yield return null;
        }
        loadingBar.color = barBaseColor;
    }

    void PlayTickSound()
    {
        if (audioSource == null || tickSounds == null || tickSounds.Length == 0) return;
        AudioClip clip = tickSounds[Random.Range(0, tickSounds.Length)];
        if (clip != null) audioSource.PlayOneShot(clip, tickVolume);
    }
}
