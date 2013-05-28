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


	Symbols and symbol table stuff for Spin compiler.

	A symbol is a named entity defined in the Spin input (or a predefined global).
	A Symbol consists of a string (the name of the entity), an associated
	Expression object, and information as to where the symbol will live in
	memory when the compiled program runs.

	A symbol table contains symbols.

	Each SpinObject has its own symbol table for objects defined in the
	object, such as constants and methods.

	There is a global symbol table which contains the symbol tables of
	all the SpinObjects in the program. If when compiling the compiler
	encounters "obj1#someConstant", it can look up "obj1" in the SpinObject
	symbol table to find out obj1's object type, such as "tv_text". Then it
	can look up "tv_text" in the global symbol table to get the symbol table
	for the tv_text object and then can look up "someConstant".

	Each method has a symbol table for local variables.


*/

using System;
using System.IO;
using System.Collections;
using System.Text;

namespace Homespun
{

    abstract class SymbolTableClass
    {
        Hashtable ht = new Hashtable();
        public Hashtable Table { get { return ht; } }
        public void AddSymbolInfo(SymbolInfo symbolInfo)
        {
            ht.Add(symbolInfo.IdToken.Text.ToUpper(), symbolInfo);
        }
    }

    class GlobalSymbolTable : SymbolTableClass
    {
        // Maps SpinObject (file) names (e.g. TV_Text) to Object file information

        static GlobalSymbolTable globalSymbolTable = new GlobalSymbolTable();
        static ArrayList globalList = new ArrayList();

        static bool duplicateObjectEliminationEnabled = true;
        static public void DisableDuplicateObjectElimination()
        {
            duplicateObjectEliminationEnabled = false;
        }

        public static GlobalSymbolInfo Lookup(SimpleToken token)
        {
            return (GlobalSymbolInfo)globalSymbolTable.Table[token.Text.ToUpper()];
        }
        public static GlobalSymbolInfo LookupExisting(SimpleToken token)
        {
            GlobalSymbolInfo gsi = (GlobalSymbolInfo)globalSymbolTable.Table[token.Text.ToUpper()];
            if (gsi == null)
                throw new ParseException("Couldn't find {0}", token);
            return gsi;
        }
        public static GlobalSymbolInfo Add(SimpleToken token, ObjectFileSymbolTable objectFileSymbolTable)
        {
            // New global symbols are added to the global symbol table and to globalList (at the end).
            // Existing global symbols are moved to the end of globalList. This is done to duplicate
            // PropTool's behavior. Why PropTool does what it does I have no idea.
            // 7/24 Finally realized why: when the objects are laid out in this order, all
            // the inter-object offsets are positive.

            GlobalSymbolInfo gsi = GlobalSymbolTable.Lookup(token);
            if (gsi == null)
            {
                gsi = new GlobalSymbolInfo(token, objectFileSymbolTable);
                globalSymbolTable.AddSymbolInfo(gsi);
                globalList.Add(gsi);
            }
            else
            {
                if (!gsi.AlreadyRead)
                    throw new ParseException("Circular object reference", token);
                for (int i = 0; i < globalList.Count; ++i)
                {
                    if (globalList[i] == gsi)
                    {
                        globalList.RemoveAt(i);
                        globalList.Add(gsi);
                        break;
                    }
                }
            }
            return gsi;
        }

