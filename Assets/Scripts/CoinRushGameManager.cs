using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
public sealed class CoinRushGameManager : MonoBehaviour
{
    private const float Duration = 20f;
    [SerializeField] private CoinRushPlayerController player;
    [SerializeField] private CoinRushCameraFollow view;
    [SerializeField] private CoinRushCoinSpawner coins;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private GameObject endPanel;
    [SerializeField] private TMP_Text finalScoreText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button quitButton;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float remaining;
    [SerializeField] private Transform courseRoot;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private TMP_Text stageText;
    [SerializeField] private TMP_Text outcomeTitle;
    [SerializeField] private Button playButton;
    [SerializeField] private Button menuQuitButton;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button pauseRestartButton;
    [SerializeField] private Button pauseMenuButton;
    [SerializeField] private Transform[] checkpoints;
    [SerializeField] private GameObject[] checkpointGates;
    [SerializeField] private AudioSource music;
    [SerializeField] private CoinRushAudioManager audioManager;
    public void ConfigureAudio(CoinRushAudioManager audio) => audioManager = audio;
    private enum CourseState { Menu, Playing, Paused, Dead, Won }
    private CourseState state;
    private int checkpoint;
    private int checkpointScore;
    private bool inSafezone;
    private float totalTime;
    private float resumeScale = 1f;
    private float pausedVerticalSpeed;
    private Vector2 pausedLook;
    public int CurrentStage => checkpoint + 1;
    public int StageCount => checkpoints != null && checkpoints.Length > 0 ? checkpoints.Length : 3;
    public bool IsCourse => courseRoot != null;

    public void ConfigureCourse(Transform root, Transform[] spawnPoints, GameObject[] gates,
        GameObject menu, GameObject pause, TMP_Text stage, TMP_Text title,
        Button play, Button exit, Button resume, Button restart, Button back, AudioSource soundtrack)
    {
        courseRoot = root;
        checkpoints = spawnPoints;
        checkpointGates = gates;
        mainMenuPanel = menu;
        pausePanel = pause;
        stageText = stage;
        outcomeTitle = title;
        playButton = play;
        menuQuitButton = exit;
        resumeButton = resume;
        pauseRestartButton = restart;
        pauseMenuButton = back;
        music = soundtrack;
    }

    public int Score { get; private set; }
    public bool IsRunning { get; private set; }
    public float RemainingTime => remaining;

    public void Configure(CoinRushPlayerController character, CoinRushCameraFollow cameraView,
        CoinRushCoinSpawner spawner, TMP_Text scoreLabel, TMP_Text timerLabel,
        GameObject panel, TMP_Text finalLabel, Button restart, Button quit)
    {
        player = character;
        view = cameraView;
        coins = spawner;
        scoreText = scoreLabel;
        timerText = timerLabel;
        endPanel = panel;
        finalScoreText = finalLabel;
        restartButton = restart;
        quitButton = quit;
        coins.SetGameManager(this);
    }

    private void Start()
    {
        if (audioManager == null) audioManager = GetComponent<CoinRushAudioManager>();
        if (audioManager != null) audioManager.StopRound();
        else if (music != null) music.Stop();
        if (player == null || view == null || coins == null || restartButton == null || quitButton == null
            || scoreText == null || timerText == null || finalScoreText == null || endPanel == null)
        {
            Debug.LogError("Coin Rush: execute Criar HUD e Timer para configurar as referencias.", this);
            enabled = false;
            return;
        }
        initialPosition = player.transform.position;
        initialRotation = player.transform.rotation;
        restartButton.onClick.AddListener(RestartGame);
        if (IsCourse)
        {
            quitButton.onClick.AddListener(ReturnToMenu);
            playButton.onClick.AddListener(StartCourse);
            menuQuitButton.onClick.AddListener(QuitGame);
            resumeButton.onClick.AddListener(ResumeGame);
            pauseRestartButton.onClick.AddListener(StartCourse);
            pauseMenuButton.onClick.AddListener(ReturnToMenu);
            coins.SetGameManager(this);
            ReturnToMenu();
            return;
        }
        quitButton.onClick.AddListener(QuitGame);
        coins.SetGameManager(this);
        BeginRound();
    }

    private void BeginRound()
    {
        Time.timeScale = 1f;
        Score = 0;
        remaining = Duration;
        IsRunning = true;
        endPanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        player.enabled = true;
        view.enabled = true;
        view.SetTarget(player.transform);
        if (audioManager != null) audioManager.StartRound();
        UpdateLabels();
    }

