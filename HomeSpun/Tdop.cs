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

	2008
	7/03	converted to spin precedence convention
	7/04	unary op works; must contemplate how
	7/04	added Visitor pattern
	7/04	parens
	7/04	assignment operator, right-associative
	7/04	NOT operator; low-precedence unary op. So far, behavior matches Spin tool.
	7/05	IsAssignment. Symbol.Expression() => Expr.Parse().
	7/07	Integrating Spin lexer. Move Expr.Parse back to Symbol.Expression
			and use a Tokenizer instance rather than static methods.
	7/07	Add Tokenizer, lineNumber, and column info to Symbols.
	7/07	Renamed Plus PlusSymbol, etc. Added a bunch of Symbol classes for
			indent, eol, eof, rparen, numbers, strings.
	7/08	Starting statements. Toy code to test while and if/else with indented blocks.
			Amazingly appears to work.
	7/09	Realized we don't need an indent symbol, we can use the column info on the
			first symbol of the line.
	7/10	Reworked Tokenizer so it can recognize "+ =" as "+=".
	7/11	MemoryAccessExpr
	7/11	Variable indexes, method calls.
	7/12	BinaryOpSymbol, AssignOpSymbol
	The Great Renaming. What I've been calling Symbols will be renamed Tokens.
	Symbols are things that are in symbol tables. They are named entities (var, dat, con, pub, pri,
	or obj) defined in the input (or perhaps predefined as globals).
	Tokens are the lexical components of the input. Operator tokens have precedence information,
	nuds, leds, maybe stds.
	7/12	Great Renaming II: Symbol => Token
	7/13	Pass SimpleTokens instead of filename/line/col info to maybe simplify things.
	7/13	Much pointless busywork to implement the "simplification".
	7/13	First postfix op (plusplus).
	7/13	Std: PubToken
	7/14	Adding token to Expr for more informative error messages during expression evaluation.
			What a pain. I hope it's worth it.
	7/14	Renamed VariableToken => IdToken.
	7/14	Starting symbols.cs
	7/15	Able to parse PUB params, local vars.
	7/15	Constant expression evaluator.
	7/18	Because a local var declaration can include a count expression that can't be evaluated
			at parse time, I am now saving local variable names and count expressions when parsing
			and not inserting them into a local symbol table. Later, when it's time to compile
			the method, we'll go through the lists and insert local symbols.
	7/18	Constant expression evaluator can now look up existing symbols in current (i.e., only) object.
	7/18	Parses VAR blocks. It puts the vars into symbol tables as it parses, but assigning
			addresses (i.e. offsets in var space) has to wait until the whole program is parsed --
			again because of possible forward references to constants.
	7/18	Split Expr and Visitor stuff off into exprs.cs
	7/18	Start bytecode generation for int literals.
	7/19	Can compile simple literals. Preparing to compile some simple code.
	7/19	Reconsidering the IsAssignment thing. It may be unnecessary. Perhaps a topLevel flag
			that's true only for, well, the top-level of evaluation would work.
			I think making leaveOnStack true at the top level and false everywhere else will
			do the trick.
	7/19	Meanwhile, adding IdExpr for those "could be a variable, could be a call"-type cases.
			And ConstantExpr for the "definitely a constant" cases.
	7/20	Getting ready for recursively including files. Procrastinating on the bytecode generation.
	7/20	Took a while to get ParseException to work with recursive files. Might be working now.
	7/20	Recursive input files working; circular references detected.
	7/23	Grunt work.
	7/24	I knew that the proptool eliminates redundant objects, but I had assumed it just
			went by filenames. Turns out it looks for identical objects at the binary level.
	7/25	I think I have VAR allocation working now. At first I'd forgotten to include
			the VAR space of child objects.
 	7/25	Size of locals apparently does not include params and return value.
	7/25	News flash: PUB methods are laid out in memory before PRI methods.
	7/25	Eliminated redundant reparsing of already-parsed object files.
	7/26	Replaced single method list with pub and pri lists to facilitate laying out
			PUBs followed by PRIs.
	7/27	Now able to write out a .eeprom file. Caveat: incomplete file header.
	7/29	Fixed tokenizer bug when parsing int literals containing "_".
			Adding more IntExpr cases.
	7/30	Can push local variables.
	7/30	Wrote Expr.MakeStackOp.
	7/31	Can push VARs. Can push variables with (simple) indexes. First binary ops. And unary op (but
			only for leaveOnStack true; still have to do the unary assignment versions).
	8/01	First USING op: for unary assignment - operator.
			Binary assignment operators working. true/false wasn't enough for MakeBytes; had to make
			a MakeBytes that takes a StackOp in order to generate POP sequences. Should probably rethink 
			the MakeBytes thing.
	8/01	Remember: the size field on a symbol is the declared size; the specifiedSize field (if nonzero) on the
			expression overrides the declared size.
			MemoryAccessExpr now working. Had to add a Nud to SizeToken.
	8/02	Starting DAT processing.
			Looks like I have to rethink [. Right now it has a Led, but it breaks in a dat: long 1[100].
	8/03	Adding ORG token.
			Removed LBracketToken and DotToken; moved [ and . processing inline. Now DATs with counts in [] work.
			Now can actually write DAT areas.
			Also modified tokenizer to auto-concat strings so that obj o1 : "ob", "ject", "1" works. Craziness.
	8/04	A few bug fixes.
	8/05	Got redundant object elimination working, at least to start. 
	8/06	More work on redundant object elimination: looking at OBJ-related section of object headers.
	8/07	Posted version of test05 to forum. Looks like a real compiler bug.
	8/08	obj.method calls working.
	8/09	Now with argument lists.
	8/10	Starts in CON mode, just like proptool.
	8/10	Enums. And you can use [n] "indexes" to space out enums. PhiPi's Fibonacci trick works.
			In method calls, made sure #args is correct.
	8/11	RESULT keyword
	8/11	The rest of the operators, almost.
	8/12	Refactored PrePlusTokens (++, --, ~, etc).
	8/12	Got @ working, at least on the compile side. Still have to implement it in the constant evaluator.
	8/13	@ now working in constant contexts. (Added CONSTANT keyword.) Implemented @@.
			IF/IFNOT/ELSE/ELSEIF/ELSEIFNOT working.
			Disaster: Just realized that my byte list approach won't work with NEXT and QUIT. Or will it, somehow?
	8/15	Moving project to Visual Studio. Notepad just not cutting it anymore.
			OK, time for major surgery. Taking a snapshot
	8/15	MakeBytes is now MakeBytecode, doesn't make a byte[], instead it adds bytes to an ArrayList.
			The plan, such as it is: most things in the ArrayList will be bytes, but there will also be
			objects for jumps and jump targets. A separate pass will resolve those objects to real bytes.
	8/16	JmpStart and JmpTarget classes. Fixup pass repeated until nothing changes.
			Simple REPEAT forms work. NEXT and QUIT too.
			Made separate Stmt classes for statements.
	8/17	WHILE/UNTIL loops.
	8/18	Constants: true, false, posx, etc.
	8/19	_CLKFREQ, _CLKMODE, _XINFREQ
			REPEAT FROM TO STEP
			Also: discovered and fixed bug in PUSH#kp (was pushing 7 then bitnot instead of just -8).
	8/20	Looking into the rest of the keywords.
	8/21	More. Learned that CASE/LOOKDOWN/LOOKUP require something like jump resolution, but using
			absolute address info (well, offsets within the object).
	8/22	So now I'm going to rework things so that the Fixup pass knows where in the object things go.
			First, take a snapshot: old05
			OK, now Fixup takes an address.
			Now to parse LOOKUP. CASE looks way harder than LOOK*.
			Created OffsetStart class: similar to JmpStart, but for the PUSH# offset for LOOK*, CASE.
			LOOK* mostly working, but discovered that my "clever" auto-concatenation of comma-separated strings
			(see 8/03 above) messes things up in lookup( x : "a".."c", "def".."g" )	==> "a".."cdef".."g"
			Must regroup.
	8/23	Snapshot: old06. Going to tear out StringToken and StringExpr; instead, the tokenizer will
			split strings into ints with commas between. This should work great for LOOK*. It will
			mess up things that expect actual strings, like OBJ declarations and the inside of STRING();
			those will just have to string the numbers back together.
			And now LOOK* works.
	8/23	Now tackling CASE... and it's done.
	8/24	New statements: ABORT, REBOOT -- working.
			New statement: FillMoveStmt for {BYTE|WORD|LONG}{FILL|MOVE}.
			New statement: WaitStmt for WAITCNT, WAITPEQ, WAITPNE, WAITVID.	
			MiscStmt for CLKSET, COGSTOP, LOCKRET
			RegisterExpr for CNT, CTRA, CTRB, etc.
			Snapshot: old07.
	8/25	Finally figured out how to compute initial stack pointer.
			Added SPR.
	8/26	Read-only variables: CHIPVER, CLKFREQ, CLKMODE, COGID.
			Starting to think about floating-point.
	8/27	Now parses floating-point literals.
			Discovered bug: parser accepted 1.23 and if statement in a DAT block.
			Introduced ParseBlock. Spent too much time tracking down stupid bug in floating-point lexer mishandling "_".
			Added EvaluateFloatConstant, but discovered problem: can't always tell in advance if it should be
			an int or a float constant. Gotta rethink this.
	8/28	Broken, and may get worse.
			Now have FloInt type and EvaluateConstant.
			Dang. CONs need to know if they're int or fp.
			OK, fp seems to be working now. Snapshot: old08.
			Implemented STRING().
	8/29	_FREE, _STACK.
			COGNEW -- working.
			COGINIT -- working.
	8/30	STRCOMP, STRSIZE -- working.
			LOCK* -- working. Took LOCKRET out of MiscStmt. Snapshot: old09.
	8/30	Starting assembler.
			CondToken class.
			Eliminated MiscToken/Stmt; split it into ClkSet & CogstopToken/Stmt.
	8/31	Adding assembly language instructions. Skipping CALL for now.
	9/1		Local labels. Added a Tokenizer mode for parsing ":" + <id> together (kinda klugey)
			Started effects (WC, WR, WZ, NR).
	9/2		All instructions except CALL.
			Snapshot: old10.
	9/3		CALL.
			Detour fixing bug: added check for Size override must be smaller
	9/4		ORGX, RES, FIT	
			Trying to compile Planetary Defense. Discovered bug in binary number parser.
			Discovered that proptool generates suboptimal jump offset 80 3f instead of just 3f;
			made equivalent pessimization in homespun.
			Had to squash a bunch of little bugs, but PD now compiles.
			Battlez0wned revealed more bugs(!)
	9/5		Added code to UnaryExpr to handle negating CON symbols.
			Battlez0wned now compiles cleanly.
	9/5		Abort trap (\) working.
			FILE directive working.
	9/6		$ (here) works.
			Snapshot: old11.
	9/7		Added warnings: JMP w/o #, RES without count.
	9/8		Added ORG without address. Added command-line options.
			Snapshot: old11.
	9/8		Released version 0.10 to prop forum.
			Added column 1 check to DAT, CON, OBJ, VAR.
			hippy's x.spin: ORG was not evaluating constant in DAT context (found other instances too).
			Also in hippy's x: @datsymbol was compiling PUSH#.B instead of PUSH#.L	-- label on ORG has long alignment,
			even though it is otherwise unaffected by the ORG.
			Added warnings for DJNZ, TJNZ, TJZ w/ non-immediate operand.
			Added exit codes.
			Added ^Z handling: changes ^Z to space.
			Made built-in constant PI a float (was int, duh).
			Added _SPACE per hippy (experimental)
			Fixed "other" bug (forgot to ToUpper()).
	9/9		Version 0.12 => old12
			new bugs from heater: proptol can parse 32-bit unsigned int; bug in computing clock frequency from _CLKFREQ.
			(both fixed)
			Praxis found a situation where the compiler got stuck in the fixup loop (clusodebug_215.spin).
			Now variable-sized sequences can only grow, not shrink. Seems to work.
			hippy discovered that "CON _space" causes unhandled exception. Plus I did the whole _SPACE thing wrong
			anyway.
			CON resets enum counter (#0). I did not know that. Fixed.
	9/10	Removed _SPACE stuff. Will have to think about it.
			Version 0.13 => old13
	9/11	Added warning for cognew/init with call to method in another object.
			Added Options class.
			Simple symbol dump.
			Version 0.14 => old14
	9/13	symbol table should also list VAR offsets for sub-objects, maybe? And maybe absolute
			VAR addresses for top object.
			Trying _SPACE again: fake top object.
	9/15	Fixed duplicate object elimination (forgot to check DAT -- oops!)
			_SPACE fake top object seems to work now.
			Added warning for stuff after RES.			
			Version 0.15 => old15
	9/17	Attempt compiling in order from low memory to high, so each object knows its absolute
			hub address (for @@@).
	9/18	0.15x released with @@@. Two bugs reported, both unrelated to @@@!
			First, from Brad: DAT x long 0[@@@x] broke because x changed between pass 1 and pass 2
			(phase error) to account for the header. Realized I could account for header before pass 1.
			Second, from Ale: \fn generated a bad CallExpr (object and method reversed, null for args list).
			But there's still a bug in the sd demo.
	9/19	SD demo bug fixed. If you skip a file because it's already been read, you still have to
			reorder the global object list for any objects the file contains.
			Version 0.16 => old16
	9/19	Adding code to disable duplicate object elimination if @@@ is used.
			Version 0.17 => old17
	9/20	Dump now writes to StringWriter. Eventually: dump to a file.
			Make Tokenizer's internal line number be zero-based.
	9/21	Added end-of-line info to statements for eventual synchronization with bytecode listing.
	9/21	BradC reported bug: dat byte "abc:def" ==> Expected local label
			Fixed (in tokenizer, when parseLocalLabels is enabled, check for ":" and TokenType.Op).
			Tokenizer now remembers lines, so it can print offending lines in error messages
			(this is actually for matching up bytecode with source, but better error messages are
			a first easy consequence).
			Snapshot => old\temp	
	9/23	Added SPINLIB environment variable and /Lpath or -Lpath option.
	9/24	0.17x released.
			Snapshot => old\temp.
	9/25	Added new warning for data truncation.
			todo: Add end-of-line info to DAT lines
	9/27	Simple #define capability.
			Simple #ifdef capability.
			Snapshot => old\temp.
	9/28	Starting bytecode dump.
	9/29	Brad releases his compiler (SpinTool)
	9/29	Adding dis.cs to project.
	9/30	Disassembly progressing.
	10/1	added <32k check (untested)
			Spin disassembly basically done.
			added check for already #defined symbols
			snapshot => old\temp
	10/2	sync DAT and source
			dump 16-byte file header
			dump to filename.lst
			#end
			#region
			snapshot => old\old18
	10/5	Trying to add array support (multiple subscripts) -- done.
			snapsphot => old\old19
	10/6	hippy points out that [i][j] syntax could be confusing given byte[i][j].
			Trying [i,j,k] syntax -- done.
			snapshot => old\old20
	10/8	This morning I had an idea for the perpetual "How do I share objects?" problem.
	10/9	Today I started implementing my idea (involved new OBJPTR type) and then realized
			regular old OBJ should do.
			Basically you can apply @ to any OBJ and get a long that contains the absolute
			hub address of the object and the VAR offset. You can assign that long to an
			OBJ and it gets converted to an object-local offset (and VAR offset) and gets
			put into the object's OBJ table. The overwritten OBJ should have the same
			"type" as the OBJ that @ is applied to.
			Disadvantage: The VAR space allocated for the overwritten OBJ is wasted.
			Also, it kinda changes the meaning of @ (not a pure address). Should use a
			different operator (&) but too lazy.
			First see if my idea works, then make it work well.

			Also discovered that my last couple of backups didn't happen. I'm saving this
			as old\old20, even though it's not what was released as 0.20.

			Turns out my idea did not work. I forgot that the VAR offset is relative
			to the object. I'll try adjusting the VAR offset in addition to the
			object pointer offset, but this means it will only work for one instance
			(in the whole program) of the (modified) object.									

	10/10	Use - for command-line options, not / (conflicts with non-WIndows filesystems)
			Change -L option to no longer be conjoined with directory.
			-i option to control "parsing" and "compiling" messages;
			for Praxis: single quotes on comment lines in list file
			Add "Error: " to error messages;
			Drop ".out" from output filename.
			
			2nd try at object sharing: success! Has the limitation mentioned above.
			Also wastes memory because it allocates VAR space for objects whether
			or not the object is used.
			snapshot => old\temp
			Let's see if we can eliminate the unnecessary VAR allocation. Done (needs testing)
			Generate .binary file
			version 0.21 => old\old21
	10/11	Mike Green reported bug in command line processing: sent debug version 0.21x, no repro.
			Mike also reported bug: if x <> "="
			This was related to Brad's "abc:def" bug. Modified the code that breaks a string into
			chars to return "'='" instead of "=" as the token string.
	10/17	Hmmm. -L "program files\etc" doesn't work.
			Now adds '\' to end of -L arg if necessary.
			0.23 => old\old23
			
	10/19	Been thinking about compiling .spin files to individual .sob files (provisional name).
			This would be the bytecode for an object, plus constants and pub information.
			When compiling an object, read the sub-objects' .sob files to get pubs and constants.
			There'll be a separate link step to combine the .sobs.
			No particular advantage for PC-side development, but if it works, it may be
			the way to go for Prop-based dev: smaller-sized chunks for compilation, since memory
			is so tight.
	10/21	Also, .sobs could be produced by other language compilers, like C. Or on the Prop,
			perhaps a custom language like Action! that's easy to compile.
			Anyway, today I'm taking a first step: pretend to dump .sob.
	10/22	Errm, actually, *today* I'll take that first step.
	10/25	(Wenatchee) agodwin reported bug: Proptool parses "NOT x := 3" as "NOT (x := 3)",
			Homespun doesn't.
	10/26	"Breaking" Homespun's precedence to match Proptool.
			Since I'm doing distasteful things anyway, I added "0x" hex number syntax too.
			0.24 => old\old24
	2009
	01/02	Should get back to it.
			.sob files also need import information (sub-objects)
	04/03	Changing .sob file format to binary rather than human-readable text.
	04/xx	Managed to link .sobs into .eeprom.
	04/09	Converting linker to spin. .sob format is "SOB0".
	04/21	Favor for RossH: added Options.memorySize, option -M
	05/07	Added Options.outputFilename, option -o for RossH.
	05/08	Fixed(?) long alignment bug, missing filler bytes in listing.
	05/10	"dat long 0[33333]" causes crash. "0xffff - alignment + 1" => "-alignment"; fixed(?)
			Also removed outputFilename extension check; rely on -b and new -e option to force
			.binary or .eeprom output.
		    0.24zo
	05/11	Found related alignment bug (& 0xfffc => & -4).
			0.25x
	05/29	.binary files truncated when > 64k; fixed.
			Listing file now takes name of output file (-o option) if specified.
2010
	11/28	0.27x: exploring possibility of #INCLUDE...
	11/29	0.27y: minor fix to #PRINT
	11/30	0.27z: was appending \ to library paths if necessary; now appends / for unix-style paths
			(makes best guess; better to specify final / or \; e.g. -L /foo/bar/).
			Added TESTN instruction per Hanno.
			FILE and #INCLUDE now search libraries.
	11/30	Added for jazzed: prints size of output file.
			=> 0.28
	12/16	Adding .dat output (-c option).
			=> 0.29				
	12/17	Added #ifndef for jazzed
			=> 0.30
2011
	01/29	RossH reported elseifdef bug. Elseifdef code was erroneously uppercasing the symbol. Fixed.
		Realized that elseifndef hadn't been implemented so added that.
		Did a little refactoring.
	02/24	=> 0.31
		
TODO:
			.sob support; separate linker
			Support Spin extensions only in .spasm files
			Specify output filename
BUGS:
	result.byte, result[3] don't parse.

Precedence is based on Spin and is the opposite of Crockford's convention;
that is, a low numerical value indicates a high precedence.

Precedence	Example Operator
	0	unary --
	1	unary -
	2	->
	3	&
	4	|
	5	/
	6	-
	7	<#
	8	<
	9	not
	10	and
	11	or
	12	:=

*/

using System;
using System.IO;
using System.Collections;

namespace Homespun
{
    enum InstructionTypeEnum { None, D, S, DS }
    interface IPropInstruction
    {
        uint Propcode { get; }
        InstructionTypeEnum InstructionType { get; }
        string Mnemonic { get; }
    }

    class Token : SimpleToken
    {
        public Token(SimpleToken st)
            : base(st.Tokenizer, st.Type, st.Text, st.LineNumber, st.Column, st.IntValue, st.FloatValue)
        {
        }

        public Token(SimpleToken st, int lbp)
            : base(st.Tokenizer, st.Type, st.Text, st.LineNumber, st.Column, st.IntValue)
        {
            this.lbp = lbp;
        }

        int lbp = 666;
        public int Lbp { get { return lbp; } }

        public virtual Expr Nud()
        {
            throw new ParseException("no Nud", this);
        }
        public virtual Expr Led(Expr left)
        {
            throw new ParseException("no Led", this);
        }
        public virtual bool Std(out Stmt s)
        {
            s = null;
            return false;
        }
        public static Expr ParseExpression(Tokenizer Tokenizer, int rbp)
        {
            Token t = Tokenizer.Current;
            Tokenizer.Advance();
            Expr left = t.Nud();
            while (rbp > Tokenizer.Current.Lbp)
            {
                t = Tokenizer.Current;
                Tokenizer.Advance();
                left = t.Led(left);
            }
            return left;
        }
        static public Stmt ParseBlock(Tokenizer Tokenizer)
        {
            Stmt s;
            if (!(Tokenizer.Current is BlockDesignatorToken))
                throw new ParseException("Unexpected " + Tokenizer.Current.Text, Tokenizer.Current);
            Tokenizer.Current.Std(out s);
            return s;
        }
        static public Stmt ParseStatement(Tokenizer Tokenizer)
        {
            Stmt s;
            if (Tokenizer.Current.Std(out s))
            {
                return s;
            }
            else
            {
                SimpleToken token = Tokenizer.Current;
                Expr e = ParseExpression(Tokenizer, 13);
                s = new ExprStmt(token, Tokenizer.Current.LineNumber, e);
                Tokenizer.Advance("(eol)");
                return s;
            }
        }
        static public ArrayList ParseStatements(Tokenizer Tokenizer, int indent)
        {
            ArrayList statementList = new ArrayList();
            while (true)
            {
                if (Tokenizer.Current.Text == "(eof)")
                    break;
                if (Tokenizer.Current is BlockDesignatorToken)
                    break;
                if (Tokenizer.Current.Column <= indent)
                    break;
                statementList.Add(ParseStatement(Tokenizer));
            }
            return statementList;
        }

        public static ArrayList ParseArgumentList(Tokenizer Tokenizer)
        {
            ArrayList argList = new ArrayList();
            if (Tokenizer.Current.Text != "(")
                return argList;
            Tokenizer.Advance("(");
            while (true)
            {
                argList.Add(ParseExpression(Tokenizer, 13));
                if (Tokenizer.Current.Text != ",")
                    break;
                Tokenizer.Advance(",");
            }
            Tokenizer.Advance(")");
            return argList;
        }

        static public int SizeSpecifier(string s)
        {
            s = s.ToUpper();
            if (s == "BYTE") return 1;
            if (s == "WORD") return 2;
            if (s == "LONG") return 4;
            return 0;
        }
        static public bool CouldBeLvalue(Expr e)
        {
            return e is IdExpr || e is VariableExpr || e is MemoryAccessExpr || e is RegisterExpr || e is SprExpr;
        }
    }

    class IdToken : Token
    {
        public string Id { get { return Text; } }

        public IdToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            if (Tokenizer.Current.Text == "#")	// <id> # <id>
            {
                Tokenizer.Advance("#");
                IdToken id2 = Tokenizer.GetToken() as IdToken;
                if (id2 == null)
                    throw new ParseException("Expected constant name", id2);
                return new ConExpr(this, id2);
            }
            if (Tokenizer.Current.Text == "(")	// <id> ( <arglist> )   (method call)
            {
                ArrayList argList = ParseArgumentList(Tokenizer);
                return new CallExpr(null, this, null, argList);
            }
            if (Tokenizer.Current.Text == ".")	// <id> . <something>
            {
                Tokenizer.Advance(".");
                if (Tokenizer.Current is IdToken)	// <id> . <id>  (method call)
                {
                    IdToken method = Tokenizer.GetToken() as IdToken;
                    ArrayList argList = ParseArgumentList(Tokenizer);
                    return new CallExpr(this, method, null, argList);
                }
                else if (Tokenizer.Current is SizeToken)	// <id> . BYTE|WORD|LONG
                {
                    int size = SizeSpecifier(Tokenizer.GetToken().Text);
                    VariableExpr v = new VariableExpr(this, size);
                    if (Tokenizer.Current.Text == "[")	// <id> . BYTE|WORD|LONG [ <expr> ]
                    {
                        Tokenizer.Advance("[");
                        ArrayList indexExprList = new ArrayList();
                        indexExprList.Add(ParseExpression(Tokenizer, 13));
                        v.IndexExprList(indexExprList);
                        Tokenizer.Advance("]");
                    }
                    return v;
                }
            }

            if (Tokenizer.Current.Text == "[")	// <id> [ <expr> ( , <expr> )* ]
            {
                Tokenizer.Advance("[");

                ArrayList indexExprList = new ArrayList();
                VariableExpr v = new VariableExpr(this, 0);
                while (true)
                {
                    Expr e = ParseExpression(Tokenizer, 13);
                    indexExprList.Add(e);
                    if (Tokenizer.Current.Text != ",")
                        break;
                    Tokenizer.Advance(",");
                }
                Tokenizer.Advance("]");
                if (Tokenizer.Current.Text == ".")	// <id> [ <expr> ( , <expr> )* ] . <id> (method call)
                {
                    Tokenizer.Advance(".");

                    if (indexExprList.Count > 1)
                    {
                        throw new ParseException("multiple dimensions not allowed", ((Expr)indexExprList[1]).Token);
                    }
                    if (Tokenizer.Current is IdToken)
                    {
                        IdToken method = Tokenizer.GetToken() as IdToken;
                        ArrayList argList = ParseArgumentList(Tokenizer);
                        return new CallExpr(this, method, (Expr)indexExprList[0], argList);
                    }
                    else
                        throw new ParseException("Expected method name", Tokenizer.Current);
                }
                else	// <id> [ <expr> ( , <expr> )* ] -- i.e., variable with at least one subscript
                {
                    v.IndexExprList(indexExprList);
                }
                return v;
            }

            return new IdExpr(this);
        }
    }

    class SizeToken : Token
    {
        public SizeToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            int size = SizeSpecifier(Text);
            Tokenizer.Advance("[");
            Expr indexExpr = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance("]");
            MemoryAccessExpr mem = new MemoryAccessExpr(this, size, indexExpr);
            if (Tokenizer.Current.Text == "[")
            {
                Tokenizer.Advance("[");
                mem.IndexExpr2 = ParseExpression(Tokenizer, 13);
                Tokenizer.Advance("]");
            }
            return mem;
        }
    }

    class ResultToken : Token
    {
        public ResultToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            return new VariableExpr(this, 4);
        }
    }

    class ConstantToken : Token
    {
        public ConstantToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Tokenizer.Advance("(");
            Expr e = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(")");
            return new ConstantExpr(this, e);
        }
    }

    class IntToken : Token
    {
        public IntToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            return new IntExpr(this, this.IntValue);
        }
    }

    class FloatToken : Token
    {
        public FloatToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            return new FloatExpr(this, this.FloatValue);
        }
    }

    class LParenToken : Token
    {
        public LParenToken(SimpleToken tokenInfo)
            : base(tokenInfo, -1)
        {
        }
        public override Expr Nud()
        {
            Expr e = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(")");
            return e;
        }
    }

    class OpToken : Token
    {
        int opcode;
        public int Opcode { get { return opcode; } }
        public OpToken(SimpleToken tokenInfo, int lbp, int opcode)
            : base(tokenInfo, lbp)
        {
            this.opcode = opcode;
        }
    }

    class BinaryOpToken : OpToken
    {
        public BinaryOpToken(SimpleToken tokenInfo, int lbp, int opcode)
            : base(tokenInfo, lbp, opcode)
        {
        }
        public override Expr Led(Expr left)
        {
            return new BinaryExpr(this, Opcode, left, ParseExpression(Tokenizer, Lbp));
        }
    }

    class AndToken : OpToken, IPropInstruction
    {
        public AndToken(SimpleToken tokenInfo)
            : base(tokenInfo, 10, 0xf0)
        {
        }
        public override Expr Led(Expr left)
        {
            return new BinaryExpr(this, 0xf0, left, ParseExpression(Tokenizer, 10));
        }
        public uint Propcode { get { return 0x60bc0000; } }
        public InstructionTypeEnum InstructionType { get { return InstructionTypeEnum.DS; } }
        public string Mnemonic { get { return Text; } }
    }

    class OrToken : OpToken, IPropInstruction
    {
        public OrToken(SimpleToken tokenInfo)
            : base(tokenInfo, 11, 0xf2)
        {
        }
        public override Expr Led(Expr left)
        {
            return new BinaryExpr(this, 0xf2, left, ParseExpression(Tokenizer, 11));
        }
        public uint Propcode { get { return 0x68bc0000; } }
        public InstructionTypeEnum InstructionType { get { return InstructionTypeEnum.DS; } }
        public string Mnemonic { get { return Text; } }
    }

    class AssignOpToken : OpToken
    {
        public AssignOpToken(SimpleToken tokenInfo, int opcode)
            : base(tokenInfo, 0, opcode)
        {	//																		 ^ should be 12, but
            //												in order to match Proptool's buggy (imho)
            //												behavior, I am forced to make it 0.
        }
        public override Expr Led(Expr left)
        {
            if (CouldBeLvalue(left))
            {
                return new BinaryAssignExpr(this, Opcode, left, ParseExpression(Tokenizer, 13));
            }
            else
            {
                throw new ParseException("bad lvalue", this);
            }
        }
    }

    class PlusToken : OpToken
    {
        public PlusToken(SimpleToken tokenInfo)
            : base(tokenInfo, 6, 0xec)	// ADD
        {
        }
        public override Expr Nud()
        {
            return ParseExpression(Tokenizer, 1);
        }
        public override Expr Led(Expr left)
        {
            return new BinaryExpr(this, Opcode, left, ParseExpression(Tokenizer, 6));
        }
    }

    class MinusToken : OpToken
    {
        public MinusToken(SimpleToken tokenInfo)
            : base(tokenInfo, 6, 0xed)	// SUB
        {
        }
        public override Expr Nud()
        {
            bool negativeIntLiteral = Tokenizer.Current.Type == SimpleTokenType.IntLiteral;
            bool negativeFloatLiteral = Tokenizer.Current.Type == SimpleTokenType.FloatLiteral;
            Expr right = ParseExpression(Tokenizer, 1);
            Expr u;
            if (negativeIntLiteral)
            {
                u = new IntExpr(right.Token, -(right as IntExpr).IntValue);
            }
            else if (negativeFloatLiteral)
            {
                u = new FloatExpr(right.Token, -(right as FloatExpr).FloatValue);
            }
            else
            {
                u = new UnaryExpr(this, 0xe6, right);	// NEG
            }
            if (right is VariableExpr || right is MemoryAccessExpr)
            {
                ///			u.IsAssignment = true;
            }
            return u;
        }
        public override Expr Led(Expr left)
        {
            return new BinaryExpr(this, Opcode, left, ParseExpression(Tokenizer, 6));
        }
    }

    class PrePostToken : Token
    {
        int preOpcode;
        int postOpcode;
        public PrePostToken(SimpleToken tokenInfo, int preOpcode, int postOpcode)
            : base(tokenInfo, 0)
        {
            this.preOpcode = preOpcode;
            this.postOpcode = postOpcode;
        }
        public override Expr Nud()
        {
            Expr right = ParseExpression(Tokenizer, 0);
            if (CouldBeLvalue(right))
            {
                UnaryExpr u = new UnaryExpr(this, preOpcode, right);
                return u;
            }
            ///		Console.WriteLine("-------------");
            ///		right.Accept( new RPNPrintVisitor() );
            ///		Console.WriteLine();
            ///		Console.WriteLine("-------------");
            throw new ParseException("bad lvalue", this);
        }
        public override Expr Led(Expr left)
        {
            if (CouldBeLvalue(left))
            {
                UnaryExpr u = new UnaryExpr(this, postOpcode, left);
                return u;
            }
            ///		Console.WriteLine("-------------");
            ///		left.Accept( new RPNPrintVisitor() );
            ///		Console.WriteLine();
            ///		Console.WriteLine("-------------");
            throw new ParseException("bad lvalue", this);
        }
    }

    class UnaryOpToken : OpToken
    {
        public UnaryOpToken(SimpleToken tokenInfo, int lbp, int opcode)
            : base(tokenInfo, lbp, opcode)
        {
        }
        public override Expr Nud()
        {
            return new UnaryExpr(this, Opcode, ParseExpression(Tokenizer, Lbp));
        }
    }

    abstract class BlockDesignatorToken : Token
    {
        public BlockDesignatorToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
    }

    class ConToken : BlockDesignatorToken
    {
        public ConToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }

        public override bool Std(out Stmt s)
        {
            s = null;
            if (this.Column != 0)
                throw new ParseException("CON must be in 1st column", this);
            Tokenizer.Advance();	// past "CON"
            if (Tokenizer.Current.Text == "(eol)")
                Tokenizer.Advance("(eol)");

            Expr conOrgExpr = new IntExpr(new SimpleToken(Tokenizer, SimpleTokenType.IntLiteral, "0", 0, 0, 0), 0);

            while (true)
            {
                if (Tokenizer.Current.Text == "#")
                {
                    Tokenizer.Advance("#");
                    conOrgExpr = ParseExpression(Tokenizer, 13);
                }
                else if (Tokenizer.Current is IdToken)
                {
                    IdToken constantName = Tokenizer.GetToken() as IdToken;
                    if (Tokenizer.Current.Text == "=")
                    {
                        Tokenizer.Advance("=");
                        SymbolTable.AddConSymbol(constantName, ParseExpression(Tokenizer, 13));
                    }
                    else
                    {
                        SymbolTable.AddConSymbol(constantName, conOrgExpr);
                        if (Tokenizer.Current.Text == "[")
                        {
                            Tokenizer.Advance("[");
                            Expr incrExpr = ParseExpression(Tokenizer, 13);
                            Tokenizer.Advance("]");

                            conOrgExpr = new BinaryExpr(
                                new SimpleToken(Tokenizer, SimpleTokenType.Op, "+", 0, 0),
                                666, // fake opcode
                                conOrgExpr, incrExpr);
                        }
                        else
                        {
                            Expr incrExpr = new IntExpr(new SimpleToken(Tokenizer, SimpleTokenType.IntLiteral, "(1)", 0, 0, 1), 1);

                            conOrgExpr = new BinaryExpr(
                                new SimpleToken(Tokenizer, SimpleTokenType.Op, "+", 0, 0),
                                666, // fake opcode
                                conOrgExpr, incrExpr);
                        }
                    }
                }
                else
                    break;
                if (Tokenizer.Current.Text == ",")
                    Tokenizer.Advance(",");
                else
                    Tokenizer.Advance("(eol)");
            }
            return true;
        }
    }

    class DatToken : BlockDesignatorToken
    {
        public DatToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            s = null;
            bool afterRes = false;

            Tokenizer.SetLocalLabelParsingEnable(true);

            if (this.Column != 0)
                throw new ParseException("DAT must be in 1st column", this);
            Tokenizer.Advance();	// past "DAT"
            if (Tokenizer.Current.Text == "(eol)")
                Tokenizer.Advance("(eol)");

            while (!(Tokenizer.Current is BlockDesignatorToken) && Tokenizer.Current.Text != "(eof)")
            {
                IdToken labelToken = null;
                if (Tokenizer.Current is IdToken)
                {
                    labelToken = Tokenizer.GetToken() as IdToken;
                }
                if (Tokenizer.Current.Text.ToUpper() == "ORG")
                {
                    SimpleToken orgToken = Tokenizer.GetToken();
                    if (labelToken != null)
                        SymbolTable.AddDatLabelEntry(4, labelToken, false);
                    Expr orgExpr = null;
                    if (Tokenizer.Current.Text != "(eol)")
                        orgExpr = ParseExpression(Tokenizer, 13);
                    else
                        Tokenizer.PrintWarning("ORG without address (defaults to 0)", Tokenizer.Current);
                    SymbolTable.AddDatOrgEntry(orgToken, orgExpr, Tokenizer.Current.LineNumber);
                    Tokenizer.Advance("(eol)");
                    afterRes = false;
                    continue;
                }
                else if (Tokenizer.Current.Text.ToUpper() == "ORGX")
                {
                    SimpleToken orgxToken = Tokenizer.GetToken();
                    if (labelToken != null)
                        SymbolTable.AddDatLabelEntry(0, labelToken, false);
                    SymbolTable.AddDatOrgxEntry(orgxToken);
                    Tokenizer.Advance("(eol)");
                    afterRes = false;
                    continue;
                }
                else if (Tokenizer.Current.Text.ToUpper() == "RES")
                {
                    SimpleToken resToken = Tokenizer.GetToken();
                    if (labelToken != null)
                        SymbolTable.AddDatLabelEntry(4, labelToken, false);
                    Expr e = null;
                    if (Tokenizer.Current.Text != "(eol)")
                        e = ParseExpression(Tokenizer, 13);
                    else
                        Tokenizer.PrintWarning("RES without count (defaults to 1)", Tokenizer.Current);
                    SymbolTable.AddDatResEntry(resToken, e, Tokenizer.Current.LineNumber);

                    Tokenizer.Advance("(eol)");
                    afterRes = true;
                    continue;
                }
                else if (Tokenizer.Current.Text.ToUpper() == "FIT")
                {
                    SimpleToken fitToken = Tokenizer.GetToken();
                    if (labelToken != null)
                        SymbolTable.AddDatLabelEntry(0, labelToken, false);
                    Expr e = null;
                    if (Tokenizer.Current.Text != "(eol)")
                        e = ParseExpression(Tokenizer, 13);
                    SymbolTable.AddDatFitEntry(fitToken, e);
                    Tokenizer.Advance("(eol)");
                    continue;
                }
                else if (Tokenizer.Current.Text.ToUpper() == "FILE")
                {
                    if (afterRes)
                        Tokenizer.PrintWarning("FILE after RES", Tokenizer.Current);
                    SimpleToken fileToken = Tokenizer.GetToken();
                    if (labelToken != null)
                        SymbolTable.AddDatLabelEntry(0, labelToken, false);
                    SimpleToken filenameToken = Tokenizer.Current;
                    string filename = "";
                    while (true)
                    {
                        Expr e = ParseExpression(Tokenizer, 13);
                        filename += (char)(Expr.EvaluateIntConstant(e));
                        if (Tokenizer.Current.Text != ",")
                            break;
                        Tokenizer.Advance(",");
                    }
                    try
                    {
                        filenameToken.Text = filename;
                        string path = Options.TryPaths(filenameToken);
                        FileStream fs = new FileStream(path + filename, FileMode.Open);
                        BinaryReader br = new BinaryReader(fs);
                        byte[] bytes = br.ReadBytes((int)fs.Length);
                        SymbolTable.AddDatFileEntry(fileToken, filenameToken, bytes, Tokenizer.Current.LineNumber);
                    }
                    catch (Exception e)
                    {
                        throw new ParseException(e.Message, filenameToken);
                    }
                    Tokenizer.Advance("(eol)");
                    continue;
                }
                if (Tokenizer.Current.Text == "(eol)")
                {
                    if (labelToken != null)
                        SymbolTable.AddDatLabelEntry(0, labelToken, true);
                }
                else if (Tokenizer.Current is SizeToken)
                {
                    if (afterRes)
                        Tokenizer.PrintWarning("Data after RES", Tokenizer.Current);
                    SizeToken alignmentToken = Tokenizer.GetToken() as SizeToken;
                    int alignment = SizeSpecifier(alignmentToken.Text);

                    SimpleToken firstToken = alignmentToken;

                    if (labelToken != null)
                    {
                        SymbolTable.AddDatLabelEntry(alignment, labelToken, false);
                        firstToken = labelToken;
                    }

                    if (Tokenizer.Current.Text != "(eol)")
                    {
                        while (true)
                        {
                            int size = alignment;
                            if (Tokenizer.Current is SizeToken)
                            {
                                SizeToken sizeToken = Tokenizer.GetToken() as SizeToken;
                                size = SizeSpecifier(sizeToken.Text);
                                if (size < alignment)
                                    throw new ParseException("Size override must be larger", sizeToken);
                            }
                            Expr dataExpr = ParseExpression(Tokenizer, 13);
                            Expr countExpr = null;
                            if (Tokenizer.Current.Text == "[")
                            {
                                Tokenizer.Advance("[");
                                countExpr = ParseExpression(Tokenizer, 13);
                                Tokenizer.Advance("]");
                            }
                            SymbolTable.AddDatDataEntry(alignment, size, dataExpr, countExpr, firstToken);
                            if (Tokenizer.Current.Text != ",")
                                break;
                            Tokenizer.Advance(",");
                        }
                    }
                    else
                    {
                        SymbolTable.AddDatDataEntry(alignment, alignment, new IntExpr(null, 0), new IntExpr(null, 0), firstToken);
                    }
                    SymbolTable.AddDatSourceReference(Tokenizer.Current);
                }
                else	// assembly language
                {
                    if (labelToken != null)						// handle label if there is one
                        SymbolTable.AddDatLabelEntry(4, labelToken, false);

                    SimpleToken token = Tokenizer.Current;

                    int cond = 0x0f;			// default is IF_ALWAYS
                    CondToken condToken = null;
                    if (Tokenizer.Current is CondToken)
                    {
                        condToken = Tokenizer.GetToken() as CondToken;
                        cond = condToken.CondValue;
                    }
                    if (!(Tokenizer.Current is IPropInstruction))
                        throw new ParseException("Expected instruction mnemonic", Tokenizer.Current);

                    if (afterRes)
                        Tokenizer.PrintWarning("Assembly language after RES", Tokenizer.Current);

                    string mnemonic = Tokenizer.Current.Text.ToUpper();
                    IPropInstruction instruction = Tokenizer.GetToken() as IPropInstruction;
                    if (instruction.Propcode == 0)
                    {
                        if (condToken != null)
                            throw new ParseException("Condition not allowed on NOP", condToken);
                        cond = 0;
                    }
                    Expr eD = null;
                    Expr eS = null;
                    bool immediate = false;
                    if (instruction.InstructionType == InstructionTypeEnum.D || instruction.InstructionType == InstructionTypeEnum.DS)
                    {
                        eD = ParseExpression(Tokenizer, 13);
                    }
                    if (instruction.InstructionType == InstructionTypeEnum.DS)
                    {
                        Tokenizer.Advance(",");
                    }
                    if (instruction.InstructionType == InstructionTypeEnum.S || instruction.InstructionType == InstructionTypeEnum.DS)
                    {
                        if ((mnemonic == "JMP" || mnemonic == "DJNZ" || mnemonic == "TJNZ" || mnemonic == "TJZ") && Tokenizer.Current.Text != "#")
                            Tokenizer.PrintWarning(mnemonic + " with non-immediate operand", Tokenizer.Current);
                        if (Tokenizer.Current.Text == "#" || mnemonic == "CALL")
                        {
                            Tokenizer.Advance("#");
                            immediate = true;
                        }
                        eS = ParseExpression(Tokenizer, 13);
                    }
                    if (mnemonic == "CALL")
                    {
                        if (!(eS is IdExpr))
                            throw new ParseException("Expected label", eS.Token);
                        eD = new IdExpr(new SimpleToken(Tokenizer, SimpleTokenType.Id, eS.Token.Text + "_ret", eS.Token.LineNumber, eS.Token.Column));
                    }
                    bool memoryInstruction = mnemonic.StartsWith("RD") || mnemonic.StartsWith("WR");
                    int effect = 0;	// bit 0 - WR
                    // bit 1 - WC
                    // bit 2 - WZ
                    // bit 3 - NR
                    if (Tokenizer.Current is EffectToken && instruction.Propcode != 0)	// No effects allowed on NOP
                    {
                        while (true)
                        {
                            Token effectToken = Tokenizer.GetToken();
                            int e = (effectToken as EffectToken).Effect;
                            if (e == 1)		// WR
                            {
                                if (memoryInstruction)
                                    throw new ParseException("Memory instructions cannot use WR/NR", effectToken);
                                effect &= 7;	// clear NR bit
                            }
                            else if (e == 8)	// NR?
                            {
                                if (memoryInstruction)
                                    throw new ParseException("Memory instructions cannot use WR/NR", effectToken);
                                effect &= 0xe;	// clear WR bit
                            }
                            effect |= e;
                            if (Tokenizer.Current.Text == "(eol)")
                                break;
                            Tokenizer.Advance(",");
                        }
                    }
                    SymbolTable.AddDatInstructionEntry(instruction, cond, eD, eS, immediate, effect, token, Tokenizer.Current.LineNumber);
                }
                Tokenizer.Advance("(eol)");
            }
            Tokenizer.SetLocalLabelParsingEnable(false);
            return true;
        }
    }

    class ObjToken : BlockDesignatorToken
    {
        public ObjToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            s = null;

            if (this.Column != 0)
                throw new ParseException("OBJ must be in 1st column", this);
            Tokenizer.Advance();	// past "OBJ".
            if (Tokenizer.Current.Text == "(eol)")
                Tokenizer.Advance("(eol)");

            while (Tokenizer.Current is IdToken)
            {
                IdToken objName = Tokenizer.GetToken() as IdToken;

                Expr countExpr = null;
                if (Tokenizer.Current.Text == "[")
                {
                    Tokenizer.Advance("[");
                    countExpr = ParseExpression(Tokenizer, 13);
                    Tokenizer.Advance("]");
                }

                Tokenizer.Advance(":");
                string filename = "";
                int lineNumber = Tokenizer.Current.LineNumber;
                int column = Tokenizer.Current.Column;

                while (Tokenizer.Current is IntToken)
                {
                    IntToken intToken = Tokenizer.GetToken() as IntToken;
                    filename += (char)intToken.IntValue;
                    if (Tokenizer.Current.Text != ",")
                        break;
                    Tokenizer.Advance(",");
                }
                if (filename == "")
                    throw new ParseException("Expected object file name", Tokenizer.Current);
                filename.Trim();
                if (!filename.ToUpper().EndsWith(".SPIN"))
                    filename += ".spin";
                // filename always includes .spin suffix.

                SimpleToken filenameToken = new SimpleToken(Tokenizer, SimpleTokenType.Id, filename, lineNumber, column);
                bool needsVarSpace = true;
                if (Tokenizer.Current.Text.ToUpper() == "POINTER")
                {
                    Tokenizer.Advance();
                    needsVarSpace = false;
                }
                Tokenizer.Advance("(eol)");
                SymbolTable.AddObjSymbol(objName, filenameToken, countExpr, needsVarSpace);
                Tokenizer tokenizer = new Tokenizer(filenameToken, Tokenizer.Defines);
                tokenizer.Go();
            }
            return true;
        }
    }

    class PriPubToken : BlockDesignatorToken
    {
        bool isPub;

        public PriPubToken(SimpleToken tokenInfo, bool isPub)
            : base(tokenInfo)
        {
            this.isPub = isPub;
        }
        public override bool Std(out Stmt s)
        {
            s = null;

            ArrayList localNameList = new ArrayList();
            ArrayList localCountList = new ArrayList();
            // I *should* make a name/countExpr structure, but I'm too lazy,
            // so for now I'll keep name and count expressions in parallel arraylists.

            localNameList.Add(null);	// placeholder for result variable
            localCountList.Add(null);	// and its count expression.

            if (this.Column != 0)
                throw new ParseException("PUB/PRI must be in 1st column", this);
            Tokenizer.Advance();	// past "PUB" or "PRI"
            SimpleToken token = Tokenizer.GetToken();
            IdToken methodName = token as IdToken;
            if (methodName == null)
                throw new ParseException("Expected method name", token);

            int nParams = 0;

            if (Tokenizer.Current.Text == "(")	// Parse parameter list.
            {
                Tokenizer.Advance("(");
                while (true)
                {
                    IdToken paramToken = Tokenizer.Current as IdToken;
                    if (paramToken == null)
                        throw new ParseException("Expected parameter name", Tokenizer.Current);
                    Tokenizer.Advance();
                    localNameList.Add(paramToken);
                    localCountList.Add(null);
                    ++nParams;
                    if (Tokenizer.Current.Text != ",")
                        break;
                    Tokenizer.Advance(",");
                }
                Tokenizer.Advance(")");
            }
            if (Tokenizer.Current.Text == ":") // Parse result variable.
            {
                Tokenizer.Advance(":");
                Token t = Tokenizer.GetToken();
                IdToken resultToken = t as IdToken;
                if (resultToken == null)
                {
                    if (t.Text.ToUpper() == "RESULT")
                        resultToken = new IdToken(t);
                    else
                        throw new ParseException("Expected result variable", t);
                }
                localNameList[0] = resultToken;
            }
            if (Tokenizer.Current.Text == "|") // Parse local variables.
            {
                Tokenizer.Advance("|");
                while (true)
                {
                    IdToken localToken = Tokenizer.GetToken() as IdToken;
                    if (localToken == null)
                        throw new ParseException("Expected local variable name", Tokenizer.Current);
                    localNameList.Add(localToken);
                    if (Tokenizer.Current.Text == "[")
                    {
                        Tokenizer.Advance("[");
                        localCountList.Add(ParseExpression(Tokenizer, 13));
                        Tokenizer.Advance("]");
                    }
                    else
                    {
                        localCountList.Add(null);
                    }
                    if (Tokenizer.Current.Text != ",")
                        break;
                    Tokenizer.Advance(",");
                }
            }
            int endLineNumber = Tokenizer.Current.LineNumber;

            Tokenizer.Advance("(eol)");

            ArrayList statementList = ParseStatements(Tokenizer, -1);

            SymbolTable.AddMethod(methodName, isPub, nParams, localNameList, localCountList, statementList, endLineNumber);
            return true;
        }
    }

    class VarToken : BlockDesignatorToken
    {
        public VarToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            s = null;
            if (this.Column != 0)
                throw new ParseException("VAR must be in 1st column", this);
            Tokenizer.Advance("VAR");
            if (!(Tokenizer.Current is SizeToken))
                Tokenizer.Advance("(eol)");

            while (Tokenizer.Current is SizeToken)
            {
                SizeToken sizeToken = Tokenizer.GetToken() as SizeToken;
                if (sizeToken == null)
                    throw new ParseException("Expected BYTE|WORD|LONG", sizeToken);
                while (true)
                {
                    IdToken varName = Tokenizer.GetToken() as IdToken;
                    if (varName == null)
                        throw new ParseException("Expected VAR name", varName);
                    ArrayList countExprList = new ArrayList();
                    if (Tokenizer.Current.Text == "[")
                    {
                        Tokenizer.Advance("[");
                        while (true)
                        {
                            countExprList.Add(ParseExpression(Tokenizer, 13));
                            if (Tokenizer.Current.Text != ",")
                                break;
                            Tokenizer.Advance(",");
                        }
                        Tokenizer.Advance("]");
                    }
                    SymbolTable.AddVarSymbol(varName, sizeToken, countExprList);
                    if (Tokenizer.Current.Text != ",")
                        break;
                    Tokenizer.Advance(",");
                }
                Tokenizer.Advance("(eol)");
            }
            return true;
        }
    }

    class ReturnToken : Token
    {
        public ReturnToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            Token returnToken = Tokenizer.GetToken();	// "RETURN"
            if (Tokenizer.Current.Text != "(eol)")
            {
                Expr e = ParseExpression(Tokenizer, 13);
                s = new ReturnStmt(returnToken, Tokenizer.Current.LineNumber, e);
            }
            else
                s = new ReturnStmt(returnToken, returnToken.LineNumber, null);

            Tokenizer.Advance("(eol)");
            return true;
        }
    }

    class IfToken : Token
    {
        public IfToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            int indent = Tokenizer.Current.Column;
            Token IfToken = Tokenizer.GetToken();	// "IF" (or "IFNOT" or "ELSEIF" or "ELSEIFNOT")

            Expr condExpr = ParseExpression(Tokenizer, 13);
            int endLineNumber = Tokenizer.Current.LineNumber;
            Tokenizer.Advance("(eol)");

            SimpleToken elseToken = null;
            int elseEndLineNumber = 0;

            ArrayList ifStatementsList = ParseStatements(Tokenizer, indent);

            ArrayList elseStatementsList = null;
            if (Tokenizer.Current.Column == indent)
            {
                if (Tokenizer.Current.Text.ToUpper() == "ELSE")
                {
                    elseToken = Tokenizer.GetToken();
                    elseEndLineNumber = Tokenizer.Current.LineNumber;
                    Tokenizer.Advance("(eol)");
                    elseStatementsList = ParseStatements(Tokenizer, indent);
                }
                else if (Tokenizer.Current.Text.ToUpper() == "ELSEIF" || Tokenizer.Current.Text.ToUpper() == "ELSEIFNOT")
                {
                    Stmt elseifStmt;
                    Std(out elseifStmt);
                    elseStatementsList = new ArrayList();
                    elseStatementsList.Add(elseifStmt);
                }
            }
            s = new IfStmt(IfToken, endLineNumber, elseToken, elseEndLineNumber, condExpr, ifStatementsList, elseStatementsList);
            return true;
        }
    }

    class RepeatToken : Token
    {
        public RepeatToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            int indent = Tokenizer.Current.Column;
            SimpleToken token = Tokenizer.GetToken();	// REPEAT
            int endLineNumber = token.LineNumber;
            RepeatType type;
            bool whileNotUntil = true;

            Expr repeatExpr = null;
            Expr fromExpr = null;
            Expr toExpr = null;
            Expr stepExpr = null;

            if (Tokenizer.Current.Text != "(eol)")
            {
                if (Tokenizer.Current.Text.ToUpper() == "WHILE" || Tokenizer.Current.Text.ToUpper() == "UNTIL")
                {
                    type = RepeatType.WhileLoop;
                    whileNotUntil = Tokenizer.GetToken().Text.ToUpper() == "WHILE";
                    repeatExpr = ParseExpression(Tokenizer, 13);
                }
                else
                {
                    repeatExpr = ParseExpression(Tokenizer, 13);
                    if (Tokenizer.Current.Text == "(eol)")
                    {
                        type = RepeatType.NTimes;
                    }
                    else
                    {
                        type = RepeatType.FromTo;
                        Tokenizer.Advance("FROM");
                        fromExpr = ParseExpression(Tokenizer, 13);
                        Tokenizer.Advance("TO");
                        toExpr = ParseExpression(Tokenizer, 13);
                        if (Tokenizer.Current.Text != "(eol)")
                        {
                            Tokenizer.Advance("STEP");
                            stepExpr = ParseExpression(Tokenizer, 13);
                        }
                    }
                }
            }
            else
            {
                type = RepeatType.Plain;
            }
            endLineNumber = Tokenizer.Current.LineNumber;
            Tokenizer.Advance("(eol)");

            ArrayList statementsList = ParseStatements(Tokenizer, indent);

            if (type == RepeatType.Plain && Tokenizer.Current.Column == indent &&
                (Tokenizer.Current.Text.ToUpper() == "WHILE" || Tokenizer.Current.Text.ToUpper() == "UNTIL"))
            {
                type = RepeatType.LoopWhile;
                whileNotUntil = Tokenizer.GetToken().Text.ToUpper() == "WHILE";
                repeatExpr = ParseExpression(Tokenizer, 13);
                /// probably have to get start/end info for WHILE|UNTIL <expr>, tag it onto RepeatStmt somehow;;;
                Tokenizer.Advance("(eol)");
            }
            if (type != RepeatType.FromTo)
            {
                s = new RepeatStmt(token, endLineNumber, type, repeatExpr, statementsList, whileNotUntil);
            }
            else
            {
                s = new RepeatFromToStmt(token, endLineNumber, repeatExpr, fromExpr, toExpr, stepExpr, statementsList);
            }
            return true;
        }
    }

    class NextQuitToken : Token
    {
        public NextQuitToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            int indent = Tokenizer.Current.Column;
            SimpleToken token = Tokenizer.GetToken();	// NEXT | CONTINUE

            s = new NextQuitStmt(token, Tokenizer.Current.LineNumber);
            Tokenizer.Advance("(eol)");
            return true;
        }
    }

    class LookToken : Token
    {
        public LookToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Tokenizer.Advance("(");
            Expr expr = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(":");

            ArrayList args = new ArrayList();
            while (true)
            {
                Expr e;
                {
                    e = ParseExpression(Tokenizer, 13);
                }
                if (Tokenizer.Current.Text == "..")
                {
                    Tokenizer.Advance("..");
                    Expr f;
                    {
                        f = ParseExpression(Tokenizer, 13);
                    }
                    args.Add(new PairOfExpr(e, f));
                }
                else
                {
                    args.Add(e);
                }
                if (Tokenizer.Current.Text != ",")
                    break;
                Tokenizer.Advance(",");
            }
            Tokenizer.Advance(")");
            return new LookExpr(this, expr, args);
        }
    }

    struct PairOfExpr
    {
        public Expr left;
        public Expr right;
        public PairOfExpr(Expr left, Expr right)
        {
            this.left = left;
            this.right = right;
        }
    }

    class CaseToken : Token
    {
        public CaseToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            SimpleToken caseToken = Tokenizer.GetToken();	// CASE
            int caseIndent = caseToken.Column;
            Expr caseExpr = ParseExpression(Tokenizer, 13);
            int endLineNumber = Tokenizer.Current.LineNumber;	// end of the CASE <expr>
            Tokenizer.Advance("(eol)");
            ArrayList matchExprListList = new ArrayList();
            ArrayList matchStmtListList = new ArrayList();
            ArrayList otherStmtList = new ArrayList();
            ArrayList matchTokenList = new ArrayList();
            ArrayList matchEndLineNumberList = new ArrayList();

            int matchIndent = Tokenizer.Current.Column;
            if (matchIndent <= caseIndent)
                throw new ParseException("No cases encountered", Tokenizer.Current);
            bool otherEncountered = false;
            while (Tokenizer.Current.Column == matchIndent)
            {
                if (otherEncountered)
                {
                    throw new ParseException("OTHER must be last case", Tokenizer.Current);
                }
                matchTokenList.Add(Tokenizer.Current);
                if (Tokenizer.Current.Text.ToUpper() == "OTHER")
                {
                    otherEncountered = true;
                    Tokenizer.Advance("OTHER");
                    Tokenizer.Advance(":");
                    matchEndLineNumberList.Add(Tokenizer.Current.LineNumber);

                    if (Tokenizer.Current.Text == "(eol)")
                        Tokenizer.Advance();
                    otherStmtList = ParseStatements(Tokenizer, matchIndent);
                }
                else
                {
                    ArrayList matchExprList = new ArrayList();
                    while (true)	// parse match expressions
                    {
                        Expr e = ParseExpression(Tokenizer, 13);
                        if (Tokenizer.Current.Text == "..")
                        {
                            Tokenizer.Advance("..");
                            Expr f = ParseExpression(Tokenizer, 13);
                            matchExprList.Add(new PairOfExpr(e, f));
                        }
                        else
                        {
                            matchExprList.Add(e);
                        }
                        if (Tokenizer.Current.Text != ",")
                            break;
                        Tokenizer.Advance(",");
                    }
                    Tokenizer.Advance(":");
                    matchEndLineNumberList.Add(Tokenizer.Current.LineNumber);

                    matchExprListList.Add(matchExprList);

                    if (Tokenizer.Current.Text == "(eol)")
                        Tokenizer.Advance();
                    matchStmtListList.Add(ParseStatements(Tokenizer, matchIndent));
                }
            }
            s = new CaseStmt(caseToken, endLineNumber, caseExpr, matchExprListList, matchStmtListList, otherStmtList, matchTokenList, matchEndLineNumberList);
            return true;
        }
    }

    class AbortToken : Token
    {
        public AbortToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            SimpleToken abortToken = Tokenizer.GetToken();
            Expr abortExpr = null;
            if (Tokenizer.Current.Text != "(eol)")
            {
                abortExpr = ParseExpression(Tokenizer, 13);
            }
            s = new AbortStmt(abortToken, Tokenizer.Current.LineNumber, abortExpr);
            Tokenizer.Advance("(eol)");
            return true;
        }
    }

    class RebootToken : Token
    {
        public RebootToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            SimpleToken rebootToken = Tokenizer.GetToken();
            s = new RebootStmt(rebootToken, Tokenizer.Current.LineNumber);
            Tokenizer.Advance("(eol)");
            return true;
        }
    }

    class FillMoveToken : Token
    {
        // For {BYTE|WORD|LONG}{FILL|MOVE}
        public FillMoveToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            SimpleToken fillMoveToken = Tokenizer.GetToken();
            ArrayList argList = ParseArgumentList(Tokenizer);
            if (argList.Count != 3)
                throw new ParseException("Expected three arguments", Tokenizer.Current);
            s = new FillMoveStmt(fillMoveToken, Tokenizer.Current.LineNumber, argList);
            Tokenizer.Advance("(eol)");
            return true;
        }
    }

    class WaitToken : Token, IPropInstruction
    {
        public WaitToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            Tokenizer.Advance();	// past WAIT*
            ArrayList argList = ParseArgumentList(Tokenizer);
            int nExpected = 0;
            switch (Text.ToUpper())
            {
                case "WAITCNT": nExpected = 1; break;
                case "WAITPEQ": nExpected = 3; break;
                case "WAITPNE": nExpected = 3; break;
                case "WAITVID": nExpected = 2; break;
            }
            if (argList.Count != nExpected)
                throw new ParseException("Expected this many arguments: " + nExpected.ToString(), Tokenizer.Current);
            s = new WaitStmt(this, Tokenizer.Current.LineNumber, argList);
            Tokenizer.Advance("(eol)");
            return true;
        }
        public uint Propcode
        {
            get
            {
                switch (Text.ToUpper())
                {
                    case "WAITCNT": return 0xf8bc0000;
                    case "WAITPEQ": return 0xf03c0000;
                    case "WAITPNE": return 0xf43c0000;
                    case "WAITVID": return 0xfc3c0000;
                    default: return 666;	// won't happen (I hope!)
                }
            }
        }
        public InstructionTypeEnum InstructionType { get { return InstructionTypeEnum.DS; } }
        public string Mnemonic { get { return Text; } }
    }

    class ClksetToken : Token, IPropInstruction
    {
        public ClksetToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            Tokenizer.Advance();
            ArrayList argList = ParseArgumentList(Tokenizer);
            if (argList.Count != 2)
                throw new ParseException("Expected two arguments", Tokenizer.Current);
            s = new ClksetStmt(this, Tokenizer.Current.LineNumber, argList);
            Tokenizer.Advance("(eol)");
            return true;
        }
        public uint Propcode { get { return 0x0c7c0000; } }
        public InstructionTypeEnum InstructionType { get { return InstructionTypeEnum.D; } }
        public string Mnemonic { get { return Text; } }
    }

    class CogstopToken : Token, IPropInstruction
    {
        public CogstopToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override bool Std(out Stmt s)
        {
            Tokenizer.Advance();
            ArrayList argList = ParseArgumentList(Tokenizer);
            if (argList.Count != 1)
                throw new ParseException("Expected one argument", Tokenizer.Current);
            s = new CogstopStmt(this, Tokenizer.Current.LineNumber, argList);
            Tokenizer.Advance("(eol)");
            return true;
        }
        public uint Propcode { get { return 0x0c7c0003; } }
        public InstructionTypeEnum InstructionType { get { return InstructionTypeEnum.D; } }
        public string Mnemonic { get { return Text; } }
    }

    class RegisterToken : Token
    {
        //	For CNT, CTRA, CTRB, etc.
        byte reg;
        public RegisterToken(SimpleToken tokenInfo, byte reg)
            : base(tokenInfo)
        {
            this.reg = reg;
        }
        public override Expr Nud()
        {
            Expr e = null;
            Expr f = null;
            if (Tokenizer.Current.Text == "[")
            {
                Tokenizer.Advance("[");
                e = ParseExpression(Tokenizer, 13);
                if (Tokenizer.Current.Text == "..")
                {
                    Tokenizer.Advance("..");
                    f = ParseExpression(Tokenizer, 13);
                }
                Tokenizer.Advance("]");
            }
            return new RegisterExpr(this, reg, e, f);
        }
    }

    class SprToken : Token
    {
        //	For SPR
        public SprToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Tokenizer.Advance("[");
            Expr e = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance("]");
            return new SprExpr(this, e);
        }
    }

    class ReadOnlyVariableToken : Token
    {
        //	For CHIPVER, CLKFREQ, CLKMODE
        public ReadOnlyVariableToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            return new ReadOnlyVariableExpr(this);
        }
    }

    class CogidToken : Token, IPropInstruction
    {
        public CogidToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            return new ReadOnlyVariableExpr(this);
        }
        public uint Propcode { get { return 0x0cfc0001; } }
        public InstructionTypeEnum InstructionType { get { return InstructionTypeEnum.D; } }
        public string Mnemonic { get { return Text; } }
    }

    class ConverterToken : Token
    {
        //	For FLOAT, ROUND, TRUNC
        public ConverterToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Tokenizer.Advance("(");
            Expr operand = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(")");
            return new ConverterExpr(this, operand);
        }
    }

    class StringToken : Token
    {
        //	For STRING( "blah" )
        public StringToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Tokenizer.Advance("(");
            string s = "";
            Token startToken = Tokenizer.Current;
            while (true)
            {
                Expr e = ParseExpression(Tokenizer, 13);
                s += (char)(Expr.EvaluateIntConstant(e));
                if (Tokenizer.Current.Text != ",")
                    break;
                Tokenizer.Advance(",");
            }
            Tokenizer.Advance(")");
            startToken.Text = s;
            return new StringExpr(this, new StringToken(startToken));
        }
    }

    class CoginitToken : Token, IPropInstruction
    {
        public CoginitToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Tokenizer.Advance("(");
            Expr e0 = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(",");
            Expr e1 = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(",");
            Expr e2 = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(")");
            return new CoginewtExpr(this, e0, e1, e2);
        }
        public uint Propcode
        {
            get
            {
                return 0x0c7c0002;
            }
        }
        public InstructionTypeEnum InstructionType
        {
            get
            {
                return InstructionTypeEnum.D;
            }
        }
        public string Mnemonic { get { return Text; } }
    }

    class CognewToken : Token
    {
        public CognewToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Tokenizer.Advance("(");
            Expr e1 = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(",");
            Expr e2 = ParseExpression(Tokenizer, 13);
            Tokenizer.Advance(")");
            return new CoginewtExpr(this, null, e1, e2);
        }
    }

    class StrFunctionToken : Token
    {
        // for STRCOMP and STRSIZE
        bool isComp;
        public StrFunctionToken(SimpleToken tokenInfo, bool isComp)
            : base(tokenInfo)
        {
            this.isComp = isComp;
        }
        public override Expr Nud()
        {
            SimpleToken token = Tokenizer.Current;
            ArrayList args = ParseArgumentList(Tokenizer);
            if (args.Count != (isComp ? 2 : 1))
                throw new ParseException("Wrong number of arguments", token);
            return new StrFunctionExpr(this, isComp, args);
        }
    }

    class LockToken : Token, IPropInstruction
    {
        // for LOCKCLR, LOCKNEW, LOCKRET, LOCKSET
        public LockToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            ArrayList args = null;
            SimpleToken token = Tokenizer.Current;
            if (Text.ToUpper() != "LOCKNEW")
            {
                args = ParseArgumentList(Tokenizer);
                if (args.Count != 1)
                    throw new ParseException("Expected one argument", token);
            }
            return new LockExpr(this, args);
        }
        public uint Propcode
        {
            get
            {
                switch (Text.ToUpper())
                {
                    case "LOCKCLR": return 0x0c7c0007;
                    case "LOCKNEW": return 0x0cfc0004;
                    case "LOCKRET": return 0x0c7c0005;
                    case "LOCKSET": return 0x0c7c0006; ;
                }
                return 0;
            }
        }
        public Homespun.InstructionTypeEnum InstructionType { get { return InstructionTypeEnum.D; } }
        public string Mnemonic { get { return Text; } }
    }

    class CondToken : Token
    {
        // assembly language conditionals
        int condValue;
        public int CondValue { get { return condValue; } }
        public CondToken(SimpleToken tokenInfo, int condValue)
            : base(tokenInfo)
        {
            this.condValue = condValue;
        }
    }

    class InstructionToken : Token, IPropInstruction
    {
        uint propcode;
        InstructionTypeEnum instructionType;
        public InstructionToken(SimpleToken tokenInfo, uint propcode, InstructionTypeEnum instructionType)
            : base(tokenInfo)
        {
            this.propcode = propcode;
            this.instructionType = instructionType;
        }
        public uint Propcode { get { return propcode; } }
        public InstructionTypeEnum InstructionType { get { return instructionType; } }
        public string Mnemonic { get { return Text; } }
    }

    class EffectToken : Token
    {
        // WC, WZ, WR, NR
        int effect;
        public EffectToken(SimpleToken tokenInfo, int effect)
            : base(tokenInfo)
        {
            this.effect = effect;
        }
        public int Effect { get { return effect; } }
    }

    class BackslashToken : Token
    {
        public BackslashToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            Expr e = ParseExpression(Tokenizer, 0);
            if (e is CallExpr)
            {
                CallExpr ce = e as CallExpr;
                ce.AbortTrap = true;
                return ce;
            }
            if (e is IdExpr)
            {
                IdExpr ie = e as IdExpr;
                CallExpr ce = new CallExpr(null, ie.Token, null, new ArrayList());
                ce.AbortTrap = true;
                return ce;
            }
            throw new ParseException("Expected method call", this);
        }
    }

    class DollarToken : Token
    {
        public DollarToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            return new DollarExpr(this);
        }
    }

    class AtAtAtToken : Token
    {
        public AtAtAtToken(SimpleToken tokenInfo)
            : base(tokenInfo)
        {
        }
        public override Expr Nud()
        {
            if (!(tokenizer.Current is IdToken))
                throw new ParseException("Expected DAT symbol", tokenizer.Current);
            GlobalSymbolTable.DisableDuplicateObjectElimination();
            return new AtAtAtExpr(tokenizer.GetToken() as IdToken);
        }
    }

    class Options
    {
        public static bool dump = false;
        public static bool saveBinary = false;
        public static bool saveDat = false;
        public static bool warnings = false;
        public static ArrayList pathList = new ArrayList();
        public static int informationLevel = 3;
        public static bool saveSob = false;
        public static int memorySize = 32768;
        public static string outputFilename = "";
        public static Hashtable defines = new Hashtable();
        public static string TryPaths(SimpleToken filenameToken)
        {
            string filename = filenameToken.Text;
            foreach (string trypath in Options.pathList)
            {
                if (Options.informationLevel == 7)
                    Console.WriteLine("{0}?", trypath + filename);
                FileInfo fi = new FileInfo(trypath + filename);
                if (fi.Exists)
                    return trypath;
            }
            throw new ParseException("File not found", filenameToken);
        }
    }

    class Blah
    {
        static void PrintHelp()
        {
            Console.WriteLine(
@"Usage: homespun filename [options]
Options:
-? -- Quick help (this message).
-b -- Write .binary file instead of the default .eeprom file.
-c -- Write .dat file too.
-d -- Dump memory listing.
-D -- Define symbol; e.g. -D DEBUG
-e -- Write .eeprom file (default).
-i<n> -- Set information level: -i0 -- no messages. -i1, -i2 -- some. -i3 -- all (default).
-L -- Specify library path; e.g. -L c:\mylib\ -L c:\obex\
-M -- Override 32k limit (use at own risk); e.g. -M 65536
-o -- Specify output filename; e.g. -o blah
-w -- Enable warnings.");
        }
        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("Homespun Spin Compiler 0.32p1 - Batang Build");

                ArrayList filenameList = new ArrayList();
                if (args.Length == 0)
                {
                    PrintHelp();
                }

                Options.pathList.Add("");

                for (int i = 0; i < args.Length; ++i)
                {
                    string s = args[i];
                    if (s[0] == '-')
                    {
                        switch (s)
                        {
                            case "-?": PrintHelp(); break;
                            case "-b": Options.saveBinary = true; break;
                            case "-c": Options.saveDat = true; break;
                            case "-d": Options.dump = true; break;
                            case "-D":
                                if (++i >= args.Length)
                                {
                                    Console.WriteLine("-D must be followed by symbol");
                                }
                                Options.defines.Add(args[i], "");
                                break;
                            case "-e": Options.saveBinary = false; break;
                            case "-w": Options.warnings = true; break;
                            case "-i0": Options.informationLevel = 0; break;
                            case "-i1": Options.informationLevel = 1; break;
                            case "-i2": Options.informationLevel = 2; break;
                            case "-i3": Options.informationLevel = 3; break;
                            case "-i7": Options.informationLevel = 7; break; ; ;
                            case "-L":
                                if (++i >= args.Length)
                                {
                                    Console.WriteLine("-L option must be followed by library path");
                                }
                                string path = args[i];
                                if (!path.EndsWith("\\") && !path.EndsWith("/"))
                                {
                                    if (path.IndexOf('/') != -1)
                                        path += "/";
                                    else
                                        path += "\\";
                                }
                                Options.pathList.Add(path);
                                break;
                            case "-M":
                                if (++i >= args.Length)
                                {
                                    Console.WriteLine("-M option must be followed by a number (memory size)");
                                }
                                Options.memorySize = int.Parse(args[i]);
                                break;
                            case "-sob": Options.saveSob = true; break;
                            case "-o":
                                if (++i >= args.Length)
                                {
                                    Console.WriteLine("-o option must be followed by output filename");
                                }
                                Options.outputFilename = args[i];
                                break;
                            default: Console.WriteLine("Unknown option: {0}", s); break;
                        }
                    }
                    else
                        filenameList.Add(s);
                }

                string spinlib = Environment.GetEnvironmentVariable("SPINLIB");
                if (spinlib != null)
                {
                    foreach (string s in spinlib.Split(new char[] { ';' }))
                    {
                        string path = s;
                        if (!path.EndsWith("\\") && !path.EndsWith("/"))
                        {
                            if (path.IndexOf('/') != -1)
                                path += "/";
                            else
                                path += "\\";
                        }
                        Options.pathList.Add(path);
                    }
                }

                if (filenameList.Count == 0)
                {
                    Console.WriteLine("No input file specified");
                    Environment.Exit(1);
                }
                else if (filenameList.Count > 1)
                {
                    Console.WriteLine("More than one input file specified");
                    Environment.Exit(1);
                }
                else
                {
                    string filename = filenameList[0] as string;
                    int m = filename.LastIndexOfAny(new char[] { '/', '\\' });
                    if (m >= 0)
                    {
                        Options.pathList[0] = filename.Substring(0, m + 1);
                        filename = filename.Substring(m + 1, filename.Length - m - 1);
                    }

                    if (!filename.ToUpper().EndsWith(".SPIN"))
                    {
                        filename += ".spin";
                    }
                    Compile(filename, Options.defines);
                    if (Options.dump)
                    {
                        StringWriter sw = new StringWriter();
                        GlobalSymbolTable.Dump(sw);
                        string listFilename = filename.Substring(0, filename.Length - 5) + ".lst";
                        if (Options.outputFilename != "")
                        {
                            listFilename = Options.outputFilename + ".lst";
                        }
                        Console.WriteLine("Writing listing to {0}", listFilename);
                        StreamWriter listFile = new StreamWriter(listFilename);
                        listFile.Write(sw);
                        listFile.Close();
                    }
                }
                Environment.Exit(0);
            }
            catch (ParseException e)
            {
                if (e.Tokenizer != null)
                    Console.WriteLine(e.Tokenizer.FormatError(e));
                else
                    Console.WriteLine(e.Message);
                Environment.Exit(1);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }
        static void Compile(string filename, Hashtable defines)
        {
            SimpleToken st = new SimpleToken(null, SimpleTokenType.Id, filename, 0, 0);
            Tokenizer tokenizer = new Tokenizer(st, defines);
            tokenizer.Go();

            GlobalSymbolTable.CompileAll();
            GlobalSymbolTable.EliminateDuplicateObjects();
            byte[] memory = new byte[Options.memorySize];
            GlobalSymbolTable.ToMemory(memory);
            Dis.Mem = memory;
            if (Options.saveBinary)
            {
                string outputFilename = filename.Substring(0, filename.Length - 5) + ".binary";
                if (Options.outputFilename != "")
                {
                    outputFilename = Options.outputFilename;
                    if (!outputFilename.ToUpper().EndsWith(".BINARY"))
                        outputFilename += ".binary";
                }
                int n = GlobalSymbolTable.varAddress;
                if ((n & 0xffff) != (memory[8] + (memory[9] << 8)))
                {
                    throw new Exception("internal error writing .binary file; please report");
                }
                Console.WriteLine("writing {0} bytes to {1}", n, outputFilename);
                BinaryWriter bw = new BinaryWriter(new FileStream(outputFilename, FileMode.Create));
                bw.Write(memory, 0, n);
            }
            else	// save .eeprom
            {
                string outputFilename = filename.Substring(0, filename.Length - 5) + ".eeprom";
                if (Options.outputFilename != "")
                {
                    outputFilename = Options.outputFilename;
                    if (!outputFilename.ToUpper().EndsWith(".EEPROM"))
                        outputFilename += ".eeprom";
                }
                Console.WriteLine("writing 32768 bytes to {0}", outputFilename);
                BinaryWriter bw = new BinaryWriter(new FileStream(outputFilename, FileMode.Create));
                bw.Write(memory);
            }
            if (Options.saveDat)
            {
                string outputFilename = filename.Substring(0, filename.Length - 5) + ".dat";
                if (Options.outputFilename != "")
                {
                    outputFilename = Options.outputFilename;
                    if (!outputFilename.ToUpper().EndsWith(".DAT"))
                        outputFilename += ".dat";
                }
                int start = (memory[0x12] + memory[0x13]) * 4;
                int n = memory[0x14] + memory[0x15] * 256 // assumes 1st PUB's code immediately follows DAT
                    - start;
                start += 0x0010;
                Console.WriteLine("writing {0} bytes to {1}", n, outputFilename);
                BinaryWriter bw = new BinaryWriter(new FileStream(outputFilename, FileMode.Create));
                bw.Write(memory, start, n);
            }
            if (Options.saveSob)
                GlobalSymbolTable.WriteSobs();
        }
    }

} // namespace Homespun
