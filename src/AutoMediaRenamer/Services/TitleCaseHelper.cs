using System.Text;

namespace AutoMediaRenamer.Services;

public static class TitleCaseHelper
{
    private static readonly HashSet<string> MinorWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the",
        "and", "but", "or", "nor", "for", "so", "yet",
        "at", "by", "for", "from", "in", "into", "of", "off", "on", "onto", "out", "over", "to", "up", "with", "vs", "v"
    };

    private static readonly HashSet<string> RomanNumerals = new(StringComparer.OrdinalIgnoreCase)
    {
        "i", "ii", "iii", "iv", "v", "vi", "vii", "viii", "ix", "x",
        "xi", "xii", "xiii", "xiv", "xv", "xvi", "xvii", "xviii", "xix", "xx"
    };

    private static readonly HashSet<string> KnownAcronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "usa", "uk", "fbi", "cia", "csi", "swat", "tv", "ufo", "us", "la", "nyc", "dc"
    };

    /// <summary>
    /// Converts a raw string (which may have dots, underscores, odd casing) into standardized Title Case.
    /// </summary>
    public static string ToTitleCase(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        // Replace dots, underscores, hyphens, multiple spaces with single space
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            if (c is '.' or '_' or '-' or '\t')
            {
                sb.Append(' ');
            }
            else
            {
                sb.Append(c);
            }
        }

        string normalized = sb.ToString();
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
            return string.Empty;

        var resultWords = new List<string>(words.Length);

        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i].Trim(' ', ':', ';', ',');
            if (string.IsNullOrEmpty(word))
                continue;

            // Check roman numerals
            if (RomanNumerals.Contains(word))
            {
                resultWords.Add(word.ToUpperInvariant());
                continue;
            }

            // Check known acronyms
            if (KnownAcronyms.Contains(word))
            {
                resultWords.Add(word.ToUpperInvariant());
                continue;
            }

            // Check minor words (only lowercased if not the first or last word)
            bool isFirst = resultWords.Count == 0;
            bool isLast = i == words.Length - 1;

            if (!isFirst && !isLast && MinorWords.Contains(word))
            {
                resultWords.Add(word.ToLowerInvariant());
                continue;
            }

            // Capitalize word (handling apostrophes like Don't or O'Connor)
            resultWords.Add(CapitalizeWord(word));
        }

        return string.Join(" ", resultWords);
    }

    private static string CapitalizeWord(string word)
    {
        if (word.Length == 0)
            return word;

        // Handle words starting with non-letter/digit
        int firstLetterIdx = 0;
        while (firstLetterIdx < word.Length && !char.IsLetterOrDigit(word[firstLetterIdx]))
        {
            firstLetterIdx++;
        }

        if (firstLetterIdx >= word.Length)
            return word;

        char[] chars = word.ToCharArray();

        // Make first letter upper
        chars[firstLetterIdx] = char.ToUpperInvariant(chars[firstLetterIdx]);

        // Lowercase the rest unless special surname prefix like O'Connor or after opening bracket
        for (int i = firstLetterIdx + 1; i < chars.Length; i++)
        {
            char prev = chars[i - 1];
            if (prev is '\'' && i == firstLetterIdx + 2 && (chars[firstLetterIdx] is 'O' or 'D'))
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
            }
            else if (prev is '(' or '[')
            {
                chars[i] = char.ToUpperInvariant(chars[i]);
            }
            else
            {
                chars[i] = char.ToLowerInvariant(chars[i]);
            }
        }

        // Special case: Mc/Mac prefixes (e.g., McDonald)
        string res = new string(chars);
        if (res.Length > 2 && res.StartsWith("Mc", StringComparison.Ordinal) && char.IsLetter(res[2]))
        {
            chars[2] = char.ToUpperInvariant(chars[2]);
            return new string(chars);
        }

        return res;
    }
}
