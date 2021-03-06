﻿using System.Reflection;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

namespace CsSC
{
    public static class Program
    {

        static object RunFile(string filename, string entry, object[] args, bool fullsource = false)
        {

            string fileid;
            string[] filenames;
            CompilerResults compilerResults = CompileUnit.Compile(filename, fullsource, false, out fileid, out filenames);

            if (compilerResults.Errors.Count == 0)
            {
                ///如果错误数为0则进行调用
                Assembly asm = compilerResults.CompiledAssembly;
                Type type = asm.GetType("ScriptRunner.Program" + fileid);
                MethodInfo methodInfo = type.GetMethod(entry);
                //object obj = System.Activator.CreateInstance(type);
                try
                {
                    object result = methodInfo.Invoke(null, args);
                    //object result = type.InvokeMember("Main", BindingFlags.InvokeMethod | BindingFlags.Static | BindingFlags.Public, null, null, argv);
                    //Console.WriteLine(result.ToString());
                    return result;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.InnerException);
                    return null;
                }
            }
            else
            {
                //如果出错则返回错误文本
                foreach (CompilerError compilerError in compilerResults.Errors)
                {
                    string idstr = compilerError.FileName.Substring(compilerError.FileName.IndexOf('.') + 1);
                    if (idstr.Contains(".")) idstr = idstr.Substring(0, idstr.IndexOf('.'));
                    int fileidx = string.IsNullOrEmpty(idstr) ? 0 : int.Parse(idstr);
                    compilerError.FileName = filenames[fileidx];
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
                return null;
            }
        }
       
        static void Main(string[] argv)
        {

            #region PARSE COMMAND LINE ARGS
            string filename = null;
            bool fullsource = false;

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
                            System.IO.File.WriteAllText(filename, CompileUnit.template);
                            return;
                            break;
                        case "-full":
                        case "-f":
                            fullsource = true;
                            break;
                        case "-addpath":
                        case "-a":
                            if (!filename.EndsWith(";")) filename += ";";
                            Properties.Settings.Default.ScriptPaths += filename;
                            Properties.Settings.Default.Save();
                            return;
                            break;
                    }
                else
                    if (filename == null) { filename = s; break; }
            }
            #endregion
            
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
                Console.WriteLine("Error 2: File not found.");
                return;
            }

            ++args_offset;
            string[] child_argv = new string[Math.Max(0, argv.Length - args_offset)];
            for (int i = args_offset; i < argv.Length; ++i)
                child_argv[i - args_offset] = argv[i];

            RunFile(filename, "Main", new object[] { child_argv }, fullsource);

        }
    }
}
