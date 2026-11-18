using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla as rodadas de uma partida: sorteia uma pergunta e lanca as esferas para cima.
/// A MESMA pergunta continua ate o acerto ou ate esgotar as chances (regra no MatchStats).
/// Cortes errados eliminam aquela esfera; se todas caem, as restantes sao relancadas.
/// A cada 3 ou 4 ondas, uma granada sobe junto com as esferas.
/// So roda entre StartMatch() e StopMatch() (chamados pelo GameFlow).
/// A posicao do objeto e o ponto central de onde os alvos saem.
/// </summary>
public class AnswerSpawner : MonoBehaviour
{
    [Header("Referencias")]
    public AnswerTarget prefab;
    public AnswerTarget grenadePrefab;
    public ScoreManager score;
    [Tooltip("Texto do quadro negro onde a pergunta aparece.")]
    public TextMesh questionText;
    [Tooltip("Texto do quadro para avisos (acertou, errou...).")]
    public TextMesh feedbackText;

    [Header("Lancamento")]
    [Tooltip("Altura maxima (em metros) que o alvo sobe acima do ponto de saida.")]
    public float apexHeight = 2.6f;
    [Tooltip("Largura da faixa horizontal onde os alvos saem.")]
    public float spread = 3f;
    public float jitterX = 0.1f;
    [Tooltip("Quanto o alvo se inclina em direcao ao centro durante o voo.")]
    public float driftToCenter = 0.15f;

    [Header("Granada")]
    [Tooltip("Intervalo minimo/maximo (em ondas) entre granadas; sorteado a cada granada.")]
    public int grenadeEveryMin = 3;
    public int grenadeEveryMax = 4;

    [Header("Fisica")]
    [Range(0.2f, 1.5f)] public float gravityScale = 0.7f;
    [Tooltip("Giro leve (rad/s) em direcao aleatoria.")]
    public float minSpin = 1f;
    public float maxSpin = 3f;

    [Header("Ritmo")]
    public float firstDelay = 1.5f;
    [Tooltip("Pausa antes de relancar as esferas quando todas cairam.")]
    public float relaunchDelay = 0.8f;
    [Tooltip("Pausa depois de um acerto, antes da proxima operacao.")]
    public float nextQuestionDelay = 1.6f;

    static readonly Color[] palette =
    {
        new Color(0.95f, 0.35f, 0.30f),
        new Color(0.25f, 0.55f, 0.95f),
        new Color(0.30f, 0.80f, 0.45f),
        new Color(1.00f, 0.60f, 0.15f),
        new Color(0.65f, 0.40f, 0.90f)
    };

    readonly List<AnswerTarget> current = new List<AnswerTarget>();
    readonly List<int> remaining = new List<int>(); // opcoes ainda nao cortadas
    bool running;
    bool solved;
    int wavesUntilGrenade;

    public Question CurrentQuestion { get; private set; }
    public bool IsRunning => running;

    void OnEnable() => AnswerTarget.Sliced += HandleSliced;
    void OnDisable() => AnswerTarget.Sliced -= HandleSliced;

    void OnDestroy()
    {
        AnswerTarget.Sliced -= HandleSliced;
        running = false;
    }

    // ---------------------------------------------------------------- controle

    public void StartMatch()
    {
        StopAllCoroutines();
        ClearWave();
        running = true;
        solved = false;
        CurrentQuestion = null;
        wavesUntilGrenade = NextGrenadeGap();
        SetQuestion("");
        SetFeedback("Prepare-se!");
        StartCoroutine(MatchLoop());
    }

    public void StopMatch()
    {
        running = false;
        StopAllCoroutines();
        ClearWave();
    }

    /// <summary>Texto do quadro fora da partida (menu / fim).</summary>
    public void ShowBoardMessage(string question, string feedback)
    {
        SetQuestion(question);
        SetFeedback(feedback);
    }

    IEnumerator MatchLoop()
    {
        yield return new WaitForSeconds(firstDelay);
        while (running)
        {
            NewQuestion();
            while (running && !solved)
            {
                LaunchWave();
                // espera todos os alvos da onda sumirem (cortados ou caidos) ou o acerto
                yield return new WaitUntil(() => !running || solved || AllGone());
                if (!running) yield break;
                if (solved) break;

                // onda sem acerto: nao muda pontos, so relanca as restantes
                SetFeedback("Tente de novo!");
                yield return new WaitForSeconds(relaunchDelay);
            }
            if (!running) yield break;

            // cortes do mesmo frame do acerto ja foram contados; agora limpa o resto da onda
            ClearWave();
            yield return new WaitForSeconds(nextQuestionDelay);
        }
    }

