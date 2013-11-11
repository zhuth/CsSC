using System.Reflection;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

namespace CsSC
{
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

            string fileid;
            CompilerResults compilerResults = CompileUnit.Compile(filename, fullsource, showsource, out fileid);

            ++args_offset;
            string[] child_argv = new string[Math.Max(0, argv.Length - args_offset)];
            for (int i = args_offset; i < argv.Length; ++i)
                child_argv[i - args_offset] = argv[i];

            if (compilerResults.Errors.Count == 0)
            {
                ///如果错误数为0则进行调用
                Assembly asm = compilerResults.CompiledAssembly;
                Type type = asm.GetType("ScriptRunner.Program" + fileid);
                MethodInfo methodInfo = type.GetMethod("Main");
                //object obj = System.Activator.CreateInstance(type);
                try
                {
                    object result = methodInfo.Invoke(null, new object[]{child_argv});
                    //object result = type.InvokeMember("Main", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public, null, null, argv);
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
