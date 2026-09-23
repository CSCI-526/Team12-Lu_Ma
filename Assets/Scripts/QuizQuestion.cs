// QuizQuestion.cs
// ---------------------------------------------------------------------------
// One boss-quiz question with three typed answers (see QuizBank and Boss).
// Answers are single UPPERCASE words (A-Z only) with three DIFFERENT first
// letters, so the first key always picks exactly one answer.
// ---------------------------------------------------------------------------
public class QuizQuestion
{
    public string Question;     // shown on screen, e.g. "WHAT DO ZOMBIES CRAVE?"
    public string[] Answers;    // 3 answers, already in the order they are shown
    public int CorrectIndex;    // which of Answers is right

    public QuizQuestion(string question, string[] answers, int correctIndex)
    {
        Question = question;
        Answers = answers;
        CorrectIndex = correctIndex;
    }

    public string CorrectAnswer
    {
        get { return Answers[CorrectIndex]; }
    }
}
