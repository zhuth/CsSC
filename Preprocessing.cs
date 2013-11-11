using System;
using System.Reflection;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Text;

namespace CsSC
{
    public class CompileUnit
    {
        public const string template = "// !CsScript Generated, do not remove\r\n";
        public const string default_using = "using System;\r\nusing System.IO;\r\nusing System.Text;\r\nusing System.Text.RegularExpressions;\r\nusing System.Collections.Generic;\r\n";
        public const string default_main = "namespace ScriptRunner {\r\npublic static partial class Program##id## {\r\npublic static void Main(string[] args) {\r\n #### \r\n}\r\n}\r\n}\r\n";
        public const string default_functions = "namespace ScriptRunner {\r\npublic static partial class Program##id## {\r\n #### \r\n}\r\n}\r\n";

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

        public static CompilerResults Compile(string[] source, CompilerParameters compilerParameters)
        {
            CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();
            ICodeCompiler icodeCompiler = CScodeProvider.CreateCompiler();
            CompilerResults compilerResults = icodeCompiler.CompileAssemblyFromSourceBatch(compilerParameters, source);
            return compilerResults;
        }

        public static CompilerResults Compile(string filename, bool fullsource, bool showsource, out string fileid)
        {
            string source;
            return Compile(filename, null, fullsource, out source, out fileid);

            if (showsource) { Console.WriteLine(source); Environment.Exit(0); return null; }
        }

        public static CompilerResults Compile(string filename, string fileid1, bool fullsource, out string source, out string fileid)
        {
            source = System.IO.File.ReadAllText(filename, EncodingType.GetType(filename));
            string using_text = "", functions = ""; string[] references = null;

            if (fileid1 != null)
                fileid = fileid1;
            else
            {
                fileid = filename.Substring(filename.LastIndexOf(System.IO.Path.DirectorySeparatorChar) + 1);
                fileid = "file" + fileid.Substring(0, fileid.IndexOf('.'));
            }

            string[] sources;

            if (!fullsource && !source.StartsWith(CompileUnit.template))
            {
                var incls = CompileUnit.ParseSource(ref source, fileid, out using_text, out references, out functions);
                source = CompileUnit.default_using + using_text + Environment.NewLine + CompileUnit.default_main.Replace("##id##", fileid).Replace("####", source);
                source += CompileUnit.default_functions.Replace("##id##", fileid).Replace("####", functions);
                sources = new string[incls.Count + 1];
                sources[0] = source;
                int idx=0;
                foreach (string v in incls.Values) sources[++idx] = v;
            }
            else
            {
                sources = new string[] { source };
            }

            CompilerParameters compilerParameters = CompileUnit.BuildCompileParameters(true, false, null);
            foreach (string refs in references)
                compilerParameters.ReferencedAssemblies.Add(refs);

            return CompileUnit.Compile(sources, compilerParameters);
        }

        public static Dictionary<string, string> ParseSource(ref string source, string fileid, out string using_text, out string[] references, out string functions)
        {
            Dictionary<string, string> incls = new Dictionary<string, string>();

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
                            string include_filename = s.Substring("#include ".Length);
                            string include_filecontent, tf;
                            Compile(include_filename, null, false, out include_filecontent, out tf);
                            if (!incls.ContainsKey(fileid)) incls.Add(fileid, include_filecontent);
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

            return incls;
        }
    }

    class Token
    {
        public Token subtoken;
        public string plainText;
        public int line, col;

        public Token(int Line, int Col, string PlainText)
        {
            line = Line; col = Col; plainText = PlainText;
            subtoken = null;
        }
    }
     
    class Preprocessing
    {
        public static Token[] SplitTokens(string source)
        {
            List<Token> lst = new List<Token>();

            bool blank = false;
            string current_token = "";
            int line=0,col=0;
            for (int i = 0; i < source.Length; ++i)
            {
                switch (source[i])
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        if (blank) continue;
                        blank = true;
                        lst.Add(new Token(line, col, current_token));
                        if (source[i] == '\r' || source[i] == '\n') { ++line; col = 0; }
                        else if (source[i] == '\t') col += 4; else col++;
                        break;
                    case '\"':
                    case '\'':
                        int i0 = i;
                        do
                        {
                            ++i;
                        } while (source[i] == source[i0] && source[i - 1] != '\\' && i < source.Length);
                        current_token = source.Substring(i0, i - i0 + 1);
                        lst.Add(new Token(line, col, current_token));
                        break;
                }
            }

            return lst.ToArray();
        }
    }
}
