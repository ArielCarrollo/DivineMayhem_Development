using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpikyBallGameManager : MonoBehaviour
{
    public static SpikyBallGameManager Instance { get; private set; }

    [Header("Referencias")]
    [SerializeField] private SpikyBallHUD topHUD;
    [SerializeField] private SpikyBallHazard ball;

    [Header("UI Cuenta Regresiva")]
    [SerializeField] private TMPro.TextMeshProUGUI countdownText;
    [SerializeField] private GameObject hudContainer;

    private List<CharacterBase> activePlayers = new List<CharacterBase>();
    private List<CharacterBase> allPlayers = new List<CharacterBase>();

    private bool gameEnded = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (hudContainer != null) hudContainer.SetActive(false);
        if (countdownText != null) countdownText.text = "";
        StartCoroutine(StartGameRoutine());
    }

    // ... (Tu código de StartGameRoutine y RegisterPlayer sigue igual) ...

    public void RegisterPlayer(CharacterBase player)
    {
        if (!allPlayers.Contains(player))
        {
            allPlayers.Add(player);
            activePlayers.Add(player);
            if (topHUD != null) topHUD.RegisterPlayer(player.PlayerIndex, player.name);
        }
    }

    // --- AQUÍ ESTÁ LA CORRECCIÓN ---

    // Este es el método que CharacterBase y DeathZone están buscando
    public void OnPlayerDied(CharacterBase player)
    {
        // Reutilizamos la lógica de "HitByBall" porque el resultado es el mismo: eliminación.
        OnPlayerHitByBall(player);
    }

    // -------------------------------

    public void OnPlayerHitByBall(CharacterBase player)
    {
        if (gameEnded) return;
        if (!activePlayers.Contains(player)) return;

        Debug.Log($"¡BOOM! {player.name} eliminado por la bola.");

        // 1. Matar
        player.Kill();

        // 2. Actualizar lista
        activePlayers.Remove(player);

        // 3. Actualizar HUD
        if (topHUD != null) topHUD.OnPlayerEliminated(player.PlayerIndex);

        // --- NUEVO: CALMAR LA BOLA ---
        // Al matar a alguien, la bola vuelve a velocidad lenta para reiniciar la tensión
        if (ball != null)
        {
            ball.ResetSpeed();
        }
        // -----------------------------

        // 4. Chequear Victoria
        CheckWinCondition();
    }

    // ... (El resto del script CheckWinCondition, EndGame, etc. se mantiene igual) ...
    private void CheckWinCondition()
    {
        if (activePlayers.Count <= 1)
        {
            gameEnded = true;
            if (ball != null) ball.StopBall();

            int winnerIndex = -1;
            if (activePlayers.Count == 1) winnerIndex = activePlayers[0].PlayerIndex;
            EndGame(winnerIndex);
        }
    }

    private void EndGame(int winnerIndex)
    {
        Debug.Log($"Ganador Ronda Bola: P{winnerIndex + 1}");
        if (GameManager.Instance != null)
        {
            if (winnerIndex != -1) GameManager.Instance.AddMatchScore(winnerIndex, 10);
            foreach (var p in allPlayers)
            {
                if (p.PlayerIndex != winnerIndex) GameManager.Instance.AddMatchScore(p.PlayerIndex, 5);
            }
            StartCoroutine(EndSequence());
        }
    }

    private IEnumerator EndSequence()
    {
        yield return new WaitForSeconds(3f);
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithFade("Intermission");
        else
            UnityEngine.SceneManagement.SceneManager.LoadScene("Intermission");
    }

    // Y no olvides pegar el StartGameRoutine y AnimateCountdownNumber del mensaje anterior si no los tienes
    private IEnumerator StartGameRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (countdownText != null)
        {
            yield return StartCoroutine(AnimateCountdownNumber("3"));
            yield return StartCoroutine(AnimateCountdownNumber("2"));
            yield return StartCoroutine(AnimateCountdownNumber("1"));
            yield return StartCoroutine(AnimateCountdownNumber("¡YA!", Color.red));
            countdownText.text = "";
        }
        if (hudContainer != null) hudContainer.SetActive(true);
        if (ball != null) ball.ActivateBall();
    }

    private IEnumerator AnimateCountdownNumber(string text, Color? colorOverride = null)
    {
        countdownText.text = text;
        countdownText.color = colorOverride ?? Color.white;
        countdownText.transform.localScale = Vector3.zero;
        countdownText.transform.DOScale(1.2f, 0.3f).SetEase(DG.Tweening.Ease.OutBack);
        yield return new WaitForSeconds(0.8f);
        countdownText.transform.DOScale(0f, 0.2f).SetEase(DG.Tweening.Ease.InBack);
        yield return new WaitForSeconds(0.2f);
    }
}