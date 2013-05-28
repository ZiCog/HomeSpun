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


	Simplified Spin lexer.
	
	2008
	7/6	Cannibalized old lexer code, simplified operator lookup, got nested {} comments to work.
	7/6	Integrating into tdop.
	7/9	Deleting IndentSymbol stuff.
	7/10	Make Tokenizer return Tokens that get turned into Symbols. When Tokenizer was
		returning Symbols, it would try to call itself recursively after "+" to see if
		the next symbol was "=", and recursion didn't work.
	7/12	Great Renaming I: Token => SimpleToken.
	7/12	Great Renaming II: Symbol => Token.
	7/13	Pass SimpleTokens instead of filename/line/col info to maybe simplify things.

 */
using System;
using System.IO;
using System.Text;
using System.Collections;

namespace Homespun
{
    enum SimpleTokenType
    {
        Id, IntLiteral, FloatLiteral, Op, Eol, Eof
    }
    class SimpleToken
    {
        SimpleTokenType type;
        protected string text;
        protected int intValue;
        protected float floatValue;
        protected int lineNumber;
        protected int column;
        protected Tokenizer tokenizer;

        public SimpleTokenType Type { get { return type; } }
        public string Text { get { return text; } set { text = value; } }
        public int IntValue { get { return intValue; } }
        public float FloatValue { get { return floatValue; } }
        public Tokenizer Tokenizer { get { return tokenizer; } }
        public int LineNumber { get { return lineNumber; } set { lineNumber = value; } }
        public int Column { get { return column; } }
        public ObjectFileSymbolTable SymbolTable { get { return tokenizer.SymbolTable; } }	// shortcut

        public SimpleToken(Tokenizer tokenizer, SimpleTokenType type, string text, int lineNumber, int column)
            : this(tokenizer, type, text, lineNumber, column, 0, 0.0f)
        {
        }
        public SimpleToken(Tokenizer tokenizer, SimpleTokenType type, string text, int lineNumber, int column, int intValue)
            : this(tokenizer, type, text, lineNumber, column, intValue, 0.0f)
        {
        }
        public SimpleToken(Tokenizer tokenizer, SimpleTokenType type, string text, int lineNumber, int column, int intValue, float floatValue)
        {
            this.tokenizer = tokenizer;
            this.type = type;
            this.text = text;
            this.lineNumber = lineNumber;
            this.column = column;
            this.intValue = intValue;
            this.floatValue = floatValue;
        }
    }

    class Tokenizer
    {
        delegate Token TokenMakerDelegate(SimpleToken st);

        //	0	Unary --, ++, ~, ~~, ?, @, @@

