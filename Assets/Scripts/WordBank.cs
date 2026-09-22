// WordBank.cs
// ---------------------------------------------------------------------------
// The static list of words that zombies can carry, split into three buckets:
//   short (3-4 letters), medium (5-7 letters), long (8-12 letters).
// All words are UPPERCASE A-Z only (no spaces, no punctuation).
//
// PickWord() picks a bucket (explosive zombies: long; normal zombies: about
// 60% short, 40% medium) and tries
// to return a word whose FIRST LETTER is not already used by an alive zombie,
// so that typing a first letter always points at exactly one zombie.
//
// To add words: just add them to the right list below. Keep them uppercase
// and keep each word inside its bucket's length range.
// ---------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;

public static class WordBank
{
    // 3-4 letters: small blast, quick and safe to type.
    private static readonly string[] ShortWords =
    {
        "AXE", "ARM", "BITE", "BONE", "CLAW", "COLD", "DEAD", "DUSK", "EYE", "EVIL",
        "FOG", "FEAR", "GORE", "GRIM", "HOWL", "HUNT", "ICE", "IRON", "JAW", "JOLT",
        "KEY", "KILL", "LIMB", "LURK", "MOAN", "MUD", "NAIL", "NUMB", "OOZE", "OMEN",
        "PIT", "PALE", "QUIT", "RUN", "ROT", "SAW", "SKIN", "TOMB", "TUSK", "URN",
        "UGLY", "VEIN", "VILE", "WAIL", "WORM", "YELL", "YAWN", "ZAP", "ZONE"
    };

    // 5-7 letters: medium blast.
    private static readonly string[] MediumWords =
    {
        "ASHES", "ATTIC", "BLOOD", "BRAINS", "CORPSE", "CREEPY", "DECAY", "DREAD", "EERIE", "ESCAPE",
        "FLESH", "FRIGHT", "GHOUL", "GRAVE", "HAUNT", "HORROR", "INFECT", "IVORY", "JAGGED", "JITTER",
        "KNIFE", "KNUCKLE", "LANTERN", "LURCH", "MORGUE", "MUTANT", "NIGHT", "NOXIOUS", "OUTRUN", "ORPHAN",
        "PLAGUE", "PANIC", "QUIVER", "QUAKE", "RAVEN", "ROTTEN", "SCREAM", "SHADOW", "TERROR", "TOXIC",
        "UNDEAD", "UNHOLY", "VENOM", "VICTIM", "WICKED", "WRAITH", "YONDER", "ZOMBIE", "ZEALOT"
    };

    // 8-12 letters: big blast, but a long commitment while the horde closes in.
    private static readonly string[] LongWords =
    {
        "ABANDONED", "APOCALYPSE", "BARRICADE", "BLACKOUT", "CATASTROPHE", "CEMETERY", "DARKNESS", "DECOMPOSE",
        "EPIDEMIC", "EVACUATE", "FESTERING", "FRANKENSTEIN", "GRAVEYARD", "GRUESOME", "HEADSTONE", "HYSTERIA",
        "INFECTION", "INCUBATE", "JUGGERNAUT", "JEOPARDY", "KIDNAPPED", "LABORATORY", "LUMBERING", "MONSTROUS",
        "MUTATION", "NIGHTMARE", "NECROPOLIS", "OUTBREAK", "OVERWHELM", "PESTILENCE", "PANDEMIC", "QUARANTINE",
        "RESURRECT", "REANIMATED", "SURVIVOR", "SHAMBLING", "TOMBSTONE", "TERRIFIED", "UNDERTAKER", "UNEARTHLY",
        "VENGEANCE", "VIRULENT", "WASTELAND", "WEREWOLF", "YESTERDAY", "ZOMBIFIED"
    };

    // 10-13 letters, hard to spell: only used by the boss's body parts.
    private static readonly string[] BossWords =
    {
        "ANNIHILATION", "ABOMINATION", "BLOODCURDLING", "CATASTROPHIC", "CARNIVOROUS", "DISMEMBERMENT",
        "DECAPITATION", "EXCRUCIATING", "EXTERMINATION", "FOREBODING", "GHASTLINESS", "HALLUCINATION",
        "INSATIABLE", "JABBERWOCKY", "LYCANTHROPY", "MALEVOLENCE", "MONSTROSITY", "NECROMANCER",
        "OBLITERATION", "PUTREFACTION", "PESTILENTIAL", "QUINTESSENCE", "RELENTLESSLY", "SEPULCHRAL",
        "SUPERNATURAL", "TREACHEROUS", "UNSPEAKABLE", "VENGEFULNESS", "WITCHCRAFT", "WRETCHEDNESS"
    };

    // Returns a random hard word for a boss part, with a first letter that is
    // not in usedFirstLetters (the other parts' words), so each part can be
    // targeted by its first letter.
    public static string PickBossWord(List<char> usedFirstLetters)
    {
        string word = PickWordWithUnusedFirstLetter(BossWords, usedFirstLetters);
        if (word != null)
        {
            return word;
        }
        return BossWords[Random.Range(0, BossWords.Length)];
    }

    // Returns a random word for a new zombie.
    // usedFirstLetters = the first letter of every alive zombie's word.
    // explosive = true for an explosive (elite) zombie: it always gets a long word,
    // so its word is always longer than a normal zombie's.
    public static string PickWord(List<char> usedFirstLetters, bool explosive)
    {
        // 1. Pick the length bucket.
        //    Explosive zombie: always long. Normal zombie: 60% short, 40% medium.
        string[] rolledBucket;
        if (explosive)
        {
            rolledBucket = LongWords;
        }
        else if (Random.value < 0.60f)
        {
            rolledBucket = ShortWords;
        }
        else
        {
            rolledBucket = MediumWords;
        }

        // 2. Best case: a word from the rolled bucket with an unused first letter.
        string word = PickWordWithUnusedFirstLetter(rolledBucket, usedFirstLetters);
        if (word != null)
        {
            return word;
        }

        // 3. The rolled bucket has no unused first letter left. A normal zombie
        //    tries the other normal bucket. (An explosive zombie keeps a long word:
        //    it must stay longer than the normal ones.)
        if (!explosive)
        {
            word = PickWordWithUnusedFirstLetter(ShortWords, usedFirstLetters);
            if (word == null)
            {
                word = PickWordWithUnusedFirstLetter(MediumWords, usedFirstLetters);
            }
            if (word != null)
            {
                return word;
            }
        }

        // 4. Every first letter is taken (only with a huge crowd). A duplicate
        //    letter cannot be avoided; typing it will target the NEAREST match.
        return rolledBucket[Random.Range(0, rolledBucket.Length)];
    }

    // Returns a random word from the bucket whose first letter is not in
    // usedFirstLetters, or null if the bucket has no such word.
    private static string PickWordWithUnusedFirstLetter(string[] bucket, List<char> usedFirstLetters)
    {
        List<string> candidates = new List<string>();
        foreach (string word in bucket)
        {
            if (!usedFirstLetters.Contains(word[0]))
            {
                candidates.Add(word);
            }
        }

        if (candidates.Count == 0)
        {
            return null;
        }
        return candidates[Random.Range(0, candidates.Count)];
    }
}
