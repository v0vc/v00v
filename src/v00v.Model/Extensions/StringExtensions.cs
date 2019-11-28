using System;
using System.IO;
using System.Text;

namespace v00v.Model.Extensions
{
    public static class StringExtensions
    {
        #region Constants

        private const string Newline = "\r\n";

        #endregion

        #region Static Methods

        public static string ArrangeToUi(this string text)
        {
            return text.PadRight(40 - text.Length, ' ');
        }

        public static string FilterWhiteSpaces(this string input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            var stringBuilder = new StringBuilder(input.Length);
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                if (i == 0 || !char.IsWhiteSpace(c) || char.IsWhiteSpace(c) && !char.IsWhiteSpace(input[i - 1]))
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString();
        }

        public static string RemoveInvalidChars(this string filename)
        {
            return string.Concat(filename.Split(Path.GetInvalidPathChars())).Replace(":", string.Empty);
        }

        public static string RemoveNewLine(this string str)
        {
            var sb = new StringBuilder(str.Length);

            foreach (char i in str)
            {
                if (i != '\n' && i != '\r' && i != '\t')
                {
                    sb.Append(i);
                }
            }

            return sb.ToString();
        }

        public static string RemoveSpecialCharacters(this string str)
        {
            var sb = new StringBuilder(str.Length);
            foreach (char c in str)
            {
                if (c >= '0' && c <= '9' || c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= 'а' && c <= 'я' || c >= 'А' && c <= 'Я'
                    || c == '.' || c == '_' || c == '-' || c == '+' || c == '|' || c == ' ' || c == '"' || c == '|' || c == ','
                    || c == '!' || c == '?' || c == '(' || c == ')' || c == '<' || c == '>' || c == '%' || c == '#' || c == '@'
                    || c == '&' || c == '*' || c == ':' || c == ';' || c == '^' || c == '=' || c == 'Ё' || c == 'ё' || c == '–'
                    || c == '/')
                {
                    sb.Append(c);
                }
            }

            return string.Join(" ", sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static string WordWrap(this string theString, int width)
        {
            if (string.IsNullOrEmpty(theString))
            {
                return string.Empty;
            }

            int pos, next;
            var sb = new StringBuilder();

            // Lucidity check
            if (width < 1)
            {
                return theString;
            }

            // Parse each line of text
            for (pos = 0; pos < theString.Length; pos = next)
            {
                int eol = theString.IndexOf(Newline, pos, StringComparison.Ordinal);

                next = eol == -1 ? eol = theString.Length : eol + Newline.Length;

                if (eol > pos)
                {
                    do
                    {
                        int len = eol - pos;

                        if (len > width)
                        {
                            len = BreakLine(theString, pos, width);
                        }

                        sb.Append(theString, pos, len);
                        sb.Append(Newline);

                        pos += len;

                        while (pos < eol && char.IsWhiteSpace(theString[pos]))
                        {
                            pos++;
                        }
                    }
                    while (eol > pos);
                }
                else sb.Append(Newline); // Empty line
            }

            return sb.ToString();
        }

        private static int BreakLine(string text, int pos, int max)
        {
            // Find last whitespace in line
            int i = max - 1;
            while (i >= 0 && !char.IsWhiteSpace(text[pos + i]))
            {
                i--;
            }

            if (i < 0)
            {
                return max; // No whitespace found; break at maximum length
            }

            // Find start of whitespace
            while (i >= 0 && char.IsWhiteSpace(text[pos + i]))
            {
                i--;
            }

            // Return length of text before whitespace
            return i + 1;
        }

        #endregion
    }
}
