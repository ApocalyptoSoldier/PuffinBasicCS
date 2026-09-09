namespace PuffinBasicCS
{
    using PuffinBasicCS.Runtime;

    using System.Reflection;
    using System.Text;

    using static PuffinBasicCS.PuffinBasicInterpreterMain;

    [TestClass]
    public class IntegrationTest
    {
        private IEnvironment env;

        private Dictionary<string, string> fileNameToResource;

        private static Dictionary<string, string> GetResourceNamesToResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var manifestResourceNames = assembly.GetManifestResourceNames();

            var resourcePrefix = $"{assembly.GetName().Name}.Resources.";

            return manifestResourceNames.ToDictionary(resourceName => resourceName.Replace(resourcePrefix, String.Empty));
        }

        [TestInitialize]
        public virtual void Setup()
        {
            env = new IEnvironment.SystemEnv();

            fileNameToResource = GetResourceNamesToResources();
        }

        [TestMethod]
        public virtual void TestForLoop()
        {
            RunTest("forloop.bas", "forloop.bas.output");
        }

        [TestMethod]
        public virtual void TestNestedForLoop()
        {
            RunTest("nested_forloop.bas", "nested_forloop.bas.output");
        }

        [TestMethod]
        public virtual void TestScalarVariable()
        {
            RunTest("scalar_var.bas", "scalar_var.bas.output");
        }

        [TestMethod]
        public virtual void TestArrayVariable()
        {
            RunTest("array_var.bas", "array_var.bas.output");
        }

        [TestMethod]
        public virtual void TestArrayCopy()
        {
            RunTest("array_copy.bas", "array_copy.bas.output");
        }

        [TestMethod]
        public virtual void TestArrayFunc()
        {
            RunTest("array_func.bas", "array_func.bas.output");
        }

        [TestMethod]
        public virtual void TestWhile()
        {
            RunTest("while.bas", "while.bas.output");
        }

        [TestMethod]
        public virtual void TestExpr()
        {
            RunTest("expr.bas", "expr.bas.output");
        }

        [TestMethod]
        public virtual void TestFunc()
        {
            RunTest("func.bas", "func.bas.output");
        }

        [TestMethod]
        public virtual void TestFunc2()
        {
            RunTest("func2.bas", "func2.bas.output");
        }

        [TestMethod]
        public virtual void TestIf()
        {
            RunTest("if.bas", "if.bas.output");
        }

        [TestMethod]
        public virtual void TestIfThenBegin()
        {
            RunTest("ifthenbegin.bas", "ifthenbegin.bas.output");
        }

        [TestMethod]
        public virtual void TestReadData()
        {
            RunTest("readdata.bas", "readdata.bas.output");
        }

        [TestMethod]
        public virtual void TestGosub()
        {
            RunTest("gosub.bas", "gosub.bas.output");
        }

        [TestMethod]
        public virtual void TestGosublabel()
        {
            RunTest("gosublabel.bas", "gosublabel.bas.output");
        }

        [TestMethod]
        public virtual void TestGotolabel()
        {
            RunTest("gotolabel.bas", "gotolabel.bas.output");
        }

        [TestMethod]
        public virtual void TestDef()
        {
            RunTest("def.bas", "def.bas.output");
        }

        [TestMethod]
        public virtual void TestUdf()
        {
            RunTest("udf.bas", "udf.bas.output");
        }

        [TestMethod]
        public virtual void TestStrStmt()
        {
            RunTest("strstmt.bas", "strstmt.bas.output");
        }

        [TestMethod]
        public virtual void TestPrintUsing()
        {
            RunTest("printusing.bas", "printusing.bas.output");
        }

        [TestMethod]
        public virtual void TestWrite()
        {
            RunTest("write.bas", "write.bas.output");
        }

        [TestMethod]
        public virtual void TestSwap()
        {
            RunTest("swap.bas", "swap.bas.output");
        }

        [TestMethod]
        public virtual void TestRef()
        {
            RunTest("ref.bas", "ref.bas.output");
        }

        [TestMethod]
        public virtual void TestRandomAccessFile()
        {
            string tmpdir = Path.GetTempPath();
            string filename = "puffin_basic_test_random_access_file_" + DateTime.Now.TimeOfDay.TotalSeconds + ".data";
            env["TEST_TMP_DIR"] = tmpdir;
            env["TEST_FILENAME"] = filename;
            RunTest("randomaccessfile.bas", "randomaccessfile.bas.output");
            System.IO.File.Delete(Path.Join(tmpdir, filename));
        }

        [TestMethod]
        public virtual void TestSequentialAccessFile()
        {
            string tmpdir = Path.GetTempPath();
            string filename = "puffin_basic_test_sequential_access_file_" + DateTime.Now.TimeOfDay.TotalSeconds + ".data";
            env["TEST_TMP_DIR"] = tmpdir;
            env["TEST_SEQ_FILENAME"] = filename;
            RunTest("sequentialaccessfile.bas", "sequentialaccessfile.bas.output");
            System.IO.File.Delete(Path.Join(tmpdir, filename));
        }

        [TestMethod]
        public virtual void TestStruct()
        {
            RunTest("struct.bas", "struct.bas.output");
        }

        [TestMethod]
        public virtual void TestList()
        {
            RunTest("list.bas", "list.bas.output");
        }

        [TestMethod]
        public virtual void TestSet()
        {
            RunTest("set.bas", "set.bas.output");
        }

        [TestMethod]
        public virtual void TestDict()
        {
            // The original output had 2 1 on line 15, but the order of dictonary keys is not guaranteed, I think, so maybe make the test more dynamic
            RunTest("dict.bas", "dict.bas.output");
        }

        private void RunTest(string source, string output)
        {
            var ms = new MemoryStream();
            var @out = new StreamWriter(ms);
            PuffinBasicInterpreterMain.InterpretAndRun(UserOptions.OfTest(), LoadResource(source), @out, env);
            @out.Dispose();
            
            var expected = LoadResource(output).ReplaceLineEndings();
            var actual = Encoding.UTF8.GetString(ms.ToArray()).ReplaceLineEndings();

            Assert.AreEqual(expected, actual);
        }

        private string LoadResource(string resource)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();

                using (Stream stream = assembly.GetManifestResourceStream(fileNameToResource[resource]))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        string result = reader.ReadToEnd();
                        return result;
                    }
                }
            }
            // TODO: check if I need to implement custom exceptions
            catch (FileLoadException ex)
            {
                throw ex;
            }
            catch (FileNotFoundException ex)
            {
                throw ex;
            }
            catch (Exception ex) {
                throw ex;
            }
        }
    }
}