        static Token PlusPlusMaker(SimpleToken st)
        {
            return new PrePostToken(st, 0x20, 0x28);	// PREINC, POSTINC
        }
        static Token MinusMinusMaker(SimpleToken st)
        {
            return new PrePostToken(st, 0x30, 0x38);	// PREDEC, POSTDEC
        }
        static Token TildeMaker(SimpleToken st)
        {
            return new PrePostToken(st, 0x10, 0x18);	// sign-extend byte, POSTCLR
        }
        static Token TildeTildeMaker(SimpleToken st)
        {
            return new PrePostToken(st, 0x14, 0x1c);	// sign-extend word, POSTSET

        }
        static Token QuestionMaker(SimpleToken st)
        {
            return new PrePostToken(st, 0x08, 0x0c);	// FWDRAND, REVRAND
        }
        static Token AtMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 0, 0x00);	// @
        }
        static Token AtAtMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 0, 0x00);	// @@
        }
        static Token AtAtAtMaker(SimpleToken st)
        {
            return new AtAtAtToken(st);			// @@@
        }

        //	1	Unary +, -, ^^, ||, |<, >|, !	(+ and - are taken care of in PlusMaker/MinusMaker)

        static Token SqrtMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 1, 0xf8);	// ^^
        }
        static Token BarBarMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 1, 0xe9);	// ||
        }
        static Token DecodeMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 1, 0xf3);	// |<
        }
        static Token EncodeMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 1, 0xf1);	// >|
        }
        static Token BitNotMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 1, 0xe7);	// !
        }

        //	2	->, <-, >>, <<, ~>, ><

        static Token DashGreaterMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 2, 0xe0);	// ->
        }
        static Token LessDashMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 2, 0xe1);	// <-
        }
        static Token GreaterGreaterMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 2, 0xe2);	// >>
        }
        static Token LessLessMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 2, 0xe3);	// <<
        }
        static Token TildeGreaterMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 2, 0xee);	// ~>
        }
        static Token GreaterLessMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 2, 0xef);	// ><
        }

        //	3	&

        static Token BitAndMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 3, 0xe8);	// &
        }

        //	4	|, ^

        static Token BitOrMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 4, 0xea);	// |
        }
        static Token BitXorMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 4, 0xeb);	// ^
        }

        //	5	*, **, /, //

        static Token MulMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 5, 0xf4);	// *
        }
        static Token MulHighMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 5, 0xf5);	// **
        }
        static Token DivMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 5, 0xf6);	// /
        }
        static Token ModMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 5, 0xf7);	// //
        }


        //	6	+, -

        static Token PlusMaker(SimpleToken st)
        {
            if (st.Tokenizer.Current.Text == "=")
            {
                st.Tokenizer.Advance("=");
                st.Text = "+=";
                return new AssignOpToken(st, 0x4c);	// +
            }
            return new PlusToken(st);	// + operator is different from regular binary operators.
        }
        static Token MinusMaker(SimpleToken st)
        {
            if (st.Tokenizer.Current.Text == "=")
            {
                st.Tokenizer.Advance("=");
                st.Text = "-=";
                return new AssignOpToken(st, 0x4d);	// -
            }
            return new MinusToken(st);	// - operator is different from regular binary operators.
        }


        //	7	#>, <#

        static Token PoundGreaterMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 7, 0xe4);	// #>
        }
        static Token LessPoundMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 7, 0xe5);	// <#
        }


        //	8	<, >, <>, ==, =<, =>

        static Token LTMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 8, 0xf9);	// <
        }
        static Token GTMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 8, 0xfa);	// >
        }
        static Token NEMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 8, 0xfb);	// <>
        }
        static Token EQMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 8, 0xfc);	// ==
        }
        static Token LEMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 8, 0xfd);	// =<
        }
        static Token GEMaker(SimpleToken st)
        {
            return BinaryOpMaker(st, 8, 0xfe);	// =>
        }

        //	9	NOT

        static Token NotMaker(SimpleToken st)
        {
            return new UnaryOpToken(st, 9, 0xff);	// logical NOT
        }

        //	10	AND

        static Token AndMaker(SimpleToken st)
        {
            if (st.Tokenizer.Current.Text == "=")
            {
                st.Tokenizer.Advance("=");
                st.Text = st.Text + "=";
                return new AssignOpToken(st, 0xf0 - 0xa0);	// logical AND
            }
            return new AndToken(st);
        }

        //	11	OR

        static Token OrMaker(SimpleToken st)
        {
            if (st.Tokenizer.Current.Text == "=")
            {
                st.Tokenizer.Advance("=");
                st.Text = st.Text + "=";
                return new AssignOpToken(st, 0xf2 - 0xa0);	// logical OR
            }
            return new OrToken(st);
        }

        static Token BinaryOpMaker(SimpleToken st, int precedence, int opcode)
        {
            if (st.Tokenizer.Current.Text == "=")
            {
                st.Tokenizer.Advance("=");
                st.Text = st.Text + "=";
                return new AssignOpToken(st, opcode - 0xa0);
            }
            return new BinaryOpToken(st, precedence, opcode);
        }

        //	12	:=

        static Token AssignMaker(SimpleToken st)
        {
            return new AssignOpToken(st, 0x00);	/// also special
        }


        static Token LParenMaker(SimpleToken st)
        {
            return new LParenToken(st);
        }

        static Token TokenMaker(SimpleToken st)
        {
            return new Token(st);
        }

        static Token SizeMaker(SimpleToken st)
        {
            return new SizeToken(st);
        }

        static Token ConstantMaker(SimpleToken st)
        {
            return new ConstantToken(st);
        }

        static Token ConMaker(SimpleToken st)
        {
            return new ConToken(st);
        }

        static Token DatMaker(SimpleToken st)
        {
            return new DatToken(st);
        }

        static Token ObjMaker(SimpleToken st)
        {
            return new ObjToken(st);
        }

        static Token PriMaker(SimpleToken st)
        {
            return new PriPubToken(st, false);
        }

        static Token PubMaker(SimpleToken st)
        {
            return new PriPubToken(st, true);
        }

        static Token VarMaker(SimpleToken st)
        {
            return new VarToken(st);
        }

        static Token ReturnMaker(SimpleToken st)
        {
            return new ReturnToken(st);
        }

        static Token ResultMaker(SimpleToken st)
        {
            return new ResultToken(st);
        }

        static Token IfMaker(SimpleToken st)
        {
            return new IfToken(st);
        }

        static Token RepeatMaker(SimpleToken st)
        {
            return new RepeatToken(st);
        }

        static Token NextQuitMaker(SimpleToken st)
        {
            return new NextQuitToken(st);
        }

        static Token LookMaker(SimpleToken st)
        {
            return new LookToken(st);
        }

        static Token CaseMaker(SimpleToken st)
        {
            return new CaseToken(st);
        }

        static Token AbortMaker(SimpleToken st)
        {
            return new AbortToken(st);
        }

        static Token RebootMaker(SimpleToken st)
        {
            return new RebootToken(st);
        }

        static Token FillMoveMaker(SimpleToken st)
        {
            return new FillMoveToken(st);
        }

        static Token WaitMaker(SimpleToken st)
        {
            return new WaitToken(st);
        }

        static Token ClksetMaker(SimpleToken st)
        {
            return new ClksetToken(st);
        }

        static Token CogstopMaker(SimpleToken st)
        {
            return new CogstopToken(st);
        }

        static Token ParMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x00);
        }
        static Token CntMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x01);
        }
        static Token InaMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x02);
        }
        static Token InbMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x03);
        }
        static Token OutaMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x04);
        }
        static Token OutbMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x05);
        }
        static Token DiraMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x06);
        }
        static Token DirbMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x07);
        }
        static Token CtraMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x08);
        }
        static Token CtrbMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x09);
        }
        static Token FrqaMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x0a);
        }
        static Token FrqbMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x0b);
        }
        static Token PhsaMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x0c);
        }
        static Token PhsbMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x0d);
        }
        static Token VcfgMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x0e);
        }
        static Token VsclMaker(SimpleToken st)
        {
            return new RegisterToken(st, (byte)0x0f);
        }

        static Token SprMaker(SimpleToken st)
        {
            return new SprToken(st);
        }

        static Token ReadOnlyVariableMaker(SimpleToken st)
        {
            return new ReadOnlyVariableToken(st);
        }

        static Token CogidMaker(SimpleToken st)
        {
            return new CogidToken(st);
        }

        static Token ConverterMaker(SimpleToken st)
        {
            return new ConverterToken(st);
        }

        static Token StringMaker(SimpleToken st)
        {
            return new StringToken(st);
        }

        static Token CoginitMaker(SimpleToken st)
        {
            return new CoginitToken(st);
        }

        static Token CognewMaker(SimpleToken st)
        {
            return new CognewToken(st);
        }

        static Token StrcompMaker(SimpleToken st)
        {
            return new StrFunctionToken(st, true);
        }

        static Token StrsizeMaker(SimpleToken st)
        {
            return new StrFunctionToken(st, false);
        }

        static Token LockMaker(SimpleToken st)
        {
            return new LockToken(st);
        }

        static Token BackslashMaker(SimpleToken st)
        {
            return new BackslashToken(st);
        }

        static Token DollarMaker(SimpleToken st)
        {
            return new DollarToken(st);
        }

        static Token Cond0Maker(SimpleToken st) { return new CondToken(st, 0x00); }
        static Token Cond1Maker(SimpleToken st) { return new CondToken(st, 0x01); }
        static Token Cond2Maker(SimpleToken st) { return new CondToken(st, 0x02); }
        static Token Cond3Maker(SimpleToken st) { return new CondToken(st, 0x03); }
        static Token Cond4Maker(SimpleToken st) { return new CondToken(st, 0x04); }
        static Token Cond5Maker(SimpleToken st) { return new CondToken(st, 0x05); }
        static Token Cond6Maker(SimpleToken st) { return new CondToken(st, 0x06); }
        static Token Cond7Maker(SimpleToken st) { return new CondToken(st, 0x07); }
        static Token Cond8Maker(SimpleToken st) { return new CondToken(st, 0x08); }
        static Token Cond9Maker(SimpleToken st) { return new CondToken(st, 0x09); }
        static Token CondAMaker(SimpleToken st) { return new CondToken(st, 0x0a); }
        static Token CondBMaker(SimpleToken st) { return new CondToken(st, 0x0b); }
        static Token CondCMaker(SimpleToken st) { return new CondToken(st, 0x0c); }
        static Token CondDMaker(SimpleToken st) { return new CondToken(st, 0x0d); }
        static Token CondEMaker(SimpleToken st) { return new CondToken(st, 0x0e); }
        static Token CondFMaker(SimpleToken st) { return new CondToken(st, 0x0f); }

        static Token NrMaker(SimpleToken st) { return new EffectToken(st, 0x08); }
        static Token WcMaker(SimpleToken st) { return new EffectToken(st, 0x02); }
        static Token WrMaker(SimpleToken st) { return new EffectToken(st, 0x01); }
        static Token WzMaker(SimpleToken st) { return new EffectToken(st, 0x04); }

        static Token AbsMaker(SimpleToken st) { return new InstructionToken(st, 0xa8bc0000, InstructionTypeEnum.DS); }
        static Token AbsnegMaker(SimpleToken st) { return new InstructionToken(st, 0xacbc0000, InstructionTypeEnum.DS); }
        static Token AddMaker(SimpleToken st) { return new InstructionToken(st, 0x80bc0000, InstructionTypeEnum.DS); }
        static Token AddabsMaker(SimpleToken st) { return new InstructionToken(st, 0x88bc0000, InstructionTypeEnum.DS); }
        static Token AddsMaker(SimpleToken st) { return new InstructionToken(st, 0xd0bc0000, InstructionTypeEnum.DS); }
        static Token AddsxMaker(SimpleToken st) { return new InstructionToken(st, 0xd8bc0000, InstructionTypeEnum.DS); }
        static Token AddxMaker(SimpleToken st) { return new InstructionToken(st, 0xc8bc0000, InstructionTypeEnum.DS); }
        static Token AndnMaker(SimpleToken st) { return new InstructionToken(st, 0x64bc0000, InstructionTypeEnum.DS); }
        static Token CallMaker(SimpleToken st) { return new InstructionToken(st, 0x5cbc0000, InstructionTypeEnum.S); }
        static Token CmpMaker(SimpleToken st) { return new InstructionToken(st, 0x843c0000, InstructionTypeEnum.DS); }
        static Token CmpsMaker(SimpleToken st) { return new InstructionToken(st, 0xc03c0000, InstructionTypeEnum.DS); }
        static Token CmpsubMaker(SimpleToken st) { return new InstructionToken(st, 0xe0bc0000, InstructionTypeEnum.DS); }
        static Token CmpsxMaker(SimpleToken st) { return new InstructionToken(st, 0xc43c0000, InstructionTypeEnum.DS); }
        static Token CmpxMaker(SimpleToken st) { return new InstructionToken(st, 0xcc3c0000, InstructionTypeEnum.DS); }
        static Token DjnzMaker(SimpleToken st) { return new InstructionToken(st, 0xe4bc0000, InstructionTypeEnum.DS); }
        static Token HubopMaker(SimpleToken st) { return new InstructionToken(st, 0x0c3c0000, InstructionTypeEnum.DS); }
        static Token JmpMaker(SimpleToken st) { return new InstructionToken(st, 0x5c3c0000, InstructionTypeEnum.S); }
        static Token JmpretMaker(SimpleToken st) { return new InstructionToken(st, 0x5cbc0000, InstructionTypeEnum.DS); }
        static Token MaxMaker(SimpleToken st) { return new InstructionToken(st, 0x4cbc0000, InstructionTypeEnum.DS); }
        static Token MaxsMaker(SimpleToken st) { return new InstructionToken(st, 0x44bc0000, InstructionTypeEnum.DS); }
        static Token MinMaker(SimpleToken st) { return new InstructionToken(st, 0x48bc0000, InstructionTypeEnum.DS); }
        static Token MinsMaker(SimpleToken st) { return new InstructionToken(st, 0x40bc0000, InstructionTypeEnum.DS); }
        static Token MovMaker(SimpleToken st) { return new InstructionToken(st, 0xa0bc0000, InstructionTypeEnum.DS); }
        static Token MovdMaker(SimpleToken st) { return new InstructionToken(st, 0x54bc0000, InstructionTypeEnum.DS); }
        static Token MoviMaker(SimpleToken st) { return new InstructionToken(st, 0x58bc0000, InstructionTypeEnum.DS); }
        static Token MovsMaker(SimpleToken st) { return new InstructionToken(st, 0x50bc0000, InstructionTypeEnum.DS); }
        static Token MuxcMaker(SimpleToken st) { return new InstructionToken(st, 0x70bc0000, InstructionTypeEnum.DS); }
        static Token MuxncMaker(SimpleToken st) { return new InstructionToken(st, 0x74bc0000, InstructionTypeEnum.DS); }
        static Token MuxnzMaker(SimpleToken st) { return new InstructionToken(st, 0x7cbc0000, InstructionTypeEnum.DS); }
        static Token MuxzMaker(SimpleToken st) { return new InstructionToken(st, 0x78bc0000, InstructionTypeEnum.DS); }
        static Token NegMaker(SimpleToken st) { return new InstructionToken(st, 0xa4bc0000, InstructionTypeEnum.DS); }
        static Token NegcMaker(SimpleToken st) { return new InstructionToken(st, 0xb0bc0000, InstructionTypeEnum.DS); }
        static Token NegncMaker(SimpleToken st) { return new InstructionToken(st, 0xb4bc0000, InstructionTypeEnum.DS); }
        static Token NegnzMaker(SimpleToken st) { return new InstructionToken(st, 0xbcbc0000, InstructionTypeEnum.DS); }
        static Token NegzMaker(SimpleToken st) { return new InstructionToken(st, 0xb8bc0000, InstructionTypeEnum.DS); }
        static Token NopMaker(SimpleToken st) { return new InstructionToken(st, 0x00000000, InstructionTypeEnum.None); }
        static Token RdbyteMaker(SimpleToken st) { return new InstructionToken(st, 0x00bc0000, InstructionTypeEnum.DS); }
        static Token RdlongMaker(SimpleToken st) { return new InstructionToken(st, 0x08bc0000, InstructionTypeEnum.DS); }
        static Token RdwordMaker(SimpleToken st) { return new InstructionToken(st, 0x04bc0000, InstructionTypeEnum.DS); }
        static Token RclMaker(SimpleToken st) { return new InstructionToken(st, 0x34bc0000, InstructionTypeEnum.DS); }
        static Token RcrMaker(SimpleToken st) { return new InstructionToken(st, 0x30bc0000, InstructionTypeEnum.DS); }
        static Token RetMaker(SimpleToken st) { return new InstructionToken(st, 0x5c7c0000, InstructionTypeEnum.None); }
        static Token RevMaker(SimpleToken st) { return new InstructionToken(st, 0x3cbc0000, InstructionTypeEnum.DS); }
        static Token RolMaker(SimpleToken st) { return new InstructionToken(st, 0x24bc0000, InstructionTypeEnum.DS); }
        static Token RorMaker(SimpleToken st) { return new InstructionToken(st, 0x20bc0000, InstructionTypeEnum.DS); }
        static Token SarMaker(SimpleToken st) { return new InstructionToken(st, 0x38bc0000, InstructionTypeEnum.DS); }
        static Token ShlMaker(SimpleToken st) { return new InstructionToken(st, 0x2cbc0000, InstructionTypeEnum.DS); }
        static Token ShrMaker(SimpleToken st) { return new InstructionToken(st, 0x28bc0000, InstructionTypeEnum.DS); }
        static Token SubMaker(SimpleToken st) { return new InstructionToken(st, 0x84bc0000, InstructionTypeEnum.DS); }
        static Token SubabsMaker(SimpleToken st) { return new InstructionToken(st, 0x8cbc0000, InstructionTypeEnum.DS); }
        static Token SubsMaker(SimpleToken st) { return new InstructionToken(st, 0xd4bc0000, InstructionTypeEnum.DS); }
        static Token SubsxMaker(SimpleToken st) { return new InstructionToken(st, 0xdcbc0000, InstructionTypeEnum.DS); }
        static Token SubxMaker(SimpleToken st) { return new InstructionToken(st, 0xccbc0000, InstructionTypeEnum.DS); }
        static Token SumcMaker(SimpleToken st) { return new InstructionToken(st, 0x90bc0000, InstructionTypeEnum.DS); }
        static Token SumncMaker(SimpleToken st) { return new InstructionToken(st, 0x94bc0000, InstructionTypeEnum.DS); }
        static Token SumnzMaker(SimpleToken st) { return new InstructionToken(st, 0x9cbc0000, InstructionTypeEnum.DS); }
        static Token SumzMaker(SimpleToken st) { return new InstructionToken(st, 0x98bc0000, InstructionTypeEnum.DS); }
        static Token TestMaker(SimpleToken st) { return new InstructionToken(st, 0x603c0000, InstructionTypeEnum.DS); }
        static Token TestnMaker(SimpleToken st) { return new InstructionToken(st, 0x643c0000, InstructionTypeEnum.DS); }
        static Token TjnzMaker(SimpleToken st) { return new InstructionToken(st, 0xe83c0000, InstructionTypeEnum.DS); }
        static Token TjzMaker(SimpleToken st) { return new InstructionToken(st, 0xec3c0000, InstructionTypeEnum.DS); }
        static Token WrbyteMaker(SimpleToken st) { return new InstructionToken(st, 0x003c0000, InstructionTypeEnum.DS); }
        static Token WrlongMaker(SimpleToken st) { return new InstructionToken(st, 0x083c0000, InstructionTypeEnum.DS); }
        static Token WrwordMaker(SimpleToken st) { return new InstructionToken(st, 0x043c0000, InstructionTypeEnum.DS); }
        static Token XorMaker(SimpleToken st) { return new InstructionToken(st, 0x6cbc0000, InstructionTypeEnum.DS); }

        class TokenLookupTable
        {
            Hashtable ht = new Hashtable();
            public TokenLookupTable()
            {

                //	0	--, ++, ~, ~~, ?, @, @@
                ht.Add("++", new TokenMakerDelegate(PlusPlusMaker));
                ht.Add("--", new TokenMakerDelegate(MinusMinusMaker));
                ht.Add("~", new TokenMakerDelegate(TildeMaker));
                ht.Add("~~", new TokenMakerDelegate(TildeTildeMaker));
                ht.Add("?", new TokenMakerDelegate(QuestionMaker));
                ht.Add("@", new TokenMakerDelegate(AtMaker));
                ht.Add("@@", new TokenMakerDelegate(AtAtMaker));
                ht.Add("@@@", new TokenMakerDelegate(AtAtAtMaker));
                //	1	Unary +, -, ^^, ||, |<, >|, !	(+ and - are taken care of in PlusMaker/MinusMaker)
                ht.Add("^^", new TokenMakerDelegate(SqrtMaker));
                ht.Add("||", new TokenMakerDelegate(BarBarMaker));
                ht.Add("|<", new TokenMakerDelegate(DecodeMaker));
                ht.Add(">|", new TokenMakerDelegate(EncodeMaker));
                ht.Add("!", new TokenMakerDelegate(BitNotMaker));
                //	2	->, <-, >>, <<, ~>, ><
                ht.Add("->", new TokenMakerDelegate(DashGreaterMaker));
                ht.Add("<-", new TokenMakerDelegate(LessDashMaker));
                ht.Add(">>", new TokenMakerDelegate(GreaterGreaterMaker));
                ht.Add("<<", new TokenMakerDelegate(LessLessMaker));
                ht.Add("~>", new TokenMakerDelegate(TildeGreaterMaker));
                ht.Add("><", new TokenMakerDelegate(GreaterLessMaker));
                //	3	&
                ht.Add("&", new TokenMakerDelegate(BitAndMaker));
                //	4	|, ^
                ht.Add("|", new TokenMakerDelegate(BitOrMaker));
                ht.Add("^", new TokenMakerDelegate(BitXorMaker));
                //	5	*, **, /, //
                ht.Add("*", new TokenMakerDelegate(MulMaker));
                ht.Add("**", new TokenMakerDelegate(MulHighMaker));
                ht.Add("/", new TokenMakerDelegate(DivMaker));
                ht.Add("//", new TokenMakerDelegate(ModMaker));
                //	6	+, -
                ht.Add("+", new TokenMakerDelegate(PlusMaker));
                ht.Add("-", new TokenMakerDelegate(MinusMaker));
                //	7	#>, <#
                ht.Add("#>", new TokenMakerDelegate(PoundGreaterMaker));
                ht.Add("<#", new TokenMakerDelegate(LessPoundMaker));
                //	8	<, >, <>, ==, =<, =>
                ht.Add("<", new TokenMakerDelegate(LTMaker));
                ht.Add(">", new TokenMakerDelegate(GTMaker));
                ht.Add("<>", new TokenMakerDelegate(NEMaker));
                ht.Add("==", new TokenMakerDelegate(EQMaker));
                ht.Add("=<", new TokenMakerDelegate(LEMaker));
                ht.Add("=>", new TokenMakerDelegate(GEMaker));
                //	9	NOT
                ht.Add("NOT", new TokenMakerDelegate(NotMaker));
                //	10	AND
                ht.Add("AND", new TokenMakerDelegate(AndMaker));
                //	11	OR
                ht.Add("OR", new TokenMakerDelegate(OrMaker));
                //	12	=, : =, all other assignments
                ht.Add(":=", new TokenMakerDelegate(AssignMaker));


                ht.Add("(", new TokenMakerDelegate(LParenMaker));

                ht.Add("BYTE", new TokenMakerDelegate(SizeMaker));
                ht.Add("WORD", new TokenMakerDelegate(SizeMaker));
                ht.Add("LONG", new TokenMakerDelegate(SizeMaker));

                ht.Add("CON", new TokenMakerDelegate(ConMaker));
                ht.Add("DAT", new TokenMakerDelegate(DatMaker));
                ht.Add("OBJ", new TokenMakerDelegate(ObjMaker));
                ht.Add("PRI", new TokenMakerDelegate(PriMaker));
                ht.Add("PUB", new TokenMakerDelegate(PubMaker));
                ht.Add("VAR", new TokenMakerDelegate(VarMaker));

                ht.Add("RETURN", new TokenMakerDelegate(ReturnMaker));
                ht.Add("RESULT", new TokenMakerDelegate(ResultMaker));
                ht.Add("CONSTANT", new TokenMakerDelegate(ConstantMaker));
                ht.Add("IF", new TokenMakerDelegate(IfMaker));
                ht.Add("IFNOT", new TokenMakerDelegate(IfMaker));
                ht.Add("REPEAT", new TokenMakerDelegate(RepeatMaker));
                ht.Add("NEXT", new TokenMakerDelegate(NextQuitMaker));
                ht.Add("QUIT", new TokenMakerDelegate(NextQuitMaker));
                ht.Add("LOOKDOWN", new TokenMakerDelegate(LookMaker));
                ht.Add("LOOKDOWNZ", new TokenMakerDelegate(LookMaker));
                ht.Add("LOOKUP", new TokenMakerDelegate(LookMaker));
                ht.Add("LOOKUPZ", new TokenMakerDelegate(LookMaker));
                ht.Add("CASE", new TokenMakerDelegate(CaseMaker));
                ht.Add("ABORT", new TokenMakerDelegate(AbortMaker));
                ht.Add("REBOOT", new TokenMakerDelegate(RebootMaker));
                ht.Add("BYTEFILL", new TokenMakerDelegate(FillMoveMaker));
                ht.Add("BYTEMOVE", new TokenMakerDelegate(FillMoveMaker));
                ht.Add("WORDFILL", new TokenMakerDelegate(FillMoveMaker));
                ht.Add("WORDMOVE", new TokenMakerDelegate(FillMoveMaker));
                ht.Add("LONGFILL", new TokenMakerDelegate(FillMoveMaker));
                ht.Add("LONGMOVE", new TokenMakerDelegate(FillMoveMaker));
                ht.Add("WAITCNT", new TokenMakerDelegate(WaitMaker));
                ht.Add("WAITPEQ", new TokenMakerDelegate(WaitMaker));
                ht.Add("WAITPNE", new TokenMakerDelegate(WaitMaker));
                ht.Add("WAITVID", new TokenMakerDelegate(WaitMaker));
                ht.Add("CLKSET", new TokenMakerDelegate(ClksetMaker));
                ht.Add("COGSTOP", new TokenMakerDelegate(CogstopMaker));
                ht.Add("PAR", new TokenMakerDelegate(ParMaker));
                ht.Add("CNT", new TokenMakerDelegate(CntMaker));
                ht.Add("INA", new TokenMakerDelegate(InaMaker));
                ht.Add("INB", new TokenMakerDelegate(InbMaker));
                ht.Add("OUTA", new TokenMakerDelegate(OutaMaker));
                ht.Add("OUTB", new TokenMakerDelegate(OutbMaker));
                ht.Add("DIRA", new TokenMakerDelegate(DiraMaker));
                ht.Add("DIRB", new TokenMakerDelegate(DirbMaker));
                ht.Add("CTRA", new TokenMakerDelegate(CtraMaker));
                ht.Add("CTRB", new TokenMakerDelegate(CtrbMaker));
                ht.Add("FRQA", new TokenMakerDelegate(FrqaMaker));
                ht.Add("FRQB", new TokenMakerDelegate(FrqbMaker));
                ht.Add("PHSA", new TokenMakerDelegate(PhsaMaker));
                ht.Add("PHSB", new TokenMakerDelegate(PhsbMaker));
                ht.Add("VCFG", new TokenMakerDelegate(VcfgMaker));
                ht.Add("VSCL", new TokenMakerDelegate(VsclMaker));
                ht.Add("SPR", new TokenMakerDelegate(SprMaker));
                ht.Add("CHIPVER", new TokenMakerDelegate(ReadOnlyVariableMaker));
                ht.Add("CLKFREQ", new TokenMakerDelegate(ReadOnlyVariableMaker));
                ht.Add("CLKMODE", new TokenMakerDelegate(ReadOnlyVariableMaker));
                ht.Add("COGID", new TokenMakerDelegate(CogidMaker));
                ht.Add("FLOAT", new TokenMakerDelegate(ConverterMaker));
                ht.Add("ROUND", new TokenMakerDelegate(ConverterMaker));
                ht.Add("TRUNC", new TokenMakerDelegate(ConverterMaker));
                ht.Add("STRING", new TokenMakerDelegate(StringMaker));
                ht.Add("COGINIT", new TokenMakerDelegate(CoginitMaker));
                ht.Add("COGNEW", new TokenMakerDelegate(CognewMaker));
                ht.Add("STRCOMP", new TokenMakerDelegate(StrcompMaker));
                ht.Add("STRSIZE", new TokenMakerDelegate(StrsizeMaker));
                ht.Add("LOCKCLR", new TokenMakerDelegate(LockMaker));
                ht.Add("LOCKNEW", new TokenMakerDelegate(LockMaker));
                ht.Add("LOCKRET", new TokenMakerDelegate(LockMaker));
                ht.Add("LOCKSET", new TokenMakerDelegate(LockMaker));

                ht.Add("IF_ALWAYS", new TokenMakerDelegate(CondFMaker)); // 1111
                ht.Add("IF_NEVER", new TokenMakerDelegate(Cond0Maker)); // 0000
                ht.Add("IF_E", new TokenMakerDelegate(CondAMaker)); // 1010
                ht.Add("IF_NE", new TokenMakerDelegate(Cond5Maker)); // 0101
                ht.Add("IF_A", new TokenMakerDelegate(Cond1Maker)); // 0001
                ht.Add("IF_B", new TokenMakerDelegate(CondCMaker)); // 1100
                ht.Add("IF_AE", new TokenMakerDelegate(Cond3Maker)); // 0011
                ht.Add("IF_BE", new TokenMakerDelegate(CondEMaker)); // 1110
                ht.Add("IF_C", new TokenMakerDelegate(CondCMaker)); // 1100
                ht.Add("IF_NC", new TokenMakerDelegate(Cond3Maker)); // 0011
                ht.Add("IF_Z", new TokenMakerDelegate(CondAMaker)); // 1010
                ht.Add("IF_NZ", new TokenMakerDelegate(Cond5Maker)); // 0101
                ht.Add("IF_C_EQ_Z", new TokenMakerDelegate(Cond9Maker)); // 1001
                ht.Add("IF_C_NE_Z", new TokenMakerDelegate(Cond6Maker)); // 0110
                ht.Add("IF_C_AND_Z", new TokenMakerDelegate(Cond8Maker)); // 1000
                ht.Add("IF_C_AND_NZ", new TokenMakerDelegate(Cond4Maker)); // 0100
                ht.Add("IF_NC_AND_Z", new TokenMakerDelegate(Cond2Maker)); // 0010
                ht.Add("IF_NC_AND_NZ", new TokenMakerDelegate(Cond1Maker)); // 0001
                ht.Add("IF_C_OR_Z", new TokenMakerDelegate(CondEMaker)); // 1110
                ht.Add("IF_C_OR_NZ", new TokenMakerDelegate(CondDMaker)); // 1101
                ht.Add("IF_NC_OR_Z", new TokenMakerDelegate(CondBMaker)); // 1011
                ht.Add("IF_NC_OR_NZ", new TokenMakerDelegate(Cond7Maker)); // 0111
                ht.Add("IF_Z_EQ_C", new TokenMakerDelegate(Cond9Maker)); // 1001
                ht.Add("IF_Z_NE_C", new TokenMakerDelegate(Cond6Maker)); // 0110
                ht.Add("IF_Z_AND_C", new TokenMakerDelegate(Cond8Maker)); // 1000
                ht.Add("IF_Z_AND_NC", new TokenMakerDelegate(Cond2Maker)); // 0010
                ht.Add("IF_NZ_AND_C", new TokenMakerDelegate(Cond4Maker)); // 0100
                ht.Add("IF_NZ_AND_NC", new TokenMakerDelegate(Cond1Maker)); // 0001
                ht.Add("IF_Z_OR_C", new TokenMakerDelegate(CondEMaker)); // 1110
                ht.Add("IF_Z_OR_NC", new TokenMakerDelegate(CondBMaker)); // 1011
                ht.Add("IF_NZ_OR_C", new TokenMakerDelegate(CondDMaker)); // 1101
                ht.Add("IF_NZ_OR_NC", new TokenMakerDelegate(Cond7Maker)); // 0111

                ht.Add("NR", new TokenMakerDelegate(NrMaker));
                ht.Add("WC", new TokenMakerDelegate(WcMaker));
                ht.Add("WR", new TokenMakerDelegate(WrMaker));
                ht.Add("WZ", new TokenMakerDelegate(WzMaker));

                ht.Add("ABS", new TokenMakerDelegate(AbsMaker));
                ht.Add("ABSNEG", new TokenMakerDelegate(AbsnegMaker));
                ht.Add("ADD", new TokenMakerDelegate(AddMaker));
                ht.Add("ADDABS", new TokenMakerDelegate(AddabsMaker));
                ht.Add("ADDS", new TokenMakerDelegate(AddsMaker));
                ht.Add("ADDSX", new TokenMakerDelegate(AddsxMaker));
                ht.Add("ADDX", new TokenMakerDelegate(AddxMaker));
                ht.Add("ANDN", new TokenMakerDelegate(AndnMaker));
                ht.Add("CALL", new TokenMakerDelegate(CallMaker));
                ht.Add("CMP", new TokenMakerDelegate(CmpMaker));
                ht.Add("CMPS", new TokenMakerDelegate(CmpsMaker));
                ht.Add("CMPSUB", new TokenMakerDelegate(CmpsubMaker));
                ht.Add("CMPSX", new TokenMakerDelegate(CmpsxMaker));
                ht.Add("CMPX", new TokenMakerDelegate(CmpxMaker));
                ht.Add("DJNZ", new TokenMakerDelegate(DjnzMaker));
                ht.Add("HUBOP", new TokenMakerDelegate(HubopMaker));
                ht.Add("JMP", new TokenMakerDelegate(JmpMaker));
                ht.Add("JMPRET", new TokenMakerDelegate(JmpretMaker));
                ht.Add("MAX", new TokenMakerDelegate(MaxMaker));
                ht.Add("MAXS", new TokenMakerDelegate(MaxsMaker));
                ht.Add("MIN", new TokenMakerDelegate(MinMaker));
                ht.Add("MINS", new TokenMakerDelegate(MinsMaker));
                ht.Add("MOV", new TokenMakerDelegate(MovMaker));
                ht.Add("MOVD", new TokenMakerDelegate(MovdMaker));
                ht.Add("MOVI", new TokenMakerDelegate(MoviMaker));
                ht.Add("MOVS", new TokenMakerDelegate(MovsMaker));
                ht.Add("MUXC", new TokenMakerDelegate(MuxcMaker));
                ht.Add("MUXNC", new TokenMakerDelegate(MuxncMaker));
                ht.Add("MUXNZ", new TokenMakerDelegate(MuxnzMaker));
                ht.Add("MUXZ", new TokenMakerDelegate(MuxzMaker));
                ht.Add("NEG", new TokenMakerDelegate(NegMaker));
                ht.Add("NEGC", new TokenMakerDelegate(NegcMaker));
                ht.Add("NEGNC", new TokenMakerDelegate(NegncMaker));
                ht.Add("NEGNZ", new TokenMakerDelegate(NegnzMaker));
                ht.Add("NEGZ", new TokenMakerDelegate(NegzMaker));
                ht.Add("NOP", new TokenMakerDelegate(NopMaker));
                ht.Add("RDBYTE", new TokenMakerDelegate(RdbyteMaker));
                ht.Add("RDLONG", new TokenMakerDelegate(RdlongMaker));
                ht.Add("RDWORD", new TokenMakerDelegate(RdwordMaker));
                ht.Add("RCL", new TokenMakerDelegate(RclMaker));
                ht.Add("RCR", new TokenMakerDelegate(RcrMaker));
                ht.Add("RET", new TokenMakerDelegate(RetMaker));
                ht.Add("REV", new TokenMakerDelegate(RevMaker));
                ht.Add("ROL", new TokenMakerDelegate(RolMaker));
                ht.Add("ROR", new TokenMakerDelegate(RorMaker));
                ht.Add("SAR", new TokenMakerDelegate(SarMaker));
                ht.Add("SHL", new TokenMakerDelegate(ShlMaker));
                ht.Add("SHR", new TokenMakerDelegate(ShrMaker));
                ht.Add("SUB", new TokenMakerDelegate(SubMaker));
                ht.Add("SUBABS", new TokenMakerDelegate(SubabsMaker));
                ht.Add("SUBS", new TokenMakerDelegate(SubsMaker));
                ht.Add("SUBSX", new TokenMakerDelegate(SubsxMaker));
                ht.Add("SUBX", new TokenMakerDelegate(SubxMaker));
                ht.Add("SUMC", new TokenMakerDelegate(SumcMaker));
                ht.Add("SUMNC", new TokenMakerDelegate(SumncMaker));
                ht.Add("SUMNZ", new TokenMakerDelegate(SumnzMaker));
                ht.Add("SUMZ", new TokenMakerDelegate(SumzMaker));
                ht.Add("TEST", new TokenMakerDelegate(TestMaker));
                ht.Add("TESTN", new TokenMakerDelegate(TestnMaker));
                ht.Add("TJNZ", new TokenMakerDelegate(TjnzMaker));
                ht.Add("TJZ", new TokenMakerDelegate(TjzMaker));
                ht.Add("WRBYTE", new TokenMakerDelegate(WrbyteMaker));
                ht.Add("WRLONG", new TokenMakerDelegate(WrlongMaker));
                ht.Add("WRWORD", new TokenMakerDelegate(WrwordMaker));
                ht.Add("XOR", new TokenMakerDelegate(XorMaker));

                ht.Add("\\", new TokenMakerDelegate(BackslashMaker));
                ht.Add("$", new TokenMakerDelegate(DollarMaker));
                ht.Add(".", new TokenMakerDelegate(TokenMaker));
                ht.Add("[", new TokenMakerDelegate(TokenMaker));
                ht.Add("]", new TokenMakerDelegate(TokenMaker));
                ht.Add(")", new TokenMakerDelegate(TokenMaker));
                ht.Add(",", new TokenMakerDelegate(TokenMaker));
                ht.Add("#", new TokenMakerDelegate(TokenMaker));
                ht.Add(":", new TokenMakerDelegate(TokenMaker));
                ht.Add("=", new TokenMakerDelegate(TokenMaker));
                ht.Add("..", new TokenMakerDelegate(TokenMaker));
                ht.Add("ELSE", new TokenMakerDelegate(TokenMaker));
                ht.Add("ELSEIF", new TokenMakerDelegate(TokenMaker));
                ht.Add("ELSEIFNOT", new TokenMakerDelegate(TokenMaker));
                ht.Add("WHILE", new TokenMakerDelegate(TokenMaker));
                ht.Add("UNTIL", new TokenMakerDelegate(TokenMaker));
                ht.Add("FROM", new TokenMakerDelegate(TokenMaker));
                ht.Add("TO", new TokenMakerDelegate(TokenMaker));
                ht.Add("STEP", new TokenMakerDelegate(TokenMaker));
                ht.Add("OTHER", new TokenMakerDelegate(TokenMaker));
                ht.Add("ORG", new TokenMakerDelegate(TokenMaker));
                ht.Add("ORGX", new TokenMakerDelegate(TokenMaker));
                ht.Add("RES", new TokenMakerDelegate(TokenMaker));
                ht.Add("FIT", new TokenMakerDelegate(TokenMaker));
                ht.Add("FILE", new TokenMakerDelegate(TokenMaker));
            }
            public TokenMakerDelegate Lookup(string s)
            {
                return (TokenMakerDelegate)ht[s];
            }
        }

        static TokenLookupTable tlt = new TokenLookupTable();

        ObjectFileSymbolTable symbolTable;
        public ObjectFileSymbolTable SymbolTable { get { return symbolTable; } }

        ArrayList lineList = new ArrayList();

        public string Line(int lineNumber)
        {
            if (lineNumber >= lineList.Count)
                return "(lineNumber out of range)";
            return lineList[lineNumber] as string;
        }

        SimpleToken filenameToken;

        public Tokenizer(SimpleToken filenameToken, Hashtable inheritedDefines)
        {
            this.filenameToken = filenameToken;
            filename = filenameToken.Text;
            path = Options.TryPaths(filenameToken);

            symbolTable = new ObjectFileSymbolTable(this);
            foreach (DictionaryEntry de in inheritedDefines)
            {
                defines.Add(de.Key, de.Value);
            }
        }

        public void Go()
        {
            GlobalSymbolInfo gsi = SymbolTable.AddToGlobalSymbolTable(filenameToken);
            try
            {
                if (gsi.AlreadyRead)
                {
                    gsi.SymbolTable.Skipped();
                    return;
                }
                gsi.Path = path;
                if ((Options.informationLevel & 1) != 0)
                    Console.WriteLine("parsing {0}", path + filename);
                sr = new StreamReader(path + filename);
                fileMapInfoList.Add(new FileMapInfo(path + filename, 0, 0));

                // Proptool starts in CON mode, so here we fake a few tokens
                // so it looks like the file starts "CON\n".
                UngetSimpleToken(new SimpleToken(this, SimpleTokenType.Eol, "(eol)", 0, 0));
                UngetSimpleToken(new SimpleToken(this, SimpleTokenType.Id, "CON", 0, 0));

                while (Current.Text != "(eof)")
                {
                    Token.ParseBlock(this);
                }

                if (SymbolTable.PubCount() == 0)
                    throw new ParseException("No PUB routines found in " + filenameToken.Text, filenameToken);
                gsi.AlreadyRead = true;
            }
            catch (ParseException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new ParseException(e.Message, filenameToken);
            }
        }

        Token[] tokenBuffer = new Token[5];
        int nTokens = 0;

        public Token Current
        {
            get
            {
                Token t = GetToken();
                UngetToken(t);
                return t;
            }
        }

        public Token GetToken()
        {
            if (nTokens != 0)
                return tokenBuffer[--nTokens];
            return Tokenify(GetSimpleToken());
        }

        public void UngetToken(Token t)
        {
            tokenBuffer[nTokens++] = t;
        }

        public void Advance()
        {
            GetToken();
        }

        public void Advance(string expected)
        {
            Token t = GetToken();
            if (t.Text.ToUpper() != expected.ToUpper())
                throw new ParseException("Expected \"" + expected + "\", got " + t.Text, t);
        }

        bool localLabelParsingEnable = false;
        public void SetLocalLabelParsingEnable(bool b)
        {
            localLabelParsingEnable = b;
        }

        private Token Tokenify(SimpleToken st)
        {
            if (localLabelParsingEnable)
            {
                if (st.Text == ":")
                {
                    SimpleToken label = GetSimpleToken();
                    if (label.Type != SimpleTokenType.Id)
                        throw new ParseException("Expected local label", st);
                    st.Text += label.Text;
                    return new IdToken(st);
                }
            }
            switch (st.Type)
            {
                case SimpleTokenType.Eol:
                    return new Token(st);
                case SimpleTokenType.Eof:
                    return new Token(st);
                case SimpleTokenType.Id:
                    TokenMakerDelegate tmd = tlt.Lookup(st.Text.ToUpper());
                    if (tmd != null)
                        return tmd(st);
                    return new IdToken(st);
                case SimpleTokenType.IntLiteral:
                    return new IntToken(st);
                case SimpleTokenType.FloatLiteral:
                    return new FloatToken(st);
                case SimpleTokenType.Op:
                    return tlt.Lookup(st.Text)(st);
                default:
                    throw new Exception("Couldn't Tokenify " + st.Text);
            }
        }

        SimpleToken[] simpleTokenBuffer = new SimpleToken[5];
        int nSimpleTokens = 0;

        private SimpleToken GetSimpleToken()
        {
            if (nSimpleTokens != 0)
                return simpleTokenBuffer[--nSimpleTokens];
            return Tokenize();
        }

        public void UngetSimpleToken(SimpleToken t)
        {
            simpleTokenBuffer[nSimpleTokens++] = t;
        }

        string s;
        string filename;
        ArrayList filenames = new ArrayList();
        public string Filename { get { return filename; } }
        string path;
        int cp = 0;
        int lineNumber = -1;	// first line is line 0
        int lineNumberOffset = 0;
        public int LineNumberOffset { get { return lineNumberOffset; } }
        // lineNumber is the 0-based line# in the virtual fully-expanded (all #included files included) file;
        // lineNumber + lineNumberOffset is the line# within the current file.
        ArrayList saveLineNumbers = new ArrayList();
        // saveLineNumbers holds local line# (i.e. lineNumber + lineNumberOffset).
        bool blankLine = true;
        bool eol = true;
        bool eof = false;
        StreamReader sr = null;
        ArrayList srs = new ArrayList();
        string stringToParse = null;
        bool comma;
        int stringToParseIndex;
        int stringToParseStart;
        int ppState = 0;	// "preprocessor" state
        int[] ppStates = new int[5];
        int ppDepth = 0;

        // Now that a file can #INCLUDE other files, we need a way to map a line #
        // to a particular line in a particular file. Hence FileMapInfo, FilenameAt, and LineNumberAt.
        class FileMapInfo
        {
            public FileMapInfo(string filename, int startLine, int offset)
            {
                this.filename = filename;
                this.startLine = startLine;
                this.offset = offset;
            }
            public string filename;
            public int startLine;
            public int offset;
        }
        ArrayList fileMapInfoList = new ArrayList();

        public string FilenameAt(int ln)
        {
            string filename = "";
            for (int i = 0; i < fileMapInfoList.Count; ++i)
            {
                FileMapInfo fmi = fileMapInfoList[i] as FileMapInfo;
                if (fmi.startLine > ln)
                    break;
                filename = fmi.filename;
            }
            return filename;
        }

        public int LineNumberAt(int ln)
        {
            int lineNumberOffset = 0;
            for (int i = 0; i < fileMapInfoList.Count; ++i)
            {
                FileMapInfo fmi = fileMapInfoList[i] as FileMapInfo;
                if (fmi.startLine > ln)
                    break;
                lineNumberOffset = fmi.offset;
            }
            return ln + lineNumberOffset;
        }

        // Hashtable of #defined symbols: symbol -> replacement text (if any).
        Hashtable defines = new Hashtable();

        public Hashtable Defines { get { return defines; } }

        /*              ----------looking-----------    ----------ignoring--------
                                0               1               2               3
                IFDEF           push, 1/2       push, 1/2       push, 3         push, 3
                ELSEIFDEF       error           3               1/2             3
                ELSE            error           2               1               3
                ENDIF           error           pop             pop             pop
                eof             return          return          ignore          ignore
                other           return          error           error           error
         */

        private string ReadLine()
        {
            while (true)
            {
                string s;
                while (true)
                {
                    s = sr.ReadLine();
                    if (s != null)
                        break;
                    if (srs.Count > 0)
                    {
                        sr = srs[srs.Count - 1] as StreamReader;
                        srs.Remove(sr);
                        filename = filenames[filenames.Count - 1] as string;
                        filenames.Remove(filename);
                        int l = (int)saveLineNumbers[saveLineNumbers.Count - 1];
                        saveLineNumbers.RemoveAt(saveLineNumbers.Count - 1);
                        lineNumberOffset = l - lineNumber;
                        fileMapInfoList.Add(new FileMapInfo(filename, lineNumber + 1, lineNumberOffset));
                    }
                    else
                        break;
                }
                s = Detab(s);
                lineList.Add(s);
                ++lineNumber;

                if (s == null)
                {
                    if (ppState == 0)
                        return s;
                    else
                        throw new ParseException("Unterminated #IFDEF", this, lineNumber, 0);
                }
                else if (s.Length == 0 || s[0] != '#')
                {
                    if (ppState < 2)
                        return s;
                    else
                        continue; // we're in an ignore state.
                }
                else if (s.ToUpper().StartsWith("#IFDEF"))
                {
                    if (ppDepth >= 5)
                        throw new ParseException("Maximum #IFDEF nesting depth exceeded", this, lineNumber, 0);
                    ppStates[ppDepth++] = ppState;
                    if (ppState < 2)
                    {
                        s = ThePartAfter("#IFDEF", s, true);
                        ppState = defines[s] != null ? 1 : 2;
                    }
                    else
                    {
                        ppState = 3;
                    }
                }
                else if (s.ToUpper().StartsWith("#IFNDEF"))
                {
                    if (ppDepth >= 5)
                        throw new ParseException("Maximum #IFDEF nesting depth exceeded", this, lineNumber, 0);
                    ppStates[ppDepth++] = ppState;
                    if (ppState < 2)
                    {
                        s = ThePartAfter("#IFNDEF", s, true);
                        ppState = defines[s] == null ? 1 : 2;
                    }
                    else
                    {
                        ppState = 3;
                    }
                }
                else if (s.ToUpper().StartsWith("#ELSEIFDEF"))
                {
                    if (ppState == 0)
                        throw new ParseException("Unexpected #ELSEIFDEF", this, lineNumber, 0);
                    if (ppState == 1)
                        ppState = 3;
                    else if (ppState == 2)
                    {
                        s = ThePartAfter("#ELSEIFDEF", s, true);
                        ppState = defines[s] != null ? 1 : 2;
                    }
                }
                else if (s.ToUpper().StartsWith("#ELSEIFNDEF"))
                {
                    if (ppState == 0)
                        throw new ParseException("Unexpected #ELSEIFDEF", this, lineNumber, 0);
                    if (ppState == 1)
                        ppState = 3;
                    else if (ppState == 2)
                    {
                        s = ThePartAfter("#ELSEIFNDEF", s, true);
                        ppState = defines[s] == null ? 1 : 2;
                    }
                }
                else if (s.ToUpper().StartsWith("#ELSE"))
                {
                    s = ThePartAfter("#ELSE", s, false);
                    if (ppState == 0)
                        throw new ParseException("Unexpected #ELSE", this, lineNumber, 0);
                    if (ppState == 1)
                        ppState = 2;
                    else if (ppState == 2)
                        ppState = 1;
                }
                else if (s.ToUpper().StartsWith("#ENDIF"))
                {
                    s = ThePartAfter("#ENDIF", s, false);
                    if (ppState == 0)
                        throw new ParseException("Unexpected #ENDIF", this, lineNumber, 0);
                    ppState = ppStates[--ppDepth];
                }
                else if (s.ToUpper().StartsWith("#DEFINE"))
                {
                    if (ppState >= 2)
                        continue; // we're in an ignore state.
                    string sub = ThePartAfter("#DEFINE", s, true) + " ";
                    int cp = 0;
                    while (sub[cp] != ' ')
                        ++cp;
                    string d = sub.Substring(0, cp);
                    sub = sub.Substring(cp, sub.Length - cp).Trim();
                    if (defines[d] == null)
                    {
                        defines.Add(d, sub);
                    }
                    else
                    {
                        int start = 7;
                        while (s[++start] == ' ')
                            ;
                        throw new ParseException("Symbol already #defined", this, lineNumber, start);
                    }
                }
                else if (s.ToUpper().StartsWith("#PRINT"))
                {
                    if (ppState >= 2)
                        continue; // we're in an ignore state.
                    s = ThePartAfter("#PRINT", s, true);
                    Console.WriteLine("{0} ({1}) #PRINT: \"{2}\"", filename, LineNumberAt(lineNumber) + 1, s);
                }
                else if (s.ToUpper().StartsWith("#INCLUDE"))
                {
                    if (ppState >= 2)
                        continue; // we're in an ignore state.
                    s = ThePartAfter("#INCLUDE", s, true);
                    if (s[0] != '"' || s[s.Length - 1] != '"')
                        throw new ParseException("#INCLUDE must be followed by \"-enclosed filename", this, lineNumber, 9);
                    s = s.Substring(1, s.Length - 2).Trim();
                    filenames.Add(filename);
                    filename = s;
                    srs.Add(sr);

                    SimpleToken filenameToken = new SimpleToken(this, SimpleTokenType.Id, filename, lineNumber, 9);
                    string path = Options.TryPaths(filenameToken);
                    sr = new StreamReader(path + s);
                    saveLineNumbers.Add(lineNumber + lineNumberOffset);
                    lineNumberOffset = -lineNumber - 1;
                    fileMapInfoList.Add(new FileMapInfo(filename, lineNumber + 1, lineNumberOffset));
                }
                else if (s.ToUpper().StartsWith("#END"))	/// for Praxis
                {
                    continue;
                }
                else if (s.ToUpper().StartsWith("#REGION")) /// for Praxis
                {
                    continue;
                }
                else
                {
                    if (ppState < 2)
                        return s;
                    else
                        continue; // we're in an ignore state.
                }
            }
        }

        private string ThePartAfter(string head, string s, bool shouldExist)
        {
            s += " ";
            int l = head.Length;
            if (s[l] != ' ')
                throw new ParseException("Unknown # directive", this, lineNumber, 0);
            string tail = s.Substring(l, s.Length - l).Trim();
            if (shouldExist && tail == "")
                throw new ParseException("Expected something more", this, lineNumber, l + 1);
            if (!shouldExist && tail != "")
                throw new ParseException("Did not expect this", this, lineNumber, l + 1);
            return tail;
        }

        private SimpleToken Tokenize()
        {
            // stringToParse is a string literal that we're going to chop up into individual characters
            // separated by commas.
            if (stringToParse != null && stringToParseIndex < stringToParse.Length)
            {
                if (comma)
                {
                    comma = false;
                    return new SimpleToken(this, SimpleTokenType.Op, ",", lineNumber, stringToParseStart + stringToParseIndex);
                }
                else
                {
                    comma = true;
                    string ss = stringToParse.Substring(stringToParseIndex++, 1);
                    return new SimpleToken(this, SimpleTokenType.IntLiteral, "\'" + ss + "\'", lineNumber, stringToParseStart + stringToParseIndex, (int)ss[0]);
                }
            }
            int start = 0;
            StringBuilder sb = new StringBuilder();
            long v;
            if (eof)
                return new SimpleToken(this, SimpleTokenType.Eof, "(eof)", lineNumber, cp);
            while (true)
            {
                if (eol)
                {
                    s = ReadLine();
                    cp = 0;
                    if (s == null)
                    {
                        eof = true;
                        return new SimpleToken(this, SimpleTokenType.Eof, "(eof)", lineNumber, cp);
                    }
                    s += " ";
                    eol = false;
                    blankLine = true;
                }
            XXX:
                while (cp < s.Length && char.IsWhiteSpace(s[cp]))	// Find 1st non-whitespace
                    ++cp;
                if (cp >= s.Length || s[cp] == '\'')	// End of line or start of single-line comment?
                {
                    eol = true;
                    if (!blankLine)
                    {
                        return new SimpleToken(this, SimpleTokenType.Eol, "(eol)", lineNumber, cp);
                    }
                    continue;
                }
                if (s[cp] == '{')
                {
                    int lineNumber0 = lineNumber;
                    start = cp;
                    if (s[cp + 1] == '{')	// {{ comment. No nesting.
                    {
                        while (true)
                        {
                            cp = s.IndexOf("}}", cp);
                            if (cp >= 0)
                            {
                                cp += 2;			// Increment past }}.
                                goto XXX;		// Yes, a goto, for shame!
                            }
                            s = ReadLine();
                            cp = 0;
                            if (s == null)
                                throw new ParseException("Unterminated comment", this, lineNumber0, start);
                            s += " ";
                        }
                    }
                    else	// { comment. Difficulty: nesting.
                    {
                        int nestingLevel = 1;
                        ++cp;		// Increment past first {.
                        while (true)
                        {
                            for (; cp < s.Length; ++cp)
                            {
                                if (s[cp] == '{')
                                {
                                    ++nestingLevel;
                                }
                                else if (s[cp] == '}')
                                {
                                    if (--nestingLevel == 0)
                                    {
                                        ++cp;				// Increment past }.
                                        goto XXX;		// Yes, yes, hang head in shame, etc.
                                    }
                                }
                            }
                            s = ReadLine();
                            cp = 0;
                            if (s == null)
                                throw new ParseException("Unterminated comment", this, lineNumber0, start);
                            s += " ";
                        }
                    }
                }
                break;	// All right, s[cp] is a good non-blank character, so let's blow this loop
            }
            blankLine = false;
            start = cp;
            if (char.IsLetter(s[cp]) || s[cp] == '_')
            {
                do
                {
                    sb.Append(s[cp++]);
                } while ((char.IsLetterOrDigit(s[cp]) || s[cp] == '_'));
                string text = sb.ToString();
                string replacement = (string)defines[text.ToUpper()];
                if (replacement != null && replacement != "")
                {
                    s = s.Substring(0, start) + replacement + s.Substring(cp, s.Length - cp);
                    lineList[lineList.Count - 1] = s;
                    cp = start;
                    return Tokenize();
                }
                return new SimpleToken(this, SimpleTokenType.Id, text, lineNumber, start);
            }
            else if (s[cp] == '$' || (s[cp] == '0' && (s[cp + 1] == 'x' || s[cp + 1] == 'X')))	// Hex literal
            {
                char ch0 = s[cp];
                sb.Append(s[cp++]);
                if (ch0 == '$')
                {
                    if (!IsHexDigit(s[cp]))
                        return new SimpleToken(this, SimpleTokenType.Op, sb.ToString(), lineNumber, start);
                }
                else
                {
                    sb.Append(s[cp++]);	// 'x'
                }
                v = 0;
                while (IsHexDigit(s[cp]) || s[cp] == '_')
                {
                    if (s[cp] != '_')
                        v = 16 * v + HexValue(s[cp]);
                    sb.Append(s[cp++]);
                }
                if (v > 4294967295L)
                    throw new ParseException("Constant exceeds 32 bits", this, lineNumber, start);
                return new SimpleToken(this, SimpleTokenType.IntLiteral, sb.ToString(), lineNumber, start, (int)v);
            }
            else if (s[cp] == '%')	// Binary or quaternary literal
            {
                int radix = 2;
                char maxdigit = '1';
                string bq = "binary";

                sb.Append(s[cp++]);
                if (s[cp] == '%')	// Quaternary literal
                {
                    radix = 4;
                    maxdigit = '3';
                    bq = "quaternary";
                    sb.Append(s[cp++]);
                }
                if (!('0' <= s[cp] && s[cp] <= maxdigit))
                    throw new ParseException("Bad " + bq + " literal", this, lineNumber, cp);
                v = 0;
                while (('0' <= s[cp] && s[cp] <= maxdigit) || s[cp] == '_')
                {
                    if (s[cp] != '_')
                        v = radix * v + HexValue(s[cp]); // Not really a hex value, but HexValue works here
                    sb.Append(s[cp++]);
                }
                if (v > 4294967295L)
                    throw new ParseException("Constant exceeds 32 bits", this, lineNumber, start);
                return new SimpleToken(this, SimpleTokenType.IntLiteral, sb.ToString(), lineNumber, start, (int)v);
            }
            else if (char.IsDigit(s[cp]))
            {
                int fp = 0;	// 0 - int
                // 1 - have seen .
                // 2 - have seen e or E
                // 3 - have seen + or -
                while (char.IsDigit(s[cp])
                    || s[cp] == '_'
                    || (fp < 1) && (s[cp] == '.' && s[cp + 1] != '.')
                    || (fp < 2) && (s[cp] == 'e' || s[cp] == 'E')
                    || (fp == 1 || fp == 2) && (s[cp] == '+' || s[cp] == '-'))
                {
                    if (s[cp] == '.')
                        fp = 1;
                    if (s[cp] == 'e' || s[cp] == 'E')
                        fp = 2;
                    if (s[cp] == '+' || s[cp] == '-')
                        fp = 3;

                    if (s[cp] != '_')
                        sb.Append(s[cp]);
                    ++cp;
                }

                if (fp == 0)
                {
                    v = long.Parse(sb.ToString());
                    if (v > 4294967295L)
                        throw new ParseException("Constant exceeds 32 bits", this, lineNumber, start);
                    return new SimpleToken(this, SimpleTokenType.IntLiteral, sb.ToString(), lineNumber, start, (int)v);
                }
                else
                {
                    try
                    {
                        return new SimpleToken(this, SimpleTokenType.FloatLiteral, sb.ToString(), lineNumber, start, 0, float.Parse(sb.ToString()));
                    }
                    catch (Exception e)
                    {
                        throw new ParseException(e.Message, this, lineNumber, start);
                    }
                }
            }
            else if (s[cp] == '"')
            {
                start = cp++;
                while (cp < s.Length)
                {
                    if (s[cp] == '"')
                    {
                        ++cp;

                        string stringLiteral = sb.ToString();
                        if (stringLiteral.Length == 0)
                        {
                            throw new ParseException("Empty string", new SimpleToken(this, SimpleTokenType.Id, stringLiteral, lineNumber, start));
                        }
                        if (stringLiteral.Length > 1)
                        {
                            stringToParse = stringLiteral;
                            comma = true;
                            stringToParseIndex = 1;
                            stringToParseStart = start + 1;
                        }
                        return new SimpleToken(this, SimpleTokenType.IntLiteral, "\'" + stringLiteral.Substring(0, 1) + "\'", lineNumber, start, (int)stringLiteral[0]);
                    }
                    sb.Append(s[cp++]);
                }
                throw new ParseException("Unterminated string literal", this, lineNumber, start);
            }
            // If we arrive here, s[cp] is a nonalphanumeric character; let's see if it's an operator
            int foundlength = 0;
            start = cp;
            while (!Char.IsLetterOrDigit(s[cp]) && !Char.IsWhiteSpace(s[cp]))
            {
                if (tlt.Lookup(s.Substring(start, cp - start + 1)) != null)
                {
                    foundlength = cp - start + 1;
                }
                ++cp;
            }
            if (foundlength > 0)
            {
                cp = start + foundlength;
                return new SimpleToken(this, SimpleTokenType.Op, s.Substring(start, foundlength), lineNumber, start);
            }
            throw new ParseException("Syntax error", this, lineNumber, start);
        }
        private bool IsHexDigit(char ch)
        {
            ch = char.ToLower(ch);
            return char.IsDigit(ch) || 'a' <= ch && ch <= 'f';
        }
        private int HexValue(char ch)
        {
            ch = char.ToLower(ch);
            if (ch <= '9')
                return (int)ch - (int)'0';
            else
                return (int)ch - (int)'a' + 10;
        }
        public static string Detab(string s)
        {
            return Detab(s, 8);
        }
        public static string Detab(string s, int t)
        {
            if (s == null) return null;
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (ch == '\t')
                {
                    do
                        sb.Append(' ');
                    while (sb.Length % t != 0);
                }
                else if (ch == (char)26)	// ^Z
                {
                    sb.Append(" ");
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }
        public string FormatError(ParseException e)
        {
            return string.Format("Error: {0} ({1}, {2}): {3}\r\n{4}\r\n{5}^",
                e.Filename, e.Tokenizer.LineNumberAt(e.LineNumber) + 1, e.Column + 1, e.Message, e.Tokenizer.Line(e.LineNumber), "".PadLeft(e.Column));
        }
        public void PrintWarning(string warning, SimpleToken token)
        {
            if (!Options.warnings)
                return;
            Console.WriteLine("Warning: {0} ({1}, {2}): {3}\r\n{4}\r\n{5}^",
                token.Tokenizer.FilenameAt(token.LineNumber), token.Tokenizer.LineNumberAt(lineNumber) + 1, token.Column + 1, warning, token.Tokenizer.Line(token.LineNumber), "".PadLeft(token.Column));
        }
    }

    class ParseException : Exception
    {
        Tokenizer tokenizer;
        int lineNumber;
        int column;
        public Tokenizer Tokenizer { get { return tokenizer; } }
        public string Filename { get { return tokenizer.FilenameAt(lineNumber); } }
        public int LineNumber { get { return lineNumber; } }
        public int Column { get { return column; } }

        public ParseException(string message, Tokenizer tokenizer, int lineNumber, int column)
            : base(message)
        {
            this.tokenizer = tokenizer;
            this.lineNumber = lineNumber;
            this.column = column;
        }
        public ParseException(string message, SimpleToken tokenInfo)
            : this(message, tokenInfo.Tokenizer, tokenInfo.LineNumber, tokenInfo.Column)
        {
        }
        public ParseException(string message)
            : this(message, null, 0, 0)
        {
        }
    }
}
