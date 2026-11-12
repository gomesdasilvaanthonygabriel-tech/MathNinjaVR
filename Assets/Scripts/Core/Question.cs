using System.Collections.Generic;

namespace MathNinja.Core
{
    public enum Operation { Addition, Subtraction, Multiplication, Division }
    public enum Difficulty { Easy, Medium, Hard }

    /// <summary>Uma pergunta pronta: texto, resposta certa e opções embaralhadas.</summary>
    public class Question
    {
        public string text;            // ex.: "7 × 8 = ?"
        public int correctAnswer;      // ex.: 56
        public List<int> options;      // ex.: [54, 56, 63, 65], já embaralhada
    }
}
