using UnityEngine;

// Spins a UI element (loading indicators). Unscaled time, so it also spins on paused screens.
public class UISpin : MonoBehaviour
{
    public float degreesPerSecond = -240f;

    void Update()
    {
        transform.Rotate(0f, 0f, degreesPerSecond * Time.unscaledDeltaTime);
    }
}
