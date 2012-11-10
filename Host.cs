using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CsSC
{
    public class EncodingType
    {
        /// <summary>
        /// 给定文件的路径，读取文件的二进制数据，判断文件的编码类型
        /// </summary>
        /// <param name="FILE_NAME">文件路径</param>
        /// <returns>文件的编码类型</returns>
        public static System.Text.Encoding GetType(string FILE_NAME)
        {
            FileStream fs = new FileStream(FILE_NAME, FileMode.Open, FileAccess.Read);
            Encoding r = GetType(fs);
            fs.Close();
            return r;
        }

        /// <summary>
        /// 通过给定的文件流，判断文件的编码类型
        /// </summary>
        /// <param name="fs">文件流</param>
        /// <returns>文件的编码类型</returns>
        public static System.Text.Encoding GetType(FileStream fs)
        {
            byte[] Unicode = new byte[] { 0xFF, 0xFE, 0x41 };
            byte[] UnicodeBIG = new byte[] { 0xFE, 0xFF, 0x00 };
            byte[] UTF8 = new byte[] { 0xEF, 0xBB, 0xBF }; //带BOM
            Encoding reVal = Encoding.Default;

            BinaryReader r = new BinaryReader(fs, System.Text.Encoding.Default);
            int i;
            int.TryParse(fs.Length.ToString(), out i);
            byte[] ss = r.ReadBytes(i);
            if (IsUTF8Bytes(ss) || (ss[0] == 0xEF && ss[1] == 0xBB && ss[2] == 0xBF))
            {
                reVal = Encoding.UTF8;
            }
            else if (ss[0] == 0xFE && ss[1] == 0xFF && ss[2] == 0x00)
            {
                reVal = Encoding.BigEndianUnicode;
            }
            else if (ss[0] == 0xFF && ss[1] == 0xFE && ss[2] == 0x41)
            {
                reVal = Encoding.Unicode;
            }
            r.Close();
            return reVal;

        }

        /// <summary>
        /// 判断是否是不带 BOM 的 UTF8 格式
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static bool IsUTF8Bytes(byte[] data)
        {
            int charByteCounter = 1; //计算当前正分析的字符应还有的字节数
            byte curByte; //当前分析的字节.
            for (int i = 0; i < data.Length; i++)
            {
                curByte = data[i];
                if (charByteCounter == 1)
                {
                    if (curByte >= 0x80)
                    {
                        //判断当前
                        while (((curByte <<= 1) & 0x80) != 0)
                        {
                            charByteCounter++;
                        }
                        //标记位首位若为非0 则至少以2个1开始 如:110XXXXX...........1111110X 
                        if (charByteCounter == 1 || charByteCounter > 6)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    //若是UTF-8 此时第一位必须为1
                    if ((curByte & 0xC0) != 0x80)
                    {
                        return false;
                    }
                    charByteCounter--;
                }
            }
            if (charByteCounter > 1)
            {
                throw new Exception("非预期的byte格式");
            }
            return true;
        }

    }

    public class Host
    {
        public static string GetVersion()
        {
            return "CsSC 0.2-build20120901";
        }

        public static string GetFilename(string path, out string extension)
        {
            string filename = path; extension = "";
            if (path.IndexOf(System.IO.Path.DirectorySeparatorChar) > 0)
                filename = path.Substring(path.IndexOf(System.IO.Path.DirectorySeparatorChar) + 1);
            if (path.LastIndexOf('.') > 0)
                extension = filename.Substring(path.LastIndexOf('.') + 1);
            return filename;
        }

        public static string GetFilename(string path)
        {
            string ext = "";
            return GetFilename(path, out ext);
        }

        public static string GetFileExtension(string path)
        {
            string ext = "";
            GetFilename(path, out ext); 
            return ext;
        }

        public static bool IsAbsolutePath(string path)
        {
            return path.StartsWith("/") // *nix
                || path.IndexOf(":\\") > 0 // Windows
                ;
        }

        public static string SubstringBetween(string str, int end1, int end2)
        {
            if (end1 >= end2 || end1 < 0) return "";
            return str.Substring(end1, end2 - end1);
        }

        public static string SubstringBetween(string str, char end1, char end2, bool longer = false)
        {
            if (longer) return SubstringBetween(str, str.IndexOf(end1), str.LastIndexOf(end2));
            return SubstringBetween(str, str.IndexOf(end1), str.IndexOf(end2));
        }

        public static void Break()
        {
            System.Diagnostics.Debugger.Break();
        }

        public static void Exit()
        {
            Environment.Exit(0);
        }

        public static void MessageBox(string content)
        {
            System.Windows.Forms.MessageBox.Show(content, "CsSC Script");
        }

        public static string[] Dir(string dir)
        {
            return System.IO.Directory.GetFiles(dir);
        }

        public static string[] Dir(string dir, string pattern)
        {
            return Directory.GetFiles(dir, pattern);
        }

        public static StreamWriter NewSW(string filename, System.Text.Encoding encoding = null)
        {
            if (encoding == null) encoding = System.Text.Encoding.Unicode;
            return new StreamWriter(filename, false, encoding);
        }
        
        public static StreamReader NewSR(string filename)
        {
            return new StreamReader(filename, EncodingType.GetType(filename));
        }
    }
}
