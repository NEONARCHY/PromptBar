using System;
using System.Text;
using System.Globalization;

namespace PromptBar
{
    public static class ScriptTextSanitizer
    {
        private const int SoftLineLimit = 900;

        public static string Clean(string text)
        {
            if (String.IsNullOrEmpty(text))
            {
                return "";
            }

            StringBuilder output = new StringBuilder(text.Length);
            int lineLength = 0;
            bool previousWasNewline = false;
            int consecutiveBlankLines = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];

                if (ch == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    AppendNewline(output, ref lineLength, ref previousWasNewline, ref consecutiveBlankLines);
                    continue;
                }

                if (ch == '\n')
                {
                    AppendNewline(output, ref lineLength, ref previousWasNewline, ref consecutiveBlankLines);
                    continue;
                }

                if (ch == '\t')
                {
                    output.Append("    ");
                    lineLength += 4;
                    previousWasNewline = false;
                    continue;
                }

                if (ch == '\u00A0')
                {
                    ch = ' ';
                }

                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.Control ||
                    category == UnicodeCategory.Format ||
                    category == UnicodeCategory.Surrogate ||
                    category == UnicodeCategory.OtherNotAssigned)
                {
                    continue;
                }

                output.Append(ch);
                lineLength++;
                previousWasNewline = false;
                consecutiveBlankLines = 0;

                if (lineLength >= SoftLineLimit && Char.IsWhiteSpace(ch))
                {
                    AppendNewline(output, ref lineLength, ref previousWasNewline, ref consecutiveBlankLines);
                }
            }

            return output.ToString().Trim();
        }

        private static void AppendNewline(
            StringBuilder output,
            ref int lineLength,
            ref bool previousWasNewline,
            ref int consecutiveBlankLines)
        {
            if (previousWasNewline)
            {
                consecutiveBlankLines++;
                if (consecutiveBlankLines > 1)
                {
                    return;
                }
            }
            else
            {
                consecutiveBlankLines = 0;
            }

            output.Append(Environment.NewLine);
            lineLength = 0;
            previousWasNewline = true;
        }
    }
}
