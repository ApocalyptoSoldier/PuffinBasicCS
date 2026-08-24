using Org.Antlr.V4.Runtime;
using Java.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Parser
{
    public class PuffinBasicSourceFile
    {
        private readonly string relativePath;
        private readonly string libtag;
        private readonly string sourceCode;
        private readonly CharStream sourceCodeStream;
        private readonly LinkedHashSet<PuffinBasicSourceFile> importFiles;
        public PuffinBasicSourceFile(string relativePath, string libtag, string sourceCode, CodePointCharStream sourceCodeStream, LinkedHashSet<PuffinBasicSourceFile> importFiles)
        {
            this.relativePath = relativePath;
            this.libtag = libtag;
            this.sourceCode = sourceCode;
            this.sourceCodeStream = sourceCodeStream;
            this.importFiles = new LinkedHashSet(importFiles);
        }

        public virtual LinkedHashSet<PuffinBasicSourceFile> GetImportFiles()
        {
            return importFiles;
        }

        public virtual string GetRelativePath()
        {
            return relativePath;
        }

        public virtual string GetLibtag()
        {
            return libtag;
        }

        public virtual string GetSourceCode()
        {
            return sourceCode;
        }

        public virtual CharStream GetSourceCodeStream()
        {
            return sourceCodeStream;
        }

        public virtual bool Equals(object o)
        {
            if (this == o)
                return true;
            if (o == null || GetType() != o.GetType())
                return false;
            PuffinBasicSourceFile that = (PuffinBasicSourceFile)o;
            return libtag.Equals(that.libtag);
        }

        public virtual int GetHashCode()
        {
            return Objects.Hash(libtag);
        }

        public virtual string ToString()
        {
            return GetRelativePath();
        }
    }
}

