using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Fatty.Scanners
{
    // ---------------------------------------------------------------------
    // The part of classification decided purely by an item's NAME.
    //
    // Deliberately free of Unity and Valheim types. Everything stat-driven (eitr dominance, reading
    // a drink's granted status effect) stays in FoodScanner, which is where the game types belong.
    // Keeping this half dependency-free means it can be compiled on its own and run against a real
    // Fatty_FoodData.json dump - which matters, because a change here silently re-files hundreds of
    // foods at once and "it built" says nothing about whether it re-filed them CORRECTLY.
    // ---------------------------------------------------------------------
    internal static class FoodWordClassifier
    {
        // Splits PascalCase/camelCase prefab names into whole words (e.g. "PerchSteak" -> "perch",
        // "steak"). Keywords are matched against these words, never against the raw string - a plain
        // string.Contains("tea") matched inside "Steak" and Contains("mead") inside "Meadows",
        // silently miscategorizing solid food as a drink (stealing the one drink slot and blocking
        // real food/drink from being eaten).
        private static readonly Regex WordSplitter = new Regex(@"[A-Z]?[a-z0-9]+|[A-Z0-9]+(?![a-z])", RegexOptions.Compiled);

        // Includes a few whole-word compounds ("meadnog", "alenog", "shroomshake") that fuse a
        // keyword with no internal capital to split on. Suffix matching is OFF for this list (see
        // Matches), so unlike the diet lists these still have to be spelled out.
        private static readonly HashSet<string> DrinkWords = new HashSet<string> { "tea", "coffee", "mead", "ale", "drink", "meadnog", "alenog", "shake", "smoothie", "shroomshake", "beer", "brew", "booze", "perry", "blaand", "nog", "cider", "juice", "infusion", "wine", "mjod", "grog", "elixir", "tonic", "draught", "wort" };

        // INGREDIENTS only - animals and cuts. Preparation words live in MeatPreparationWords and are
        // matched in a separate, later pass; see ClassifyDiet for why that separation is load-bearing.
        private static readonly HashSet<string> MeatWords = new HashSet<string> { "meat", "meatball", "steak", "pork", "sausage", "meatbug", "boar", "svin", "deer", "wolf", "lox", "neck", "serpent", "troll", "bear", "hare", "chicken", "fowl", "fowler", "poultry", "egg", "seeker", "asksvin", "volture", "bonemaw", "drake", "kraken", "ham", "jerky", "salami", "bacon", "entrails", "heart", "liver", "ribs", "larva", "morgen", "ulv", "hakarl", "tentacle" };

        // Cooked-dish words that imply meat ONLY when nothing else in the name says otherwise.
        //
        // These cannot live in MeatWords. Meat is tested first, so "stew" sitting there made
        // TurnipStew, VC_FishStew, VC_MagmafishStew and VC_MushroomStew all Meat - the preparation
        // beat the actual ingredient. Matched in a second pass instead, after every ingredient list
        // has had its say, which is what lets "FierySvinstew" still land on Meat while "TurnipStew"
        // correctly reaches Produce.
        //
        // "soup" is deliberately NOT here: it implies nothing at all (VC_DandelionSoup), so it is
        // better left Unknown than guessed.
        private static readonly HashSet<string> MeatPreparationWords = new HashSet<string> { "stew", "broth", "roast", "goulash", "ragout", "patty" };

        private static readonly HashSet<string> FishWords = new HashSet<string> { "fish", "salmon", "magmafish", "perch", "pike", "trout", "herring", "lutefisk", "rakfisk", "stockfish", "gravlaks", "puffer", "grouper", "tetra", "hafgufa", "anglerfish", "cod", "tuna", "eel" };

        // Confectionery and PREPARED sweets only. Raw fruit lives in ProduceWords instead, and Sweet
        // is tested BEFORE Produce so the preparation wins: "Blueberries" is produce, "BlueberryPie"
        // is a dessert.
        private static readonly HashSet<string> SweetWords = new HashSet<string> { "cake", "sweet", "honey", "cookie", "jam", "jelly", "marmalade", "preserve", "compote", "pie", "tart", "krem", "candy", "sugar", "syrup", "caramel", "toffee", "fudge", "marzipan", "pudding", "custard", "dessert", "muffin", "biscuit", "chocolate", "waffle", "pancake", "sorbet", "eyescream", "icecream" };

        // Vegetables AND fruit, merged into one "Produce" diet - see CategoryManager.BaseCategories.
        // "berry"/"berries" are ordinary keywords here rather than a special case: at 5+ characters
        // they clear MinSuffixLength, so suffix matching picks up raspberry/blueberry/cloudberry/
        // vineberry and any modded berry without enumerating them.
        private static readonly HashSet<string> ProduceWords = new HashSet<string> { "berry", "berries", "turnip", "carrot", "onion", "mushroom", "shroom", "magecap", "vegetable", "veggies", "greens", "root", "beetroot", "beet", "barley", "flax", "seed", "salad", "cabbage", "leek", "herb", "herbal", "bread", "porridge", "dumpling", "omelette", "potato", "pickled", "sap", "fern", "dandelion", "thistle", "nettle", "kale", "spinach", "asparagus", "truffle", "pea", "bean", "corn", "squash", "pumpkin", "fruit", "apple", "pear", "plum", "grape", "fig", "melon", "cherry", "peach", "apricot", "citrus", "lemon", "orange", "banana", "rhubarb", "currant" };

        // ------------------------------------------------------------------
        // Keyword matching, in two passes of increasing tolerance.
        //
        // Exact whole-word matching alone misses two whole classes of real food name, and it misses
        // them SILENTLY - straight into Unknown, which earns no category milestone:
        //
        //   * PLURALS.   "GlazedCarrots" -> "carrots", and the list says "carrot".
        //                "SauteMushrooms" -> "mushrooms", and the list says "mushroom".
        //   * COMPOUNDS  with no internal capital to split on. "Raspberry" is the single word
        //                "raspberry", not "berry". "VikingCupcake" -> "cupcake", not "cake".
        //                "MisthareSupreme" -> "misthare", not "hare". "FierySvinstew" -> "svinstew".
        //
        // The old code handled exactly two instances of this by hand (the "meatbug"/"magmafish"
        // literals, and a berry-only suffix test) which is why it kept recurring: 85 real foods on a
        // fully modded setup sat in Unknown for one of these two reasons.
        //
        // Suffix matching, NOT substring. Substring is what the whole-word rule was written to escape
        // - Contains("tea") matched inside "Steak", Contains("mead") inside "Meadows". Anchoring to
        // the END keeps those rejected ("steak" does not END with "tea") while still catching the
        // compounds, because English compounds put the head noun last.
        // ------------------------------------------------------------------

        // Suffix matching only applies to keywords at least this long. Short ones collide with
        // ordinary words far too easily - "ale" would make Kale a drink, "ham" would make Graham meat.
        private const int MinSuffixLength = 4;

        // Long enough to clear the length test, but still a suffix of some unrelated real word.
        // "Mandrake" is a plant, not a drake; "Share" is not a hare. Both are plausible modded item
        // names, and neither needs suffix matching to be found - "BlackBear"/"MeadowDrake" split on
        // their own capitals and match as whole words anyway.
        private static readonly HashSet<string> SuffixExcluded = new HashSet<string> { "drake", "bear" };

        internal static HashSet<string> SplitWords(params string[] sources)
        {
            var words = new HashSet<string>();
            if (sources == null) return words;

            foreach (string source in sources)
            {
                if (string.IsNullOrEmpty(source)) continue;
                foreach (Match m in WordSplitter.Matches(source))
                    words.Add(m.Value.ToLowerInvariant());
            }

            return words;
        }

        // Exact, then singularised. Handles "carrots"->"carrot", "berries"->"berry",
        // "tomatoes"->"tomato". Both the -s and -es strips are tried, because neither rule alone
        // covers both "sausages"->"sausage" and "tomatoes"->"tomato".
        private static bool MatchesWord(HashSet<string> keywords, string word)
        {
            if (keywords.Contains(word)) return true;
            if (word.Length < 2 || word[word.Length - 1] != 's') return false;

            if (word.Length > 3 && word.EndsWith("ies", System.StringComparison.Ordinal)
                && keywords.Contains(word.Substring(0, word.Length - 3) + "y")) return true;
            if (word.Length > 2 && word.EndsWith("es", System.StringComparison.Ordinal)
                && keywords.Contains(word.Substring(0, word.Length - 2))) return true;

            return keywords.Contains(word.Substring(0, word.Length - 1));
        }

        // allowSuffix is off for DrinkWords and only for DrinkWords. That list carries several short,
        // extremely common English endings - "ale" (Kale), "wine" (Swine), "wort" (any -wort herb) -
        // and a false positive there is far worse than a missed category: it hands the single drink
        // slot to a solid food, which then blocks a real drink from being drunk at all. A missed DIET
        // keyword only ever costs an Unknown.
        private static bool Matches(HashSet<string> words, HashSet<string> keywords, bool allowSuffix = true)
        {
            foreach (string word in words)
                if (MatchesWord(keywords, word)) return true;

            if (!allowSuffix) return false;

            foreach (string word in words)
                foreach (string keyword in keywords)
                {
                    if (keyword.Length < MinSuffixLength || word.Length <= keyword.Length) continue;
                    if (SuffixExcluded.Contains(keyword)) continue;
                    if (word.EndsWith(keyword, System.StringComparison.Ordinal)) return true;
                }

            return false;
        }

        // Returns the diet implied by the name, or null when nothing matched - the caller decides
        // what an unmatched name means (Unknown for solid food, Meat for a drink whose effect was
        // unreadable too).
        internal static string ClassifyDiet(HashSet<string> words)
        {
            // Pass 1 - what it is MADE of. Every ingredient list gets a say before any preparation
            // word does, so the contents of the pot decide the category rather than the pot.
            if (Matches(words, MeatWords)) return "Meat";
            if (Matches(words, FishWords)) return "Fish";

            // Sweet stays ahead of Produce on purpose. That ordering is what separates raw fruit from
            // what is made out of it: "BlueberryPie" hits the whole word "pie" here and stays Sweet,
            // while "Blueberries" falls through to the "berries" keyword and becomes Produce.
            // Flipping these two would drag every fruit dessert into Produce.
            if (Matches(words, SweetWords)) return "Sweet";
            if (Matches(words, ProduceWords)) return "Produce";

            // Pass 2 - a cooked dish whose ingredients are unrecognisable. "VikingStew" and
            // "BoneBroth" name nothing Fatty knows, and meat is the right default for those; but this
            // only runs once "TurnipStew" and "FishStew" have already been claimed above.
            if (Matches(words, MeatPreparationWords)) return "Meat";

            return null;
        }

        internal static bool IsDrink(HashSet<string> words)
        {
            return Matches(words, DrinkWords, allowSuffix: false);
        }
    }
}
