//using Com.Google.Common.Base;
//using Net.Sourceforge.Argparse4j;
//using Net.Sourceforge.Argparse4j.Impl;
//using Net.Sourceforge.Argparse4j.Inf;
//using Org.Antlr.V4.Runtime;
//using Org.Antlr.V4.Runtime.Tree;
//using Org.Puffinbasic.Antlr4;
namespace Org.Puffinbasic
{
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using Org.Puffinbasic.Domain;
using Org.Puffinbasic.Error;
using Org.Puffinbasic.Parser;
using static Org.Puffinbasic.Parser.LinenumberListener;
using Org.Puffinbasic.Runtime;
using static Org.Puffinbasic.Runtime.IEnvironment;
//using Java.Io;
//using Java.Nio.Charset;
//using Java.Nio.File;
//using Java.Time;
//using Java.Util;
//using Java.Util.Stream;
using static Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using static Org.Puffinbasic.Parser.LinenumberListener.ThrowOnDuplicate;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.CommandLine;
using Org.Puffinbasic.Antlr;

    public sealed class PuffinBasicInterpreterMain
    {
        private static readonly string UNKNOWN_SOURCE_FILE = "<UNKNOWN>";
        private enum SourceFileMode
        {
            MAIN,
            LIB
        }

        public static void Main(params string[] args)
        {
            var userOptions = ParseCommandLineArgs(args);
            string mainSource = userOptions.filename;
            DateTime t0 = DateTime.Now;
            var sourceCode = LoadSource(mainSource);
            LogTimeTaken("LOAD", t0, userOptions.timing);
            InterpretAndRun(userOptions, mainSource, sourceCode, Console.Out, new SystemEnv());
        }

        private static UserOptions ParseCommandLineArgs(params string[] args)
        {
            RootCommand command = new RootCommand();

            Option<bool> logDuplicate = new Option<bool>("-d", "--logduplicate") { Description = "Log error on duplicate"};
            Option<bool> list= new Option<bool>("-l", "--list") { Description = "Print Sorted Source Code"};
            Option<bool> ir = new Option<bool>("-i", "--ir") { Description = "Print IR"};
            Option<bool> timing = new Option<bool>("-t", "--timing") { Description = "Print timing"};
            Option<bool> graphics = new Option<bool>("-g", "--graphics") { Description = "Enable graphics" };

            Option<string> file = new Option<string>("file");

            command.Options.Add(logDuplicate);
            command.Options.Add(list);
            command.Options.Add(ir);
            command.Options.Add(timing);
            command.Options.Add(graphics);

            command.Options.Add(file);

            ParseResult res = command.Parse(args);

            if (res.Errors.Count > 0)
                Environment.Exit(1);

            return new UserOptions(res.GetValue(logDuplicate), res.GetValue(list), res.GetValue(ir), res.GetValue(timing), res.GetValue(graphics), System.IO.Path.GetFullPath(res.GetValue(file)));
            //return new UserOptions(res.GetBoolean("logduplicate"), res.GetBoolean("list"), res.GetBoolean("ir"), res.GetBoolean("timing"), res.GetBoolean("graphics"), (string)res.GetList("file")[0]);
        }

        private static string LoadSource(string filename)
        {
            var sb = new StringBuilder();
            try
            {
                //foreach (string line in System.IO.File.ReadLines(Paths[filename], Encoding.ASCII))
                //    sb.AppendLine(line);
                foreach (string line in System.IO.File.ReadLines(filename))
                    sb.AppendLine(line);
            }
            catch (System.IO.IOException e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to read source code: {filename}, error: {e.Message}");
            }

            return sb.ToString();
        }

        static void InterpretAndRun(UserOptions userOptions, string sourceCode, TextWriter @out, IEnvironment env)
        {
            InterpretAndRun(userOptions, UNKNOWN_SOURCE_FILE, sourceCode, @out, env);
        }

        static void InterpretAndRun(UserOptions userOptions, string sourceFilename, string sourceCode, TextWriter @out, IEnvironment env)
        {
            var importPath = new PuffinBasicImportPath(sourceFilename);
            DateTime t1 = DateTime.Now;
            var sourceFile = SyntaxCheckAndSortByLineNumber(importPath, sourceFilename, sourceCode, userOptions.logOnDuplicate ? LOG : THROW, SourceFileMode.MAIN);
            if (String.IsNullOrEmpty(sourceFile.GetSourceCode()))
            {
                throw new PuffinBasicSyntaxError("Failed to parse source code! Check if a linenumber is missing");
            }

            LogTimeTaken("SORT", t1, userOptions.timing);
            Log("LIST", userOptions.listSourceCode);
            Log(sourceFile.GetSourceCode(), userOptions.listSourceCode);
            DateTime t2 = DateTime.Now;
            var ir = GenerateIR(sourceFile, userOptions.graphics);
            LogTimeTaken("IR", t2, userOptions.timing);
            Log("IR", userOptions.printIR);
            if (userOptions.printIR)
            {
                int i = 0;
                foreach (var instruction in ir.GetInstructions())
                {
                    Log(i++ + ": " + instruction, true);
                }
            }

            Log("RUN", userOptions.timing);
            DateTime t3 = DateTime.Now;
            Run(ir, @out, env);
            LogTimeTaken("RUN", t3, userOptions.timing);
        }

        private static void Log(string s, bool log)
        {
            if (log)
            {
                Console.WriteLine(s);
            }
        }

        private static void LogTimeTaken(string tag, DateTime t1, bool log)
        {
            var duration = DateTime.Now - t1;
            Log($"[{tag}] time taken = {duration.TotalSeconds} s", log);
        }