    void NewQuestion()
    {
        solved = false;
        CurrentQuestion = QuestionGenerator.Generate(GameSettings.operation, GameSettings.difficulty);
        remaining.Clear();
        remaining.AddRange(CurrentQuestion.options);
        if (score != null) score.StartQuestion();
        SetQuestion(CurrentQuestion.text);
        SetFeedback("");
    }

    bool AllGone()
    {
        current.RemoveAll(t => t == null || !t.IsAlive);
        return current.Count == 0;
    }

    int NextGrenadeGap()
    {
        int lo = Mathf.Max(1, grenadeEveryMin);
        int hi = Mathf.Max(lo, grenadeEveryMax);
        return Random.Range(lo, hi + 1);
    }

    // ---------------------------------------------------------------- cortes

    void HandleSliced(AnswerTarget t)
    {
        if (!running || t == null || score == null || CurrentQuestion == null) return;

        if (t.IsGrenade)
        {
            if (score.RegisterGrenade() == MatchStats.CutResult.Grenade && running)
                SetFeedback("BOOM! Granada!");
            return;
        }

        if (solved) return; // a rodada so resolve uma vez (acerto ou ultima chance perdida)

        if (t.isCorrect)
        {
            if (score.RegisterCorrect() != MatchStats.CutResult.Solved) return;
            solved = true;
            if (!running) return;
            ShowAnswer();
            SetFeedback("Acertou! +" + score.Stats.LastPoints);
        }
        else
        {
            remaining.Remove(t.value);
            var r = score.RegisterWrong();
            if (r == MatchStats.CutResult.WrongQuestionLost)
            {
                solved = true;
                if (!running) return;
                ShowAnswer();
                SetFeedback("Errou! Sem chances nesta conta");
            }
            else if (r == MatchStats.CutResult.WrongTryAgain && running)
            {
                SetFeedback("Errou! Proximo acerto vale +" + score.Stats.PointsForCurrentChance);
            }
        }
    }

    void ShowAnswer()
    {
        SetQuestion(CurrentQuestion.text.Replace("?", CurrentQuestion.correctAnswer.ToString()));
    }

    // ---------------------------------------------------------------- lancamento

    /// <summary>Lanca as opcoes restantes da pergunta atual (e, as vezes, uma granada).</summary>
    void LaunchWave()
    {
        if (prefab == null) { Debug.LogWarning("AnswerSpawner: prefab nao definido."); return; }
        if (CurrentQuestion == null) return;

        ClearWave();

        // null = granada
        var slots = new List<int?>();
        foreach (int v in remaining) slots.Add(v);
        Shuffle(slots);

        wavesUntilGrenade--;
        if (wavesUntilGrenade <= 0)
        {
            wavesUntilGrenade = NextGrenadeGap();
            if (grenadePrefab != null) slots.Insert(Random.Range(0, slots.Count + 1), null);
        }

        int n = slots.Count;
        float g = Mathf.Abs(Physics.gravity.y) * gravityScale;
        int colorIdx = 0;

        for (int i = 0; i < n; i++)
        {
            float t = n == 1 ? 0.5f : i / (float)(n - 1);
            float x = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t) + Random.Range(-jitterX, jitterX);
            Vector3 pos = transform.position + new Vector3(x, 0f, Random.Range(-0.15f, 0.15f));

            AnswerTarget target;
            if (slots[i].HasValue)
            {
                target = Instantiate(prefab, pos, Random.rotation, transform);
                int value = slots[i].Value;
                target.Setup(value, value == CurrentQuestion.correctAnswer, palette[colorIdx++ % palette.Length]);
            }
            else
            {
                target = Instantiate(grenadePrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-20f, 20f)), transform);
            }
            target.gravityScale = gravityScale;

            // v = sqrt(2 * g * h) para atingir a altura h
            float h = apexHeight * Random.Range(0.88f, 1f);
            float vy = Mathf.Sqrt(2f * g * h);
            Vector3 velocity = new Vector3(
                -x * driftToCenter + Random.Range(-0.15f, 0.15f),
                vy,
                Random.Range(-0.1f, 0.1f));

            Vector3 spin = Random.onUnitSphere * Random.Range(minSpin, maxSpin);
            if (target.IsGrenade) spin *= 0.5f;

            target.Launch(velocity, spin);
            current.Add(target);
        }
    }

    public void ClearWave()
    {
        foreach (var t in current)
            if (t != null && t.IsAlive) t.Despawn();
        current.Clear();
    }

    void SetQuestion(string msg)
    {
        if (questionText != null) questionText.text = msg;
    }

    void SetFeedback(string msg)
    {
        if (feedbackText != null) feedbackText.text = msg;
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + Vector3.left * spread * 0.5f, transform.position + Vector3.right * spread * 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * apexHeight, 0.15f);
    }
}
