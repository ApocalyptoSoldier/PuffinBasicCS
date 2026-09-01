namespace Org.Puffinbasic.Parser
{
    using Org.Puffinbasic.Error;
    //using Java.Io;
    //using Java.Nio.File;
    //using Java.Util;
    //using Java.Util.Stream;
    using System;
    using System.Collections.Generic;

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
            List<string> searchPaths = new List<string>();
            var paths = Environment.GetEnvironmentVariable(PUFFIN_BASIC_PATH_ENVVAR);
            if (paths != null)
            {
                //searchPaths.AddRange(Arrays.Stream(paths.Split(File.pathSeparator)).Collect(Collectors.ToList()));
            }

            searchPaths.Add(mainModulePath);
            return searchPaths;
        }

        public virtual string Find(string relativePath)
        {
            throw new NotImplementedException();
            //foreach (string searchPath in searchPaths)
            //{
            //    var file = Paths.Get(searchPath, relativePath).ToFile();
            //    if (file.Exists())
            //    {
            //        return file.GetPath();
            //    }
            //}

            throw new PuffinBasicRuntimeError(PuffinBasicRuntimeError.ErrorCode.IMPORT_ERROR, "Search failed for relative path: " + relativePath);
        }
    }
}

