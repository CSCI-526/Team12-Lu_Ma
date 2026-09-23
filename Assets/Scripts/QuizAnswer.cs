// QuizAnswer.cs
// ---------------------------------------------------------------------------
// One answer of the boss quiz: a plain cyan cube floating in front of the
// boss, carrying the answer word (cyan text). The player types it like any
// other word; when the final shot hits, CompleteWord() tells the Boss which
// answer was chosen (see Boss.QuizRound). The Boss creates and removes them.
// ---------------------------------------------------------------------------
using TMPro;
using UnityEngine;

public class QuizAnswer : MonoBehaviour, ITypingTarget
{
    private const float CubeSize = 0.5f;       // metres
    private const float LabelAbove = 0.5f;     // the word sits this far above the cube

    public string Word { get; private set; }
    public string ColoredWord { get; private set; }
    public int TypedCount { get; private set; }
    public bool IsAlive { get; private set; }
    public TMP_Text Label { get; private set; }

    private Boss boss;

    // Makes an answer cube floating at position.
    public static QuizAnswer Create(Vector3 position, string word, Boss owner, HUD hud)
    {
        GameObject cube = Shapes.Block(PrimitiveType.Cube, "QuizAnswer", null, position,
            Vector3.one * CubeSize, Palette.Lit(Palette.QuizAnswer));

        QuizAnswer answer = cube.AddComponent<QuizAnswer>();
        answer.Word = word;
        answer.boss = owner;
        answer.IsAlive = true;
        answer.Label = hud.CreateWordLabel(new Vector2(0.5f, 0f)); // centred above the cube
        answer.Label.color = Palette.WordQuiz;
        answer.RefreshLabel();
        return answer;
    }

    // Removes the cube and its word (the Boss calls this when the question is over).
    public void Remove()
    {
        IsAlive = false;
        if (Label != null)
        {
            Destroy(Label.gameObject);
        }
        Destroy(gameObject);
    }

    // ---- ITypingTarget ----

    public bool IsTargeted
    {
        get { return TypedCount > 0; }
    }

    public bool IsCaseSensitive
    {
        get { return false; }
    }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public Vector3 HitPoint
    {
        get { return transform.position; }
    }

    public Vector3 LabelAnchor
    {
        get { return transform.position + Vector3.up * LabelAbove; }
    }

    public char NextLetter
    {
        get { return Word[TypedCount]; }
    }

    public bool IsWordComplete
    {
        get { return TypedCount >= Word.Length; }
    }

    public void AdvanceProgress()
    {
        TypedCount += 1;
        RefreshLabel();
    }

    public void ResetProgress()
    {
        TypedCount = 0;
        RefreshLabel();
    }

    // The player's final shot hit this answer: it is the chosen one.
    public void CompleteWord()
    {
        if (IsAlive)
        {
            boss.OnQuizAnswer(this);
        }
    }

    // Same look as a zombie's word: stored UPPERCASE, shown in lowercase.
    private void RefreshLabel()
    {
        string shown = Word.ToLowerInvariant();
        ColoredWord = Zombie.TypedColorTag + shown.Substring(0, TypedCount) + "</color>" + shown.Substring(TypedCount);
        Label.text = ColoredWord;
    }
}
