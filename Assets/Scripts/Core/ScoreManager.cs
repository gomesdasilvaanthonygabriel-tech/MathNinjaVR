using UnityEngine;

/// <summary>
/// Pontuacao: acerto de 1a tentativa = 5 pontos, 2a = 3 pontos, 3a ou 4a = 1 ponto.
/// "Tentativa" = cada corte errado soma 1; uma onda inteira sem nenhum corte tambem soma 1.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public int firstTryPoints = 5;
    public int secondTryPoints = 3;
    public int laterTryPoints = 1;

    [Header("Textos no quadro")]
    public TextMesh scoreText;

    public int Score { get; private set; }
    /// <summary>Tentativas erradas na pergunta atual (0 = ainda acertaria de primeira).</summary>
    public int Attempts { get; private set; }
    public int CorrectCount { get; private set; }

    void Start() => Refresh();

    public void ResetAttempts() => Attempts = 0;

    public void RegisterWrong() => Attempts++;

    public void RegisterMiss() => Attempts++;

    /// <summary>Soma os pontos conforme a tentativa e devolve quantos foram ganhos.</summary>
    public int RegisterCorrect()
    {
        int points = Attempts == 0 ? firstTryPoints
                   : Attempts == 1 ? secondTryPoints
                   : laterTryPoints;
        Score += points;
        CorrectCount++;
        Refresh();
        return points;
    }

    public void ResetScore()
    {
        Score = 0;
        CorrectCount = 0;
        Attempts = 0;
        Refresh();
    }

    void Refresh()
    {
        if (scoreText != null) scoreText.text = "Pontos: " + Score;
    }
}
