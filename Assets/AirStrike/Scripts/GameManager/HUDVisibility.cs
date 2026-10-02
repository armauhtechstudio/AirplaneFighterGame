using UnityEngine;
using SickscoreGames.HUDNavigationSystem;

// Hides the HUD Navigation markers while any of the given panels (pause / win / fail / revive) is open.
public static class HUDVisibility
{
    public static void Sync(HUDNavigationSystem hud, params GameObject[] panels)
    {
        Sync(hud, AnyOpen(panels));
    }

    public static void Sync(HUDNavigationSystem hud, bool panelOpen)
    {
        if (hud == null) return;
        if (hud.isEnabled == panelOpen)
            hud.EnableSystem(!panelOpen);
    }

    public static bool AnyOpen(params GameObject[] panels)
    {
        foreach (GameObject panel in panels)
            if (panel != null && panel.activeInHierarchy) return true;
        return false;
    }

    /// <summary>Shows a HUD element only while no panel is open.</summary>
    public static void ShowUnlessPanel(GameObject element, bool panelOpen)
    {
        if (element != null && element.activeSelf == panelOpen) element.SetActive(!panelOpen);
    }
}