        private static void Run(PuffinBasicIR ir, TextWriter @out, IEnvironment env)
        {
            var runtime = new PuffinBasicRuntime(ir, @out, env);
            runtime.Run();
        }

        private static PuffinBasicIR GenerateIR(PuffinBasicSourceFile sourceFile, bool graphics)
        {
            var symbolTable = new PuffinBasicSymbolTable();
            var ir = new PuffinBasicIR(symbolTable);
            foreach (var importFile in sourceFile.GetImportFiles())
            {
                GenerateIR(importFile, ir, graphics);
            }

            GenerateIR(sourceFile, ir, graphics);
            return ir;
        }

        private static void GenerateIR(PuffinBasicSourceFile sourceFile, PuffinBasicIR ir, bool graphics)
        {
            var @in = sourceFile.GetSourceCodeStream();
            var lexer = new PuffinBasicLexer(@in);
            var tokens = new CommonTokenStream(lexer);
            var parser = new PuffinBasicParser(tokens);
            var tree = parser.prog();
            var walker = new ParseTreeWalker();
            var irListener = new PuffinBasicIRListener(sourceFile, @in, ir, graphics);
            walker.Walk(irListener, tree);
            irListener.SemanticCheckAfterParsing();
        }

        private static PuffinBasicSourceFile SyntaxCheckAndSortByLineNumber(PuffinBasicImportPath importPath, string sourceFile, string input, ThrowOnDuplicate throwOnDuplicate, SourceFileMode sourceFileMode)
        {
            var @in = CharStreams.fromString(input);
            var lexer = new PuffinBasicLexer(@in);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(new LexerErrorListener(input));
            var tokens = new CommonTokenStream(lexer);
            var parser = new PuffinBasicParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new ThrowingErrorListener(input));
            var tree = parser.prog();
            var walker = new ParseTreeWalker();
            var linenumListener = new LinenumberListener(@in, throwOnDuplicate);
            walker.Walk(linenumListener, tree);
            if (sourceFileMode == SourceFileMode.LIB)
            {
                if (linenumListener.HasLineNumbers())
                {
                    throw new PuffinBasicRuntimeError(IMPORT_ERROR, $"Lib {sourceFile} should not have line numbers!");
                }

                if (linenumListener.GetLibtag() == null)
                {
                    throw new PuffinBasicRuntimeError(IMPORT_ERROR, $"Lib {sourceFile} should set a LIBTAG!");
                }
            }

            HashSet<PuffinBasicSourceFile> importSourceFiles = new HashSet<PuffinBasicSourceFile>();
            foreach (string importFilename in linenumListener.GetImportFiles())
            {
                var importedInput = LoadSource(importPath.Find(importFilename));
                var importSourceFile = SyntaxCheckAndSortByLineNumber(importPath, importFilename, importedInput, throwOnDuplicate, SourceFileMode.LIB);
                importSourceFiles.Add(importSourceFile);
                foreach (var importFile in importSourceFile.GetImportFiles())
                    importSourceFiles.Add(importFile);
            }

            string sortedCode = linenumListener.GetSortedCode();
 
            return new PuffinBasicSourceFile(sourceFile, linenumListener.GetLibtag(), sortedCode, CharStreams.fromString(sortedCode), importSourceFiles);
        }

        internal sealed class ThrowingErrorListener : BaseErrorListener
        {
            private readonly string input;
            public ThrowingErrorListener(string input)
            {
                this.input = input;
            }

            public override void SyntaxError(
                TextWriter output, IRecognizer recognizer,
                IToken offendingSymbol, int line,
                int charPositionInLine, string msg,
                RecognitionException e)
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

                throw new PuffinBasicSyntaxError($"[{line}:{charPositionInLine}] {msg}{Environment.NewLine}{inputLine}");
            }

            //public /*override*/ void SyntaxError(Recognizer<Symbol, Antlr4.Runtime.Atn.ATNSimulator> recognizer, IToken offendingSymbol, int line, int charPositionInLine, string msg, RecognitionException e)
            //{
            //    var lineIndex = line - 1;

            //    var lines = input.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.None);
            //    string inputLine;
            //    if (lineIndex >= 0 && lineIndex < lines.Length)
            //    {
            //        inputLine = lines[lineIndex];
            //        if (charPositionInLine >= 0 && charPositionInLine <= inputLine.Length)
            //        {
            //            inputLine += Environment.NewLine + "^".PadLeft(Math.Max(0, charPositionInLine));
            //        }
            //    }
            //    else
            //    {
            //        inputLine = "<LINE OUT OF RANGE>";
            //    }

            //    throw new PuffinBasicSyntaxError("[" + line + ":" + charPositionInLine + "] " + msg + Environment.NewLine + inputLine);
            //}
        }

        public sealed class UserOptions
        {
            static UserOptions OfTest()
            {
                return new UserOptions(false, false, false, false, false, null);
            }

            public readonly bool logOnDuplicate;
            public readonly bool listSourceCode;
            public readonly bool printIR;
            public readonly bool timing;
            public readonly bool graphics;
            public readonly string filename;
            public UserOptions(bool logOnDuplicate, bool listSourceCode, bool printIR, bool timing, bool graphics, string filename)
            {
                this.logOnDuplicate = logOnDuplicate;
                this.listSourceCode = listSourceCode;
                this.printIR = printIR;
                this.timing = timing;
                this.graphics = graphics;
                this.filename = filename;
            }
        }
    }
}

