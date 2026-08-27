//using Com.Google.Common.Base;
//using It.Unimi.Dsi.Fastutil.Ints;
//using Org.Antlr.V4.Runtime;
//using Org.Antlr.V4.Runtime.Misc;
//using Org.Jetbrains.Annotations;
//using Org.Puffinbasic.Antlr4;
using Org.Puffinbasic.Error;
//using Java.Util;
//using Java.Util.Concurrent.Atomic;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using static Org.Puffinbasic.Runtime.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Org.Puffinbasic.Antlr;
using System.Threading;
using Antlr4.Runtime.Misc;

namespace Org.Puffinbasic.Parser
{
    public class LinenumberListener : PuffinBasicBaseListener
    {
        public enum ThrowOnDuplicate
        {
            THROW,
            LOG
        }

        private readonly Antlr4.Runtime.ICharStream input;
        private readonly ThrowOnDuplicate throwOnDuplicate;
        private readonly SortedDictionary<int, string> sortedLines;
        private readonly HashSet<string> importFiles;
        int linenum;
        private int numLinenum;
        private int numNoLinenum;
        private int numStmtWithLinenum;
        private string libtag;
        public LinenumberListener(Antlr4.Runtime.ICharStream input, ThrowOnDuplicate throwOnDuplicate)
        {
            if (input == null) throw new ArgumentNullException("input");
            if (throwOnDuplicate == null) throw new ArgumentNullException("throwOnDuplicate");
            this.input = input;
            this.throwOnDuplicate = throwOnDuplicate;
            this.sortedLines = new SortedDictionary<int, string>();
            this.importFiles = new HashSet<string>();
        }

        public virtual bool HasLineNumbers()
        {
            return numLinenum > 0;
        }

        public virtual string GetSortedCode()
        {
            CheckLinenumberMode();
            return String.Join("", sortedLines.Values);
        }

        public virtual HashSet<string> GetImportFiles()
        {
            return importFiles;
        }

        public virtual string GetLibtag()
        {
            return libtag;
        }

        private void CheckLinenumberMode()
        {
            if (numLinenum > 0 && numNoLinenum > 0)
            {
                throw new PuffinBasicSyntaxError("Cannot mix linenumber and no-linenumber mode!");
            }

            if (numNoLinenum > 0)
            {
                if (numStmtWithLinenum > 0)
                {
                    throw new PuffinBasicSyntaxError("GOTO/GOSUB/RETURN linenumber cannot be used in no-linenumber mode!");
                }
            }
        }

        public override void ExitLine(PuffinBasicParser.LineContext ctx)
        {
            string line = input.GetText(new Interval(ctx.Start.StartIndex, ctx.Stop.StopIndex));

            if (ctx.linenum() != null)
            {
                linenum = ParseLinenum(ctx.linenum().DECIMAL().GetText());
                numLinenum++;
            }
            else
            {
                linenum = Interlocked.Increment(ref linenum); // TODO: check if this is even neccessary
                numNoLinenum++;
            }

            if (sortedLines.ContainsKey(linenum))
            {
                var message = "Duplicate line number!" + Environment.NewLine + "OLD:" + Environment.NewLine + line + "NEW:" + Environment.NewLine + line;
                if (throwOnDuplicate == ThrowOnDuplicate.THROW)
                {
                    throw new PuffinBasicSyntaxError(message);
                }
                else
                {
                    Console.Error.WriteLine(message);
                }
            }
            else
                sortedLines.Add(linenum, line);
        }

        public override void ExitGosubstmt(PuffinBasicParser.GosubstmtContext ctx)
        {
            numStmtWithLinenum++;
        }

        public override void ExitGotostmt(PuffinBasicParser.GotostmtContext ctx)
        {
            numStmtWithLinenum++;
        }

        public override void ExitThen(PuffinBasicParser.ThenContext ctx)
        {
            if (ctx.linenum() != null)
            {
                numStmtWithLinenum++;
            }
        }

        public override void ExitElsestmt(PuffinBasicParser.ElsestmtContext ctx)
        {
            if (ctx.linenum() != null)
            {
                numStmtWithLinenum++;
            }
        }

        public override void ExitImportstmt(PuffinBasicParser.ImportstmtContext ctx)
        {
            var filename = Unquote(ctx.filename.STRING().GetText());
            importFiles.Add(filename);
        }

        public override void ExitLibtagstmt(PuffinBasicParser.LibtagstmtContext ctx)
        {
            var tag = Unquote(ctx.tag.STRING().GetText());
            if (libtag == null)
            {
                libtag = tag;
            }
            else
            {
                throw new PuffinBasicRuntimeError(IMPORT_ERROR, "Multiple libtags found: " + tag + ", previous: " + libtag);
            }
        }

        public static int ParseLinenum(string txt)
        {
            try
            {
                return int.Parse(txt);
            }
            catch (FormatException e)
            {
                throw new PuffinBasicSyntaxError("Bad line number: '" + txt + "'");
            }
        }
    }
}