        public static void CompileAll()
        {
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                gsi.SymbolTable.ResolveObjIndexes();
                gsi.SymbolTable.ResolveMethodIndexes();
            }
            int addr = 0x0010;
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                if ((Options.informationLevel & 2) != 0)
                    Console.WriteLine("compiling {0}", gsi.IdToken.Text);
                gsi.SymbolTable.HubAddress = addr;
                gsi.SymbolTable.DatPass1();
                gsi.SymbolTable.ResolveVarAndObj();
                gsi.SymbolTable.DatPass2();
                gsi.SizeInBytes = gsi.SymbolTable.CompileMethods();
                addr += gsi.SizeInBytes;
            }
        }

        public static void EliminateDuplicateObjects()
        {
            if (!duplicateObjectEliminationEnabled)
            {
                Console.WriteLine("Duplicate object elimination is disabled");
                return;
            }
            ///Console.WriteLine( "Eliminating dupes..." );;;
            ///		foreach( GlobalSymbolInfo gsi in globalList )
            ///			Console.WriteLine( gsi.IdToken.Text );;;
            for (int i = globalList.Count; --i >= 0; )
            {
                GlobalSymbolInfo gsiHi = globalList[i] as GlobalSymbolInfo;
                if (gsiHi.ForwardLink != null)
                    gsiHi = gsiHi.ForwardLink;
                for (int j = i; --j >= 0; )
                {
                    GlobalSymbolInfo gsiLo = globalList[j] as GlobalSymbolInfo;
                    //Console.WriteLine( "\t{0} vs {1}", gsiHi.IdToken.Text, gsiLo.IdToken.Text );;;
                    if (gsiLo.ForwardLink != null)
                    {
                        //Console.WriteLine( "\t\t{0} => {1} already", gsiLo.IdToken.Text, gsiLo.ForwardLink.IdToken.Text );;;
                        continue;
                    }
                    if (gsiLo.SymbolTable.CompareSymbolTable(gsiHi.SymbolTable))
                    {
                        //Console.WriteLine( "\t\t{0} => {1}", gsiLo.IdToken.Text, gsiHi.IdToken.Text );;;
                        gsiLo.ForwardLink = gsiHi;
                    }
                }
            }
            //foreach( GlobalSymbolInfo gsi in globalList )
            //	Console.WriteLine( "{0} -- {1}", gsi.IdToken.Text, gsi.ForwardLink == null ? "yes" : "no" );;;
        }

        public static void ToMemory(byte[] memory)
        {
            int baseAddress = 0x0010;
            int _SPACE = 0;
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                ConSymbolInfo csi_SPACE = LookupCon("_SPACE", gsi);
                if (csi_SPACE != null)
                {
                    int space = 0;
                    if (csi_SPACE.Value.IsInt)
                        space = csi_SPACE.Value.IntValue;
                    else
                        throw new ParseException("_SPACE must be an int", csi_SPACE.IdToken);
                    if (space < 0)
                        throw new ParseException("_SPACE < 0", csi_SPACE.IdToken);
                    Console.WriteLine("{0}#_SPACE = {1}", gsi.IdToken.Text, space); ; ;
                    _SPACE += space;
                }
            }
            if (_SPACE > 0)
            {
                _SPACE = (_SPACE + 3) & ~3;
                if (_SPACE < 12)
                    _SPACE = 12;
                Console.WriteLine("Reserving {0} bytes @ $0010", _SPACE);
                _SPACE += 8;	// reserve 8 bytes at the top for method
                baseAddress += _SPACE;
            }

            int address = baseAddress;
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                if (gsi.ForwardLink == null)
                {
                    address = gsi.AssignAddress(address);
                    ///Console.WriteLine( "assigned address to {0}", gsi.IdToken.Text );;;
                }
                ///else	Console.WriteLine( "did not assign address to {0}", gsi.IdToken.Text );;;
            }
            if (address >= Options.memorySize)
                throw new ParseException("Compiled image exceeds " + Options.memorySize.ToString() + " bytes");

            address = baseAddress;
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                if (gsi.ForwardLink == null)
                {
                    address = gsi.SymbolTable.ToMemory(memory, address);
                }
            }

            int frequency = 12000000;

            ConSymbolInfo csi_CLKFREQ = LookupCon("_CLKFREQ");
            ConSymbolInfo csi_XINFREQ = LookupCon("_XINFREQ");
            ConSymbolInfo csi_CLKMODE = LookupCon("_CLKMODE");

            int f = 0;
            if (csi_CLKFREQ != null) f |= 1;
            if (csi_XINFREQ != null) f |= 2;
            if (csi_CLKMODE != null) f |= 4;

            switch (f)
            {
                case 0:
                    break;
                case 1:
                    throw new ParseException("_CLKMODE must be specified", csi_CLKFREQ.IdToken);
                case 2:
                case 3:
                    throw new ParseException("_CLKMODE must be specified", csi_XINFREQ.IdToken);
                case 4:
                    throw new ParseException("_CLKFREQ or _XINFREQ must be specified", csi_CLKMODE.IdToken);
                case 5:
                    frequency = csi_CLKFREQ.Value.IntValue;
                    break;
                case 6:
                    frequency = csi_XINFREQ.Value.IntValue * (csi_CLKMODE.Value.IntValue >> 6);
                    break;
                case 7:
                    if (csi_CLKFREQ.Value.IntValue != csi_XINFREQ.Value.IntValue * (csi_CLKMODE.Value.IntValue >> 6))
                        throw new ParseException("Conflicting _CLKFREQ and _XINFREQ", csi_CLKFREQ.IdToken);
                    break;
            }
            memory[0x00] = (byte)((frequency) & 0xff);
            memory[0x01] = (byte)((frequency >> 8) & 0xff);
            memory[0x02] = (byte)((frequency >> 16) & 0xff);
            memory[0x03] = (byte)((frequency >> 24) & 0xff);

            memory[0x04] = Clkmode(csi_CLKMODE);

            memory[0x06] = (byte)0x10;
            memory[0x07] = (byte)0x00;

            varAddress = varBase = address;
            memory[0x08] = (byte)(address & 0xff);	// base of variables
            memory[0x09] = (byte)(address >> 8);

            GlobalSymbolInfo gsi0 = globalList[0] as GlobalSymbolInfo;	// This is the first object.

            address += gsi0.SymbolTable.TotalVarSize();

            ConSymbolInfo csi_FREE = LookupCon("_FREE");
            ConSymbolInfo csi_STACK = LookupCon("_STACK");
            int _FREE = csi_FREE == null ? 0 : csi_FREE.Value.IntValue;
            int _STACK = csi_STACK == null ? 16 : csi_STACK.Value.IntValue;

            if (Options.memorySize == 32768)
            {
                if (_STACK + _FREE >= 8192)
                    throw new ParseException("_STACK + _FREE >= 8192");

                int d = address / 4 + _FREE + _STACK - 8192;
                if (d > 0)
                    throw new ParseException("Too big by " + d.ToString() + " longs");
            }

            for (int i = 0; i < 2; ++i)
            {
                memory[address++] = 0xff;
                memory[address++] = 0xff;
                memory[address++] = 0xf9;
                memory[address++] = 0xff;
            }
            memory[0x0a] = (byte)(address & 0xff);	// base of stack
            memory[0x0b] = (byte)(address >> 8);

            int initialPC = gsi0.SymbolTable.OffsetOfFirstPub() + baseAddress;
            memory[0x0c] = (byte)(initialPC & 0xff);	// initial PC
            memory[0x0d] = (byte)(initialPC >> 8);

            address = (memory[0x0b] << 8) + memory[0x0a] + gsi0.SymbolTable.AllLocalsSize();	// initial SP;
            memory[0x0e] = (byte)(address & 0xff);	// initial SP
            memory[0x0f] = (byte)(address >> 8);

            if (_SPACE != 0)
            {
                /*
                Initial PC = $001c
                PBASE = $0010
                0010 Object xx xx 02 01 -- _SPACE bytes, 2+1 table entries
                0014 MetPtr yy yy 00 00 => Method at xxxx-8 (locals size:0)
                0018 ObjPtr xx xx 00 00 => Object (VAR offset: 0)
                ...
                00.. Method 01             FRAME        Call without return value
                00..        06 02 01       CALLOBJ      2:1 (method 1 of object @ $0024)
                00..        32             RETURN
                00..        00
                00..        00
                00..        00
                xxxx
                */
                int x = 0x0010 + _SPACE - 8;
                memory[0x0c] = (byte)(x & 0xff);	// initial PC
                memory[0x0d] = (byte)(x >> 8);

                memory[0x10] = (byte)(_SPACE & 0xff);
                memory[0x11] = (byte)(_SPACE >> 8);
                memory[0x12] = (byte)2;		// 2 table entries
                memory[0x13] = (byte)1;		// 1 table entry (object pointer)

                memory[0x14] = (byte)((x - 0x0010) & 0xff);	// ptr to method
                memory[0x15] = (byte)((x - 0x0010) >> 8);
                memory[0x16] = (byte)0;
                memory[0x17] = (byte)0;

                memory[0x18] = (byte)(_SPACE & 0xff);	// ptr to object
                memory[0x19] = (byte)(_SPACE >> 8);
                memory[0x1a] = (byte)0;
                memory[0x1b] = (byte)0;

                memory[x++] = (byte)0x01;	// FRAME (no return value)
                memory[x++] = (byte)0x06;	// CALLOBJ 2:1
                memory[x++] = (byte)0x02;
                memory[x++] = (byte)0x01;
                memory[x++] = (byte)0x32;	// RETURN
            }

            byte chksum = 0;
            foreach (byte b in memory)
                chksum += b;
            memory[0x05] = (byte)-chksum;
        }

        static int varBase;
        public static int varAddress; // start of VAR space; used to determine how many bytes to write to .binary file.
        // probably the same as varBase, but I'm too lazy to verify; easier just to add a new variable.

        static ConSymbolInfo LookupCon(string name)
        {
            return LookupCon(name, globalList[0] as GlobalSymbolInfo);
        }
        static ConSymbolInfo LookupCon(string name, GlobalSymbolInfo gsi)
        {
            SymbolInfo si = gsi.SymbolTable.Lookup(new SimpleToken(null, SimpleTokenType.Id, name, 0, 0));
            if (si == null)
            {
                return null;
            }
            else
            {
                ConSymbolInfo csi = si as ConSymbolInfo;
                if (csi == null)
                {
                    throw new ParseException(name + " must be a CON", si.IdToken);
                }
                return csi;
            }
        }
        static byte Clkmode(ConSymbolInfo csi)
        {
            if (csi == null)
                return 0;
            int clkmode = csi.Value.IntValue;
            int b = -1;
            for (int i = 0; i < 6; ++i)
            {
                if ((clkmode & (1 << i)) != 0)
                {
                    if (b != -1)
                        throw new ParseException("Invalid _CLKMODE", csi.IdToken);
                    b = i;
                }
            }
            byte m = 0;
            switch (b)
            {
                case 0: m = 0x00; break;
                case 1: m = 0x01; break;
                case 2: m = 0x02; break;
                case 3: m = 0x2a; break;
                case 4: m = 0x32; break;
                case 5: m = 0x3a; break;
            }
            clkmode >>= 6;
            if (clkmode == 0)
                return m;
            m |= 0x60;

            b = -1;
            for (int i = 0; i < 5; ++i)
            {
                if ((clkmode & (1 << i)) != 0)
                {
                    if (b != -1)
                        throw new ParseException("Invalid _CLKMODE", csi.IdToken);
                    b = i;
                }
            }
            m += (byte)(b + 1);
            return m;
        }
        public static void Dump(StringWriter sw)
        {
            ArrayList lines = new ArrayList();
            lines.Add(string.Format("Frequency: {0} Hz", Dis.Mem[0] + (Dis.Mem[1] << 8) + (Dis.Mem[2] << 16) + (Dis.Mem[3] << 24)));
            Dis.DumpColumns(sw, 0, 4, lines, 4, "' ");
            lines[0] = "XTAL mode";
            Dis.DumpColumns(sw, 4, 1, lines, 4, "' ");
            lines[0] = "Checksum";
            Dis.DumpColumns(sw, 5, 1, lines, 4, "' ");
            lines[0] = "Base of program";
            Dis.DumpColumns(sw, 6, 2, lines, 4, "' ");
            lines[0] = "Base of variables";
            Dis.DumpColumns(sw, 8, 2, lines, 4, "' ");
            lines[0] = "Base of stack";
            Dis.DumpColumns(sw, 10, 2, lines, 4, "' ");
            lines[0] = "Initial program counter";
            Dis.DumpColumns(sw, 12, 2, lines, 4, "' ");
            lines[0] = "Initial stack pointer";
            Dis.DumpColumns(sw, 14, 2, lines, 4, "' ");

            bool first = true;
            GlobalSymbolInfo gsi0 = globalList[0] as GlobalSymbolInfo;
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                if (gsi.ForwardLink == null)
                {
                    sw.WriteLine();
                    Dis.Heading(sw, "", '*');
                    Dis.Heading(sw, gsi.IdToken.Text, ' ');
                    Dis.Heading(sw, "", '*');
                    sw.WriteLine();
                    gsi.SymbolTable.Dump1(sw, gsi.Address);
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        ///						gsi.SymbolTable.Dump2( sw, 0, false );
                    }
                    first = false;
                }
            }
            gsi0.SymbolTable.Dump2(sw, varBase, true);

            first = true;
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                if (gsi.ForwardLink != null)
                {
                    if (first)
                    {
                        sw.Write("Eliminated objects: ");
                    }
                    sw.WriteLine("{0} (duplicate of {1})", gsi.IdToken.Text, gsi.ForwardLink.IdToken.Text);
                }
            }
        }
        public static void WriteSobs()
        {
            foreach (GlobalSymbolInfo gsi in globalList)
            {
                string filename = gsi.Path + gsi.IdToken.Text;
                filename = filename.Substring(0, filename.Length - 4) + "sob";	// .spin => .sob
                gsi.SymbolTable.WriteSob(filename);
            }
        }
    }

    class ObjectFileSymbolTable : SymbolTableClass
    {
        // Maps Object-defined IDs to Symbol information

        LocalSymbolTable localSymbolTable = null;

        ArrayList conList = new ArrayList();
        ArrayList datList = new ArrayList();
        ArrayList datEntryList = new ArrayList();
        ArrayList objList = new ArrayList();
        ArrayList priList = new ArrayList();
        ArrayList pubList = new ArrayList();
        ArrayList byteVarList = new ArrayList();
        ArrayList wordVarList = new ArrayList();
        ArrayList longVarList = new ArrayList();

        public ObjectFileSymbolTable(Tokenizer tokenizer)
        {
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "TRUE", 0, 0)),
                new IntExpr(null, -1));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "FALSE", 0, 0)),
                new IntExpr(null, 0));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "POSX", 0, 0)),
                new IntExpr(null, 2147483647));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "NEGX", 0, 0)),
                new IntExpr(null, -2147483648));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "PI", 0, 0)),
                new FloatExpr(null, (float)Math.PI));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "RCFAST", 0, 0)),
                new IntExpr(null, 0x001));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "RCSLOW", 0, 0)),
                new IntExpr(null, 0x002));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "XINPUT", 0, 0)),
                new IntExpr(null, 0x004));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "XTAL1", 0, 0)),
                new IntExpr(null, 0x008));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "XTAL2", 0, 0)),
                new IntExpr(null, 0x010));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "XTAL3", 0, 0)),
                new IntExpr(null, 0x020));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "PLL1X", 0, 0)),
                new IntExpr(null, 0x040));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "PLL2X", 0, 0)),
                new IntExpr(null, 0x080));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "PLL4X", 0, 0)),
                new IntExpr(null, 0x100));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "PLL8X", 0, 0)),
                new IntExpr(null, 0x200));
            AddBuiltInConSymbol(new IdToken(new SimpleToken(tokenizer, SimpleTokenType.Id, "PLL16X", 0, 0)),
                new IntExpr(null, 0x400));
        }

        public int OffsetOfFirstPub()	/// gack. my symbol table design needs work.
        {
            return (pubList[0] as MethodSymbolInfo).Offset;
        }

        public int PubCount()
        {
            return pubList.Count;
        }

        public int AllLocalsSize()		/// see gack above
        {
            // returns size of locals + params + result variable
            MethodSymbolInfo msi = pubList[0] as MethodSymbolInfo;
            return msi.LocalsSize + (msi.ParamCount + 1) * 4;
        }

        int sizeInBytes;
        int varSize;
        public int VarSize { get { return varSize; } }

        public GlobalSymbolInfo AddToGlobalSymbolTable(SimpleToken token)
        {
            return GlobalSymbolTable.Add(token, this);
        }

        public SymbolInfo Lookup(SimpleToken idToken)
        // Does a lookup in local symbol table (if we're in a method scope),
        // then object file symbol table, then global symbol table.
        // Returns null if id is not found.
        {
            SymbolInfo symbolInfo = null;

            if (idToken.Text[0] == ':')
            {
                idToken.Text = latestLabel + idToken.Text;
            }

            if (localSymbolTable != null)
            {
                symbolInfo = localSymbolTable.Lookup(idToken);
                if (symbolInfo != null)
                    return symbolInfo;
            }

            symbolInfo = (SymbolInfo)Table[idToken.Text.ToUpper()];
            if (symbolInfo != null)
                return symbolInfo;

            return GlobalSymbolTable.Lookup(idToken);
        }
        public SymbolInfo LookupExisting(SimpleToken idToken)
        // Throws if id is not found.
        {
            SymbolInfo symbolInfo = Lookup(idToken);
            if (symbolInfo == null)
                throw new ParseException("Unknown symbol " + idToken.Text, idToken);
            return symbolInfo;
        }
        public void AssertUndefined(IdToken idToken)
        {
            if (Lookup(idToken) != null)
                throw new ParseException(idToken.Text + " is already defined", idToken);
        }

        public void BeginMethodScope()
        {
            localSymbolTable = new LocalSymbolTable();
            stringList = new ArrayList();
            stringTargetList = new ArrayList();
        }
        public void EndMethodScope()
        {
            localSymbolTable = null;
            stringList = null;
            stringTargetList = null;
        }

        ArrayList stringList;
        public ArrayList StringList { get { return stringList; } }
        ArrayList stringTargetList;
        public ArrayList StringTargetList { get { return stringTargetList; } }

        public JmpTarget nextJmpTarget = null;
        public JmpTarget quitJmpTarget = null;
        public int caseNesting = 0;	/* levels of CASE between REPEAT and NEXT/QUIT:
									 * REPEAT
									 *  CASE
									 *   NEXT   <-- caseNesting = 1
									 *   CASE
									 *    QUIT  <-- caseNesting = 2
									 *  NEXT    <-- caseNesting = 0
									 */
        public bool insidePlainRepeat;

        public void AddLocalVariable(IdToken idToken, int offset)
        {
            AssertUndefined(idToken);
            LocalSymbolInfo localSymbolInfo = new LocalSymbolInfo(idToken, offset);
            localSymbolTable.AddSymbolInfo(localSymbolInfo);
        }
        public void AddConSymbol(IdToken idToken, Expr e)
        {
            AssertUndefined(idToken);
            ConSymbolInfo conSymbolInfo = new ConSymbolInfo(idToken, e);
            AddSymbolInfo(conSymbolInfo);
            conList.Add(conSymbolInfo);
        }
        public void AddBuiltInConSymbol(IdToken idToken, Expr e)
        {
            AssertUndefined(idToken);
            ConSymbolInfo conSymbolInfo = new ConSymbolInfo(idToken, e);
            AddSymbolInfo(conSymbolInfo);
        }

        public void Skipped()
        {
            // When a file is skipped (because a file of the same name has already
            // been seen) we still have to make sure its objects are moved to the
            // end of the global symbol list.
            foreach (ObjSymbolInfo osi in objList)
            {
                osi.FilenameToken.SymbolTable.AddToGlobalSymbolTable(osi.FilenameToken);
            }
        }

        struct DatLabelEntry
        {
            public int alignment;
            public IdToken labelToken;
            public bool standalone;

            public DatLabelEntry(int alignment, IdToken labelToken, bool standalone)
            {
                this.alignment = alignment;
                this.labelToken = labelToken;
                this.standalone = standalone;
            }
        }
        struct DatOrgEntry
        {
            public SimpleToken orgToken;
            public Expr orgExpr;
            public int endLineNumber;
            public DatOrgEntry(SimpleToken orgToken, Expr orgExpr, int endLineNumber)
            {
                this.orgToken = orgToken;
                this.orgExpr = orgExpr;
                this.endLineNumber = endLineNumber;
            }
        }
        struct DatOrgxEntry
        {
            public SimpleToken orgxToken;

            public DatOrgxEntry(SimpleToken orgxToken)
            {
                this.orgxToken = orgxToken;
            }
        }
        struct DatDataEntry
        {
            public int alignment;
            public int size;
            public Expr dataExpr;
            public Expr countExpr;
            public SimpleToken token;

            public DatDataEntry(int alignment, int size, Expr dataExpr, Expr countExpr, SimpleToken token)
            {
                this.alignment = alignment;
                this.size = size;
                this.dataExpr = dataExpr;
                this.countExpr = countExpr;
                this.token = token;
            }
        }
        struct DatInstructionEntry
        {
            public IPropInstruction instruction;
            public int cond;
            public Expr eD;
            public Expr eS;
            public bool immediate;
            public int effect;
            public SimpleToken token;
            public int endLineNumber;

            public DatInstructionEntry(IPropInstruction instruction, int cond, Expr eD, Expr eS, bool immediate, int effect, SimpleToken token, int endLineNumber)
            {
                this.instruction = instruction;
                this.cond = cond;
                this.eD = eD;
                this.eS = eS;
                this.immediate = immediate;
                this.effect = effect;
                this.token = token;
                this.endLineNumber = endLineNumber;
            }
        }
        struct DatResEntry
        {
            public Expr e;
            public SimpleToken resToken;
            public int endLineNumber;
            public DatResEntry(SimpleToken resToken, Expr e, int endLineNumber)
            {
                this.resToken = resToken;
                this.e = e;
                this.endLineNumber = endLineNumber;
            }
        }
        struct DatFitEntry
        {
            public SimpleToken token;
            public Expr e;
            public DatFitEntry(SimpleToken token, Expr e)
            {
                this.token = token;
                this.e = e;
            }
        }
        struct DatFileEntry
        {
            public SimpleToken fileToken;
            public SimpleToken filenameToken;
            public byte[] bytes;
            public int endLineNumber;

            public DatFileEntry(SimpleToken fileToken, SimpleToken filenameToken, byte[] bytes, int endLineNumber)
            {
                this.fileToken = fileToken;
                this.filenameToken = filenameToken;
                this.bytes = bytes;
                this.endLineNumber = endLineNumber;
            }
        }
        public void AddDatLabelEntry(int alignment, IdToken labelToken, bool standalone)
        {
            datEntryList.Add(new DatLabelEntry(alignment, labelToken, standalone));
        }
        public void AddDatOrgEntry(SimpleToken orgToken, Expr orgExpr, int endLineNumber)
        {
            datEntryList.Add(new DatOrgEntry(orgToken, orgExpr, endLineNumber));
        }
        public void AddDatOrgxEntry(SimpleToken orgxToken)
        {
            datEntryList.Add(new DatOrgxEntry(orgxToken));
        }
        public void AddDatDataEntry(int alignment, int size, Expr dataExpr, Expr countExpr, SimpleToken token)
        {
            datEntryList.Add(new DatDataEntry(alignment, size, dataExpr, countExpr, token));
        }
        public void AddDatInstructionEntry(IPropInstruction instruction, int cond, Expr eD, Expr eS, bool immediate, int effect, SimpleToken token, int endLineNumber)
        {
            datEntryList.Add(new DatInstructionEntry(instruction, cond, eD, eS, immediate, effect, token, endLineNumber));
        }
        public void AddDatResEntry(SimpleToken resToken, Expr e, int endLineNumber)
        {
            datEntryList.Add(new DatResEntry(resToken, e, endLineNumber));
        }
        public void AddDatFitEntry(SimpleToken token, Expr e)
        {
            datEntryList.Add(new DatFitEntry(token, e));
        }
        public void AddDatFileEntry(SimpleToken fileToken, SimpleToken filenameToken, byte[] bytes, int endLineNumber)
        {
            datEntryList.Add(new DatFileEntry(fileToken, filenameToken, bytes, endLineNumber));
        }
        public void AddDatSourceReference(SimpleToken endToken)
        {
            // Using SourceReference to mark the end of a line of data entries.
            datEntryList.Add(new SourceReference(endToken, endToken.LineNumber));
        }
        string latestLabel;	// latest non-local label. Prepended to local labels to disambiguate them.

        int dp;		// pointer into DAT space.
        int ooo;		// ORG offset offset.
        bool orgxMode = false;

        public int Here { get { return orgxMode ? 0 : (dp + ooo) / 4; } }

        public void DatPass1()
        {
            int alignment = 1;	// default alignment and size for labels.

            dp += sizeOfObjHeader;
            ooo = -dp;

            latestLabel = "";

            foreach (object o in datEntryList)
            {
                if (o is DatLabelEntry)
                {
                    DatLabelEntry dle = (DatLabelEntry)o;
                    if (dle.alignment != 0)
                    {
                        alignment = dle.alignment;
                        dp = (dp + alignment - 1) & -alignment;
                    }
                    else
                    {
                        dle.alignment = alignment;
                    }
                    if (dle.labelToken.Text[0] != ':')
                    {
                        AddDatSymbol(dle.labelToken, alignment, dp, orgxMode ? 0 : dp + ooo);
                        latestLabel = dle.labelToken.Text;
                    }
                    else
                    {
                        AddLocalDatSymbol(dle.labelToken, alignment, dp, orgxMode ? 0 : dp + ooo);
                    }
                }
                else if (o is DatOrgEntry)
                {
                    DatOrgEntry doe = (DatOrgEntry)o;
                    dp = (dp + 3) & -4;
                    int org = 0;
                    if (doe.orgExpr != null)
                    {
                        org = Expr.EvaluateIntConstant(doe.orgExpr, true);
                    }
                    ooo = 4 * org - dp;
                    orgxMode = false;
                    alignment = 4;
                }
                else if (o is DatOrgxEntry)
                {
                    orgxMode = true;
                }
                else if (o is DatInstructionEntry)
                {
                    alignment = 4;
                    dp = (dp + 3) & -4;
                    dp += 4;
                }
                else if (o is DatResEntry)
                {
                    alignment = 4;
                    dp = (dp + 3) & -4;
                    DatResEntry dre = (DatResEntry)o;
                    if (dre.e != null)
                        ooo += 4 * Expr.EvaluateIntConstant(dre.e, true);
                    else
                        ooo += 4;	// default: one long.
                }
                else if (o is DatFitEntry && !orgxMode)
                {
                    DatFitEntry dfe = (DatFitEntry)o;
                    int n = 496;
                    if (dfe.e != null)
                        n = Expr.EvaluateIntConstant(dfe.e, true);
                    int cogAddr = (dp + ooo + 3) / 4;
                    if (cogAddr > n)
                        throw new ParseException("Origin exceeds FIT limit by " + (cogAddr - n).ToString(), dfe.token);
                }
                else if (o is DatFileEntry)
                {
                    DatFileEntry dfe = (DatFileEntry)o;
                    alignment = 1;
                    dp += dfe.bytes.Length;
                }
                else if (o is DatDataEntry)
                {
                    DatDataEntry dde = (DatDataEntry)o;
                    dp = (dp + dde.alignment - 1) & -dde.alignment;
                    int count = 1;
                    if (dde.countExpr != null)
                        count = Expr.EvaluateIntConstant(dde.countExpr, true);
                    dp += dde.size * count;
                    alignment = dde.alignment;
                }
            }
            datBytes = new byte[dp - sizeOfObjHeader];
        }

        byte[] datBytes;

        int WriteDatBytes(int dp, int size, int data)
        {
            while (--size >= 0)
            {
                datBytes[dp++ - sizeOfObjHeader] = (byte)data;
                data >>= 8;
            }
            return dp;
        }

        public void DatPass2()
        {
            dp = sizeOfObjHeader;				// pointer into DAT space.
            ooo = 0;
            orgxMode = false;
            int alignment = 1;	// default alignment and size for labels.

            latestLabel = "";

            foreach (object o in datEntryList)
            {
                if (o is DatLabelEntry)
                {
                    DatLabelEntry dle = (DatLabelEntry)o;
                    if (dle.alignment != 0)
                    {
                        alignment = dle.alignment;
                        dp = (dp + alignment - 1) & -alignment;
                    }
                    if (dle.labelToken.Text.IndexOf(':') < 0)
                    {
                        latestLabel = dle.labelToken.Text;
                    }
                }
                else if (o is DatOrgEntry)
                {
                    DatOrgEntry doe = (DatOrgEntry)o;
                    dp = (dp + 3) & -4;
                    int org = 0;
                    if (doe.orgExpr != null)
                    {
                        org = Expr.EvaluateIntConstant(doe.orgExpr, true);
                    }
                    ooo = 4 * org - dp;
                    orgxMode = false;
                    alignment = 4;
                }
                else if (o is DatOrgxEntry)
                {
                    orgxMode = true;
                }
                else if (o is DatInstructionEntry)
                {
                    DatInstructionEntry die = (DatInstructionEntry)o;
                    int d = 0;
                    if (die.eD != null)
                    {
                        d = Expr.EvaluateIntConstant(die.eD, true);
                        if (d != (d & 0x1ff))
                            throw new ParseException("Destination register cannot exceed $1ff", die.eD.Token);
                        d &= 0x1ff;
                    }
                    int s = 0;
                    if (die.eS != null)
                    {
                        s = Expr.EvaluateIntConstant(die.eS, true);
                        if (s != (s & 0x1ff))
                            throw new ParseException("Source register cannot exceed $1ff", die.eS.Token);
                        s &= 0x1ff;
                    }
                    int i = (int)die.instruction.Propcode + (d << 9) + s + (die.immediate ? 1 << 22 : 0);
                    i |= ((die.effect & 7) << 23);	// effect bits
                    if (die.effect >= 8)			// die.effect & 8 => NR
                        i &= ~(1 << 23);
                    i = (i & ~(0x0f << 18)) | (die.cond << 18);			// cond bits
                    alignment = 4;
                    dp = (dp + 3) & -4;
                    dp = WriteDatBytes(dp, 4, i);
                }
                else if (o is DatResEntry)
                {
                    alignment = 4;
                    dp = (dp + 3) & -4;
                    DatResEntry dre = (DatResEntry)o;
                    if (dre.e != null)
                        ooo += 4 * Expr.EvaluateIntConstant(dre.e, true);
                    else
                        ooo += 4;	// default: one long.
                }
                else if (o is DatFitEntry)
                {
                    // nothing to do in 2nd pass.
                }
                else if (o is DatFileEntry)
                {
                    DatFileEntry dfe = (DatFileEntry)o;
                    alignment = 1;
                    for (int i = 0; i < dfe.bytes.Length; ++i)
                    {
                        dp = WriteDatBytes(dp, 1, dfe.bytes[i]);
                    }
                }
                else if (o is DatDataEntry)
                {
                    DatDataEntry dde = (DatDataEntry)o;
                    dp = (dp + dde.alignment - 1) & -dde.alignment;
                    int count = 1;
                    if (dde.countExpr != null)
                        count = Expr.EvaluateIntConstant(dde.countExpr, true);

                    FloInt d = Expr.EvaluateConstant(dde.dataExpr, true);
                    int data = d.AsIntBits();
                    if (!d.IsInt && dde.size != 4)
                        throw new ParseException("Floating-point not allowed", dde.dataExpr.Token);
                    while (--count >= 0)
                    {
                        if ((dde.size == 1 && ((data & ~0xff) != 0)) || ((dde.size == 2 && ((data & ~0xffff) != 0))))
                        {
                            dde.dataExpr.Token.Tokenizer.PrintWarning("Data truncation", dde.dataExpr.Token);
                        }
                        dp = WriteDatBytes(dp, dde.size, data);
                    }
                }
            }
        }

        public void DumpDat(StringWriter sw, int hubAddress)
        {
            dp = sizeOfObjHeader + hubAddress;				// pointer into DAT space.
            ooo = -dp;
            orgxMode = false;
            int alignment = 1;	// default alignment and size for labels.

            latestLabel = "";

            for (int i = 0; i < datEntryList.Count; ++i)
            {
                object o = datEntryList[i];
                if (o is DatLabelEntry)
                {
                    DatLabelEntry dle = (DatLabelEntry)o;
                    if (dle.alignment != 0)
                    {
                        alignment = dle.alignment;
                    }
                    int dp0 = dp;
                    dp = (dp + alignment - 1) & -alignment;
                    ArrayList lines = new ArrayList();
                    if (dle.standalone)
                    {
                        lines.Add(dle.labelToken.Tokenizer.Line(dle.labelToken.LineNumber));
                        Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, lines, 4, "' ");
                    }
                    else
                    {
                        Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, lines, 4, "  ");
                    }
                }
                else if (o is DatOrgEntry)
                {
                    DatOrgEntry doe = (DatOrgEntry)o;
                    int dp0 = dp;
                    dp = (dp + 3) & -4;
                    Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, new ArrayList(), 4, "");

                    int org = 0;
                    if (doe.orgExpr != null)
                    {
                        org = Expr.EvaluateIntConstant(doe.orgExpr, true);
                    }
                    ooo = 4 * org - dp;
                    orgxMode = false;
                    alignment = 4;
                    ArrayList lines = new ArrayList();
                    for (int l = doe.orgToken.LineNumber; l <= doe.endLineNumber; ++l)
                        lines.Add(doe.orgToken.Tokenizer.Line(l));
                    Dis.DumpColumns(sw, dp, 0, ooo, orgxMode, lines, 4, "' ");
                }
                else if (o is DatOrgxEntry)
                {
                    orgxMode = true;
                }
                else if (o is DatInstructionEntry)
                {
                    DatInstructionEntry die = (DatInstructionEntry)o;
                    alignment = 4;
                    int dp0 = dp;
                    dp = (dp + 3) & -4;
                    Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, new ArrayList(), 4, "");
                    dp0 = dp;
                    dp += 4;
                    ArrayList lines = new ArrayList();
                    for (int l = die.token.LineNumber; l <= die.endLineNumber; ++l)
                        lines.Add(die.token.Tokenizer.Line(l));
                    Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, lines, 4, "' ");
                }
                else if (o is DatResEntry)
                {
                    DatResEntry dre = (DatResEntry)o;
                    alignment = 4;
                    int dp0 = dp;
                    dp = (dp + 3) & -4;
                    Dis.DumpColumns(sw, dp0, dp - dp0, new ArrayList(), 4, "");
                    dp0 = dp;
                    ArrayList lines = new ArrayList();
                    for (int l = dre.resToken.LineNumber; l <= dre.endLineNumber; ++l)
                        lines.Add(dre.resToken.Tokenizer.Line(l));
                    Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, lines, 4, "' ");

                    if (dre.e != null)
                        ooo += 4 * Expr.EvaluateIntConstant(dre.e, true);
                    else
                        ooo += 4;	// default: one long.
                }
                else if (o is DatFitEntry)
                {
                    // nothing to do in 2nd pass.
                }
                else if (o is DatFileEntry)
                {
                    DatFileEntry dfe = (DatFileEntry)o;
                    alignment = 1;
                    int dp0 = dp;
                    dp += dfe.bytes.Length;
                    ArrayList lines = new ArrayList();
                    for (int l = dfe.fileToken.LineNumber; l <= dfe.endLineNumber; ++l)
                        lines.Add(dfe.fileToken.Tokenizer.Line(l));
                    Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, lines, 4, "'");
                }
                else if (o is DatDataEntry)
                {
                    DatDataEntry dde = (DatDataEntry)o;
                    SimpleToken firstToken = dde.token;
                    int dp0 = dp;
                    dp = (dp + dde.alignment - 1) & -dde.alignment;
                    Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, new ArrayList(), 4, "");
                    dp0 = dp;
                    alignment = dde.alignment;

                    while (true)
                    {
                        int count = 1;
                        if (dde.countExpr != null)
                            count = Expr.EvaluateIntConstant(dde.countExpr, true);
                        dp += count * dde.size;

                        o = datEntryList[++i];
                        if (!(o is DatDataEntry))
                            break;
                        dde = (DatDataEntry)o;
                        dp = (dp + dde.alignment - 1) & -dde.alignment;
                    }
                    SourceReference sr = datEntryList[i] as SourceReference;
                    ArrayList lines = new ArrayList();
                    for (int l = firstToken.LineNumber; l <= sr.endLineNumber; ++l)
                        lines.Add(firstToken.Tokenizer.Line(l));
                    Dis.DumpColumns(sw, dp0, dp - dp0, ooo, orgxMode, lines, 4, "' ");
                }
                else if (o is SourceReference)
                {
                    SourceReference sr = o as SourceReference;
                    ArrayList lines = new ArrayList();
                    for (int l = sr.token.LineNumber; l <= sr.endLineNumber; ++l)
                        lines.Add(sr.token.Tokenizer.Line(l));
                    Dis.DumpColumns(sw, dp, 0, ooo, orgxMode, lines, 4, "' ");
                }
            }
        }

        public void AddDatSymbol(IdToken idToken, int alignment, int dp, int cogAddress4)
        {
            AssertUndefined(idToken);
            DatSymbolInfo dsi = new DatSymbolInfo(idToken, alignment, dp, cogAddress4);
            AddSymbolInfo(dsi);
            datList.Add(dsi);
        }
        public void AddLocalDatSymbol(IdToken idToken, int alignment, int dp, int cogAddress4)
        {
            idToken.Text = latestLabel + idToken.Text;
            AddDatSymbol(idToken, alignment, dp, cogAddress4);
        }

        public void AddObjSymbol(IdToken idToken, SimpleToken filenameToken, Expr countExpr, bool needsVarSpace)
        {
            AssertUndefined(idToken);
            ObjSymbolInfo objSymbolInfo = new ObjSymbolInfo(idToken, filenameToken, countExpr, needsVarSpace);
            AddSymbolInfo(objSymbolInfo);
            objList.Add(objSymbolInfo);
        }

        public void AddMethod(IdToken idToken, bool isPub, int paramCount, ArrayList localNameList, ArrayList localCountList, ArrayList statementList, int endLineNumber)
        {
            AssertUndefined(idToken);
            MethodSymbolInfo methodSymbolInfo = new MethodSymbolInfo(idToken, isPub, paramCount, localNameList, localCountList, statementList, endLineNumber);
            AddSymbolInfo(methodSymbolInfo);
            if (isPub)
                pubList.Add(methodSymbolInfo);
            else
                priList.Add(methodSymbolInfo);
        }
        public void AddVarSymbol(IdToken idToken, SizeToken sizeToken, ArrayList countExprList)
        {
            AssertUndefined(idToken);
            VarSymbolInfo varSymbolInfo = new VarSymbolInfo(idToken, sizeToken, countExprList);

            AddSymbolInfo(varSymbolInfo);

            switch (sizeToken.Text.ToUpper())
            {
                case "BYTE":
                    byteVarList.Add(varSymbolInfo);
                    break;
                case "WORD":
                    wordVarList.Add(varSymbolInfo);
                    break;
                case "LONG":
                    longVarList.Add(varSymbolInfo);
                    break;
            }
        }
        public void ResolveVarAndObj()
        {
            // Long vars are allocated first, then word vars, then byte vars.

            int varOffset = 0;
            foreach (VarSymbolInfo varInfo in longVarList)
            {
                varInfo.Offset = varOffset;
                varOffset += varInfo.TotalCount() * 4;
            }

            foreach (VarSymbolInfo varInfo in wordVarList)
            {
                varInfo.Offset = varOffset;
                varOffset += varInfo.TotalCount() * 2;
            }

            foreach (VarSymbolInfo varInfo in byteVarList)
            {
                varInfo.Offset = varOffset;
                varOffset += varInfo.TotalCount();
            }

            varSize = (varOffset + 3) & -4;
        }
        public void ResolveObjIndexes()
        {
            // Assign each OBJ its index in the object header.

            foreach (ObjSymbolInfo objInfo in objList)
            {
                objInfo.Index = nObjs + pubList.Count + priList.Count + 1;
                if (objInfo.CountExpr == null)
                    nObjs += objInfo.Count = 1;
                else
                    nObjs += objInfo.Count = Expr.EvaluateIntConstant(objInfo.CountExpr);
            }

            sizeOfObjHeader = (nObjs + pubList.Count + priList.Count + 1) * 4;
        }
        public void ResolveMethodIndexes()
        {
            // Assign each PUB and PRI its index in the object header.

            int methodIndex = 1;
            foreach (MethodSymbolInfo msi in pubList)
                msi.Index = methodIndex++;
            foreach (MethodSymbolInfo msi in priList)
                msi.Index = methodIndex++;
        }

        int hubAddress;
        public int HubAddress
        {
            get { return hubAddress; }
            set { hubAddress = value; }
        }

        int sizeOfObjHeader;
        int nObjs = 0;

        public int CompileMethods()
        {
            int n1 = pubList.Count + priList.Count + 1;
            int n2 = nObjs;

            // First method starts after (n1 + n2) header table entries + DAT space.
            int s = n1 * 4 + n2 * 4 + datBytes.Length;

            foreach (MethodSymbolInfo methodInfo in pubList)
            {
                s = methodInfo.Compile(s);
            }
            foreach (MethodSymbolInfo methodInfo in priList)
            {
                s = methodInfo.Compile(s);
            }
            return sizeInBytes = (s + 3) & -4;	// Round size up to multiple of 4.
        }

        public void Dump1(StringWriter sw, int address)
        {
            bool b = true;
            foreach (ConSymbolInfo csi in conList)
            {
                if (csi.alreadyEvaluated)
                {
                    if (b)
                    {
                        Dis.Heading(sw, "CONs", '=');
                        b = false;
                    }
                    sw.Write("{0} = ", csi.IdToken.Text);
                    if (csi.Value.IsInt)
                        sw.WriteLine(csi.Value.IntValue);
                    else
                        sw.WriteLine(csi.Value.FloatValue);
                }
            }

            Dis.Heading(sw, "Object Header", '=');

            int n1 = pubList.Count + priList.Count + 1;
            int n2 = nObjs;
            int index = 0;

            ArrayList lines = new ArrayList();
            lines.Add(string.Format("' {0} bytes, {1}-1 methods, {2} object pointers",
                Dis.Mem[hubAddress + 0] + (Dis.Mem[hubAddress + 1] << 8), Dis.Mem[hubAddress + 2], Dis.Mem[hubAddress + 3]));
            foreach (MethodSymbolInfo pubInfo in pubList)
            {
                lines.Add(string.Format("' ptr #{0} to ${1:x4}: PUB {2} (locals size: {3})",
                    ++index, pubInfo.Offset + hubAddress, pubInfo.IdToken.Text, pubInfo.LocalsSize));
            }
            foreach (MethodSymbolInfo priInfo in priList)
            {
                lines.Add(string.Format("' ptr #{0} to ${1:x4}: PRI {2} (locals size: {3})",
                    ++index, priInfo.Offset + hubAddress, priInfo.IdToken.Text, priInfo.LocalsSize));
            }
            foreach (ObjSymbolInfo osi in this.objList)
            {
                GlobalSymbolInfo gsi = GlobalSymbolTable.LookupExisting(osi.FilenameToken);
                if (osi.CountExpr == null)
                {
                    int varOffset = Dis.Mem[address + (index + 1) * 4 + 2] + Dis.Mem[address + (index + 1) * 4 + 3] * 256;
                    lines.Add(string.Format("' ptr #{0} to ${1:x4}: OBJ {2} : {3} (VAR offset: {4})",
                        ++index, gsi.Address, osi.IdToken.Text, osi.FilenameToken.Text, varOffset));
                }
                else
                {
                    for (int i = 0; i < osi.Count; ++i)
                    {
                        int varOffset = Dis.Mem[address + (index + 1) * 4 + 2] + Dis.Mem[address + (index + 1) * 4 + 3] * 256;
                        lines.Add(string.Format("' ptr #{0} to ${1:x4}: OBJ {2}({3}) : {4} (VAR offset: {5})",
                            ++index, gsi.Address, osi.IdToken.Text, i, osi.FilenameToken.Text, varOffset));
                    }
                }
            }
            Dis.DumpColumns(sw, address, (n1 + n2) * 4, lines, 4, "");

            if (datBytes.Length > 0)
            {
                Dis.Heading(sw, "DAT Section", '=');
                DumpDat(sw, hubAddress);
            }

            int start = 0;
            int end = 0;

            foreach (MethodSymbolInfo pubInfo in pubList)
            {
                start = address + pubInfo.Offset;
                end = start + pubInfo.SizeInBytes - 1;
                Dis.Heading(sw, string.Format("Method #{0}: PUB {1}", pubInfo.Index, pubInfo.IdToken.Text), '=');
                ///				sw.WriteLine( "${0:x4}-{1:x4}: method # {2} (PUB)", start, end, pubInfo.Index );//, pubInfo.IdToken.Text );
                for (int i = pubInfo.IdToken.LineNumber; i <= pubInfo.EndLineNumber; ++i)
                {
                    sw.WriteLine("\'{0}", pubInfo.IdToken.Tokenizer.Line(i));
                }
                pubInfo.DumpBytecodeListing(sw, start, hubAddress);
                b = true;
            }
            foreach (MethodSymbolInfo priInfo in priList)
            {
                start = address + priInfo.Offset;
                end = start + priInfo.SizeInBytes - 1;
                Dis.Heading(sw, string.Format("Method #{0}: PRI {1}", priInfo.Index, priInfo.IdToken.Text), '=');
                ///				sw.WriteLine( "${0:x4}-${1:x4}: method # {2} (PRI)", start, end, priInfo.Index );//, priInfo.IdToken.Text );
                for (int i = priInfo.IdToken.LineNumber; i <= priInfo.EndLineNumber; ++i)
                {
                    sw.WriteLine("\'{0}", priInfo.IdToken.Tokenizer.Line(i));
                }
                priInfo.DumpBytecodeListing(sw, start, hubAddress);
                b = true;
            }

            ++end;
            Dis.DumpColumns(sw, end, -end & 3, new ArrayList(), 4, "");
        }
        public void Dump2(StringWriter sw, int varBase, bool topObject)
        {
            varBase = DumpVars(sw, "", varBase, topObject, true);
            Dis.DumpColumns(sw, varBase, 8, new ArrayList(), 8, "");

            sw.WriteLine();

            /*/			if( objList.Count != 0 )
                        {
                            foreach( ObjSymbolInfo osi in objList )
                            {
                                GlobalSymbolInfo gsi = GlobalSymbolTable.Lookup( osi.FilenameToken );
                                string subscript = "";
                                if( osi.CountExpr != null )
                                {
                                    subscript = string.Format( "({0})", osi.Count );
                                }
                                if( topObject )
                                {
                                    sw.WriteLine( "${0:x4}: OBJ {1}{2} : {3}", varBase, osi.IdToken.Text, subscript, osi.FilenameToken.Text );
                                }
                                else
                                {
                                    sw.WriteLine( "VAR+${0:x4}: OBJ {1}{2} : {3}", varBase, osi.IdToken.Text, subscript, osi.FilenameToken.Text );
                                }
                                varBase += gsi.SymbolTable.totalVarSize * osi.Count;
                            }
                            sw.WriteLine();
                        }
            /*/
        }
        int DumpVars(StringWriter sw, string parent, int varBase, bool printAddresses, bool topObject)
        {
            if (longVarList.Count + wordVarList.Count + byteVarList.Count != 0)
            {
                if (topObject)
                    Dis.Heading(sw, "VAR Section", '=');
                varBase = DumpVarList(sw, longVarList, "LONG", 4, parent, varBase, printAddresses);
                varBase = DumpVarList(sw, wordVarList, "WORD", 2, parent, varBase, printAddresses);
                varBase = DumpVarList(sw, byteVarList, "BYTE", 1, parent, varBase, printAddresses);
            }
            Dis.DumpColumns(sw, varBase, (-varBase) & 3, new ArrayList(), 4, "");
            varBase = (varBase + 3) & ~3;
            int varBaseBeforeObjs = varBase;

            foreach (ObjSymbolInfo osi in objList)
            {
                if (!osi.NeedsVarSpace)
                    continue;
                GlobalSymbolInfo gsi = GlobalSymbolTable.Lookup(osi.FilenameToken);
                string subscript = "";
                if (osi.CountExpr != null)
                    subscript = "[0]";
                for (int i = 0; i < osi.Count; ++i)
                {
                    if (i != 0)
                        subscript = string.Format("[{0}]", i);
                    varBase = gsi.SymbolTable.DumpVars(sw, parent + osi.IdToken.Text + subscript + ".", varBase, printAddresses, false);
                }
            }
            ///			return topObject ? varBaseBeforeObjs : varBase;
            return varBase;
        }
        static int DumpVarList(StringWriter sw, ArrayList varList, string s, int dataSize, string parent, int varBase, bool printAddresses)
        {
            foreach (VarSymbolInfo varInfo in varList)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{0} {1}{2}", s, parent, varInfo.IdToken.Text);
                int count = 1;
                string formatString = "({0}";
                foreach (Expr e in varInfo.DimExprs)
                {
                    int d = Expr.EvaluateIntConstant(e, false);
                    sb.AppendFormat(formatString, d);
                    formatString = ", {0}";
                    count *= d;
                }
                if (varInfo.DimExprs.Length != 0)
                    sb.Append(")");
                int nBytes = count * dataSize;
                ArrayList lines = new ArrayList();
                lines.Add(sb.ToString());
                Dis.DumpColumns(sw, varBase, nBytes, lines, 8, "' ");
                varBase += nBytes;
                /*/				if( printAddresses )
                                {
                                    sw.Write( "${0:x4}: {1} {2}{3}", varBase, s, parent, varInfo.IdToken.Text );
                                }
                                else
                                {
                                    sw.Write( "VAR+${0:x4}: {1} {2}{3}", varBase, s, parent, varInfo.IdToken.Text );
                                }
                                int count;
                                if( varInfo.CountExpr == null )
                                {
                                    count = 1;
                                    sw.WriteLine();
                                }
                                else
                                {
                                    count = Expr.EvaluateIntConstant( varInfo.CountExpr, false );
                                    sw.WriteLine( "({0})", count );
                                }
                                varBase += count * dataSize;
                /*/
            }
            return varBase;
        }
        public int ToMemory(byte[] memory, int address)
        {
            int baseAddress = address;
            int n1 = pubList.Count + priList.Count + 1;
            int n2 = nObjs;

            // 1. Lay out header.

            memory[address++] = (byte)(sizeInBytes & 0xff);
            memory[address++] = (byte)(sizeInBytes >> 8);
            memory[address++] = (byte)n1;
            memory[address++] = (byte)n2;

            // 2. Lay out method pointers.

            foreach (MethodSymbolInfo msi in pubList)
            {
                memory[address++] = (byte)(msi.Offset & 0xff);
                memory[address++] = (byte)(msi.Offset >> 8);
                memory[address++] = (byte)(msi.LocalsSize & 0xff);
                memory[address++] = (byte)(msi.LocalsSize >> 8);
            }
            foreach (MethodSymbolInfo msi in priList)
            {
                memory[address++] = (byte)(msi.Offset & 0xff);
                memory[address++] = (byte)(msi.Offset >> 8);
                memory[address++] = (byte)(msi.LocalsSize & 0xff);
                memory[address++] = (byte)(msi.LocalsSize >> 8);
            }

            // 3. Lay out object pointers.

            int varOffset = varSize;
            foreach (ObjSymbolInfo osi in objList)
            {
                GlobalSymbolInfo gsi = GlobalSymbolTable.Lookup(osi.FilenameToken);

                for (int i = 0; i < osi.Count; ++i)
                {
                    int objOffset = gsi.Address - baseAddress;
                    if (gsi.ForwardLink != null)
                    {
                        ///Console.WriteLine( "{0} has a forward link to {1}", osi.FilenameToken.Text, gsi.ForwardLink.IdToken.Text );;;
                        objOffset = gsi.ForwardLink.Address - baseAddress;
                        ///Console.WriteLine( "{0:x} - {1:x} => {2:x}", gsi.ForwardLink.Address, baseAddress, objOffset );;;
                    }
                    memory[address++] = (byte)(objOffset & 0xff);
                    memory[address++] = (byte)(objOffset >> 8);
                    memory[address++] = (byte)(varOffset & 0xff);
                    memory[address++] = (byte)(varOffset >> 8);
                    if (osi.NeedsVarSpace)
                        varOffset += gsi.SymbolTable.TotalVarSize();
                }
            }

            // 4. Lay out DAT area.

            foreach (byte b in datBytes)
                memory[address++] = b;

            // 5. Lay out method bytecode.

            foreach (MethodSymbolInfo methodInfo in pubList)
            {
                address = methodInfo.ToMemory(memory, address);
            }
            foreach (MethodSymbolInfo methodInfo in priList)
            {
                address = methodInfo.ToMemory(memory, address);
            }

            address = (address + 3) & -4;
            return address;
        }

        public bool CompareSymbolTable(ObjectFileSymbolTable otherSymbolTable)
        {
            ArrayList msiList0 = new ArrayList();
            ArrayList msiList1 = new ArrayList();
            foreach (MethodSymbolInfo msi in pubList)
                msiList0.Add(msi);
            foreach (MethodSymbolInfo msi in priList)
                msiList0.Add(msi);
            foreach (MethodSymbolInfo msi in otherSymbolTable.pubList)
                msiList1.Add(msi);
            foreach (MethodSymbolInfo msi in otherSymbolTable.priList)
                msiList1.Add(msi);
            if (msiList0.Count != msiList1.Count)
                return false;

            for (int i = 0; i < msiList0.Count; ++i)
            {
                MethodSymbolInfo msi0 = (MethodSymbolInfo)msiList0[i];
                MethodSymbolInfo msi1 = (MethodSymbolInfo)msiList1[i];
                if (msi0.Offset != msi1.Offset)
                    return false;
                if (msi0.LocalsSize != msi1.LocalsSize)
                    return false;
                if (msi0.SizeInBytes != msi1.SizeInBytes)
                    return false;
                byte[] bytes0 = new byte[msi0.SizeInBytes];
                byte[] bytes1 = new byte[msi1.SizeInBytes];
                msi0.ToMemory(bytes0, 0);
                msi1.ToMemory(bytes1, 0);
                for (int j = 0; j < bytes0.Length; ++j)
                    if (bytes0[j] != bytes1[j])
                        return false;
            }

            ArrayList varOffsetList0 = new ArrayList();
            ArrayList includedObjectList0 = new ArrayList();
            ArrayList varOffsetList1 = new ArrayList();
            ArrayList includedObjectList1 = new ArrayList();

            int varOffset = varSize;
            foreach (ObjSymbolInfo osi in objList)
            {
                for (int i = 0; i < osi.Count; ++i)
                {
                    GlobalSymbolInfo gsi = GlobalSymbolTable.Lookup(osi.FilenameToken);
                    GlobalSymbolInfo p = gsi.ForwardLink == null ? gsi : gsi.ForwardLink;
                    includedObjectList0.Add(p);
                    varOffsetList0.Add(varOffset);
                    varOffset += gsi.SymbolTable.TotalVarSize();
                }
            }

            varOffset = varSize;
            foreach (ObjSymbolInfo osi in otherSymbolTable.objList)
            {
                for (int i = 0; i < osi.Count; ++i)
                {
                    GlobalSymbolInfo gsi = GlobalSymbolTable.Lookup(osi.FilenameToken);
                    GlobalSymbolInfo p = gsi.ForwardLink == null ? gsi : gsi.ForwardLink;
                    includedObjectList1.Add(p);
                    varOffsetList1.Add(varOffset);
                    varOffset += gsi.SymbolTable.TotalVarSize();
                }
            }

            if (varOffsetList0.Count != varOffsetList1.Count)
            {
                Console.WriteLine("different # of objs: {0} <> {1}", varOffsetList0.Count, varOffsetList1.Count); ; ;
                return false;
            }

            for (int i = 0; i < includedObjectList0.Count; ++i)
            {
                //Console.WriteLine( "here we go" );;;
                //Console.WriteLine( "::: {0} ::: {1}", (includedObjectList0[i] as GlobalSymbolInfo).IdToken.Text,
                //				 (includedObjectList1[i] as GlobalSymbolInfo).IdToken.Text );;;
                if (includedObjectList0[i] != includedObjectList1[i])
                {
                    //					Console.WriteLine( "different objs: {0} <> {1}", (includedObjectList0[i] as GlobalSymbolInfo).IdToken.Text,
                    //											 (includedObjectList1[i] as GlobalSymbolInfo).IdToken.Text );;;
                    return false;
                }
            }

            for (int i = 0; i < varOffsetList0.Count; ++i)
            {
                if ((int)varOffsetList0[i] != (int)varOffsetList1[i])
                {
                    //Console.WriteLine( "**** UNEXPECTED **** different offsets: {0} <> {1}", (int)varOffsetList0[i], (int)varOffsetList1[i] );
                    return false;
                }
            }

            // Compare DAT bytes.
            if (this.datBytes.Length != otherSymbolTable.datBytes.Length)
                return false;
            for (int i = 0; i < this.datBytes.Length; ++i)
                if (this.datBytes[i] != otherSymbolTable.datBytes[i])
                    return false;

            return true;
        }

        int totalVarSize = -1;

        public int TotalVarSize()
        {
            if (totalVarSize >= 0)
                return totalVarSize;

            // Compute VAR space needed by object and child objects.
            totalVarSize = varSize;
            foreach (ObjSymbolInfo osi in objList)
            {
                if (!osi.NeedsVarSpace)
                    continue;
                for (int i = 0; i < osi.Count; ++i)
                {
                    GlobalSymbolInfo gsi = GlobalSymbolTable.Lookup(osi.FilenameToken);
                    totalVarSize += gsi.SymbolTable.TotalVarSize();
                }
            }
            return totalVarSize;
        }

        enum SobExport { IntCon, FloatCon, Pub, Pri };

        public void WriteSob(string filename)
        {
            int x = filename.LastIndexOf('\\');
            if (x >= 0)
                filename = filename.Substring(x + 1, filename.Length - x - 1);
            Console.WriteLine("WriteSob: {0}", filename);
            /*
                SOB file format:
                4 bytes: SOB file format version #
                4 bytes: OBJ timestamp or version
                2 bytes: NUMEXPORTS
                2 bytes: EXPORTSIZE
                2 bytes: NUMIMPORTS
                2 bytes: IMPORTSIZE
                2 bytes: OBJBINSIZE (this is always a multiple of 4)
                4 bytes: hash of OBJ's binary code.
                1 byte: checksum
                1 byte: reserved
                2 bytes: size of OBJ's VAR space.
                EXPORTSIZE bytes: exported symbols: CONs, PUBs, maybe PRIs.
                IMPORTSIZE bytes: OBJ's sub-OBJs.
                OBJBINSIZE bytes: the compiled OBJ: header followed by methods.
             */

            int SobVersion = 'S' + ('O' << 8) + ('B' << 16) + ('1' << 24);
            int timestamp = 0;
            int numExports = 0;
            int exportSize = 0;
            int numImports = 0;
            int importSize = 0;
            int objBinSize = 0;
            hash = 0;
            checksum = 0;

            FileStream fs = new FileStream(filename, FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(SobVersion);
            bw.Write(timestamp);
            bw.Write(hash);				// tbd -- we'll come back and fill this in later.
            bw.Write((short)numExports);	// "
            bw.Write((short)exportSize);	// "
            bw.Write((short)numImports);	// "
            bw.Write((short)importSize);	// "
            bw.Write((short)objBinSize);	// "
            bw.Write(checksum);			// "
            bw.Write((byte)0);
            bw.Write((short)this.varSize);

            foreach (ConSymbolInfo csi in conList)
            {
                if (csi.Value.IsInt)
                {
                    foreach (char ch in csi.IdToken.Text.ToUpper())
                        bw.Write(ch);
                    bw.Write((byte)0);
                    bw.Write((byte)SobExport.IntCon);
                    bw.Write(csi.Value.IntValue);
                }
                else
                {
                    foreach (char ch in csi.IdToken.Text.ToUpper())
                        bw.Write(ch);
                    bw.Write((byte)0);
                    bw.Write((byte)SobExport.FloatCon);
                    bw.Write(csi.Value.FloatValue);
                }
                ++numExports;
                exportSize += csi.IdToken.Text.Length + 6; // name + 1 null, 1 export type, 4 int or float
            }

            foreach (MethodSymbolInfo msi in pubList)
            {
                foreach (char ch in msi.IdToken.Text.ToUpper())
                    bw.Write(ch);
                bw.Write((byte)0);
                bw.Write((byte)SobExport.Pub);
                bw.Write((byte)msi.Index);
                bw.Write((byte)msi.ParamCount);
                ++numExports;
                exportSize += msi.IdToken.Text.Length + 4; // name + 1 null, 1 export type, 1 index, 1 param count
            }

            // (Do the same for PRIs? Maybe.)

            // Now the imports:

            foreach (ObjSymbolInfo osi in objList)
            {
                string s = osi.FilenameToken.Text.ToUpper();
                if (s.EndsWith(".SPIN"))
                    s = s.Substring(0, s.Length - 5);
                foreach (char ch in s)
                    bw.Write(ch);
                bw.Write((byte)0);
                bw.Write((short)osi.Count);
                bw.Write((byte)0);
                ++numImports;
                importSize += s.Length + 4; // name + 1 null, 2 count, 1 reserved
            }

            // Here comes the Spin object binary header:

            bw.Write(Hash(sizeInBytes & 0xff));
            bw.Write(Hash(sizeInBytes >> 8));
            bw.Write(Hash(pubList.Count + priList.Count + 1));
            bw.Write(Hash(nObjs));
            objBinSize += 4;

            foreach (MethodSymbolInfo msi in pubList)
            {
                bw.Write(Hash(msi.Offset & 0xff));
                bw.Write(Hash(msi.Offset >> 8));
                bw.Write(Hash(msi.LocalsSize & 0xff));
                bw.Write(Hash(msi.LocalsSize >> 8));
                objBinSize += 4;
            }
            foreach (MethodSymbolInfo msi in priList)
            {
                bw.Write(Hash(msi.Offset & 0xff));
                bw.Write(Hash(msi.Offset >> 8));
                bw.Write(Hash(msi.LocalsSize & 0xff));
                bw.Write(Hash(msi.LocalsSize >> 8));
                objBinSize += 4;
            }

            foreach (ObjSymbolInfo osi in objList)
            {
                for (int i = 0; i < osi.Count; ++i)
                {
                    bw.Write(Hash(0));
                    bw.Write(Hash(0));
                    bw.Write(Hash(0));
                    bw.Write(Hash(0));	// 4 bytes; these entries in the header are empty placeholders
                    //  to be filled in by the linker.
                    objBinSize += 4;
                }
            }

            foreach (byte b in datBytes)
            {
                bw.Write(Hash(b));
            }

            objBinSize += datBytes.Length;

            foreach (MethodSymbolInfo msi in pubList)
            {
                byte[] memory = new byte[msi.SizeInBytes];
                msi.ToMemory(memory, 0);
                foreach (byte b in memory)
                {
                    bw.Write(Hash(b));
                }
                objBinSize += memory.Length;
            }

            foreach (MethodSymbolInfo msi in priList)
            {
                byte[] memory = new byte[msi.SizeInBytes];
                msi.ToMemory(memory, 0);
                foreach (byte b in memory)
                {
                    bw.Write(Hash(b));
                }
                objBinSize += memory.Length;
            }
            int n = objBinSize;
            n = -n & 3;
            objBinSize += n;

            while (--n >= 0)
            {
                bw.Write(Hash(0));
            }
            bw.Seek(8, SeekOrigin.Begin);
            bw.Write(hash);				// We've come back to fill these values in, as promised.
            bw.Write((short)numExports);	// "
            bw.Write((short)exportSize);	// "
            bw.Write((short)numImports);	// "
            bw.Write((short)importSize);	// "
            bw.Write((short)objBinSize);	// "
            bw.Write(checksum);			// "
            bw.Close();
        }
        int hash;
        byte checksum;
        byte Hash(int x)
        {
            if ((x & 0xffffff00) != 0)
                throw new Exception("Bad input to Hash function");
            checksum += (byte)x; // also compute checksum while we're at it
            int c = (hash >> 31) & 1;
            hash = ((hash << 1) | c) ^ x;
            return (byte)x;
        }
        /*
                public void WriteSob( string filename)
                {
                    StreamWriter sw = new StreamWriter( filename );
                    sw.WriteLine( "sob 0.0" );
                    int nInts = 0;
                    int nFloats = 0;
                    foreach( ConSymbolInfo csi in conList )
                    {
                        if( csi.Value.IsInt )
                            ++nInts;
                        else
                            ++nFloats;
                    }
                    sw.WriteLine( "{0} int cons", nInts );
                    foreach( ConSymbolInfo csi in conList )
                    {
                        if( csi.Value.IsInt )
                            sw.WriteLine( "{0} {1}", csi.IdToken.Text, csi.Value.IntValue );
                    }
                    sw.WriteLine( "{0} float cons", nFloats );
                    foreach( ConSymbolInfo csi in conList )
                    {
                        if( !csi.Value.IsInt )
                            sw.WriteLine( "{0} {1}", csi.IdToken.Text, csi.Value.FloatValue );
                    }
                    sw.WriteLine( "{0} pubs", pubList.Count );
                    foreach( MethodSymbolInfo msi in pubList )
                    {
                        sw.WriteLine( "{0} {1}",
                            msi.IdToken.Text, msi.ParamCount );
                    }
        //			sw.WriteLine( "{0} pris", priList.Count );
        //			foreach( MethodSymbolInfo msi in priList )
        //			{
        //				sw.WriteLine( "{0} {1} {2}",
        //					msi.IdToken.Text, msi.ParamCount, msi.SizeInBytes );
        //			}
                    foreach( ObjSymbolInfo osi in objList )
                    {
                        sw.WriteLine( "{0} {1}", osi.FilenameToken.Text, osi.Count );
                    }

                    sw.WriteLine( "\t{0} var bytes", this.varSize );

                    int n1 = pubList.Count + priList.Count + 1;
                    int n2 = nObjs;
                    sw.WriteLine( "{0:x2} {1:x2} {2:x2} {3:x2}", sizeInBytes & 0xff, sizeInBytes >> 8, n1, n2 );

                    foreach( MethodSymbolInfo msi in pubList )
                    {
                        sw.WriteLine( "{0:x2} {1:x2} {2:x2} {3:x2}",
                            msi.Offset & 0xff, msi.Offset >> 8, msi.LocalsSize & 0xff, msi.LocalsSize >> 8 );
                    }
                    foreach( MethodSymbolInfo msi in priList )
                    {
                        sw.WriteLine( "{0:x2} {1:x2} {2:x2} {3:x2}",
                            msi.Offset & 0xff, msi.Offset >> 8, msi.LocalsSize & 0xff, msi.LocalsSize >> 8 );
                    }

                    foreach( ObjSymbolInfo osi in objList )
                    {
                        for( int i=0; i<osi.Count; ++i )
                        {
                            sw.WriteLine( "xx xx xx xx" );
                        }
                    }

                    foreach( byte b in datBytes )
                    {
                        sw.WriteLine( "{0:x2}", b );
                    }

                    int n = datBytes.Length;

                    foreach( MethodSymbolInfo msi in pubList )
                    {
                        byte [] memory = new byte[ msi.SizeInBytes ];
                        msi.ToMemory( memory, 0 );
                        foreach( byte b in memory )
                        {
                            sw.WriteLine( "{0:x2}", b );
                        }
                        n += memory.Length;
                    }

                    foreach( MethodSymbolInfo msi in priList )
                    {
                        byte [] memory = new byte[ msi.SizeInBytes ];
                        msi.ToMemory( memory, 0 );
                        foreach( byte b in memory )
                        {
                            sw.WriteLine( "{0:x2}", b );
                        }
                        n += memory.Length;
                    }

                    n = -n & 3;
                    while( --n >= 0 )
                    {
                        sw.WriteLine( "00" );
                    }
                    sw.Close();
                }
        */
    }

    class LocalSymbolTable : SymbolTableClass
    {
        // Maps method-local IDs to local variable information
        public SymbolInfo Lookup(SimpleToken token)
        {
            return (SymbolInfo)Table[token.Text.ToUpper()];
        }
    }

    abstract class SymbolInfo
    {
        SimpleToken idToken;
        public SimpleToken IdToken { get { return idToken; } }
        public SymbolInfo(SimpleToken idToken)
        {
            this.idToken = idToken;
        }
    }

    class GlobalSymbolInfo : SymbolInfo
    {
        ObjectFileSymbolTable symbolTable;
        public ObjectFileSymbolTable SymbolTable { get { return symbolTable; } }

        bool alreadyRead = false;
        public bool AlreadyRead
        {
            get { return alreadyRead; }
            set { alreadyRead = value; }
        }

        int address;
        public int Address { get { return address; } }
        int sizeInBytes;
        public int SizeInBytes
        {
            get { return sizeInBytes; }
            set { sizeInBytes = value; }
        }

        string path;
        public string Path
        {
            get { return path; }
            set { path = value; }
        }

        public GlobalSymbolInfo(SimpleToken idToken, ObjectFileSymbolTable symbolTable)
            : base(idToken)
        {
            this.symbolTable = symbolTable;
        }

        public int AssignAddress(int address)
        {
            this.address = address;
            return address + sizeInBytes;
        }
        GlobalSymbolInfo forwardLink = null;
        public GlobalSymbolInfo ForwardLink
        {
            get
            {
                return forwardLink;
            }
            set
            {
                forwardLink = value;
            }
        }
    }

    abstract class ObjectFileSymbolInfo : SymbolInfo
    {
        public ObjectFileSymbolInfo(IdToken idToken)
            : base(idToken)
        {
        }
    }

    class ConSymbolInfo : ObjectFileSymbolInfo
    {
        Expr e;
        public bool alreadyEvaluated = false;
        bool beingEvaluated = false;
        FloInt value;
        public ConSymbolInfo(IdToken idToken, Expr e)
            : base(idToken)
        {
            this.e = e;
        }
        public FloInt Value
        {
            get
            {
                if (!alreadyEvaluated)
                {
                    if (beingEvaluated)
                        throw new ParseException("Circular reference", e.Token);
                    beingEvaluated = true;
                    value = Expr.EvaluateConstant(e);
                    alreadyEvaluated = true;
                }
                return value;
            }
        }
    }

    class DatSymbolInfo : ObjectFileSymbolInfo
    {
        int alignment;
        int dp;
        int cogAddressX4;

        public int Alignment { get { return alignment; } }
        public int Dp
        {
            get { return dp; }
            set { dp = value; }
        }
        public int CogAddressX4 { get { return cogAddressX4; } }

        public DatSymbolInfo(IdToken idToken, int alignment, int dp, int cogAddressX4)
            : base(idToken)
        {
            this.alignment = alignment;
            this.dp = dp;
            this.cogAddressX4 = cogAddressX4;
        }
    }

    class ObjSymbolInfo : ObjectFileSymbolInfo
    {
        SimpleToken filenameToken;
        public SimpleToken FilenameToken { get { return filenameToken; ;; } }
        Expr countExpr;
        public Expr CountExpr { get { return countExpr; } }

        int count;
        public int Count
        {
            get { return count; }
            set { count = value; }
        }

        int index;
        public int Index
        {
            get { return index; }
            set { index = value; }
        }

        bool needsVarSpace;
        public bool NeedsVarSpace { get { return needsVarSpace; } }

        public ObjSymbolInfo(IdToken idToken, SimpleToken filenameToken, Expr countExpr, bool needsVarSpace)
            : base(idToken)
        {
            this.filenameToken = filenameToken;
            this.countExpr = countExpr;
            this.needsVarSpace = needsVarSpace;
        }
    }

    class MethodSymbolInfo : ObjectFileSymbolInfo
    {
        bool isPub;
        ArrayList localNameList;
        ArrayList localCountList;
        ArrayList statementList;
        int paramCount;
        int localsSize;		// valid only after Compile() has been called.
        int sizeInBytes;	// ditto
        int offset;		// Offset in memory from start of object.
        int index;		// this method's index in the method table.
        int endLineNumber;

        public bool IsPub { get { return isPub; } }
        public ArrayList LocalNameList { get { return localNameList; } }
        public ArrayList LocalCountList { get { return localCountList; } }
        public ArrayList StatementList { get { return statementList; } }
        public int ParamCount { get { return paramCount; } }
        public int LocalsSize { get { return localsSize; } }
        public int SizeInBytes { get { return sizeInBytes; } }
        public int Offset
        {
            get { return offset; }
            set { offset = value; }
        }
        public int Index
        {
            get { return index; }
            set { index = value; }
        }
        public int EndLineNumber { get { return endLineNumber; } }

        public MethodSymbolInfo(IdToken idToken, bool isPub, int paramCount, ArrayList localNameList, ArrayList localCountList, ArrayList statementList, int endLineNumber)
            : base(idToken)
        {
            this.isPub = isPub;
            this.paramCount = paramCount;
            this.localNameList = localNameList;
            this.localCountList = localCountList;
            this.statementList = statementList;
            this.endLineNumber = endLineNumber;
        }
        public int Compile(int a)
        {
            this.Offset = a;	// Compiling bytecode to object starting at offset a.
            // This information is needed by Fixup for CASE/LOOKDOWN/LOOKUP and STRING offsets.
            int i = 0;
            int offset = 0;
            if (localNameList.Count > 0 && localNameList[0] == null)
            {
                i = 1;
                offset = 4;
            }
            localsSize = 0;

            IdToken.SymbolTable.BeginMethodScope();

            for (; i < localNameList.Count; ++i)
            {
                IdToken.SymbolTable.AddLocalVariable(localNameList[i] as IdToken, offset);
                ///Console.WriteLine( "assertundefined {0}", (localNameList[i] as IdToken).Text );;;
                ///IdToken.SymbolTable.AssertUndefined( localNameList[i] as IdToken );;;
                Expr countExpr = localCountList[i] as Expr;
                int d = 4;
                if (countExpr != null)
                    d = 4 * Expr.EvaluateIntConstant(countExpr);
                offset += d;
                if (i > paramCount) localsSize += d;
            }

            SimpleToken resultToken = new SimpleToken(IdToken.Tokenizer, SimpleTokenType.Id, "result", 0, 0);
            if (IdToken.SymbolTable.Lookup(resultToken) == null)
                IdToken.SymbolTable.AddLocalVariable(new IdToken(resultToken), 0);

            bytecodeList = new ArrayList();
            foreach (Stmt s in statementList)
            {
                s.MakeByteCode(bytecodeList);
            }
            bytecodeList.Add((byte)0x32);	// every method has an invisible RETURN;

            // Now append string literals.
            for (int j = 0; j < IdToken.SymbolTable.StringList.Count; ++j)
            {
                bytecodeList.Add((JmpTarget)IdToken.SymbolTable.StringTargetList[j]);
                bytecodeList.Add(new StringStart());
                string s = (string)IdToken.SymbolTable.StringList[j];
                foreach (char ch in s)
                {
                    bytecodeList.Add((byte)ch);
                }
                bytecodeList.Add((byte)0);
                bytecodeList.Add(new StringEnd());
            }

            while (BytecodeFixupPass(a))
            {
            }

            sizeInBytes = 0;
            foreach (object o in bytecodeList)
            {
                if (o is byte)
                {
                    ++sizeInBytes;
                }
                else if (o is JmpStart)
                {
                    sizeInBytes += (o as JmpStart).bytecode.Length;
                }
                else if (o is OffsetStart)
                {
                    sizeInBytes += (o as OffsetStart).bytecode.Length;
                }
                else if (o is StringOffsetStart)
                {
                    sizeInBytes += (o as StringOffsetStart).bytecode.Length;
                }
            }

            IdToken.SymbolTable.EndMethodScope();

            return a + sizeInBytes;
        }

        ArrayList bytecodeList;

        bool BytecodeFixupPass(int address)
        {
            // Resolve jumps. If anything changes, return true.
            // Call this function repeatedly until it returns false (indicating
            // that everything's settled into place.
            bool changed = false;

            // 1st pass: assign addresses
            foreach (object o in bytecodeList)
            {
                if (o is byte)
                {
                    ++address;
                }
                else if (o is JmpStart)
                {
                    JmpStart start = o as JmpStart;
                    if (start.address != address)
                        changed = true;
                    start.address = address;
                    address += start.bytecode.Length;
                }
                else if (o is OffsetStart)
                {
                    OffsetStart start = o as OffsetStart;
                    if (start.address != address)
                        changed = true;
                    start.address = address;
                    address += start.bytecode.Length;
                }
                else if (o is StringOffsetStart)
                {
                    StringOffsetStart start = o as StringOffsetStart;
                    if (start.address != address)
                        changed = true;
                    start.address = address;
                    address += start.bytecode.Length;
                }
                else if (o is JmpTarget)
                {
                    JmpTarget target = o as JmpTarget;
                    if (target.address != address)
                        changed = true;
                    target.address = address;
                }
                // else o is SourceStart or StartEnd
            }

            // 2nd pass: resolve jumps
            foreach (object o in bytecodeList)
            {
                if (o is JmpStart)
                {
                    JmpStart start = o as JmpStart;
                    int delta = start.target.address - (start.address + start.bytecode.Length);
                    bool twoBytes = start.bytecode.Length == 3;
                    byte[] offsetBytes = MakeAddressOffset(delta, twoBytes);
                    if (start.bytecode.Length == offsetBytes.Length + 1)
                    {
                        for (int i = 0; i < offsetBytes.Length; ++i)
                        {
                            if (start.bytecode[i + 1] != offsetBytes[i])
                            {
                                changed = true;
                                start.bytecode[i + 1] = offsetBytes[i];
                            }
                        }
                    }
                    else
                    {
                        changed = true;
                        byte op = start.bytecode[0];
                        start.bytecode = new byte[3];
                        start.bytecode[0] = op;
                        start.bytecode[1] = offsetBytes[0];
                        start.bytecode[2] = offsetBytes[1];
                    }
                }
                else if (o is OffsetStart)
                {
                    OffsetStart start = o as OffsetStart;
                    int oldLength = start.bytecode.Length;
                    ArrayList tempList = new ArrayList();
                    MakePushInt(start.target.address, tempList, oldLength);
                    if (tempList.Count == oldLength)
                    {
                        for (int i = 0; i < tempList.Count; ++i)
                            if ((byte)tempList[i] != start.bytecode[i])
                                changed = true;
                    }
                    else
                    {
                        changed = true;
                        start.bytecode = new byte[tempList.Count];
                    }
                    for (int i = 0; i < tempList.Count; ++i)
                        start.bytecode[i] = (byte)tempList[i];
                }
                else if (o is StringOffsetStart)
                {
                    StringOffsetStart start = o as StringOffsetStart;
                    int d = start.target.address;
                    byte b1 = (byte)(0x80 | (d >> 8));
                    byte b2 = (byte)(d & 0xff);
                    if (start.bytecode[1] != b1 || start.bytecode[2] != b2)
                        changed = true;
                    start.bytecode[1] = b1;
                    start.bytecode[2] = b2;
                }
            }
            return changed;
        }
        byte[] MakeAddressOffset(int a)
        {
            return MakeAddressOffset(a, false); ; ; ;
        }
        byte[] MakeAddressOffset(int a, bool twoBytes)
        {
            byte[] bytes;
            if (a >= 0)
            {
                if (a < 63 && !twoBytes) // 63 instead of 64 is so wrong, but proptool generated 80 3f when compiling planetary defense;;;
                {
                    bytes = new byte[1];
                    bytes[0] = (byte)a;
                }
                else
                {
                    bytes = new byte[2];
                    bytes[0] = (byte)((a >> 8) | 0x80);
                    bytes[1] = (byte)a;
                }
            }
            else
            {
                if (a >= -64 && !twoBytes)
                {
                    bytes = new byte[1];
                    bytes[0] = (byte)(a + 128);
                }
                else
                {
                    bytes = new byte[2];
                    bytes[0] = (byte)(a >> 8);
                    bytes[1] = (byte)a;
                }
            }
            return bytes;
        }
        static public void MakePushInt(int intValue, ArrayList bytecodeList, int minLength)
        {
            int b = -1;
            if (intValue == -1 && 1 >= minLength)
            {
                bytecodeList.Add((byte)0x34);	// PUSH#-1
            }
            else if (intValue == 0 && 1 >= minLength)
            {
                bytecodeList.Add((byte)0x35);	// PUSH#0
            }
            else if (intValue == 1 && 1 >= minLength)
            {
                bytecodeList.Add((byte)0x36);	// PUSH#-1
            }
            else if (Expr.Kp(intValue, ref b) && 2 >= minLength)
            {
                bytecodeList.Add((byte)0x37);	// PUSH#kp
                bytecodeList.Add((byte)b);
            }
            else if ((intValue & 0xffffff00) == 0 && 2 >= minLength)
            {
                bytecodeList.Add((byte)0x38);	// PUSH#k1
                bytecodeList.Add((byte)intValue);
            }
            else if ((intValue | 0x000000ff) == -1 && 3 >= minLength)
            {
                bytecodeList.Add((byte)0x38);	// PUSH#k1
                bytecodeList.Add((byte)~intValue);
                bytecodeList.Add((byte)0xe7);	// BIT_NOT
            }
            else if ((intValue & 0xffff0000) == 0 && 3 >= minLength)
            {
                bytecodeList.Add((byte)0x39);	// PUSH#k2
                bytecodeList.Add((byte)(intValue >> 8));
                bytecodeList.Add((byte)intValue);
            }
            else if ((intValue | 0x0000ffff) == -1 && 4 >= minLength)
            {
                bytecodeList.Add((byte)0x39);	// PUSH#k2
                bytecodeList.Add((byte)(~intValue >> 8));
                bytecodeList.Add((byte)~intValue);
                bytecodeList.Add((byte)0xe7);	// BIT_NOT
            }
            else if ((intValue & 0xff000000) == 0 && 4 >= minLength)
            {
                bytecodeList.Add((byte)0x3a);	// PUSH#k3
                bytecodeList.Add((byte)(intValue >> 16));
                bytecodeList.Add((byte)(intValue >> 8));
                bytecodeList.Add((byte)intValue);
            }
            else
            {
                bytecodeList.Add((byte)0x3b);	// PUSH#k4
                bytecodeList.Add((byte)(intValue >> 24));
                bytecodeList.Add((byte)(intValue >> 16));
                bytecodeList.Add((byte)(intValue >> 8));
                bytecodeList.Add((byte)intValue);
            }
        }

        public int ToMemory(byte[] memory, int address)
        {
            foreach (object o in bytecodeList)
            {
                if (o is byte)
                {
                    memory[address++] = (byte)o;
                }
                else if (o is JmpStart)
                {
                    foreach (byte b in (o as JmpStart).bytecode)
                    {
                        memory[address++] = b;
                    }
                }
                else if (o is OffsetStart)
                {
                    foreach (byte b in (o as OffsetStart).bytecode)
                    {
                        memory[address++] = b;
                    }
                }
                else if (o is StringOffsetStart)
                {
                    foreach (byte b in (o as StringOffsetStart).bytecode)
                    {
                        memory[address++] = b;
                    }
                }
                // else it's something that takes up no bytes
            }
            return address;
        }
        public void DumpBytecodeListing(StringWriter sw, int address, int hubAddressOfObject)
        {
            for (int i = 0; i < bytecodeList.Count; ++i)
            {
                if (bytecodeList[i] is byte)
                {
                    int end = address;
                    while (i < bytecodeList.Count && bytecodeList[i] is byte)
                    {
                        ++end;
                        ++i;
                    }
                    ArrayList lines = Dis.Disassemble(ref address, end);
                    foreach (string s in lines)
                        sw.WriteLine(s);
                    --i;
                }
                else if (bytecodeList[i] is JmpStart)
                {
                    JmpStart js = bytecodeList[i] as JmpStart;
                    if (js.address + hubAddressOfObject == address)
                    {
                        int end = address + js.bytecode.Length;
                        sw.WriteLine(Dis.Disassemble(ref address, end)[0]);	// there'll only be one line
                    }
                }
                else if (bytecodeList[i] is OffsetStart)
                {
                    int end = address + (bytecodeList[i] as OffsetStart).bytecode.Length;
                    sw.Write(Dis.Disassemble(ref address, end)[0]);	// there'll only be one line
                    sw.WriteLine(" (Offset to ${0:x4})", (bytecodeList[i] as OffsetStart).target.address + hubAddressOfObject);
                }
                else if (bytecodeList[i] is StringOffsetStart)
                {
                    int end = address + (bytecodeList[i] as StringOffsetStart).bytecode.Length;
                    sw.Write(Dis.Disassemble(ref address, end)[0]);	// there'll only be one line
                    sw.WriteLine(" (${0:x4}, address of string)", (bytecodeList[i] as StringOffsetStart).target.address + hubAddressOfObject);
                }
                else if (bytecodeList[i] is SourceReference)
                {
                    SourceReference sr = bytecodeList[i] as SourceReference;
                    Dis.Heading(sw, "", '-');
                    for (int l = sr.token.LineNumber; l <= sr.endLineNumber; ++l)
                    {
                        sw.WriteLine(sr.token.Tokenizer.Line(l));
                    }
                    Dis.Heading(sw, "", '-');
                }
                else if (bytecodeList[i] is StringStart)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append('"');
                    int n = 0;
                    while (bytecodeList[++i] is byte)
                    {
                        ++n;
                        byte b = (byte)bytecodeList[i];
                        if (bytecodeList[i + 1] is byte)	// append all characters except trailing null
                        {
                            if (0x20 <= b && b < 0x7f)
                                sb.Append((char)b);
                            else
                                sb.Append('.');
                        }
                    }
                    sb.Append('"');
                    ArrayList lines = new ArrayList();
                    lines.Add(sb.ToString());
                    Dis.DumpColumns(sw, address, n, lines, 8, "");
                    address += n;
                }
                ///				else if( bytecodeList[i] is JmpTarget )
                ///				{
                ///					sw.WriteLine( "---- Label${0:x4}:", (bytecodeList[i] as JmpTarget).address + hubAddressOfObject );
                ///				}
            }
        }
    }

    class VarSymbolInfo : ObjectFileSymbolInfo
    {
        Expr[] dimExprs;
        public Expr[] DimExprs { get { return dimExprs; } }

        public int TotalCount()
        {
            int total = 1;
            foreach (Expr e in dimExprs)
                total *= Expr.EvaluateIntConstant(e, false);
            return total;
        }

        int offset = -1;
        public int Offset
        {
            get { return offset; }
            set { offset = value; }
        }

        int size;
        public int Size
        {
            get
            {
                if (size == 0) throw new ParseException("size == 0", IdToken); ; ;
                return size;
            }
        }

        public VarSymbolInfo(IdToken idToken, SizeToken sizeToken, ArrayList dimExprList)
            : base(idToken)
        {
            dimExprs = new Expr[dimExprList.Count];
            for (int i = 0; i < dimExprList.Count; ++i)
                dimExprs[i] = (Expr)dimExprList[i];

            this.size = Token.SizeSpecifier(sizeToken.Text);
        }
    }

    class LocalSymbolInfo : SymbolInfo
    {
        int offset;	// byte offset into local variable space.
        public int Offset { get { return offset; } }

        public LocalSymbolInfo(IdToken idToken, int offset)
            : base(idToken)
        {
            this.offset = offset;
        }
    }

} // namespace Homespun