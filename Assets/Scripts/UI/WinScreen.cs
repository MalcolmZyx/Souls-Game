using System.Collections;
using UnityEngine;

public class WinScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private CanvasGroup winText;
    [SerializeField] private BossDamageable boss;

    [SerializeField] private float fadeSpeed = 1.5f;
    [SerializeField] private float delayBeforeText = 0.5f;

    private void OnEnable()
    {
        boss.OnBossDeath += ShowWinScreen; 
    }

    private void OnDisable()
    {
        boss.OnBossDeath -= ShowWinScreen;
    }
    public void ShowWinScreen()
    {
        gameObject.SetActive(true);
        StartCoroutine(FadeSequence());
    }

    private IEnumerator FadeSequence()
    {
        yield return new WaitForSeconds(5f);

        yield return StartCoroutine(FadeCanvasGroup(blackOverlay, 1f));

        yield return new WaitForSeconds(delayBeforeText);

        yield return StartCoroutine(FadeCanvasGroup(winText, 1f));
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha)
    {
        while (!Mathf.Approximately(cg.alpha, targetAlpha))
        {
            cg.alpha = Mathf.MoveTowards(
                cg.alpha,
                targetAlpha,
                fadeSpeed * Time.unscaledDeltaTime
            );
            yield return null;
        }
    }
}
