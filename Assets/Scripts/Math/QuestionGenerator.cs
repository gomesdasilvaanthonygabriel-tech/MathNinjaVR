using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gera perguntas conforme o documento base: sem negativos, divisao sempre exata,
/// Facil = 3 respostas, Medio/Dificil = 4, com distratores plausiveis.
/// </summary>
public static class QuestionGenerator
{
    public static Question Generate(Operation op, Difficulty diff)
    {
        int a, b, answer;
        string symbol;

        switch (op)
        {
            case Operation.Addition:
            {
                Range(diff, 1, 10, 10, 50, 50, 200, out int lo, out int hi);
                a = Random.Range(lo, hi + 1);
                b = Random.Range(lo, hi + 1);
                answer = a + b;
                symbol = "+";
                break;
            }
            case Operation.Subtraction:
            {
                Range(diff, 1, 10, 10, 50, 50, 200, out int lo, out int hi);
                a = Random.Range(lo, hi + 1);
                b = Random.Range(lo, hi + 1);
                if (b > a) { int t = a; a = b; b = t; }
                answer = a - b;
                symbol = "-";
                break;
            }
            case Operation.Multiplication:
            {
                if (diff == Difficulty.Hard)
                {
                    a = Random.Range(6, 16);
                    b = Random.Range(6, 16);
                }
                else
                {
                    int tableMax = diff == Difficulty.Easy ? 5 : 10;
                    a = Random.Range(1, tableMax + 1);
                    b = Random.Range(1, 11);
                }
                answer = a * b;
                symbol = "×";
                break;
            }
            default: // Division: sorteia divisor e resultado, dividendo = divisor * resultado
            {
                int resMin, resMax, divMin, divMax;
                switch (diff)
                {
                    case Difficulty.Easy: resMin = 1; resMax = 5; divMin = 2; divMax = 5; break;
                    case Difficulty.Medium: resMin = 1; resMax = 10; divMin = 2; divMax = 10; break;
                    default: resMin = 2; resMax = 15; divMin = 2; divMax = 12; break;
                }
                int divisor = Random.Range(divMin, divMax + 1);
                answer = Random.Range(resMin, resMax + 1);
                a = divisor * answer;
                b = divisor;
                symbol = "÷";
                break;
            }
        }

        var q = new Question
        {
            text = a + " " + symbol + " " + b + " = ?",
            correctAnswer = answer
        };

        int optionCount = diff == Difficulty.Easy ? 3 : 4;
        q.options = BuildOptions(op, a, b, answer, optionCount);
        return q;
    }

    static void Range(Difficulty d, int eLo, int eHi, int mLo, int mHi, int hLo, int hHi, out int lo, out int hi)
    {
        switch (d)
        {
            case Difficulty.Easy: lo = eLo; hi = eHi; break;
            case Difficulty.Medium: lo = mLo; hi = mHi; break;
            default: lo = hLo; hi = hHi; break;
        }
    }

    static List<int> BuildOptions(Operation op, int a, int b, int answer, int count)
    {
        var pool = new List<int>();
        int[] offsets = { 1, -1, 2, -2, 10, -10 };
        foreach (int o in offsets) pool.Add(answer + o);

        // troca de digitos (56 -> 65)
        if (answer >= 10)
        {
            string s = answer.ToString();
            char[] c = s.ToCharArray();
            char tmp = c[0]; c[0] = c[1]; c[1] = tmp;
            if (int.TryParse(new string(c), out int swapped)) pool.Add(swapped);
        }

        // tabuada vizinha (7 x 8 -> 7 x 9 / 6 x 8)
        if (op == Operation.Multiplication)
        {
            pool.Add(a * (b + 1)); pool.Add(a * (b - 1));
            pool.Add((a + 1) * b); pool.Add((a - 1) * b);
        }

        var chosen = new List<int> { answer };
        Shuffle(pool);
        foreach (int p in pool)
        {
            if (chosen.Count >= count) break;
            if (p < 0 || chosen.Contains(p)) continue;
            chosen.Add(p);
        }

        // garante a quantidade mesmo se faltarem candidatos
        int guard = 0;
        while (chosen.Count < count && guard++ < 100)
        {
            int p = answer + Random.Range(-12, 13);
            if (p >= 0 && !chosen.Contains(p)) chosen.Add(p);
        }

        Shuffle(chosen);
        return chosen;
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T t = list[i]; list[i] = list[j]; list[j] = t;
        }
    }
}
