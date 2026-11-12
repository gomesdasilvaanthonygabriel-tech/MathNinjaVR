using UnityEngine;

namespace MathNinja.Core
{
    /// <summary>
    /// Teste temporário: coloque em um GameObject vazio, aperte Play
    /// e veja as perguntas no Console. Valida as regras automaticamente.
    /// </summary>
    public class QuestionGeneratorTester : MonoBehaviour
    {
        [SerializeField] private Operation operation = Operation.Multiplication;
        [SerializeField] private Difficulty difficulty = Difficulty.Easy;
        [SerializeField] private int amount = 20;

        private void Start()
        {
            var gen = new QuestionGenerator();
            int errors = 0;
            int[] positionCount = new int[4];

            for (int i = 0; i < amount; i++)
            {
                Question q = gen.Generate(operation, difficulty);
                Debug.Log($"{q.text}  certa={q.correctAnswer}  opções=[{string.Join(", ", q.options)}]");

                int pos = q.options.IndexOf(q.correctAnswer);
                if (pos >= 0 && pos < 4) positionCount[pos]++;

                errors += Validate(q);
            }

            Debug.Log($"Posição da certa (0..3): [{string.Join(", ", positionCount)}]");
            if (errors == 0) Debug.Log("<color=green>OK: todas as perguntas válidas.</color>");
            else Debug.LogError($"{errors} problema(s) encontrado(s).");
        }

        private int Validate(Question q)
        {
            int problems = 0;
            int expected = difficulty == Difficulty.Easy ? 3 : 4;

            if (q.options.Count != expected) { Debug.LogError($"{q.text}: número de opções errado"); problems++; }
            if (!q.options.Contains(q.correctAnswer)) { Debug.LogError($"{q.text}: certa ausente"); problems++; }

            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (int o in q.options)
            {
                if (o < 0) { Debug.LogError($"{q.text}: opção negativa {o}"); problems++; }
                if (!seen.Add(o)) { Debug.LogError($"{q.text}: opção repetida {o}"); problems++; }
            }
            return problems;
        }
    }
}
