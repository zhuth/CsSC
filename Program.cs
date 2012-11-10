using System.Reflection;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

namespace CsSC
{
    public class CompileUnit
    {
        public const string template = "// !CsScript Generated, do not remove\r\n";
        public const string default_using = "using System;\r\nusing System.IO;\r\nusing System.Text;\r\nusing System.Text.RegularExpressions;\r\nusing System.Collections.Generic;\r\n";
        public const string default_main = "namespace ScriptRunner {\r\npublic static partial class Program {\r\npublic static void Main##id##(string[] args) {\r\n #### \r\n}\r\n}\r\n}\r\n";
        public const string default_functions = "namespace ScriptRunner {\r\npublic static partial class Program {\r\n #### \r\n}\r\n}\r\n";

        public static CompilerParameters BuildCompileParameters(bool generateInMemory, bool generateExecutable, string outputAssembly)
        {
            CompilerParameters compilerParameters = new CompilerParameters();
            compilerParameters.GenerateInMemory = generateInMemory;
            compilerParameters.GenerateExecutable = generateExecutable;
            compilerParameters.IncludeDebugInformation = true;
            compilerParameters.ReferencedAssemblies.Add("System.dll");
            compilerParameters.ReferencedAssemblies.Add("System.Data.dll");
            compilerParameters.ReferencedAssemblies.Add(Assembly.GetExecutingAssembly().Location);
            if (outputAssembly != null)
            {
                compilerParameters.OutputAssembly = outputAssembly;
            }
            return compilerParameters;
        }

        public static CompilerResults Compile(string source, CompilerParameters compilerParameters)
        {
            CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();
            ICodeCompiler icodeCompiler = CScodeProvider.CreateCompiler();
            CompilerResults compilerResults = icodeCompiler.CompileAssemblyFromSource(compilerParameters, source);
            return compilerResults;
        }

        public static void ParseSource(ref string source, out string using_text, out string[] references, out string functions)
        {
            string new_source = "#line 1" + Environment.NewLine;
            List<string> references_list = new List<string>();
            using_text = ""; references = new string[0]; functions = "";
            string[] source_lines = source.Split('\n');
            for (int i = 0; i < source_lines.Length; ++i)
            {
                int offset = 0;
                string s = source_lines[i];
                if (s.Trim().StartsWith("#"))
                {
                    s = s.Trim();
                    if (s.EndsWith(";")) s = s.Substring(0, s.Length - 1);
                    switch (s.Split(' ')[0])
                    {
                        case "#reference":
                            references_list.Add(s.Substring("#reference ".Length));
                            new_source += Environment.NewLine;
                            continue;
                        case "#using":
                            string using_line = "using " + s.Substring("#using ".Length) + ";";
                            if (default_using.Contains(using_line)) continue;
                            using_text += using_line + Environment.NewLine;
                            new_source += Environment.NewLine;
                            continue;
                        case "#include":
                            new_source += Environment.NewLine;
                            continue;
                        case "#function":
                            new_source += Environment.NewLine;
                            functions += "#line " + (i + 2) + Environment.NewLine;
                            for (++i; i < source_lines.Length; ++i)
                                if (source_lines[i].Trim().StartsWith("#endfunction"))
                                    break;
                                else
                                    functions += source_lines[i] + Environment.NewLine;
                            new_source += "#line " + (i + 1) + Environment.NewLine;
                            continue;
                        default:
                            break;
                    }
                }

                if (offset < s.Length) s = s.Substring(offset); else s = "";
                new_source += s;
            }

            references = references_list.ToArray();
            source = new_source + Environment.NewLine + "#line 10000";
            functions += Environment.NewLine + "#line 20000";
        }        
    }

    static class Program
    {
       
