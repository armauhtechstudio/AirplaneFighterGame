using UnityEngine;
using UnityEngine.UI;

// One row of the Mod 3 leaderboard: rank, player name, score and the "YOU" indicator.
public class Mod3LeaderboardEntry : MonoBehaviour
{
    public Text rankText;
    public Text playerNameText;
    public Text scoreText;
    public GameObject currentPlayerIndicator;
    public Image background;
    public Image rankBadge;
    [Tooltip("Row bar for other players / for the current player (teal / gold).")]
    public Sprite normalBar, highlightBar;

    static readonly Color Gold = new Color(1f, 0.80f, 0.20f);
    static readonly Color Silver = new Color(0.82f, 0.85f, 0.90f);
    static readonly Color Bronze = new Color(0.86f, 0.52f, 0.25f);

    public void Set(int rank, string playerName, long score, bool isCurrentPlayer)
    {
        rankText.text = rank.ToString();
        playerNameText.text = playerName;
        scoreText.text = score.ToString("N0");
        if (currentPlayerIndicator != null) currentPlayerIndicator.SetActive(isCurrentPlayer);

        // Top three: gold / silver / bronze rank badge; everyone else a plain dark badge
        if (rankBadge != null)
        {
            rankBadge.color = rank == 1 ? Gold : rank == 2 ? Silver : rank == 3 ? Bronze : new Color(0.12f, 0.14f, 0.16f, 0.9f);
            rankText.color = Color.white; // white + dark outline reads on every badge colour
        }

        // Current player's row: gold bar instead of the normal teal one
        if (background != null)
        {
            Sprite bar = isCurrentPlayer ? highlightBar : normalBar;
            if (bar != null) background.sprite = bar;
            background.color = Color.white;
        }
        playerNameText.color = isCurrentPlayer ? new Color(1f, 0.93f, 0.7f) : Color.white;
    }
}
