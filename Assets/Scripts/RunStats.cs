// RunStats.cs
// ---------------------------------------------------------------------------
// The numbers of one play-through, shown on the "You survived" / "You died"
// screen. GameManager counts them while you play.
//
// The grade (S, A, B, C, D) rewards clean typing, staying healthy and
// surviving:
//   points = accuracy (correct keys / all keys typed, in %)
//            - 1 point for every 10 damage taken
//            - 20 points if you died.
//   S = 95+, A = 88+, B = 78+, C = 65+, D = below. No kills at all = D.
// ---------------------------------------------------------------------------
using UnityEngine;

public class RunStats
{
    public int Score;
    public int Kills;
    public int BestCombo;
    public int BiggestMultiKill;  // most enemies killed by one blast / chain reaction
    public int CorrectKeys;
    public int WrongKeys;
    public int DamageTaken;
    public float Seconds;         // time spent playing (pauses not counted)
    public bool Won;              // survived to the end

    // 0 to 100.
    public float Accuracy
    {
        get
        {
            int total = CorrectKeys + WrongKeys;
            return total == 0 ? 100f : 100f * CorrectKeys / total;
        }
    }

    // Typing speed: by convention one "word" is 5 correct keys.
    public float WordsPerMinute
    {
        get
        {
            if (Seconds < 1f)
            {
                return 0f;
            }
            return (CorrectKeys / 5f) / (Seconds / 60f);
        }
    }

    // See the comment at the top of the file.
    public string Grade
    {
        get
        {
            if (Kills == 0)
            {
                return "D";
            }
            float points = Accuracy - DamageTaken / 10f;
            if (!Won)
            {
                points -= 20f;
            }
            if (points >= 95f)
            {
                return "S";
            }
            if (points >= 88f)
            {
                return "A";
            }
            if (points >= 78f)
            {
                return "B";
            }
            if (points >= 65f)
            {
                return "C";
            }
            return "D";
        }
    }
}
