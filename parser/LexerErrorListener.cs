namespace Org.Puffinbasic.Parser
{
    using Antlr4.Runtime;

    using Org.Puffinbasic.Error;

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class LexerErrorListener : IAntlrErrorListener<int>
    {
        private readonly string input;
        public LexerErrorListener(string input) {
            this.input = input;
            }
        public void SyntaxError(TextWriter output, IRecognizer recognizer, int offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
        {
            var lineIndex = line - 1;
            var lines = input.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.None);

            string inputLine;
            if (lineIndex >= 0 && lineIndex < lines.Length)
            {
                inputLine = lines[lineIndex];
                if (charPositionInLine >= 0 && charPositionInLine <= inputLine.Length)
                {
                    inputLine += Environment.NewLine + "^".PadLeft(Math.Max(0, charPositionInLine));
                }
            }
            else
            {
                inputLine = "<LINE OUT OF RANGE>";
            }

            throw new PuffinBasicSyntaxError("[" + line + ":" + charPositionInLine + "] " + msg + Environment.NewLine + inputLine);
        }
    }
}
