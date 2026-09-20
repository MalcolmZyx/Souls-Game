using UnityEngine;

public class BossFightUIManager : MonoBehaviour
{
    private BossDamageable bossDamageable;
    private PlayerVitals playerVitals;

    private void Start()
    {
        bossDamageable = FindObjectOfType<BossDamageable>();
        playerVitals = FindObjectOfType<PlayerVitals>();
        
        if (bossDamageable == null)
        {
            Debug.LogWarning("BossFightUIManager: Could not find BossDamageable in the scene.");
        }
        if (playerVitals == null)
        {
            Debug.LogWarning("BossFightUIManager: Could not find PlayerVitals in the scene.");
        }
    }

    private void OnGUI()
    {
        // Setup text style
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 28;
        style.fontStyle = FontStyle.Bold;

        // Draw Player HP (Top Left)
        if (playerVitals != null)
        {
            style.normal.textColor = Color.green;
            string playerText = $"Player HP: {Mathf.CeilToInt(playerVitals.currentHealth)} / {Mathf.CeilToInt(playerVitals.maxHealth)}";
            GUI.Label(new Rect(20, 20, 400, 50), playerText, style);
        }

        // Draw Boss HP (Top Right)
        if (bossDamageable != null)
        {
            style.normal.textColor = Color.red;
            style.alignment = TextAnchor.UpperRight;
            string bossText = $"Boss HP: {Mathf.CeilToInt(bossDamageable.currentHealth)} / {Mathf.CeilToInt(bossDamageable.maxHealth)}";
            GUI.Label(new Rect(Screen.width - 420, 20, 400, 50), bossText, style);
        }
    }
}
