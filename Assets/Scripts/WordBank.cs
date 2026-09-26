// WordBank.cs
// ---------------------------------------------------------------------------
// The static list of words that zombies can carry, split into three buckets:
//   short (3-4 letters), medium (5-7 letters), long (8-12 letters).
// All words are UPPERCASE A-Z only (no spaces, no punctuation); the game shows
// them in lowercase (see Zombie.RefreshLabel). Orb words are the exception.
//
// PickWord() picks a bucket by zombie kind (explosive: long; runner: short;
// armored: medium; normal: about 60% short, 40% medium) and tries to return a
// word whose FIRST LETTER is not already used by anything on screen, so that
// typing a first letter always points at exactly one target.
// Barrels, supply crates and armour have their own word lists further down.
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
        "AXE", "ARM", "ACID", "ACHE", "ASH", "BITE", "BONE", "BAT", "BURN", "BUG",
        "CLAW", "COLD", "CAGE", "CULT", "CRY", "CUT", "DEAD", "DUSK", "DOOM", "DIG",
        "DIRE", "DARK", "EYE", "EVIL", "EXIT", "EEL", "FOG", "FEAR", "FANG", "FLEE",
        "FIRE", "FOUL", "GORE", "GRIM", "GASP", "GUT", "GUN", "HOWL", "HUNT", "HEX",
        "HIDE", "HOLE", "HURT", "ICE", "IRON", "ILL", "INK", "ITCH", "JAW", "JOLT",
        "JINX", "JAIL", "JUMP", "KEY", "KILL", "KNOT", "KIN", "LIMB", "LURK", "LAIR",
        "LOST", "LEG", "MOAN", "MUD", "MAIM", "MOB", "MIST", "MOLD", "NAIL", "NUMB",
        "NECK", "NUKE", "OOZE", "OMEN", "ODD", "OGRE", "OWL", "PIT", "PALE", "PAIN",
        "PREY", "PUS", "QUIT", "QUIZ", "RUN", "ROT", "RAGE", "RAT", "RIP", "RUIN",
        "SAW", "SKIN", "SCAR", "SLAY", "SOUL", "TOMB", "TUSK", "TAR", "TEAR", "TOIL",
        "URN", "UGLY", "URGE", "UNDO", "VEIN", "VILE", "VOID", "VEX", "VAT", "WAIL",
        "WORM", "WAR", "WEB", "WOE", "WAKE", "XRAY", "YELL", "YAWN", "YELP", "YUCK",
        "ZAP", "ZONE", "ZERO", "ZEST",
        "GOD" // (word family: GOD -> GODLESS)
    };

    // 5-7 letters: medium blast.
    private static readonly string[] MediumWords =
    {
        "ASHES", "ATTIC", "ABYSS", "AGONY", "ALARM", "BLOOD", "BRAINS", "BEAST", "BLEED", "BUNKER",
        "CORPSE", "CREEPY", "CURSE", "CRYPT", "DECAY", "DREAD", "DAGGER", "DEMON", "DOOMED", "EERIE",
        "ESCAPE", "EMBERS", "ENRAGED", "FLESH", "FRIGHT", "FAMINE", "FERAL", "FEVER", "GHOUL", "GRAVE",
        "GALLOWS", "GROWL", "GRISLY", "HAUNT", "HORROR", "HATCHET", "HUNGER", "HOLLOW", "INFECT", "IVORY",
        "INFERNO", "ISOLATE", "JAGGED", "JITTER", "JACKAL", "JUNGLE", "KNIFE", "KNUCKLE", "KILLER", "KRAKEN",
        "LANTERN", "LURCH", "LEECH", "LUNATIC", "MORGUE", "MUTANT", "MAGGOT", "MANGLE", "MUMMY", "NIGHT",
        "NOXIOUS", "NOOSE", "NUCLEAR", "OUTRUN", "ORPHAN", "OMINOUS", "OUTCAST", "PLAGUE", "PANIC", "PHANTOM",
        "POISON", "PROWL", "QUIVER", "QUAKE", "QUEASY", "RAVEN", "ROTTEN", "RABID", "RAMPAGE", "REAPER",
        "SCREAM", "SHADOW", "SEVER", "SKULL", "SPIDER", "SPECTER", "TERROR", "TOXIC", "TALON", "THROAT",
        "TWITCH", "UNDEAD", "UNHOLY", "UNSEEN", "UPROAR", "VENOM", "VICTIM", "VAMPIRE", "VULTURE", "WICKED",
        "WRAITH", "WARDEN", "WITHER", "YONDER", "YEARN", "ZOMBIE", "ZEALOT", "ZIGZAG",
        // Longer forms of short words, so WORD CHAINS happen (typing "hunter" also kills "hunt"):
        "GODLESS", "HEXED", "RIPPER", "DEADLY", "HUNTER", "LURKER", "PAINFUL", "SLAYER", "GUTTED",
        "HOWLING", "WAILING", "MOANING", "BURNING", "SCARRED"
    };

    // 8-12 letters: big blast, but a long commitment while the horde closes in.
    private static readonly string[] LongWords =
    {
        "ABANDONED", "APOCALYPSE", "AFTERMATH", "ANGUISHED", "BARRICADE", "BLACKOUT", "BLOODBATH", "BLOODSHED",
        "BONEYARD", "CATASTROPHE", "CEMETERY", "CANNIBAL", "CHAINSAW", "CONTAGION", "CREMATORY", "DARKNESS",
        "DECOMPOSE", "DEVASTATE", "DESOLATION", "DREADFUL", "EPIDEMIC", "EVACUATE", "ERUPTION", "EXORCISM",
        "ENDANGERED", "FESTERING", "FRANKENSTEIN", "FEARSOME", "FIREARMS", "GRAVEYARD", "GRUESOME", "GUILLOTINE",
        "GRAVEDIGGER", "HEADSTONE", "HYSTERIA", "HAUNTING", "HELLFIRE", "HORRIFIED", "INFECTION", "INCUBATE",
        "IMMORTAL", "INFESTED", "INVISIBLE", "JUGGERNAUT", "JEOPARDY", "JAILBREAK", "KIDNAPPED", "KEROSENE",
        "KNOCKOUT", "LABORATORY", "LUMBERING", "LETHARGIC", "LIGHTNING", "MONSTROUS", "MUTATION", "MAUSOLEUM",
        "MIDNIGHT", "MOONLIGHT", "NIGHTMARE", "NECROPOLIS", "NOCTURNAL", "NIGHTFALL", "OUTBREAK", "OVERWHELM",
        "OBSIDIAN", "OVERGROWN", "PESTILENCE", "PANDEMIC", "PARANOID", "PHANTASM", "POLTERGEIST", "QUARANTINE",
        "QUICKSAND", "QUAGMIRE", "RESURRECT", "REANIMATED", "RADIATION", "RAMPAGING", "SURVIVOR", "SHAMBLING",
        "SACRIFICE", "SCARECROW", "SKELETON", "TOMBSTONE", "TERRIFIED", "TENTACLES", "TWILIGHT", "UNDERTAKER",
        "UNEARTHLY", "UNDERWORLD", "UNDERGROUND", "VENGEANCE", "VIRULENT", "VAMPIRIC", "VIGILANTE", "WASTELAND",
        "WEREWOLF", "WANDERING", "WHISPERING", "XENOMORPH", "YESTERDAY", "YIELDING", "ZOMBIFIED"
    };

    // 10-13 letters, hard to spell: only used by the boss's body parts.
    private static readonly string[] BossWords =
    {
        "ANNIHILATION", "ABOMINATION", "ARMAGEDDON", "BLOODCURDLING", "BLOODTHIRSTY", "BONECHILLING",
        "CATASTROPHIC", "CARNIVOROUS", "CANNIBALISM", "CONTAMINATED", "DISMEMBERMENT", "DECAPITATION",
        "DEVASTATION", "DISEMBODIED", "EXCRUCIATING", "EXTERMINATION", "EVISCERATION", "FOREBODING",
        "FRIGHTENING", "GHASTLINESS", "GARGANTUAN", "HALLUCINATION", "HORRIFYING", "INSATIABLE",
        "INCINERATION", "INFESTATION", "JABBERWOCKY", "JEOPARDIZED", "KNUCKLEBONES", "LYCANTHROPY",
        "LABYRINTHINE", "MALEVOLENCE", "MONSTROSITY", "MUTILATION", "MORTIFICATION", "NECROMANCER",
        "NIGHTMARISH", "NETHERWORLD", "OBLITERATION", "OMNIPOTENT", "OVERWHELMING", "PUTREFACTION",
        "PESTILENTIAL", "PANDEMONIUM", "PARANORMAL", "QUINTESSENCE", "QUARANTINED", "RELENTLESSLY",
        "RESURRECTION", "REANIMATION", "SEPULCHRAL", "SUPERNATURAL", "SLAUGHTERED", "SPINECHILLING",
        "TREACHEROUS", "TERRIFYING", "THUNDERSTORM", "UNSPEAKABLE", "UNSTOPPABLE", "UNFORGIVING",
        "VENGEFULNESS", "VICIOUSNESS", "VULNERABLE", "WITCHCRAFT", "WRETCHEDNESS", "WEREWOLVES",
        "ZOMBIFICATION"
    };

    // 3-6 characters, for the boss's energy orbs. Short, but typed EXACTLY:
    // they mix upper and lower case, digits and symbols. Never use '<' or '>'
    // (TextMeshPro would read them as rich-text tags) or spaces.
    private static readonly string[] OrbWords =
    {
        "Zap!", "Hex#", "B00m", "Fry!", "Rot?", "@Burn", "#Doom", "$Void", "Kr4ck", "Sp@rk",
        "Fl4re", "V0lt", "!Hit", "&Fear", "*Ash", "%Rip", "Nuk3", "Sh0ck", "Ion+", "Jolt!",
        "Qu@ke", "Wisp~", "Emb3r", "T0xic", "Omen!", "Pyr0", "Lava*", "Mana=", "Ray^", "EMP!",
        "Xen0n", "yELL", "dOOm", "cRaCk", "gl0w", "uRGe", "HeX", "Z4p", "k!LL", "BuRn!"
    };
    // (No orb word may contain '1' or '2': those keys throw a lure bomb / freeze, see Powers.)

    // Armored zombies: the FIRST word breaks the armour (then the zombie gets a normal word).
    private static readonly string[] ArmorWords =
    {
        "IRON", "STEEL", "PLATE", "BOLT", "RIVET", "SHIELD", "HELMET", "CHAIN", "ANVIL", "TIN",
        "COPPER", "BRASS", "NICKEL", "ZINC", "LEAD", "MAIL", "GUARD", "VISOR", "KNIGHT", "ALLOY",
        "EMBLEM", "FORGE", "WELD", "QUILT", "OAK", "DENT", "HULL", "JOUST", "UNBENT", "YOKE"
    };

    // Explosive barrels (orange words).
    private static readonly string[] BarrelWords =
    {
        "FUEL", "GAS", "OIL", "TNT", "BOOM", "BLAST", "NAPALM", "PROPANE", "DIESEL", "KABOOM",
        "PETROL", "METHANE", "NITRO", "BUTANE", "KEROSENE", "DYNAMITE", "HAZARD", "IGNITE",
        "VOLATILE", "WARNING", "ACID", "JETFUEL", "LIGHTER", "ETHANOL", "RUMBLE", "SPARK",
        "UNSTABLE", "CRUDE", "ZAPPER", "YIKES", "QUAKE"
    };

    // Supply crates (green words). The word tells you what is inside.
    private static readonly string[] HealthCrateWords =
    {
        "MEDKIT", "BANDAGE", "HEALTH", "FIRSTAID", "PILLS", "TONIC", "REMEDY", "CURE", "VITAMIN",
        "SPLINT", "GAUZE", "ANTIDOTE", "ELIXIR", "OINTMENT", "NURSE", "DOCTOR", "LIFE", "KIT"
    };
    private static readonly string[] LureCrateWords =
    {
        "LURE", "BAIT", "DECOY", "SIREN", "MAGNET", "WHISTLE", "CHUM", "TRAP", "HONEY", "RATTLE",
        "GRENADE", "BOMB", "ALARM", "NOISE", "FIRECRACKER", "PINATA", "UKULELE", "EGGTIMER"
    };
    private static readonly string[] FreezeCrateWords =
    {
        "FREEZE", "ICE", "FROST", "CHILL", "COLD", "BLIZZARD", "SNOW", "POLAR", "GLACIER", "WINTER",
        "ARCTIC", "ICICLE", "TUNDRA", "HAIL", "SLEET", "KELVIN", "YETI", "DEEPFREEZE", "NORTH", "VORTEX"
    };

    // Returns a random word for an explosive barrel (first letter avoids usedFirstLetters if possible).
    public static string PickBarrelWord(List<char> usedFirstLetters)
    {
        return PickFrom(BarrelWords, usedFirstLetters);
    }

    // Returns a random word for a supply crate of this kind.
    public static string PickCrateWord(SupplyKind kind, List<char> usedFirstLetters)
    {
        if (kind == SupplyKind.Health)
        {
            return PickFrom(HealthCrateWords, usedFirstLetters);
        }
        if (kind == SupplyKind.Lure)
        {
            return PickFrom(LureCrateWords, usedFirstLetters);
        }
        return PickFrom(FreezeCrateWords, usedFirstLetters);
    }

    // Returns the first word of an armored zombie (breaks its armour).
    public static string PickArmorWord(List<char> usedFirstLetters)
    {
        return PickFrom(ArmorWords, usedFirstLetters);
    }

    // A word from list with an unused first letter if there is one, otherwise any word from list.
    private static string PickFrom(string[] list, List<char> usedFirstLetters)
    {
        string word = PickWordWithUnusedFirstLetter(list, usedFirstLetters);
        if (word != null)
        {
            return word;
        }
        return list[Random.Range(0, list.Length)];
    }

    // Returns a random energy-orb word whose first character (ignoring case) is
    // not in usedFirstLetters, so typing it never picks a boss part by mistake.
    // usedFirstLetters must be UPPERCASE (fold with char.ToUpperInvariant).
    public static string PickOrbWord(List<char> usedFirstLetters)
    {
        List<string> candidates = new List<string>();
        foreach (string word in OrbWords)
        {
            if (!usedFirstLetters.Contains(char.ToUpperInvariant(word[0])))
            {
                candidates.Add(word);
            }
        }

        if (candidates.Count == 0)
        {
            return OrbWords[Random.Range(0, OrbWords.Length)];
        }
        return candidates[Random.Range(0, candidates.Count)];
    }

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

    // Returns a random word for a new zombie of this kind.
    // usedFirstLetters = the first letter of every word that can be typed right now.
    // An explosive zombie always gets a long word (a long word = a big blast), a
    // runner a short one (it is fast, so it must be quick to type).
    public static string PickWord(List<char> usedFirstLetters, ZombieKind kind)
    {
        bool explosive = kind == ZombieKind.Explosive;

        // 1. Pick the length bucket.
        string[] rolledBucket;
        if (explosive)
        {
            rolledBucket = LongWords;
        }
        else if (kind == ZombieKind.Runner)
        {
            rolledBucket = ShortWords;
        }
        else if (kind == ZombieKind.Armored)
        {
            rolledBucket = MediumWords;
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

    // WORD CHAIN PAIRS: a short word and a longer word that starts with it
    // ("HUNT" and "HUNTER"), both normal-zombie words (short / medium lists),
    // whose first letter nothing on screen uses. Typing the long word kills
    // both (see TypingController). Returns null if no pair fits.
    public static string[] PickChainPair(List<char> usedFirstLetters)
    {
        List<string> words = new List<string>(ShortWords);
        words.AddRange(MediumWords);

        List<string[]> pairs = new List<string[]>();
        foreach (string shortWord in words)
        {
            if (usedFirstLetters.Contains(shortWord[0]))
            {
                continue;
            }
            foreach (string longWord in words)
            {
                if (longWord.Length > shortWord.Length && longWord.StartsWith(shortWord))
                {
                    pairs.Add(new[] { shortWord, longWord });
                }
            }
        }

        if (pairs.Count == 0)
        {
            return null;
        }
        return pairs[Random.Range(0, pairs.Count)];
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
