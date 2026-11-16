using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla a rodada: sorteia uma pergunta e lanca as esferas para cima com giro leve.
/// A MESMA pergunta continua ate o jogador cortar a esfera correta. Cortes errados
/// eliminam aquela esfera; se todas as esferas caem, as restantes sao relancadas.
/// A posicao do objeto e o ponto central de onde as esferas saem.
/// </summary>
public class AnswerSpawner : MonoBehaviour
{
    [Header("Referencias")]
    public AnswerTarget prefab;
    public ScoreManager score;
    [Tooltip("Texto do quadro negro onde a pergunta aparece.")]
    public TextMesh questionText;
    [Tooltip("Texto do quadro para avisos (acertou, errou...).")]
    public TextMesh feedbackText;

    [Header("Lancamento")]
    [Tooltip("Altura maxima (em metros) que a esfera sobe acima do ponto de saida.")]
    public float apexHeight = 2.7f;
    [Tooltip("Largura da faixa horizontal onde as esferas saem.")]
    public float spread = 3f;
    public float jitterX = 0.1f;
    [Tooltip("Quanto a esfera se inclina em direcao ao centro durante o voo.")]
    public float driftToCenter = 0.15f;

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
    bool solved;
    bool cutInWave;

    public Question CurrentQuestion { get; private set; }

    void OnEnable() => AnswerTarget.Sliced += HandleSliced;
    void OnDisable() => AnswerTarget.Sliced -= HandleSliced;

    IEnumerator Start()
    {
        yield return new WaitForSeconds(firstDelay);
        while (true)
        {
            NewQuestion();
            while (!solved)
            {
                LaunchWave();
                // espera todas as esferas da onda sumirem (cortadas ou caidas)
                yield return new WaitUntil(() => solved || AllGone());
                if (solved) break;

                // onda inteira sem nenhum corte conta como uma tentativa
                if (!cutInWave && score != null) score.RegisterMiss();
                SetFeedback("Tente de novo!");
                yield return new WaitForSeconds(relaunchDelay);
            }

            yield return new WaitForSeconds(nextQuestionDelay);
        }
    }

    void NewQuestion()
    {
        solved = false;
        CurrentQuestion = QuestionGenerator.Generate(GameSettings.operation, GameSettings.difficulty);
        remaining.Clear();
        remaining.AddRange(CurrentQuestion.options);
        if (score != null) score.ResetAttempts();
        if (questionText != null) questionText.text = CurrentQuestion.text;
        SetFeedback("");
    }

    bool AllGone()
    {
        current.RemoveAll(t => t == null);
        return current.Count == 0;
    }

    void HandleSliced(AnswerTarget t)
    {
        if (solved) return;
        cutInWave = true;

        if (t.isCorrect)
        {
            solved = true;
            int pts = score != null ? score.RegisterCorrect() : 0;
            if (questionText != null) questionText.text = CurrentQuestion.text.Replace("?", CurrentQuestion.correctAnswer.ToString());
            SetFeedback("Acertou! +" + pts + (pts == 1 ? " ponto" : " pontos"));
            ClearWave(); // remove as outras esferas
        }
        else
        {
            remaining.Remove(t.value);
            if (score != null) score.RegisterWrong();
            SetFeedback("Errou! Continue tentando");
        }
    }

    /// <summary>Lanca as opcoes restantes da pergunta atual.</summary>
    [ContextMenu("Lancar agora")]
    public void LaunchWave()
    {
        if (prefab == null) { Debug.LogWarning("AnswerSpawner: prefab nao definido."); return; }
        if (CurrentQuestion == null) NewQuestion();

        ClearWave();
        cutInWave = false;

        var values = new List<int>(remaining);
        Shuffle(values);

        int n = values.Count;
        float g = Mathf.Abs(Physics.gravity.y) * gravityScale;

        for (int i = 0; i < n; i++)
        {
            float t = n == 1 ? 0.5f : i / (float)(n - 1);
            float x = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t) + Random.Range(-jitterX, jitterX);
            Vector3 pos = transform.position + new Vector3(x, 0f, Random.Range(-0.15f, 0.15f));

            AnswerTarget target = Instantiate(prefab, pos, Random.rotation, transform);
            target.gravityScale = gravityScale;
            int value = values[i];
            target.Setup(value, value == CurrentQuestion.correctAnswer, palette[i % palette.Length]);

            // v = sqrt(2 * g * h) para atingir a altura h
            float h = apexHeight * Random.Range(0.88f, 1f);
            float vy = Mathf.Sqrt(2f * g * h);
            Vector3 velocity = new Vector3(
                -x * driftToCenter + Random.Range(-0.15f, 0.15f),
                vy,
                Random.Range(-0.1f, 0.1f));

            // giro leve em uma direcao qualquer
            Vector3 spin = Random.onUnitSphere * Random.Range(minSpin, maxSpin);

            target.Launch(velocity, spin);
            current.Add(target);
        }
    }

    public void ClearWave()
    {
        foreach (var t in current)
            if (t != null) Destroy(t.gameObject);
        current.Clear();
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