        static void Main(string[] argv)
        {

            #region PARSE COMMAND LINE ARGS
            string filename = null;
            bool create = false, fullsource = false, showsource = false, addpath = false;

            if (argv.Length < 1)
            {
                Console.WriteLine("Usage: cssc [-c|-s] <filename> [args...]");
                Console.WriteLine("       cssc -a <default script path>[;<script paths>...]");
                return;
            }

            int args_offset = 0;
            for (; args_offset < argv.Length; ++args_offset)
            {
                string s = argv[args_offset];
                if (s.StartsWith("-"))
                    switch (s)
                    {
                        case "-create":
                        case "-c":
                            create = true;
                            break;
                        case "-full":
                        case "-f":
                            fullsource = true;
                            break;
                        case "-source":
                        case "-s":
                            showsource = true;
                            break;
                        case "-addpath":
                        case "-a":
                            addpath = true;
                            break;
                    }
                else
                    if (filename == null) { filename = s; break; }
            }
            #endregion

            if (create)
            {
                System.IO.File.WriteAllText(filename, CompileUnit.template);
                return;
            }

            if (addpath)
            {
                if (!filename.EndsWith(";")) filename += ";";
                Properties.Settings.Default.ScriptPaths += filename;
                Properties.Settings.Default.Save();
                return;
            }

            if (Host.GetFileExtension(filename) == "") filename += ".cssc";

            if (!Host.IsAbsolutePath(filename)) {
                foreach (string p in Properties.Settings.Default.ScriptPaths.Split(';'))
                {
                    string path = p;
                    if (!path.EndsWith("" + System.IO.Path.DirectorySeparatorChar))
                        path += System.IO.Path.DirectorySeparatorChar;
                    if (System.IO.File.Exists(path + filename))
                    {
                        filename = path + filename;
                        break;
                    }
                }
            }

            if (!System.IO.File.Exists(filename))
            {
                if (!System.IO.File.Exists(filename))
                {
                    Console.WriteLine("Error 2: File not found.");
                    return;
                }
            }

            string source = System.IO.File.ReadAllText(filename, EncodingType.GetType(filename));

            string using_text = "", functions = ""; string[] references = null;
            string id = "";
           
            if (!fullsource && !source.StartsWith(CompileUnit.template))
            {
                id = filename.GetHashCode().ToString("X8");
                CompileUnit.ParseSource(ref source, out using_text, out references, out functions);
                source = CompileUnit.default_using + using_text + Environment.NewLine + CompileUnit.default_main.Replace("##id##", id).Replace("####", source);
                source += CompileUnit.default_functions.Replace("####", functions);
            }

            if (showsource) { Console.WriteLine(source); return; }

            CompilerParameters compilerParameters = CompileUnit.BuildCompileParameters(true, false, null);
            foreach(string refs in references)
                compilerParameters.ReferencedAssemblies.Add(refs);    

            CompilerResults compilerResults = CompileUnit.Compile(source, compilerParameters);
            ++args_offset;
            string[] child_argv = new string[Math.Max(0, argv.Length - args_offset)];
            for (int i = args_offset; i < argv.Length; ++i)
                child_argv[i - args_offset] = argv[i];

            if (compilerResults.Errors.Count == 0)
            {
                ///如果错误数为0则进行调用
                Assembly asm = compilerResults.CompiledAssembly;
                Type type = asm.GetTypes()[0];
                MethodInfo methodInfo = type.GetMethod("Main" + id);
                //object obj = System.Activator.CreateInstance(type);
                try
                {
                    object result = methodInfo.Invoke(null, new object[]{child_argv});
                    //type.InvokeMember("Main", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public, null, null, argv);
                    //Console.WriteLine(result.ToString());
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.InnerException);
                    return;
                }
            }
            else
            {
                //如果出错则返回错误文本
                foreach (CompilerError compilerError in compilerResults.Errors)
                {
                    compilerError.FileName = filename;
                    if (compilerError.Line > 10000)
                    {
                        Console.WriteLine("Main 函数区错误，可能是错误的 {} 匹配。");
                    }
                    else if (compilerError.Line > 20000)
                    {
                        Console.WriteLine("函数定义区错误，可能是错误的 {} 匹配。请检查 #function ... #endfunction 区间");
                    }
                    Console.WriteLine(compilerError);
                }    
                return;
            }


        }
    }
}
