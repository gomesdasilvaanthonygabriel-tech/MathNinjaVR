using System;

/// <summary>
/// Estado de uma partida (C# puro, sem MonoBehaviour, testavel isoladamente).
/// Cada pergunta tem ate MaxChances chances (padrao 3):
///   acerto na 1a chance = +5, na 2a = +3, na 3a = +1 (valores configuraveis);
///   corte errado nao tira pontos: conta 1 erro e gasta a chance; errar na ultima
///   chance encerra a pergunta com 0 pontos.
/// Granada: -5 pontos e +1 granada. Pontuacao nunca abaixo de 0.
/// Fim: vitoria ao atingir TargetScore; derrota ao cortar MaxGrenades granadas.
/// Depois do fim (ou com a pergunta ja encerrada), cortes sao ignorados.
/// </summary>
public class MatchStats
{
    public enum CutResult
    {
        /// <summary>Corte ignorado (partida acabou ou pergunta ja encerrada).</summary>
        Ignored,
        /// <summary>Acertou: pergunta encerrada com LastPoints pontos.</summary>
        Solved,
        /// <summary>Errou, mas ainda ha chances nesta pergunta.</summary>
        WrongTryAgain,
        /// <summary>Errou na ultima chance: pergunta encerrada com 0 pontos.</summary>
        WrongQuestionLost,
        /// <summary>Granada registrada.</summary>
        Grenade
    }

    readonly int[] pointsPerChance;

    public int GrenadePenalty { get; }
    public int TargetScore { get; }
    public int MaxGrenades { get; }
    public int MaxChances => pointsPerChance.Length;

    public int Score { get; private set; }
    public int Hits { get; private set; }
    public int Errors { get; private set; }
    public int GrenadesSliced { get; private set; }
    public bool IsOver { get; private set; }
    public bool Won { get; private set; }

    /// <summary>true entre StartQuestion() e o acerto / ultima chance perdida.</summary>
    public bool QuestionOpen { get; private set; }
    /// <summary>Chance atual da pergunta (1..MaxChances).</summary>
    public int Chance => Math.Min(chanceIndex + 1, MaxChances);
    /// <summary>Pontos que um acerto valeria agora.</summary>
    public int PointsForCurrentChance => QuestionOpen ? pointsPerChance[chanceIndex] : 0;
    /// <summary>Pontos ganhos no ultimo acerto.</summary>
    public int LastPoints { get; private set; }

    int chanceIndex;

    public MatchStats(int firstChancePoints = 5, int secondChancePoints = 3, int thirdChancePoints = 1,
        int grenadePenalty = 5, int targetScore = 20, int maxGrenades = 3)
    {
        pointsPerChance = new[]
        {
            Math.Max(0, firstChancePoints),
            Math.Max(0, secondChancePoints),
            Math.Max(0, thirdChancePoints)
        };
        GrenadePenalty = Math.Max(0, grenadePenalty);
        TargetScore = Math.Max(1, targetScore);
        MaxGrenades = Math.Max(1, maxGrenades);
    }

    /// <summary>Comeca uma pergunta nova na 1a chance.</summary>
    public bool StartQuestion()
    {
        if (IsOver) return false;
        chanceIndex = 0;
        QuestionOpen = true;
        return true;
    }

    /// <summary>Corte na esfera certa.</summary>
    public CutResult RegisterCorrect()
    {
        if (IsOver || !QuestionOpen) return CutResult.Ignored;
        QuestionOpen = false;
        Hits++;
        LastPoints = pointsPerChance[chanceIndex];
        AddScore(LastPoints);
        if (Score >= TargetScore) Finish(true);
        return CutResult.Solved;
    }

    /// <summary>Corte numa esfera errada: gasta uma chance, sem tirar pontos.</summary>
    public CutResult RegisterWrong()
    {
        if (IsOver || !QuestionOpen) return CutResult.Ignored;
        Errors++;
        chanceIndex++;
        if (chanceIndex >= MaxChances)
        {
            chanceIndex = MaxChances - 1;
            QuestionOpen = false;
            return CutResult.WrongQuestionLost;
        }
        return CutResult.WrongTryAgain;
    }

    /// <summary>Corte numa granada (nao mexe na chance).</summary>
    public CutResult RegisterGrenade()
    {
        if (IsOver) return CutResult.Ignored;
        GrenadesSliced++;
        AddScore(-GrenadePenalty);
        if (GrenadesSliced >= MaxGrenades) Finish(false);
        return CutResult.Grenade;
    }

    void AddScore(int delta) => Score = Math.Max(0, Score + delta);

    void Finish(bool won)
    {
        IsOver = true;
        Won = won;
        QuestionOpen = false;
    }
}