    private void Update()
    {
        if (IsCourse)
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (state == CourseState.Playing) PauseGame();
                else if (state == CourseState.Paused) ResumeGame();
            }
            if (state != CourseState.Playing) return;
            totalTime += Time.unscaledDeltaTime;
            if (player.transform.position.y < -3f) { Die(); return; }
            if (!inSafezone) remaining = Mathf.Max(0, remaining - Time.deltaTime);
            UpdateLabels();
            if (remaining <= 0) Die();
            return;
        }
        if (!IsRunning) return;
        remaining = Mathf.Max(0f, remaining - Time.deltaTime);
        UpdateLabels();
        if (remaining <= 0f) EndGame();
    }

    public void AwardCoin()
    {
        if (!IsRunning || remaining <= 0f) return;
        Score += CoinRushCoinSpawner.CoinValue;
        if (audioManager != null) audioManager.PlayCoin();
        UpdateLabels();
    }

    private void UpdateLabels()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
        if (timerText != null) timerText.text = $"{seconds / 60:00}:{seconds % 60:00}";
        if (scoreText != null) scoreText.text = "Pontos: " + Score;
        if (stageText != null)
            stageText.text = $"Trecho {CurrentStage}/{StageCount} | Checkpoint {checkpoint}/{StageCount - 1}" + (inSafezone ? " | Área segura" : "")
                + $"\nTotal: {totalTime:0.0}s";
    }

    private void EndGame()
    {
        if (IsCourse) { Die(); return; }
        IsRunning = false;
        if (audioManager != null) audioManager.StopRound();
        remaining = 0f;
        player.enabled = false;
        // Disabling FPS look also releases its cursor ownership.
        view.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        finalScoreText.text = "Pontuação final: " + Score;
        endPanel.SetActive(true);
        UpdateLabels();
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
    }

    public void RestartGame()
    {
        if (IsCourse)
        {
            if (state == CourseState.Dead) RetryCheckpoint();
            else if (state == CourseState.Won) StartCourse();
            return;
        }
        if (IsRunning) return;
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.SetPositionAndRotation(initialPosition, initialRotation);
        controller.enabled = true;
        Physics.SyncTransforms();
        BeginRound();
        coins.ResetCoins();
    }

    private void SetControls(bool enabledControls)
    {
        player.enabled = enabledControls;
        view.enabled = enabledControls;
        if (!enabledControls)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void PlacePlayer(Vector3 position, Quaternion rotation)
    {
        SetControls(false);
        CharacterController controller = player.GetComponent<CharacterController>();
        controller.enabled = false;
        player.transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;
        Physics.SyncTransforms();
        view.SetTarget(player.transform);
    }

    public void StartCourse()
    {
        checkpoint = 0;
        checkpointScore = 0;
        Score = 0;
        totalTime = 0;
        inSafezone = false;
        ResetCourse();
        PlacePlayer(initialPosition, initialRotation);
        ActivateCourse();
        coins.ResetCoins();
    }

    private void ActivateCourse()
    {
        Time.timeScale = 1f;
        remaining = Duration;
        state = CourseState.Playing;
        IsRunning = true;
        if (audioManager != null) audioManager.StartRound();
        mainMenuPanel.SetActive(false);
        pausePanel.SetActive(false);
        endPanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        SetControls(true);
        view.SetTarget(player.transform);
        UpdateLabels();
    }

    private void ResetCourse()
    {
        foreach (CoinRushCourseElement element in courseRoot.GetComponentsInChildren<CoinRushCourseElement>(true))
            element.ResetElement();
        for (int i = 0; i < checkpointGates.Length; i++) checkpointGates[i].SetActive(i < checkpoint);
        Physics.SyncTransforms();
    }

    public void EnterSafezone(int index)
    {
        if (!IsRunning || index != checkpoint + 1 || index >= StageCount) return;
        checkpoint = index;
        checkpointScore = Score;
        inSafezone = true;
        remaining = Duration;
        checkpointGates[index - 1].SetActive(true);
        coins.CommitCheckpoint(index);
        UpdateLabels();
    }

    public void ExitSafezone(int index)
    {
        if (!IsRunning || index != checkpoint || !inSafezone) return;
        inSafezone = false;
        remaining = Duration;
        UpdateLabels();
    }

    public void Die()
    {
        if (!IsCourse || state != CourseState.Playing) return;
        state = CourseState.Dead;
        if (audioManager != null) audioManager.StopRound();
        ShowOutcome("Você morreu", "Tentar novamente");
    }

    public void CompleteCourse()
    {
        if (!IsCourse || state != CourseState.Playing || checkpoint != StageCount - 1 || inSafezone) return;
        state = CourseState.Won;
        ShowOutcome("Nível concluído!", "Jogar novamente");
        if (audioManager != null) audioManager.PlayVictory();
    }

    private void ShowOutcome(string title, string action)
    {
        IsRunning = false;
        Time.timeScale = 0;
        SetControls(false);
        outcomeTitle.text = title;
        finalScoreText.text = $"Pontos: {Score}  |  Moedas: {Score / CoinRushCoinSpawner.CoinValue}\nTempo total: {totalTime:0.0}s";
        restartButton.GetComponentInChildren<TMP_Text>().text = action;
        endPanel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(restartButton.gameObject);
    }

    private void RetryCheckpoint()
    {
        Score = checkpointScore;
        ResetCourse();
        Transform spawn = checkpoints[checkpoint];
        PlacePlayer(spawn.position, spawn.rotation);
        inSafezone = checkpoint > 0;
        ActivateCourse();
        coins.RestoreCheckpoint(checkpoint);
    }

    private void PauseGame()
    {
        pausedVerticalSpeed = player.VerticalSpeed;
        pausedLook = view.LookAngles;
        if (audioManager != null) audioManager.PauseRound();
        resumeScale = Time.timeScale > 0 ? Time.timeScale : 1f;
        state = CourseState.Paused;
        IsRunning = false;
        Time.timeScale = 0;
        SetControls(false);
        pausePanel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }

    private void ResumeGame()
    {
        if (state != CourseState.Paused) return;
        state = CourseState.Playing;
        IsRunning = true;
        Time.timeScale = resumeScale;
        pausePanel.SetActive(false);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        SetControls(true);
        player.VerticalSpeed = pausedVerticalSpeed;
        view.RestoreLook(pausedLook);
        if (audioManager != null) audioManager.ResumeRound();
    }

    public void ReturnToMenu()
    {
        if (!IsCourse) return;
        if (audioManager != null) audioManager.StopRound();
        state = CourseState.Menu;
        IsRunning = false;
        Time.timeScale = 0;
        SetControls(false);
        pausePanel.SetActive(false);
        endPanel.SetActive(false);
        mainMenuPanel.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(playButton.gameObject);
    }

    public void QuitGame()
    {
        if (audioManager != null) audioManager.StopRound();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (IsCourse) Time.timeScale = 1f;
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartGame);
        if (quitButton != null) quitButton.onClick.RemoveListener(QuitGame);
    }
}
