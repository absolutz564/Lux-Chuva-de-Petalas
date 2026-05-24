using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;


public class GameManagerUI : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite sprite1;
    public Sprite sprite2;

    [Header("UI Image")]
    public Image targetImage;

    [Header("Contagem Regressiva")]
    [SerializeField] private GameObject      countdownPanel;
    [SerializeField] private TextMeshProUGUI countdownText;

    public static GameManagerUI Instance;

    public TextMeshProUGUI timerText;
    public TextMeshProUGUI scoreText;
    public GameObject gameOverPanelWin;
    public GameObject gameOverPanelLoss;
    public GameObject Spawner;

    private float timeRemaining = 30f;
    public int score = 0;
    public bool gameActive    = false;
    private bool  _gameStarted     = false;
    private float _countdown       = 3f;
    private const float CountdownDuration  = 3f;
    private float _twoHandsTimer   = 0f;
    private const float TwoHandsDebounce = 0.3f;

    void Awake() => Instance = this;

    public void AssignRandomSprite()
    {
        if (targetImage == null)
        {
            Debug.LogWarning("Target Image não atribuída!");
            return;
        }

        int randomIndex = Random.Range(0, 2);
        targetImage.sprite = (randomIndex == 0) ? sprite1 : sprite2;
    }

    void Start()
    {
        AssignRandomSprite();
        timerText.text = Mathf.CeilToInt(timeRemaining).ToString();
        scoreText.text = "0";
        if (countdownText != null)
            countdownText.text = "Mostre as duas mãos\npara começar";
        if (countdownPanel != null)
            countdownPanel.SetActive(true);
    }

    void Update()
    {
        if (!_gameStarted)
        {
            HandleCountdown();
            return;
        }

        if (!gameActive) return;

        timeRemaining -= Time.deltaTime;
        timerText.text = "" + Mathf.CeilToInt(timeRemaining).ToString();

        if (timeRemaining <= 0)
        {
            gameActive = false;
            GameOver();
            return;
        }

        // Input por gesto de mão: fecha o punho sobre a bolha para estourar
        if (HandTracker.Instance != null)
        {
            for (int h = 0; h < HandTracker.Instance.HandCount; h++)
            {
                if (!HandTracker.Instance.IsHandJustClosed(h)) continue;

                Vector3 palmWorld = HandTracker.Instance.GetPalmCenter(h);
                Vector2 screenPos = Camera.main.WorldToScreenPoint(palmWorld);
                HandleTouch(screenPos);
            }
        }

#if UNITY_EDITOR
        // Fallback de mouse para testar no Editor sem o hand tracker ativo
        if (Input.GetMouseButtonDown(0))
            HandleTouch(Input.mousePosition);
#endif
    }

    void HandleCountdown()
    {
        bool twoHands = HandTracker.Instance != null && HandTracker.Instance.HandCount >= 2;

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space)) twoHands = true;
#endif

        if (twoHands)
            _twoHandsTimer += Time.deltaTime;
        else
            _twoHandsTimer = 0f;

        if (_twoHandsTimer < TwoHandsDebounce)
        {
            _countdown = CountdownDuration;
            if (countdownText != null)
                countdownText.text = "Mostre as duas mãos\npara começar";
            return;
        }

        _countdown -= Time.deltaTime;

        if (countdownText != null)
        {
            int secs = Mathf.CeilToInt(_countdown);
            countdownText.text = secs > 0 ? secs.ToString() : "";
        }

        if (_countdown <= 0f)
        {
            _gameStarted = true;
            gameActive   = true;
            if (countdownPanel != null)
                countdownPanel.SetActive(false);
        }
    }

    void HandleTouch(Vector2 screenPosition)
    {
        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = screenPosition
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count > 0)
        {
            // ordena pelo sortingOrder (topmost primeiro)
            results.Sort((a, b) => b.sortingOrder.CompareTo(a.sortingOrder));

            // pega o que está na frente
            var hit = results[0];

            var bubble = hit.gameObject.GetComponent<BubbleUI>();
            if (bubble != null)
            {
                bubble.BurstAndRelease();
            }
        }
    }

    public void AddScore(int amount)
    {
        score += amount;
        scoreText.text = "" + score;
    }

    void GameOver()
    {
        Destroy(Spawner);

        if (score >= 50)
        {
            gameOverPanelWin.SetActive(true);
            gameOverPanelWin.GetComponentInChildren<TextMeshProUGUI>().text = "Pontuação: " + score;
        }
        else
        {
            gameOverPanelLoss.SetActive(true);
            gameOverPanelLoss.GetComponentInChildren<TextMeshProUGUI>().text = "Pontuação: " + score;
        }
    }

    public void RestartGame() => UnityEngine.SceneManagement.SceneManager.LoadScene(1);
}
