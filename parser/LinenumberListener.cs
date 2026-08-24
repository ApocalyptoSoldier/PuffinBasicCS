using Com.Google.Common.Base;
using It.Unimi.Dsi.Fastutil.Ints;
using Org.Antlr.V4.Runtime;
using Org.Antlr.V4.Runtime.Misc;
using Org.Jetbrains.Annotations;
using Org.Puffinbasic.Antlr4;
using Org.Puffinbasic.Error;
using Java.Util;
using Java.Util.Concurrent.Atomic;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using Org.Puffinbasic.Runtime.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Parser
{
    public class LinenumberListener : PuffinBasicBaseListener
    {
        public enum ThrowOnDuplicate
        {
            THROW,
            LOG
        }

        private readonly AtomicInteger linenumGenerator;
        private readonly CharStream input;
        private readonly ThrowOnDuplicate throwOnDuplicate;
        private readonly Int2ObjectSortedMap<string> sortedLines;
        private readonly HashSet<string> importFiles;
        private int numLinenum;
        private int numNoLinenum;
        private int numStmtWithLinenum;
        private string libtag;
        public LinenumberListener(CharStream input, ThrowOnDuplicate throwOnDuplicate)
        {
            this.linenumGenerator = new AtomicInteger();
            this.input = Preconditions.CheckNotNull(input);
            this.throwOnDuplicate = Preconditions.CheckNotNull(throwOnDuplicate);
            this.sortedLines = new Int2ObjectAVLTreeMap();
            this.importFiles = new LinkedHashSet();
        }

        public virtual bool HasLineNumbers()
        {
            return numLinenum > 0;
        }

        public virtual string GetSortedCode()
        {
            CheckLinenumberMode();
            return String.Join("", sortedLines.Values());
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
            string line = input.GetText(new Interval(ctx.start.GetStartIndex(), ctx.stop.GetStopIndex()));
            int linenum;
            if (ctx.Linenum() != null)
            {
                linenum = ParseLinenum(ctx.Linenum().DECIMAL().GetText());
                numLinenum++;
            }
            else
            {
                linenum = linenumGenerator.IncrementAndGet();
                numNoLinenum++;
            }

            var oldLine = sortedLines.Put(linenum, line);
            if (oldLine != null)
            {
                var message = "Duplicate line number!" + Environment.NewLine() + "OLD:" + Environment.NewLine() + oldLine + "NEW:" + Environment.NewLine() + line;
                if (throwOnDuplicate == ThrowOnDuplicate.THROW)
                {
                    throw new PuffinBasicSyntaxError(message);
                }
                else
                {
                    System.err.Println(message);
                }
            }
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
            if (ctx.Linenum() != null)
            {
                numStmtWithLinenum++;
            }
        }

        public override void ExitElsestmt(PuffinBasicParser.ElsestmtContext ctx)
        {
            if (ctx.Linenum() != null)
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

        static int ParseLinenum(string txt)
        {
            try
            {
                return Integer.ParseInt(txt);
            }
            catch (NumberFormatException e)
            {
                throw new PuffinBasicSyntaxError("Bad line number: '" + txt + "'");
            }
        }
    }
}

