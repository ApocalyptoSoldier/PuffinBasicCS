//using Org.Antlr.V4.Runtime;
//using Java.Util;
using Antlr4.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;

namespace Org.Puffinbasic.Parser
{
    public class PuffinBasicSourceFile
    {
        private readonly string relativePath;
        private readonly string libtag;
        private readonly string sourceCode;
        private readonly ICharStream sourceCodeStream;
        private readonly HashSet<PuffinBasicSourceFile> importFiles;
        public PuffinBasicSourceFile(string relativePath, string libtag, string sourceCode, ICharStream sourceCodeStream, HashSet<PuffinBasicSourceFile> importFiles)
        {
            this.relativePath = relativePath;
            this.libtag = libtag;
            this.sourceCode = sourceCode;
            this.sourceCodeStream = sourceCodeStream;
            this.importFiles = new HashSet<PuffinBasicSourceFile>(importFiles);
        }

        public virtual HashSet<PuffinBasicSourceFile> GetImportFiles()
        {
            return importFiles;
        }

        public virtual string GetRelativePath()
        {
            // TODO: make relative path actually be the relative path once I've ported the path related code correctly
            return relativePath.Remove(0, Environment.CurrentDirectory.Length);
            //return relativePath;
        }

        public virtual string GetLibtag()
        {
            return libtag;
        }

        public virtual string GetSourceCode()
        {
            return sourceCode;
        }

        public virtual ICharStream GetSourceCodeStream()
        {
            return sourceCodeStream;
        }

        public new virtual bool Equals(object o)
        {
            if (this == o)
                return true;
            if (o == null || GetType() != o.GetType())
                return false;
            PuffinBasicSourceFile that = (PuffinBasicSourceFile)o;
            return libtag.Equals(that.libtag);
        }

        public new virtual int GetHashCode()
        {
            return libtag.GetHashCode();
        }

        public new virtual string ToString()
        {
            return GetRelativePath();
        }
    }
}

