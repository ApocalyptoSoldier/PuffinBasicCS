using Org.Puffinbasic.Error;
//using Java.Io;
//using Java.Nio.File;
//using Java.Util;
//using Java.Util.Stream;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Parser
{
    public class PuffinBasicImportPath
    {
        private static readonly string PUFFIN_BASIC_PATH_ENVVAR = "PUFFIN_BASIC_PATH";
        private readonly string mainModulePath;
        private readonly IList<string> searchPaths;
        public PuffinBasicImportPath(string mainPath)
        {
            //this.mainModulePath = mainPath != null ? new File(mainPath).GetParent() : ".";
            this.mainModulePath = mainPath == null ? "." : System.IO.Path.GetDirectoryName(mainPath); // GetPathRoot?
            this.searchPaths = GetSearchPaths();
        }

        private IList<string> GetSearchPaths()
        {
            IList<string> searchPaths = new List<string>();
            var paths = Environment.GetEnvironmentVariable(PUFFIN_BASIC_PATH_ENVVAR);
            if (paths != null)
            {
                searchPaths.AddAll(Arrays.Stream(paths.Split(File.pathSeparator)).Collect(Collectors.ToList()));
            }

            searchPaths.Add(mainModulePath);
            return searchPaths;
        }

        public virtual string Find(string relativePath)
        {
            foreach (string searchPath in searchPaths)
            {
                var file = Paths.Get(searchPath, relativePath).ToFile();
                if (file.Exists())
                {
                    return file.GetPath();
                }
            }

            throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.IMPORT_ERROR, "Search failed for relative path: " + relativePath);
        }
    }
}

