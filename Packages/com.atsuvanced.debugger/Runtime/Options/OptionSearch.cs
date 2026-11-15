using System;
using System.Collections.Generic;

namespace Atsuvanced.Debugger
{
    public readonly struct OptionSearchResult
    {
        public readonly OptionDefinition Option;

        /// <summary>Higher is a better match.</summary>
        public readonly int Score;

        public OptionSearchResult(OptionDefinition option, int score)
        {
            Option = option;
            Score = score;
        }
    }

    /// <summary>
    ///     Incremental search over options.
    ///     The query is split on whitespace; every term must match the option name, member name, description
    ///     group label or any category segment (AND search). Matching is normalized by <see cref="SearchText" />.
    /// </summary>
    public static class OptionSearch
    {
        public const int ScoreNamePrefix = 100;
        public const int ScoreNameContains = 60;
        public const int ScoreMemberContains = 50;
        public const int ScoreGroupContains = 40;
        public const int ScoreCategoryPrefix = 30;
        public const int ScoreCategoryContains = 20;
        public const int ScoreDescriptionContains = 10;

        private static readonly char[] Whitespace = { ' ', '\t', '\n', '\r' };

        public static string[] ParseTerms(string query)
        {
            var normalized = SearchText.Normalize(query ?? string.Empty);
            return normalized.Split(Whitespace, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        ///     Fills <paramref name="results" /> (cleared first) with matches, best first.
        ///     An empty query matches nothing; the UI should show the normal tree in that case.
        /// </summary>
        public static void Search(IEnumerable<OptionDefinition> options, string query, List<OptionSearchResult> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();

            var terms = ParseTerms(query);
            if (terms.Length == 0 || options == null)
            {
                return;
            }

            foreach (var option in options)
            {
                var score = Score(option, terms);
                if (score > 0)
                {
                    results.Add(new OptionSearchResult(option, score));
                }
            }

            results.Sort((a, b) =>
            {
                var c = b.Score.CompareTo(a.Score);
                if (c != 0)
                {
                    return c;
                }

                c = string.CompareOrdinal(a.Option.Category, b.Option.Category);
                return c != 0 ? c : a.Option.SortPriority.CompareTo(b.Option.SortPriority);
            });
        }

        /// <summary>Returns 0 if any term does not match.</summary>
        public static int Score(OptionDefinition option, string[] terms)
        {
            var keys = option.SearchKeys;
            var total = 0;
            foreach (var term in terms)
            {
                var s = ScoreTerm(keys, term);
                if (s == 0)
                {
                    return 0;
                }

                total += s;
            }

            return total;
        }

        private static int ScoreTerm(OptionDefinition.SearchKeySet keys, string term)
        {
            if (keys.Name.StartsWith(term, StringComparison.Ordinal))
            {
                return ScoreNamePrefix;
            }

            if (keys.Name.IndexOf(term, StringComparison.Ordinal) >= 0)
            {
                return ScoreNameContains;
            }

            if (keys.Member.IndexOf(term, StringComparison.Ordinal) >= 0)
            {
                return ScoreMemberContains;
            }

            if (keys.Group.IndexOf(term, StringComparison.Ordinal) >= 0)
            {
                return ScoreGroupContains;
            }

            var best = 0;
            foreach (var segment in keys.CategorySegments)
            {
                if (segment.StartsWith(term, StringComparison.Ordinal))
                {
                    return ScoreCategoryPrefix;
                }

                if (segment.IndexOf(term, StringComparison.Ordinal) >= 0)
                {
                    best = ScoreCategoryContains;
                }
            }

            if (best > 0)
            {
                return best;
            }

            // Allows "player/hp"-style terms that span segments.
            if (term.IndexOf(CategoryPath.Separator) >= 0 &&
                keys.FullPath.IndexOf(term, StringComparison.Ordinal) >= 0)
            {
                return ScoreCategoryContains;
            }

            if (keys.Description.IndexOf(term, StringComparison.Ordinal) >= 0)
            {
                return ScoreDescriptionContains;
            }

            return 0;
        }
    }
}