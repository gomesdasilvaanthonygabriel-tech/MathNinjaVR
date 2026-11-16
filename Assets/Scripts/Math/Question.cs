using System.Collections.Generic;

public enum Operation { Addition, Subtraction, Multiplication, Division }

public enum Difficulty { Easy, Medium, Hard }

[System.Serializable]
public class Question
{
    public string text;
    public int correctAnswer;
    public List<int> options = new List<int>();
}
