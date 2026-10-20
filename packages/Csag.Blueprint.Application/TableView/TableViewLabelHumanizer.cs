namespace Csag.Blueprint.Application.TableView;

using System.Text;

/// <summary>
/// Turns identifier-style names (enum member names) into sentence-case labels:
/// <c>CreditCard</c> becomes "Credit card", <c>SMSCode</c> becomes "SMS code".
/// </summary>
/// <remarks>
/// Words break at a lowercase-to-uppercase change, before the last capital of a capital run that
/// is followed by a lowercase letter, between letters and digits, and at any character that is
/// neither a letter nor a digit (such as <c>_</c>). The first word keeps its casing apart from an
/// upper-cased first letter. Later words are lower-cased unless they consist only of capitals or
/// only of digits, so acronyms (<c>IBAN</c>) and single-letter suffixes (<c>PlanA</c>) stay intact.
/// </remarks>
internal static class TableViewLabelHumanizer
{
    /// <summary>
    /// Converts an identifier-style name into a sentence-case label.
    /// </summary>
    /// <param name="name">The name to humanize.</param>
    /// <returns>The humanized label, or <paramref name="name"/> unchanged when it contains no letters or digits.</returns>
    internal static string Humanize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var words = SplitWords(name);
        if (words.Count == 0)
        {
            return name;
        }

        var builder = new StringBuilder(name.Length + words.Count);
        var first = words[0];
        builder.Append(char.ToUpperInvariant(first[0])).Append(first, 1, first.Length - 1);

        for (var i = 1; i < words.Count; i++)
        {
            var word = words[i];
            builder.Append(' ').Append(KeepsCasing(word) ? word : word.ToLowerInvariant());
        }

        return builder.ToString();
    }

    private static List<string> SplitWords(string name)
    {
        var words = new List<string>();
        var start = -1;

        for (var i = 0; i < name.Length; i++)
        {
            if (!char.IsLetterOrDigit(name[i]))
            {
                if (start >= 0)
                {
                    words.Add(name[start..i]);
                    start = -1;
                }

                continue;
            }

            if (start < 0)
            {
                start = i;
                continue;
            }

            if (IsWordStart(name, i))
            {
                words.Add(name[start..i]);
                start = i;
            }
        }

        if (start >= 0)
        {
            words.Add(name[start..]);
        }

        return words;
    }

    /// <summary>
    /// Determines whether the character at <paramref name="index"/> starts a new word, given that
    /// the preceding character is a letter or digit of the current word.
    /// </summary>
    private static bool IsWordStart(string name, int index)
    {
        var previous = name[index - 1];
        var current = name[index];

        if (char.IsDigit(previous) != char.IsDigit(current))
        {
            return true;
        }

        if (char.IsLower(previous) && char.IsUpper(current))
        {
            return true;
        }

        // The last capital of a run that continues in lowercase begins the next word: "SMSCode".
        return char.IsUpper(previous)
            && char.IsUpper(current)
            && index + 1 < name.Length
            && char.IsLower(name[index + 1]);
    }

    private static bool KeepsCasing(string word)
    {
        var allUpper = true;
        var allDigits = true;
        foreach (var character in word)
        {
            allUpper &= char.IsUpper(character);
            allDigits &= char.IsDigit(character);
        }

        return allUpper || allDigits;
    }
}
