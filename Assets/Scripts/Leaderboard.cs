// Leaderboard.cs
// ---------------------------------------------------------------------------
// The six best scores, with the players' names, kept on this computer between
// games (PlayerPrefs). The end-of-game leaderboard page (HUD) shows them.
//
//   Leaderboard.Entries      the board, best first (at most Size entries)
//   Leaderboard.Add(n, s)    puts a new score on the board; returns its rank
//                            (1 = best), or 0 if it is not good enough
//
// Saved as one text: a line "name<TAB>score" per entry.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class Leaderboard
{
    public const int Size = 6;             // places on the board
    public const int MaxNameLength = 12;
    private const string SaveKey = "Leaderboard";

    public struct Entry
    {
        public string Name;
        public int Score;
    }

    private static List<Entry> entries;    // loaded on first use

    public static List<Entry> Entries
    {
        get
        {
            if (entries == null)
            {
                Load();
            }
            return entries;
        }
    }

    // Would this score get a place on the board?
    public static bool Qualifies(int score)
    {
        return Entries.Count < Size || score > Entries[Entries.Count - 1].Score;
    }

    // Adds the score (a tie goes below the older entry) and saves the board.
    // Returns the rank it got (1..Size), or 0 when it did not make the board.
    public static int Add(string name, int score)
    {
        List<Entry> board = Entries;
        int index = 0;
        while (index < board.Count && board[index].Score >= score)
        {
            index += 1;
        }
        if (index >= Size)
        {
            return 0;
        }

        board.Insert(index, new Entry { Name = CleanName(name), Score = score });
        if (board.Count > Size)
        {
            board.RemoveRange(Size, board.Count - Size);
        }
        Save();
        return index + 1;
    }

    // Letters, digits, spaces and a few symbols; never empty.
    public static string CleanName(string name)
    {
        string clean = "";
        foreach (char character in name ?? "")
        {
            if (!char.IsControl(character) && clean.Length < MaxNameLength)
            {
                clean += character;
            }
        }
        clean = clean.Trim();
        return clean.Length > 0 ? clean : "PLAYER";
    }

    private static void Load()
    {
        entries = new List<Entry>();
        string saved = PlayerPrefs.GetString(SaveKey, "");
        foreach (string line in saved.Split('\n'))
        {
            string[] parts = line.Split('\t');
            int score;
            if (parts.Length == 2 && int.TryParse(parts[1], out score))
            {
                entries.Add(new Entry { Name = parts[0], Score = score });
            }
        }
        entries.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (entries.Count > Size)
        {
            entries.RemoveRange(Size, entries.Count - Size);
        }
    }

    private static void Save()
    {
        List<string> lines = new List<string>();
        foreach (Entry entry in entries)
        {
            lines.Add(entry.Name + "\t" + entry.Score);
        }
        PlayerPrefs.SetString(SaveKey, string.Join("\n", lines));
        PlayerPrefs.Save();
    }
}
