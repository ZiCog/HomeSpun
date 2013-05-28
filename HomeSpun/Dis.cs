/*
Copyright (c) 2013 Michael Park

Permission is hereby granted, free of charge, to any person obtaining a copy
 of this software and associated documentation files (the "Software"), to deal
 in the Software without restriction, including without limitation the rights
 to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the Software is
 furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
 all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 THE SOFTWARE.
*/
using System;
using System.Collections;
using System.Text;
using System.IO;

namespace Homespun
{
    class Dis
    {
        static byte[] mem;

        public static byte[] Mem
        {
            get
            {
                return mem;
            }
            set
            {
                mem = value;
            }
        }

        /// <summary>
        /// Writes a memory dump in one column, supplied text in second column.
        /// </summary>
        /// <param name="sw"></param>
        /// <param name="address">Start address</param>
        /// <param name="n">Number of bytes</param>
        /// <param name="lines">Second-column text</param>
        /// <param name="bytesPerLine">Max number of bytes to list on a line</param>
        /// <param name="separator">Separator between columns</param>
        public static void DumpColumns(StringWriter sw, int address, int n, ArrayList lines, int bytesPerLine, string separator)
        {
            int col1Width = bytesPerLine * 3 + 6;

            int nLines = (n + bytesPerLine - 1) / bytesPerLine;
            int end = address + n;
            for (int i = 0; i < nLines; ++i)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0:x4}:", address);
                for (int k = 0; k < bytesPerLine; ++k)
                {
                    if (address < end)
                    {
                        sb.AppendFormat(" {0:x2}", Byte(address++));
                    }
                }

                string line = sb.ToString();

                int nSpaces = Math.Max(col1Width - line.Length, 0);
                sb.Append("".PadLeft(nSpaces));
                sb.Append(separator);
                if (i < lines.Count)
                {
                    sb.Append(lines[i]);
                }

