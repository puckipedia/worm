using System;
using System.Text.RegularExpressions;

namespace worm
{
    public static class HtmlRemoval
    {
        /// <summary>
        /// Remove HTML from string with Regex.
        /// </summary>
        public static string StripTagsRegex(string source)
        {
            return Regex.Replace(source, "<.*?>", string.Empty);
        }

        /// <summary>
        /// Compiled regular expression for performance.
        /// </summary>
        static Regex _htmlRegex = new Regex("<.*?>", RegexOptions.Compiled);

        /// <summary>
        /// Remove HTML from string with compiled Regex.
        /// </summary>
        public static string StripTagsRegexCompiled(string source)
        {
            return _htmlRegex.Replace(source, string.Empty);
        }

        /// <summary>
        /// Remove HTML tags from string using char array.
        /// </summary>
        public static string StripTagsCharArray(string source)
        {
            char[] array = new char[source.Length];
            int arrayIndex = 0;
            bool inside = false;

            for (int i = 0; i < source.Length; i++)
            {
                char let = source[i];
                if (let == '<')
                {
                    inside = true;
                    continue;
                }
                if (let == '>')
                {
                    inside = false;
                    continue;
                }
                if (!inside)
                {
                    array[arrayIndex] = let;
                    arrayIndex++;
                }
            }
            return new string(array, 0, arrayIndex);
        }
    }

    public static class TimeSpanExtensions
    {
        public static int GetYears(this TimeSpan timespan)
        {
            return (int)((double)timespan.Days / 365.2425);
        }
        public static int GetMonths(this TimeSpan timespan)
        {
            return (int)((double)timespan.Days / 30.436875);
        }
        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            // Unix timestamp is seconds past epoch
            System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            dtDateTime = dtDateTime.AddSeconds(unixTimeStamp).ToLocalTime();
            return dtDateTime;
        }
        public static string StripIrcColors(this string value)
        {
            string result = "";
            value = value.Trim().Replace("\u00031\u000315", "");
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\u0003')
                    i++;
                else if (value[i] == '\u000f') { }
                else
                    result += value[i];
            }
            return result;
        }
    }
}
