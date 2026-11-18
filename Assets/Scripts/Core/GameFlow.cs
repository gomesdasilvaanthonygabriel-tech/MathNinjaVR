using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, GameOver }

/// <summary>
/// Ponto unico de controle do fluxo: Menu -> Playing -> GameOver -> (recarrega a cena) Menu.
/// Liga UI, spawner, lamina e pontuacao (MatchStats via ScoreManager).
/// </summary>
public class GameFlow : MonoBehaviour
{
    public GameUI ui;
    public AnswerSpawner spawner;
    public Blade blade;
    public ScoreManager score;

    public GameState State { get; private set; } = GameState.Menu;

    void Awake()
    {
        if (ui == null) ui = FindFirstObjectByType<GameUI>();
        if (spawner == null) spawner = FindFirstObjectByType<AnswerSpawner>();
        if (blade == null) blade = FindFirstObjectByType<Blade>();
        if (score == null) score = FindFirstObjectByType<ScoreManager>();

        if (blade != null) blade.enabled = false; // nada corta antes de Comecar
        AnswerTarget.DespawnAll();
    }

    void OnEnable()
    {
        if (ui != null)
        {
            ui.StartRequested += HandleStart;
            ui.PlayAgainRequested += HandlePlayAgain;
        }
        if (score != null)
        {
            score.Changed += RefreshHud;
            score.MatchEnded += HandleMatchEnded;
        }
    }

    void OnDisable()
    {
        if (ui != null)
        {
            ui.StartRequested -= HandleStart;
            ui.PlayAgainRequested -= HandlePlayAgain;
        }
        if (score != null)
        {
            score.Changed -= RefreshHud;
            score.MatchEnded -= HandleMatchEnded;
        }
    }

    void Start() => EnterMenu();

    void EnterMenu()
    {
        State = GameState.Menu;
        if (spawner != null)
        {
            spawner.StopMatch();
            spawner.ShowBoardMessage("MathNinja", "Escolha e clique em Começar");
        }
        if (blade != null) blade.enabled = false;
        if (ui != null) ui.ShowMenu(GameSettings.operation, GameSettings.difficulty);
    }

    void HandleStart(Operation op, Difficulty diff)
    {
        if (State != GameState.Menu) return;

        GameSettings.operation = op;
        GameSettings.difficulty = diff;

        State = GameState.Playing;
        if (score != null) score.ResetMatch();
        if (ui != null)
        {
            ui.ShowHud();
            RefreshHud();
        }
        if (blade != null) blade.enabled = true;
        if (spawner != null) spawner.StartMatch();
    }

    void HandleMatchEnded(bool won)
    {
        if (State != GameState.Playing) return;
        State = GameState.GameOver;

        if (blade != null) blade.enabled = false;
        if (spawner != null)
        {
            spawner.StopMatch();
            spawner.ShowBoardMessage(won ? "Parabéns!" : "Fim de jogo", "");
        }
        AnswerTarget.DespawnAll();
        if (ui != null) ui.ShowGameOver(won, score != null ? score.Stats : null);
    }

    void HandlePlayAgain()
    {
        if (State != GameState.GameOver) return;
        State = GameState.Menu;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
        else SceneManager.LoadScene(scene.name);
    }

    void RefreshHud()
    {
        if (ui != null && score != null) ui.UpdateHud(score.Stats);
    }
}
