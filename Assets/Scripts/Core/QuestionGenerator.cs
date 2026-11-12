using System;
using System.Collections.Generic;

namespace MathNinja.Core
{
    /// <summary>
    /// Gera perguntas de acordo com operação e dificuldade.
    /// C# puro (sem Unity), então pode ser testado isoladamente.
    /// </summary>
    public class QuestionGenerator
    {
        private readonly Random rng;

        public QuestionGenerator() { rng = new Random(); }
        public QuestionGenerator(int seed) { rng = new Random(seed); }

        public Question Generate(Operation op, Difficulty diff)
        {
            int a, b, answer;
            string symbol;

            switch (op)
            {
                case Operation.Addition:
                {
                    GetAddSubRange(diff, out int min, out int max);
                    a = rng.Next(min, max + 1);
                    b = rng.Next(min, max + 1);
                    answer = a + b;
                    symbol = "+";
                    break;
                }
                case Operation.Subtraction:
                {
                    GetAddSubRange(diff, out int min, out int max);
                    a = rng.Next(min, max + 1);
                    b = rng.Next(min, max + 1);
                    if (b > a) { int t = a; a = b; b = t; } // maior primeiro: sem negativo
                    answer = a - b;
                    symbol = "-";
                    break;
                }
                case Operation.Multiplication:
                {
                    GetMultRange(diff, out int min, out int max);
                    a = rng.Next(min, max + 1);
                    b = rng.Next(min, max + 1);
                    answer = a * b;
                    symbol = "×";
                    break;
                }
                default: // Division: sorteia divisor e resultado, calcula o dividendo
                {
                    GetDivRanges(diff, out int dMin, out int dMax, out int rMin, out int rMax);
                    b = rng.Next(dMin, dMax + 1);       // divisor
                    answer = rng.Next(rMin, rMax + 1);  // resultado
                    a = b * answer;                     // dividendo
                    symbol = "÷";
                    break;
                }
            }

            int optionCount = diff == Difficulty.Easy ? 3 : 4;

            return new Question
            {
                text = $"{a} {symbol} {b} = ?",
                correctAnswer = answer,
                options = BuildOptions(op, a, b, answer, optionCount)
            };
        }

        // ---------- Faixas por dificuldade ----------

        private static void GetAddSubRange(Difficulty d, out int min, out int max)
        {
            switch (d)
            {
                case Difficulty.Easy:   min = 1;  max = 10;  break;
                case Difficulty.Medium: min = 10; max = 50;  break;
                default:                min = 50; max = 200; break;
            }
        }

        private static void GetMultRange(Difficulty d, out int min, out int max)
        {
            switch (d)
            {
                case Difficulty.Easy:   min = 1; max = 5;  break;
                case Difficulty.Medium: min = 1; max = 10; break;
                default:                min = 6; max = 15; break;
            }
        }

        private static void GetDivRanges(Difficulty d, out int dMin, out int dMax, out int rMin, out int rMax)
        {
            switch (d)
            {
                case Difficulty.Easy:   dMin = 2; dMax = 5;  rMin = 1; rMax = 5;  break;
                case Difficulty.Medium: dMin = 2; dMax = 10; rMin = 1; rMax = 10; break;
                default:                dMin = 2; dMax = 12; rMin = 2; rMax = 15; break;
            }
        }

        // ---------- Respostas erradas ----------

        private List<int> BuildOptions(Operation op, int a, int b, int correct, int count)
        {
            var wrong = new List<int>();

            // Candidatas "plausíveis" (erros típicos), embaralhadas.
            var candidates = new List<int>
            {
                correct + 1, correct - 1,
                correct + 2, correct - 2,
                correct + 10, correct - 10,
                SwapDigits(correct)
            };

            // Tabuada vizinha: 7 × 8 -> 7 × 9, 6 × 8 ...
            if (op == Operation.Multiplication)
            {
                candidates.Add(a * (b + 1));
                candidates.Add(a * (b - 1));
                candidates.Add((a + 1) * b);
                candidates.Add((a - 1) * b);
            }

            Shuffle(candidates);
            foreach (int c in candidates)
            {
                if (wrong.Count >= count - 1) break;
                if (IsValidWrong(c, correct, wrong)) wrong.Add(c);
            }

            // Reserva: se faltou (ex.: respostas pequenas), sorteia perto da certa.
            int spread = 3;
            int guard = 0;
            while (wrong.Count < count - 1)
            {
                int c = correct + rng.Next(-spread, spread + 1);
                if (IsValidWrong(c, correct, wrong)) wrong.Add(c);
                if (++guard % 20 == 0) spread++; // alarga a faixa se estiver difícil achar
            }

            var options = new List<int>(wrong) { correct };
            Shuffle(options);
            return options;
        }

        private static bool IsValidWrong(int value, int correct, List<int> existing)
        {
            return value >= 0 && value != correct && !existing.Contains(value);
        }

        /// <summary>Troca os dois últimos dígitos (56 -> 65). Retorna o mesmo número se não der.</summary>
        private static int SwapDigits(int n)
        {
            string s = n.ToString();
            if (s.Length < 2) return n;
            char[] chars = s.ToCharArray();
            int i = chars.Length - 1;
            char tmp = chars[i]; chars[i] = chars[i - 1]; chars[i - 1] = tmp;
            return int.Parse(new string(chars));
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
