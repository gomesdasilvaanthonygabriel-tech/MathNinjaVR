using System;
using UnityEngine;

/// <summary>
/// Ponte entre a cena e o MatchStats: guarda os valores de pontuacao (editaveis no
/// Inspector), cria um MatchStats novo a cada partida e avisa quando algo muda/acaba.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    [Header("Pontuacao por chance")]
    [Tooltip("Acerto na 1a chance (4 esferas).")]
    [SerializeField] int firstChancePoints = 5;
    [Tooltip("Acerto na 2a chance (3 esferas).")]
    [SerializeField] int secondChancePoints = 3;
    [Tooltip("Acerto na 3a chance (2 esferas).")]
    [SerializeField] int thirdChancePoints = 1;
    [SerializeField] int grenadePenalty = 5;

    [Header("Fim de partida")]
    [SerializeField] int targetScore = 20;
    [SerializeField] int maxGrenades = 3;

    /// <summary>Disparado sempre que pontos/contadores/chance mudam.</summary>
    public event Action Changed;
    /// <summary>Disparado uma unica vez por partida. true = vitoria.</summary>
    public event Action<bool> MatchEnded;

    public MatchStats Stats { get; private set; }

    bool endRaised;

    void Awake()
    {
        if (Stats == null) CreateStats(); // sem evento: os ouvintes podem nao estar prontos
    }

    public void ResetMatch()
    {
        CreateStats();
        Changed?.Invoke();
    }

    void CreateStats()
    {
        Stats = new MatchStats(firstChancePoints, secondChancePoints, thirdChancePoints,
            grenadePenalty, targetScore, maxGrenades);
        endRaised = false;
    }

    public bool StartQuestion()
    {
        if (Stats == null) CreateStats();
        bool ok = Stats.StartQuestion();
        if (ok) Changed?.Invoke();
        return ok;
    }

    public MatchStats.CutResult RegisterCorrect() => Apply(Stats.RegisterCorrect());
    public MatchStats.CutResult RegisterWrong() => Apply(Stats.RegisterWrong());
    public MatchStats.CutResult RegisterGrenade() => Apply(Stats.RegisterGrenade());

    MatchStats.CutResult Apply(MatchStats.CutResult result)
    {
        if (result == MatchStats.CutResult.Ignored) return result;
        Changed?.Invoke();
        if (Stats.IsOver && !endRaised)
        {
            endRaised = true;
            MatchEnded?.Invoke(Stats.Won);
        }
        return result;
    }
}
