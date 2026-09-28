using UnityEngine;
using SickscoreGames.HUDNavigationSystem;

// Hides the HUD Navigation markers while any of the given panels (pause / win / fail / revive) is open.
public static class HUDVisibility
{
    public static void Sync(HUDNavigationSystem hud, params GameObject[] panels)
    {
        if (hud == null) return;

        bool panelOpen = false;
        foreach (GameObject panel in panels)
        {
            if (panel != null && panel.activeInHierarchy)
            {
                panelOpen = true;
                break;
            }
        }

        if (hud.isEnabled == panelOpen)
            hud.EnableSystem(!panelOpen);
    }
}