                sw.WriteLine(sb.ToString());
            }
            if (nLines < lines.Count)
            {
                for (int i = nLines; i < lines.Count; ++i)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("".PadLeft(col1Width));
                    sb.Append(separator);
                    sb.Append(lines[i]);
                    sw.WriteLine(sb.ToString());
                }
            }
        }
        public static void DumpColumns(StringWriter sw, int address, int n, int ooo, bool orgxMode, ArrayList lines, int bytesPerLine, string separator)
        {
            int col1Width = bytesPerLine * 3 + 12;

            int nLines = (n + bytesPerLine - 1) / bytesPerLine;
            if (nLines == 0 && lines.Count > 0)
                nLines = 1;
            int end = address + n;
            for (int i = 0; i < nLines; ++i)
            {
                StringBuilder sb = new StringBuilder();
                if ((address & 3) == 0)
                {
                    int cogAddress = (address + ooo) >> 2;
                    if (orgxMode)
                        cogAddress = 0;
                    sb.AppendFormat("{0:x4}({1:x4}):", address, cogAddress);
                }
                else
                {
                    sb.AppendFormat("{0:x4}(----):", address);
                }
                for (int k = 0; k < bytesPerLine; ++k)
                {
                    if (address < end)
                    {
                        sb.AppendFormat(" {0:x2}", Byte(address++));
                    }
                }

                string line = sb.ToString();

                int nSpaces = Math.Max(col1Width - line.Length, 0);
                sb.Append("".PadLeft(nSpaces));
                sb.Append(separator);
                if (i < lines.Count)
                {
                    sb.Append(lines[i]);
                }

                sw.WriteLine(sb.ToString());
            }
            if (nLines < lines.Count)
            {
                for (int i = nLines; i < lines.Count; ++i)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("".PadLeft(col1Width));
                    sb.Append(separator);
                    sb.Append(lines[i]);
                    sw.WriteLine(sb.ToString());
                }
            }
        }

        public static void Heading(StringWriter sw, string s, char ch)
        {
            if (s != "")
                s = " " + s + " ";
            int m = (79 - s.Length) / 2;
            int n = 79 - m - s.Length;
            sw.Write('\'');
            while (--m >= 1)
                sw.Write(ch);
            sw.Write(s);
            while (--n >= 0)
                sw.Write(ch);
            sw.WriteLine();
        }

        /// <summary>
        /// Disassembles Spin instructions
        /// </summary>
        /// <param name="address">The address of the bytecode</param>
        /// <returns>An ArrayList of strings, each string being a line of the disassembly</returns>
        public static ArrayList Disassemble(ref int startAddress, int endAddress)
        {
            ArrayList lines = new ArrayList();
            int p = startAddress;
            while (p < endAddress)
            {
                int p0 = p;
                StringBuilder sb = new StringBuilder();
                p = Disassemble(p, sb);
                string dis = sb.ToString();

                sb = new StringBuilder();
                sb.AppendFormat("{0:x4}:", p0);
                int n = p - p0;
                while (p0 < p)
                {
                    sb.AppendFormat(" {0:x2}", Byte(p0++));
                }
                sb.Append("".PadLeft((5 - n) * 3 + 1));
                sb.Append(dis);
                lines.Add(sb.ToString());
            }
            startAddress = p;
            return lines;
        }
        static int Disassemble(int p, StringBuilder sb)
        {
            int b = Byte(p++);
            if (b < 0x3d)						// 00..3c: 64 special purpose less REG
                sb.Append(ops_00_3f[b].PadRight(5) + "\t");
            else if (b >= 0xe0)					// e0..ff: 32 unary/binary
                sb.Append(operatorOps[b - 0xe0].PadRight(5) + "\t");

            if (b == 0x05)						// CALL c
            {
                sb.AppendFormat("+{0} ", Byte(p++));
            }
            else if (b == 0x04 || 0x08 <= b && b <= 0x0e && b != 0x0c)	// GOTO, LOOPJPF, LOOPRPT, JPF, JPT, CASE, CASER
            {
                p = AddressOffset(p, sb);
            }
            else if (b == 0x06 || b == 0x07)			// CALLOBJ, CALLOBJ[]
            {
                int o = Byte(p++);
                int u = Byte(p++);
                sb.AppendFormat("{0}:{1}", o, u);
                ///				sb.AppendFormat( " (method {1} of object @ ${0:x4})", Word( pObj+o*4 ) + pObj, u );
            }
            else if (b == 0x26)					// USING SPR[]
            {
                p = UsingOp(p, sb);
            }
            else if (b == 0x37)					// PUSH#kp
            {
                int b1 = Byte(p++);
                int n = (1 << ((b1 & 31) + 1));
                if ((b1 & 32) != 0)
                    --n;
                if ((b1 & 64) != 0)
                    n = ~n;
                sb.AppendFormat("{0} (${0:x})", n);
                if ((b1 & 128) != 0)
                    sb.AppendFormat("(bit7)?");
            }
            else if (b == 0x38)					// PUSH#k1
            {
                sb.AppendFormat("{0}", Byte(p++));
            }
            else if (b == 0x39)					// PUSH#k2
            {
                int n = Byte(p++) << 8;
                n += Byte(p++);
                sb.AppendFormat("{0}", n);
            }
            else if (b == 0x3a)					// PUSH#k3
            {
                int n = Byte(p++) << 16;
                n += Byte(p++) << 8;
                n += Byte(p++);
                sb.AppendFormat("{0}", n);
            }
            else if (b == 0x3b)					// PUSH#k4
            {
                int n = Byte(p++) << 24;
                n += Byte(p++) << 16;
                n += Byte(p++) << 8;
                n += Byte(p++);
                sb.AppendFormat("{0}", n);
            }
            else if (0x3d <= b && b <= 0x3f)			// REG[], REG[..], REG
            {
                int r = Byte(p++);
                if ((r & 0x90) == 0x90)
                    sb.AppendFormat("REG{0}\t{1}", ppup[(r >> 5) & 3], regNames[r & 15]);
                else
                    sb.AppendFormat("REG{0}\t${1:x2}?", ppup[(r >> 5) & 3], r);
                ///				sb.AppendFormat( "${0:x2}?", r );
                ///				sb.AppendFormat( "REG{0}\t{1}", ppup[ (r>>5) & 3 ], regNames[ r & 15 ] );
                if (b == 0x3d)
                    sb.Append("<>");
                else if (b == 0x3e)
                    sb.Append("<..>");
                if (((r >> 5) & 3) == 2)
                {
                    sb.Append(", ");
                    p = UsingOp(p, sb);
                }
            }
            else if ((b & 0xc0) == 0x40)				// 40..7f: 64 fast access
            {
                sb.AppendFormat("{0}\t", ppup[b & 3].PadRight(5));
                sb.AppendFormat("{0}+{1}", (b & 0x20) == 0 ? "VAR" : "Locals", b & 0x1c);
                if ((b & 3) == 2)
                {
                    sb.Append(", ");
                    p = UsingOp(p, sb);
                }
            }
            else if ((b & 128) != 0 && (b & 0x60) != 0x60)		// 80..df: 96 load/save
            {
                sb.AppendFormat("{0}.{1}\t", ppup[b & 3], sizeSuffix[(b >> 5) & 3]);
                if (((b >> 2) & 3) == 0)				// mem takes address from stack
                    sb.AppendFormat("Mem[]");
                else
                {						// non-mem uses variable address offset
                    int n = Byte(p++);
                    if ((n & 128) != 0)
                        n = ((n & 0x7f) << 8) + Byte(p++);
                    sb.AppendFormat("{0}+{1}", movl[(b >> 2) & 3], n);
                }

                sb.AppendFormat("{0}", (b & 0x10) != 0 ? "[]" : "");
                if ((b & 3) == 2)
                {
                    sb.Append(", ");
                    p = UsingOp(p, sb);
                }
            }
            return p;
        }

        static string[] ops_00_3f = new string[] {
			"FRAME\tCall with return value","FRAME\tCall without return value",
			"FRAME\tCall with abort trap","FRAME\tCall ignoring abort trap","GOTO","CALL","CALLOBJ","CALLOBJ[]",       
			"LOOPJPF","LOOPRPT","JPF","JPT","GOTO[]","CASE","CASER","LOOKEND",       
			"LOOKUP","LOOKDN","LOOKUPR","LOOKDNR","QUIT?","MARK","STRSIZE","STRCOMP",       
			"BYTEFIL","WORDFIL","LONGFIL","WAITPEQ","BYTEMOV","WORDMOV","LONGMOV","WAITPNE",       
			"CLKSET","COGSTOP","LRETSUB","WAITCNT","PUSHSPR[]", "POPSPR[]", "USINGSPR[]", "WAITVID",       
			"COGIFUN","LNEWFUN","LSETFUN","LCLRFUN","COGISUB","LNEWSUB","LSETSUB","LCLRSUB",       
			"ABORT","ABOVAL","RETURN","RETVAL","PUSH#-1","PUSH#0","PUSH#1","PUSH#kp",    
			"PUSH#k1","PUSH#k2","PUSH#k3","PUSH#k4","$3c?","REG[]","REG[..]","REG"
		};
        static string[] operatorOps = new string[] {
			"ROR",    "ROL",    "SHR",    "SHL",    "MIN",    "MAX",    "NEG",    "BIT_NOT", 
			"BIT_AND","ABS",    "BIT_OR", "BIT_XOR","ADD",    "SUB",    "SAR",    "BIT_REV",     
			"LOG_AND","ENCODE", "LOG_OR", "DECODE", "MPY",    "MPY_MSW","DIV",    "MOD",
			"SQRT",   "LT",     "GT",     "NE",     "EQ",     "LE",     "GE",     "LOG_NOT"
		};

        static int AddressOffset(int p, StringBuilder sb)
        {
            int b1 = Byte(p++), n;
            if ((b1 & 0x80) == 0)
            {
                if ((b1 & 0x40) == 0)
                    n = b1;			// 0..63
                else
                    n = b1 - 0x80;		// -64..-1
            }
            else
            {
                n = ((b1 & 0x7f) << 8) | Byte(p++);

                if ((b1 & 0x40) != 0)
                    n -= 32768;		// -16384..-1
                //else
                //    n = n;			// 0..16383      Batang 28/05/2013              
            }
            sb.AppendFormat(".{0}{1} (dest:${2:x4})", n >= 0 ? "+" : "", n, n + p);
            return p;
        }
        static string[] sizeSuffix = new string[] { "B", "W", "L" };
        static string[] ppup = new string[] { "PUSH", "POP", "USING", "PUSH#" };
        static string[] movl = new string[] { "mem", "OBJ", "VAR", "Locals" };

        static string[] regNames = new string[] {
													  "PAR",
													  "CNT",
													  "INA",
													  "INB",
													  "OUTA",
													  "OUTB",
													  "DIRA",
													  "DIRB",
													  "CTRA",
													  "CTRB",
													  "FRQA",
													  "FRQB",
													  "PHSA",
													  "PHSB",
													  "VCFG",
													  "VSCL" };


        static string[] usingOps_00_07 = new string[] {
															"COPY","?","RPTINCJ","?","?","?","RPTADDJ","?"
														};

        //08-3F Unary operations
        static string usingOps_08_3f(int b)
        {
            switch (b)
            {
                case 0x08: return "FWDRAND";
                case 0x0c: return "REVRAND";
                case 0x10: return "SEXBYTE";
                case 0x14: return "SEXWORD";
                case 0x18: return "POSTCLR";
                case 0x1c: return "POSTSET";
                case 0x20: return "PREINC.R";
                case 0x22: return "PREINC.B";
                case 0x24: return "PREINC.W";
                case 0x26: return "PREINC.L";
                case 0x28: return "POSTINC.R";
                case 0x2a: return "POSTINC.B";
                case 0x2c: return "POSTINC.W";
                case 0x2e: return "POSTINC.L";
                case 0x30: return "PREDEC.R";
                case 0x32: return "PREDEC.B";
                case 0x34: return "PREDEC.W";
                case 0x36: return "PREDEC.L";
                case 0x38: return "POSTDEC.R";
                case 0x3a: return "POSTDEC.B";
                case 0x3c: return "POSTDEC.W";
                case 0x3e: return "POSTDEC.L";
                default: return string.Format("{0:x2}???", b);
            }
        }
        static int UsingOp(int p, StringBuilder sb)
        {
            int b = Byte(p++);
            string post = "";
            if ((b & 128) != 0)
            {
                post = ", PUSH";
                b &= 127;
            }
            if (b <= 7)
                sb.Append(usingOps_00_07[b] + post);
            else if (b <= 0x3f)
                sb.Append(usingOps_08_3f(b) + post);
            else if (b <= 0x5f)
                sb.Append(operatorOps[b - 0x40] + post);
            else
                sb.Append("?");
            if ((b == 2) || (b == 6))	// RPTINCJ or RPTADDJ
            {
                sb.Append(" ");
                p = AddressOffset(p, sb);
            }
            return p;
        }

        static void Blanks(int n)
        {
            while (n-- > 0)
                Console.Write("   ");
        }
        static void PrintBytes(int p, int n)
        {
            while (n-- != 0)
                Console.Write("{0:x2} ", Byte(p++));
        }
        static int Byte(int addr)
        {
            return mem[addr];
        }
        static int Word(int addr)
        {
            return mem[addr] + (mem[addr + 1] << 8);
        }
        static int Long(int addr)
        {
            return Word(addr) + (Word(addr + 2) << 16);
        }





    }//
} // namespace Homespun