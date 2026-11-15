using System.Text;

namespace Atsuvanced.Debugger
{
    /// <summary>
    ///     Normalization used by option search: case-insensitive, full-width ASCII -> half-width,
    ///     katakana -> hiragana. Implemented by hand so it behaves identically on IL2CPP / invariant globalization.
    /// </summary>
    public static class SearchText
    {
        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }

            StringBuilder sb = null;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                var n = NormalizeChar(c);
                if (n != c && sb == null)
                {
                    sb = new StringBuilder(s.Length);
                    sb.Append(s, 0, i);
                }

                sb?.Append(n);
            }

            return sb?.ToString() ?? s;
        }

        public static char NormalizeChar(char c)
        {
            if (c >= '！' && c <= '～')
            {
                c = (char)(c - 0xFEE0); // full-width ASCII
            }
            else if (c == '　')
            {
                c = ' '; // ideographic space
            }
            else if (c >= 'ァ' && c <= 'ヶ')
            {
                c = (char)(c - 0x60); // katakana -> hiragana
            }

            return char.ToLowerInvariant(c);
        }
    }
}