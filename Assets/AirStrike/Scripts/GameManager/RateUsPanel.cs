using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "RATE US" popup with 5 stars (prefab Resources/RateUsPanel, built by Tools/AirStrike/Create Rate Us Panel).
/// StoreReview shows it where it used to open the store review directly:
///  - tap a star: that star and the ones before it turn gold, and the rating is reported (onRated)
///  - 4-5 stars: the stars fill one by one with a pop while StoreReview opens the store review
///  - 1-3 stars: "THANK YOU FOR RATING!", then the panel closes and the game continues
///  - the close X (visible from the start) skips without rating
/// Either way the panel removes only itself: the win / fail panel underneath stays as it was.
/// Runs on unscaled time: the win / fail panels pause the game.
/// </summary>
public class RateUsPanel : MonoBehaviour
{
    public const string ResourcePath = "RateUsPanel";

    public RectTransform body;
    public Image[] stars;
    public Sprite emptyStar;
    public Sprite goldStar;
    public Text messageText;
    public Button closeButton;

    Action<int> onRated;
    Action onClosed;
    bool answered, closing;
    float time;
    Vector3 bodyScale = Vector3.one, closeScale = Vector3.one;

    /// <summary>Opens the panel. False if the prefab is missing (the caller then asks the store directly).</summary>
    public static bool Show(Action<int> onRated, Action onClosed)
    {
        var prefab = Resources.Load<GameObject>(ResourcePath);
        if (prefab == null) return false;
        var panel = Instantiate(prefab).GetComponent<RateUsPanel>();
        panel.onRated = onRated;
        panel.onClosed = onClosed;
        return true;
    }

    void Awake()
    {
        for (int i = 0; i < stars.Length; i++)
        {
            int rating = i + 1;
            Button b = stars[i].GetComponent<Button>();
            if (b != null) b.onClick.AddListener(() => Rate(rating));
            stars[i].sprite = emptyStar;
        }
        GameSfx.Popup();
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(GameSfx.Click);
            closeButton.onClick.AddListener(Close);
            closeScale = closeButton.transform.localScale;
        }
        if (messageText != null) messageText.text = "";
        if (body != null)
        {
            bodyScale = body.localScale;
            body.localScale = Vector3.zero;
        }
    }

    void Update()
    {
        time += Time.unscaledDeltaTime;

        // Pop in
        if (body != null && !closing)
            body.localScale = bodyScale * (time < 0.35f ? EaseOutBack(time / 0.35f) : 1f);

        // Close X: there from the start, pops in just after the panel
        if (closeButton != null && !closing)
            closeButton.transform.localScale = closeScale * EaseOutBack(Mathf.Clamp01((time - 0.2f) / 0.25f));

        // Before rating: a gentle wave runs over the empty stars to invite a tap
        if (!answered)
            for (int i = 0; i < stars.Length; i++)
            {
                float wave = Mathf.Repeat(time * 0.8f - i * 0.12f, 1f);
                float s = wave < 0.25f ? Mathf.Sin(wave / 0.25f * Mathf.PI) * 0.10f : 0f;
                stars[i].rectTransform.localScale = Vector3.one * (1f + s);
            }
    }

    void Rate(int rating)
    {
        if (answered || closing) return;
        answered = true;
        foreach (Image s in stars) s.rectTransform.localScale = Vector3.one;
        onRated?.Invoke(rating); // 4-5: StoreReview opens the store review now, while the stars fill
        StartCoroutine(rating >= 4 ? FillAnimated(rating) : FillAndThank(rating));
    }

    // 4-5 stars: one by one, each pops as it turns gold
    IEnumerator FillAnimated(int rating)
    {
        for (int i = 0; i < rating; i++)
        {
            stars[i].sprite = goldStar;
            GameSfx.Star(i); // each star rings a step higher
            StartCoroutine(Pop(stars[i].rectTransform));
            yield return new WaitForSecondsRealtime(0.14f);
        }
        if (messageText != null) messageText.text = "THANK YOU!";
        yield return new WaitForSecondsRealtime(0.4f);
        Close();
    }

    // 1-3 stars: fill, thank, close
    IEnumerator FillAndThank(int rating)
    {
        for (int i = 0; i < rating; i++) stars[i].sprite = goldStar;
        GameSfx.Star(rating - 1);
        if (messageText != null) messageText.text = "THANK YOU FOR RATING!";
        yield return new WaitForSecondsRealtime(0.8f);
        Close();
    }

    IEnumerator Pop(RectTransform star)
    {
        for (float t = 0f; t < 0.25f; t += Time.unscaledDeltaTime)
        {
            star.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(t / 0.25f * Mathf.PI));
            yield return null;
        }
        star.localScale = Vector3.one;
    }

    /// <summary>Close X, or automatically after a rating.</summary>
    public void Close()
    {
        if (closing) return;
        closing = true;
        StartCoroutine(PopOut());
    }

    IEnumerator PopOut()
    {
        if (body != null)
        {
            Vector3 from = body.localScale;
            for (float t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                body.localScale = Vector3.Lerp(from, Vector3.zero, t / 0.18f);
                yield return null;
            }
        }
        Action closed = onClosed;
        onClosed = null;
        Destroy(gameObject);
        closed?.Invoke();
    }

    static float EaseOutBack(float x)
    {
        x = Mathf.Clamp01(x);
        const float c1 = 1.70158f, c3 = c1 + 1f;
        x -= 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }
}
