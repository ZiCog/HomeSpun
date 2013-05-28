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



	leaveOnStack:	false			true

BinaryExpr		error			<operand 1>
						<operand 2>
						MUL

UnaryExpr		USING <opd> NEG		<operand>
						NEG


AssignBinaryExpr	<operand 1>		<operand 1>
			USING <opd2> MUL	USING <opd2> MUL, PUSH

CallExpr		01 Frame no return	00 Frame with return value
			CALL offset		CALL offset

variable		error			PUSH <variable>

constant		error			PUSH# <constant>

call			


 */

using System;
using System.Collections;
using System.Runtime.InteropServices;

namespace Homespun
{
    abstract class Expr : IVisitable
    {
        public abstract void Accept(IVisitor v);
        SimpleToken token;
        public SimpleToken Token { get { return token; } }
        public Expr(SimpleToken token)
        {
            this.token = token;
        }

        public abstract void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList);
        public virtual void MakeByteCode(StackOp op, ArrayList bytecodeList)
        {
            throw new ParseException("MakeByteCode(StackOp) undefined", token);
        }

        public ObjectFileSymbolTable SymbolTable { get { return token.Tokenizer.SymbolTable; } }	// shortcut

        static public FloInt EvaluateConstant(Expr e, bool insideDat)
        {
            EvaluateVisitor v = new EvaluateVisitor(insideDat);
            e.Accept(v);
            return v.Result;
        }
        static public FloInt EvaluateConstant(Expr e)
        {
            return EvaluateConstant(e, false);
        }
        static public int EvaluateIntConstant(Expr e, bool insideDat)
        {
            FloInt result = EvaluateConstant(e, insideDat);
            if (!result.IsInt)
                throw new ParseException("Floating-point expression not allowed", e.Token);
            return result.IntValue;
        }
        static public int EvaluateIntConstant(Expr e)
        {
            return EvaluateIntConstant(e, false);
        }
        static public float EvaluateFloatConstant(Expr e, bool insideDat)
        {
            FloInt result = EvaluateConstant(e, insideDat);
            if (result.IsInt)
                throw new ParseException("Integer expression not allowed", e.Token);
            return result.FloatValue;
        }
        static public float EvaluateFloatConstant(Expr e)
        {
            return EvaluateFloatConstant(e, false);
        }

        static public void MakePushInt(int intValue, ArrayList bytecodeList)
        {
            int b = -1;
            if (intValue == -1)
            {
                bytecodeList.Add((byte)0x34);	// PUSH#-1
            }
            else if (intValue == 0)
            {
                bytecodeList.Add((byte)0x35);	// PUSH#0
            }
            else if (intValue == 1)
            {
                bytecodeList.Add((byte)0x36);	// PUSH#-1
            }
            else if (Kp(intValue, ref b))
            {
                bytecodeList.Add((byte)0x37);	// PUSH#kp
                bytecodeList.Add((byte)b);
            }
            else if ((intValue & 0xffffff00) == 0)
            {
                bytecodeList.Add((byte)0x38);	// PUSH#k1
                bytecodeList.Add((byte)intValue);
            }
            else if ((intValue | 0x000000ff) == -1)
            {
                bytecodeList.Add((byte)0x38);	// PUSH#k1
                bytecodeList.Add((byte)~intValue);
                bytecodeList.Add((byte)0xe7);	// BIT_NOT
            }
            else if ((intValue & 0xffff0000) == 0)
            {
                bytecodeList.Add((byte)0x39);	// PUSH#k2
                bytecodeList.Add((byte)(intValue >> 8));
                bytecodeList.Add((byte)intValue);
            }
            else if ((intValue | 0x0000ffff) == -1)
            {
                bytecodeList.Add((byte)0x39);	// PUSH#k2
                bytecodeList.Add((byte)(~intValue >> 8));
                bytecodeList.Add((byte)~intValue);
                bytecodeList.Add((byte)0xe7);	// BIT_NOT
            }
            else if ((intValue & 0xff000000) == 0)
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
        public static bool Kp(int v, ref int b)
        {
            // b = %000bbbbb => 2^(b+1)
            // b = %001bbbbb => 2^(b+1)-1
            // b = %010bbbbb => bitnot(2^(b+1))
            // b = %011bbbbb => -(2^(b+1))

            int m = 2;
            for (b = 0; b < 31; ++b)
            {
                if (v == m)
                {
                    return true;
                }
                if (v == m - 1)
                {
                    b |= 0x20;
                    return true;
                }
                if (v == ~m)
                {
                    b |= 0x40;
                    return true;
                }
                if (v == -m)
                {
                    b |= 0x60;
                    return true;
                }
                m <<= 1;
            }
            return false;
        }
        protected void MakeStackOp(StackOp op, int size, MemorySpace space, int offset, Expr indexExpr, ArrayList bytecodeList)
        {
            // Not for MemorySpace.MEM ops.

            if (indexExpr != null)
            {
                indexExpr.MakeByteCode(true, bytecodeList);
            }

            int opcode;
            if (offset < 32
                && size == 4
                && (space == MemorySpace.VAR || space == MemorySpace.LOCAL)
                && indexExpr == null)
            {	// Use the short form if possible
                if ((offset & 3) != 0)
                    throw new ParseException("Non-long offset", Token);

                opcode = space == MemorySpace.VAR ? 0x40 : 0x60;	// VAR / LOCAL
                opcode |= (byte)op;

                bytecodeList.Add((byte)(opcode + offset));
            }
            else	// we have to use the long form
            {
                opcode = 0x80 | ((size >> 1) << 5) | ((int)space << 2) | (int)op;
                if (indexExpr != null)
                {
                    opcode |= 0x10;
                }

                if (offset < 128)
                {
                    bytecodeList.Add((byte)opcode);
                    bytecodeList.Add((byte)offset);
                }
                else
                {
                    bytecodeList.Add((byte)opcode);
                    bytecodeList.Add((byte)(offset >> 8 | 0x80));
                    bytecodeList.Add((byte)offset);
                }
            }
        }
        static protected void MakeStackOp(StackOp op, int size, Expr indexExpr1, Expr indexExpr2, ArrayList bytecodeList)
        {
            // Only for MemorySpace.MEM ops.
            // indexExpr1 should be non-null; indexExpr1 might be null.

            indexExpr1.MakeByteCode(true, bytecodeList);

            if (indexExpr2 != null)
            {
                indexExpr2.MakeByteCode(true, bytecodeList);
            }

            int opcode;
            opcode = 0x80 | ((size >> 1) << 5) | ((int)MemorySpace.MEM << 2) | (int)op;
            if (indexExpr2 != null)
            {
                opcode |= 0x10;
            }
            bytecodeList.Add((byte)opcode);
        }
        public enum StackOp { PUSH = 0, POP = 1, USING = 2, PEA = 3 }
        public enum MemorySpace { MEM = 0, OBJ = 1, VAR = 2, LOCAL = 3 }

        public virtual int SpecifiedSize
        {
            get
            {
                throw new ParseException("Bogus call to Expr.SpecifiedSize");
            }
        }
    }

    class IdExpr : Expr
    {
        // An IdExpr represents a plain ID: blah (could be variable, constant, or call)
        // Or an OBJ.

        public string Name { get { return Token.Text; } }

        public IdExpr(SimpleToken token)
            : base(token)
        {
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        int size = 666;
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            SymbolInfo si = SymbolTable.LookupExisting(Token);
            if (si is ConSymbolInfo)
            {
                if (!leaveOnStack)
                    throw new ParseException("Internal error: IdExpr.MakeByteCode(false) ConSymbolInfo", this.Token);
                ConSymbolInfo csi = si as ConSymbolInfo;
                MakePushInt(csi.Value.AsIntBits(), bytecodeList);
                return;
            }
            StackOp op = StackOp.PUSH;
            if (si is LocalSymbolInfo)
            {
                if (!leaveOnStack)
                    throw new ParseException("Internal error: IdExpr.MakeByteCode(false) LocalSymbolInfo", this.Token);
                LocalSymbolInfo lsi = si as LocalSymbolInfo;
                MakeStackOp(op, 4, MemorySpace.LOCAL, lsi.Offset, null, bytecodeList);
                size = 4;
            }
            else if (si is VarSymbolInfo)
            {
                if (!leaveOnStack)
                    throw new ParseException("Internal error: IdExpr.MakeByteCode(false) VarSymbolInfo", this.Token);
                VarSymbolInfo vsi = si as VarSymbolInfo;
                MakeStackOp(op, vsi.Size, MemorySpace.VAR, vsi.Offset, null, bytecodeList);
                size = vsi.Size;
            }
            else if (si is DatSymbolInfo)
            {
                if (!leaveOnStack)
                    throw new ParseException("Internal error: IdExpr.MakeByteCode(false) DatSymbolInfo", this.Token);
                DatSymbolInfo dsi = si as DatSymbolInfo;
                MakeStackOp(op, dsi.Alignment, MemorySpace.OBJ, dsi.Dp, null, bytecodeList);
                size = dsi.Alignment;
            }
            else if (si is MethodSymbolInfo)
            {
                CallExpr call = new CallExpr(null, this.Token, null, new ArrayList());
                call.MakeByteCode(leaveOnStack, bytecodeList);
            }
            else if (si is ObjSymbolInfo)
            {
                ObjSymbolInfo osi = si as ObjSymbolInfo;
                if (!leaveOnStack)
                    throw new ParseException("IdExpr.MakeByteCode(false) ObjSymbolInfo", Token);
                // Get the entry out of the current object table, then add current object's
                // address to the LSW and the current object's VAR address (lshifted 16) to
                // the MSW. That'll make both addresses absolute.
                MakeStackOp(StackOp.PUSH, 4, MemorySpace.OBJ, osi.Index << 2, null, bytecodeList);
                MakePushInt(this.SymbolTable.HubAddress, bytecodeList);
                bytecodeList.Add((byte)0xec);		// ADD
                bytecodeList.Add((byte)0x43);		// PUSH# VAR+0
                bytecodeList.Add((byte)0x37);		// PUSH#kp
                bytecodeList.Add((byte)0x03);		//         16
                bytecodeList.Add((byte)0xe3);		// SHL
                bytecodeList.Add((byte)0xec);		// ADD
            }
            else
                throw new ParseException("Unexpected " + si.ToString(), this.Token);	// should never happen.
        }
        public override int SpecifiedSize
        {
            get
            {
                if (size == 666)
                    throw new ParseException("666", this.Token);
                return size;
            }
        }
        public override void MakeByteCode(StackOp op, ArrayList bytecodeList)
        {
            SymbolInfo si = SymbolTable.LookupExisting(Token);
            if (si is ConSymbolInfo)
            {
                if (op != StackOp.PUSH)
                    throw new ParseException("Internal error: IdExpr.MakeByteCode(non-PUSH)", this.Token);
                ConSymbolInfo csi = si as ConSymbolInfo;
                MakePushInt(csi.Value.AsIntBits(), bytecodeList);
                return;
            }
            if (si is LocalSymbolInfo)
            {
                LocalSymbolInfo lsi = si as LocalSymbolInfo;
                MakeStackOp(op, 4, MemorySpace.LOCAL, lsi.Offset, null, bytecodeList);
                size = 4;
            }
            else if (si is VarSymbolInfo)
            {
                VarSymbolInfo vsi = si as VarSymbolInfo;
                MakeStackOp(op, vsi.Size, MemorySpace.VAR, vsi.Offset, null, bytecodeList);
                size = vsi.Size;
            }
            else if (si is DatSymbolInfo)
            {
                DatSymbolInfo dsi = si as DatSymbolInfo;
                MakeStackOp(op, dsi.Alignment, MemorySpace.OBJ, dsi.Dp, null, bytecodeList);
                size = dsi.Alignment;
            }
            else if (si is ObjSymbolInfo)
            {
                ObjSymbolInfo osi = si as ObjSymbolInfo;
                if (op != StackOp.POP)
                    throw new ParseException("IdExpr.MakeByteCode(non-PUSH) ObjSymbolInfo", Token);
                // The TOS is two words: absolute address of an OBJ and absolute address of its VAR space.
                // We're going to subtract the current OBJ's address from the LSW
                // and the current OBJ's VAR (lshifted 16) from the MSW, then store it in the current
                // object table.
                MakePushInt(this.SymbolTable.HubAddress, bytecodeList);
                bytecodeList.Add((byte)0xed);		// SUB
                bytecodeList.Add((byte)0x43);		// PUSH# VAR+0
                bytecodeList.Add((byte)0x37);		// PUSH#kp
                bytecodeList.Add((byte)0x03);		//         16
                bytecodeList.Add((byte)0xe3);		// SHL
                bytecodeList.Add((byte)0xed);		// SUB
                MakeStackOp(StackOp.POP, 4, MemorySpace.OBJ, osi.Index << 2, null, bytecodeList);
            }
            else
                throw new ParseException("Unexpected " + si.ToString(), this.Token);	// should never happen.
        }
    }

    class VariableExpr : Expr
    {
        // A VariableExpr represents an ID with size: blah.byte (definitely a variable)
        //		or an ID with index(es): blah[i], blah[i][[j] (definitely a variable)
        //		or an ID with size and index: blah.byte[i] (definitely a variable)

        // A VariableExpr is definitely a variable, but not every variable is a VariableExpr
        // (an IdExpr could be a variable; the parser just can't be sure).

        public string Name { get { return Token.Text; } }

        int specifiedSize = 0;
        public override int SpecifiedSize { get { return specifiedSize; } }

        Expr[] indexExprs = null;
        public Expr[] IndexExprs { get { return indexExprs; } }

        public void IndexExprList(ArrayList indexExprList)
        {
            indexExprs = new Expr[indexExprList.Count];
            for (int i = 0; i < indexExprs.Length; ++i)
                indexExprs[i] = (Expr)indexExprList[i];
        }

        public VariableExpr(SimpleToken token, int specifiedSize)
            : base(token)
        {
            this.specifiedSize = specifiedSize;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("VariableExpr.MakeByteCode(false)", Token);

            SymbolInfo si = SymbolTable.LookupExisting(Token);
            StackOp op = StackOp.PUSH;
            if (si is LocalSymbolInfo)
            {
                LocalSymbolInfo lsi = si as LocalSymbolInfo;
                if (specifiedSize == 0)
                    specifiedSize = 4;
                MakeStackOp(op, specifiedSize, MemorySpace.LOCAL, lsi.Offset, SubscriptExprs(indexExprs, new Expr[0]), bytecodeList);
            }
            else if (si is VarSymbolInfo)
            {
                VarSymbolInfo vsi = si as VarSymbolInfo;
                if (specifiedSize == 0)
                    specifiedSize = vsi.Size;
                if (specifiedSize > vsi.Size)
                    throw new ParseException("Size override must be smaller", Token);
                MakeStackOp(op, specifiedSize, MemorySpace.VAR, vsi.Offset, SubscriptExprs(indexExprs, vsi.DimExprs), bytecodeList);
            }
            else if (si is DatSymbolInfo)
            {
                DatSymbolInfo dsi = si as DatSymbolInfo;
                if (specifiedSize == 0)
                    specifiedSize = dsi.Alignment;
                if (specifiedSize > dsi.Alignment)
                    throw new ParseException("Size override must be smaller", Token);
                MakeStackOp(op, specifiedSize, MemorySpace.OBJ, dsi.Dp, SubscriptExprs(indexExprs, new Expr[0]), bytecodeList);
            }
        }
        Expr SubscriptExprs(Expr[] indexExprs, Expr[] dimExprs)
        {
            if (indexExprs == null)
                return null;
            if (indexExprs.Length == 1)
                return indexExprs[0];		// special case: single index is always OK

            if (indexExprs.Length < dimExprs.Length)
            {
                throw new ParseException("Not enough subscripts", indexExprs[indexExprs.Length - 1].Token);
            }
            else if (indexExprs.Length > dimExprs.Length)
            {
                throw new ParseException("Too many subscripts", indexExprs[dimExprs.Length].Token);
            }
            else // indexExprs.Length == dimExprs.Length
            {
                /*        |                   |                  |
                        ind[0]  dim[1] * ind[1] +  dim[2] * ind[2] +   dim[3] * ind[3] +
                     */
                Expr indexExpr = indexExprs[0];
                for (int i = 1; i < indexExprs.Length; ++i)
                {
                    int dim = Expr.EvaluateIntConstant(dimExprs[i]);
                    indexExpr = new BinaryExpr(null, 0xf4, indexExpr, new IntExpr(null, dim));	// mul
                    indexExpr = new BinaryExpr(null, 0xec, indexExpr, indexExprs[i]);				// add
                }
                return indexExpr;
            }
        }
        public override void MakeByteCode(StackOp op, ArrayList bytecodeList)
        {
            SymbolInfo si = SymbolTable.LookupExisting(Token);
            if (si is LocalSymbolInfo)
            {
                LocalSymbolInfo lsi = si as LocalSymbolInfo;
                specifiedSize = specifiedSize != 0 ? specifiedSize : 4;
                MakeStackOp(op, specifiedSize, MemorySpace.LOCAL, lsi.Offset, SubscriptExprs(indexExprs, new Expr[0]), bytecodeList);
            }
            else if (si is VarSymbolInfo)
            {
                VarSymbolInfo vsi = si as VarSymbolInfo;
                specifiedSize = specifiedSize != 0 ? specifiedSize : vsi.Size;
                MakeStackOp(op, specifiedSize, MemorySpace.VAR, vsi.Offset, SubscriptExprs(indexExprs, vsi.DimExprs), bytecodeList);
            }
            else if (si is DatSymbolInfo)
            {
                DatSymbolInfo dsi = si as DatSymbolInfo;
                specifiedSize = specifiedSize != 0 ? specifiedSize : dsi.Alignment;
                MakeStackOp(op, specifiedSize, MemorySpace.OBJ, dsi.Dp, SubscriptExprs(indexExprs, new Expr[0]), bytecodeList);
            }
            else
                throw new ParseException("wtf? " + si, this.Token); ; ;
        }
    }

    class ConExpr : Expr
    {
        // A ConExpr represents a constant of the form <id> or <id> # <id>

        // A ConExpr is definitely a constant, but not every constant is a conExpr
        // (an IdExpr could be a constant; the parser just can't be sure).

        SimpleToken objectToken;
        public SimpleToken ObjectToken { get { return objectToken; } }

        public ConExpr(SimpleToken objectToken, SimpleToken token)
            : base(token)
        {
            this.objectToken = objectToken;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("ConExpr.MakeByteCode(false)", Token);
            ObjSymbolInfo osi = SymbolTable.LookupExisting(objectToken) as ObjSymbolInfo;
            if (osi == null)
                throw new ParseException("Expected an object", objectToken);
            GlobalSymbolInfo gsi = GlobalSymbolTable.LookupExisting(osi.FilenameToken);
            ConSymbolInfo csi = gsi.SymbolTable.LookupExisting(Token) as ConSymbolInfo;
            if (csi == null)
                throw new ParseException("Expected a CON symbol", Token);
            MakePushInt(csi.Value.AsIntBits(), bytecodeList);
        }
    }

    class MemoryAccessExpr : Expr
    {
        public string Text { get { return Token.Text; } }
        int size;
        public override int SpecifiedSize { get { return size; } }
        Expr indexExpr1, indexExpr2 = null;
        public Expr IndexExpr1 { get { return indexExpr1; } }
        public Expr IndexExpr2 { get { return indexExpr2; } set { indexExpr2 = value; } }
        public MemoryAccessExpr(SimpleToken token, int size, Expr indexExpr)
            : base(token)
        {
            this.size = size;
            this.indexExpr1 = indexExpr;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("VariableExpr.MakeByteCode(false)", Token);
            MakeStackOp(StackOp.PUSH, size, indexExpr1, indexExpr2, bytecodeList);
        }
        public override void MakeByteCode(StackOp op, ArrayList bytecodeList)
        {
            MakeStackOp(op, size, indexExpr1, indexExpr2, bytecodeList);
        }
    }

    class CallExpr : Expr
    {
        SimpleToken objectToken;
        public SimpleToken ObjectToken { get { return objectToken; } }

        Expr indexExpr = null;
        public Expr IndexExpr { get { return indexExpr; } }
        ArrayList argList = new ArrayList();
        public ArrayList ArgList { get { return argList; } }

        bool abortTrap = false;
        public bool AbortTrap { set { abortTrap = value; } }	// write-only

        public CallExpr(SimpleToken objectToken, SimpleToken methodToken, Expr indexExpr, ArrayList argList)
            : base(methodToken)
        {
            this.objectToken = objectToken;
            this.indexExpr = indexExpr;
            this.argList = argList;
        }

        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (abortTrap)
                bytecodeList.Add(leaveOnStack ? (byte)0x02 : (byte)0x03);	// TRAP w/return value : TRAP w/o return value
            else
                bytecodeList.Add(leaveOnStack ? (byte)0x00 : (byte)0x01);	// FRAME w/return value : FRAME w/o return value
            foreach (Expr e in argList)
            {
                e.MakeByteCode(true, bytecodeList);
            }

            if (ObjectToken == null)
            {
                MethodSymbolInfo msi = SymbolTable.LookupExisting(Token) as MethodSymbolInfo;
                if (msi == null)
                    throw new ParseException("Unknown method", Token);

                if (argList.Count != msi.ParamCount)
                    throw new ParseException(string.Format("Wrong number of arguments ({0} instead of {1})", argList.Count, msi.ParamCount), Token);

                if (indexExpr != null)
                    throw new ParseException("non-null indexExpr", Token);	// Should never happen.

                bytecodeList.Add((byte)0x05);					// CALL
                bytecodeList.Add((byte)msi.Index);
            }
            else
            {
                ObjSymbolInfo osi = SymbolTable.LookupExisting(objectToken) as ObjSymbolInfo;
                if (osi == null)
                    throw new ParseException("Unknown object", objectToken);
                GlobalSymbolInfo gsi = GlobalSymbolTable.LookupExisting(osi.FilenameToken);
                MethodSymbolInfo msi = gsi.SymbolTable.LookupExisting(Token) as MethodSymbolInfo;
                if (msi == null)
                    throw new ParseException("Unknown method", Token);

                if (!msi.IsPub)
                    throw new ParseException("Method is PRI", Token);

                if (argList.Count != msi.ParamCount)
                    throw new ParseException(string.Format("Wrong number of arguments ({0} instead of {1})", argList.Count, msi.ParamCount), Token);

                if (indexExpr == null)
                {
                    bytecodeList.Add((byte)0x06);					// CALLOBJ
                    bytecodeList.Add((byte)osi.Index);
                    bytecodeList.Add((byte)msi.Index);
                }
                else
                {
                    indexExpr.MakeByteCode(true, bytecodeList);
                    bytecodeList.Add((byte)0x07);					// CALLOBJ[]
                    bytecodeList.Add((byte)osi.Index);
                    bytecodeList.Add((byte)msi.Index);
                }
            }
        }
    }

    class IntExpr : Expr
    {
        int intValue;
        Expr e;
        bool mustEval;
        public int IntValue
        {
            get
            {
                if (mustEval)
                {
                    intValue = Expr.EvaluateIntConstant(e);
                    mustEval = false;
                }
                return intValue;
            }
        }
        public IntExpr(SimpleToken token, int intValue)
            : base(token)
        {
            this.intValue = intValue;
            mustEval = false;
        }
        public IntExpr(SimpleToken token, Expr e)
            : base(token)
        {
            this.e = e;
            mustEval = true;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("Internal error: IntExpr.MakeByteCode(false)", this.Token);
            MakePushInt(IntValue, bytecodeList);
        }
    }

    class FloatExpr : Expr
    {
        float floatValue;
        Expr e;
        bool mustEval;
        public float FloatValue
        {
            get
            {
                if (mustEval)
                {
                    floatValue = Expr.EvaluateFloatConstant(e);
                    mustEval = false;
                }
                return floatValue;
            }
        }
        public FloatExpr(SimpleToken token, float floatValue)
            : base(token)
        {
            this.floatValue = floatValue;
            mustEval = false;
        }
        public FloatExpr(SimpleToken token, Expr e)
            : base(token)
        {
            this.e = e;
            mustEval = true;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("Internal error: FloatExpr.MakeByteCode(false)", this.Token);
            MakePushInt(FloInt.FloatToIntBits(FloatValue), bytecodeList);
        }
    }

    class UnaryExpr : Expr
    {
        int opcode;
        Expr operand;
        public Expr Operand { get { return operand; } }
        public UnaryExpr(SimpleToken token, int opcode, Expr operand)
            : base(token)
        {
            this.opcode = opcode;
            this.operand = operand;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (Token.Text == "@")
            {
                if (!leaveOnStack)
                    throw new ParseException("Unexpected @", Token);
                operand.MakeByteCode(StackOp.PEA, bytecodeList);
                return;
            }

            if (Token.Text == "@@")
            {
                if (!leaveOnStack)
                    throw new ParseException("Unexpected @@", Token);
                operand.MakeByteCode(true, bytecodeList);
                bytecodeList.Add((byte)0x97);		// PUSH#.B
                bytecodeList.Add((byte)0x00);		//         OBJ+0[] -- adds object base to TOS
                return;
            }

            if (opcode == 0xe6)	// NEG - special handling for negating CON symbols.
            {
                /*          Turns out proptool doesn't handle this case...
                if( operand is ConExpr )
                {
                    ConExpr ce = operand as ConExpr;
                    Console.WriteLine( "{0}#{1}", ce.ObjectToken.Text, ce.Token.Text );;;
                    if( !leaveOnStack )
                        throw new ParseException( "UnaryExpr.MakeByteCode(false) (ConExpr)", Token );
                    ObjSymbolInfo osi = SymbolTable.LookupExisting( ce.ObjectToken ) as ObjSymbolInfo;
                    if( osi == null )
                        throw new ParseException( "Expected an object", ce.ObjectToken );
                    GlobalSymbolInfo gsi = GlobalSymbolTable.LookupExisting( osi.FilenameToken );
                    ConSymbolInfo csi = gsi.SymbolTable.LookupExisting( ce.Token ) as ConSymbolInfo;
                    if( csi == null )
                        throw new ParseException( "Expected a CON symbol", ce.Token );
                    MakePushInt( -csi.Value.AsIntBits(), bytecodeList );
                    return;
                }
                else*/
                if (operand is IdExpr)
                {
                    SymbolInfo si = SymbolTable.LookupExisting(operand.Token);
                    if (si is ConSymbolInfo)
                    {
                        if (!leaveOnStack)
                            throw new ParseException("UnaryExpr.MakeByteCode(false) ConSymbolInfo", this.Token);
                        ConSymbolInfo csi = si as ConSymbolInfo;
                        MakePushInt(-csi.Value.AsIntBits(), bytecodeList);
                        return;
                    }
                }
            }

            if (opcode < 0x40)	// ?, ~, ~~, ++, --
            {
                operand.MakeByteCode(StackOp.USING, bytecodeList);
                if (0x20 <= opcode && opcode < 0x40)
                {
                    int size = operand.SpecifiedSize;
                    switch (size)
                    {
                        case 1: opcode += 2; break;
                        case 2: opcode += 4; break;
                        case 4: opcode += 6; break;
                    }
                }
                if (leaveOnStack)
                    opcode += 0x80;
                bytecodeList.Add((byte)opcode);
                return;
            }

            // else regular opcodes...

            if (leaveOnStack)
            {
                operand.MakeByteCode(true, bytecodeList);
                bytecodeList.Add((byte)opcode);
            }
            else
            {
                operand.MakeByteCode(StackOp.USING, bytecodeList);
                bytecodeList.Add((byte)(opcode - 0xa0));
            }
        }
    }

    class BinaryExpr : Expr
    {
        int opcode;
        Expr operand1, operand2;
        public Expr Operand1 { get { return operand1; } }
        public Expr Operand2 { get { return operand2; } }
        public BinaryExpr(SimpleToken token, int opcode, Expr operand1, Expr operand2)
            : base(token)
        {
            this.opcode = opcode;
            this.operand1 = operand1;
            this.operand2 = operand2;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("Internal error: Binary.MakeByteCode(false)", this.Token);
            operand1.MakeByteCode(true, bytecodeList);
            operand2.MakeByteCode(true, bytecodeList);
            bytecodeList.Add((byte)opcode);
        }
    }

    class BinaryAssignExpr : Expr
    {
        int opcode;
        public string Op { get { return Token.Text; } }
        Expr operand1, operand2;
        public Expr Operand1 { get { return operand1; } }
        public Expr Operand2 { get { return operand2; } }
        public BinaryAssignExpr(SimpleToken token, int opcode, Expr operand1, Expr operand2)
            : base(token)
        {
            this.opcode = opcode;
            this.operand1 = operand1;
            this.operand2 = operand2;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (opcode == 0x00 && !leaveOnStack)	// special case for top-level :=
            {
                operand2.MakeByteCode(true, bytecodeList);
                operand1.MakeByteCode(StackOp.POP, bytecodeList);
                return;
            }
            operand2.MakeByteCode(true, bytecodeList);
            operand1.MakeByteCode(StackOp.USING, bytecodeList);
            bytecodeList.Add((byte)(leaveOnStack ? opcode + 0x80 : opcode));	// PUSH if leaveOnStack
        }
    }

    class LookExpr : Expr
    {
        // LOOKDOWN, LOOKDOWNZ, LOOKUP, LOOKUPZ
        Expr expr;
        ArrayList args;
        string look;
        public LookExpr(SimpleToken token, Expr expr, ArrayList args)
            : base(token)
        {
            look = Token.Text.ToUpper();
            this.expr = expr;
            this.args = args;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("LookExpr.MakeByteCode(false)", Token);

            if (look.EndsWith("Z"))
            {
                bytecodeList.Add((byte)0x35);		// PUSH#0
            }
            else
            {
                bytecodeList.Add((byte)0x36);		// PUSH#1
            }

            JmpTarget offsetTarget = new JmpTarget();
            bytecodeList.Add(new OffsetStart(offsetTarget));

            expr.MakeByteCode(true, bytecodeList);

            byte op, opr;
            if (look[4] == 'U')
            {
                op = 0x10;	// LOOKUP
                opr = 0x12;	// LOOKUPR
            }
            else
            {
                op = 0x11;	// LOOKDN
                opr = 0x13;	// LOOKDNR
            }

            foreach (object o in args)
            {
                if (o is Expr)
                {
                    (o as Expr).MakeByteCode(true, bytecodeList);
                    bytecodeList.Add(op);
                }
                else
                {
                    PairOfExpr p = (PairOfExpr)o;
                    p.left.MakeByteCode(true, bytecodeList);
                    p.right.MakeByteCode(true, bytecodeList);
                    bytecodeList.Add(opr);
                }
            }
            bytecodeList.Add((byte)0x0f);	// LOOKEND
            bytecodeList.Add(offsetTarget);
        }
    }

    class OffsetStart
    {
        // This is similar to JmpStart but Fixup will generate PUSH# address-of-target.
        public int address;
        public byte[] bytecode;
        public JmpTarget target;
        public OffsetStart(JmpTarget target)
        {
            bytecode = new byte[2];	// assume it's a PUSH#k1 (1 byte offset) to start with.
            this.target = target;
        }
    }

    class RegisterExpr : Expr
    {
        // CNT, CTRA, CTRB, etc.
        byte reg;
        Expr e;
        Expr f;
        public byte Reg { get { return reg; } }
        public RegisterExpr(SimpleToken token, byte reg, Expr e, Expr f)
            : base(token)
        {
            this.reg = reg;
            this.e = e;
            this.f = f;
        }
        public override int SpecifiedSize { get { return 0; } }

        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("RegisterExpr.MakeByteCode(false)", Token);
            MakeByteCode(StackOp.PUSH, bytecodeList);
        }
        public override void MakeByteCode(Expr.StackOp op, ArrayList bytecodeList)
        {
            if (op == StackOp.PEA)
                throw new ParseException("Can't apply @ to register", Token);
            if (e == null)
            {
                bytecodeList.Add((byte)0x3f);		// REG
            }
            else
            {
                e.MakeByteCode(true, bytecodeList);
                if (f == null)
                {
                    bytecodeList.Add((byte)0x3d);		// REG<>
                }
                else
                {
                    f.MakeByteCode(true, bytecodeList);
                    bytecodeList.Add((byte)0x3e);		// REG<..>
                }
            }
            bytecodeList.Add((byte)(0x90 + reg + ((int)op << 5)));		// PUSH|POP|USING
        }
    }

    class SprExpr : Expr
    {
        // SPR
        Expr e;
        public SprExpr(SimpleToken token, Expr e)
            : base(token)
        {
            this.e = e;
        }
        public override int SpecifiedSize { get { return 4; } }

        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("SprExpr.MakeByteCode(false)", Token);
            MakeByteCode(StackOp.PUSH, bytecodeList);
        }
        public override void MakeByteCode(Expr.StackOp op, ArrayList bytecodeList)
        {
            e.MakeByteCode(true, bytecodeList);
            switch (op)
            {
                case StackOp.PEA:
                    throw new ParseException("Can't apply @ to SPR", Token);
                case StackOp.POP:
                    bytecodeList.Add((byte)0x25); break;	// POPSPR[]
                case StackOp.PUSH:
                    bytecodeList.Add((byte)0x24); break;	// PUSHSPR[]
                case StackOp.USING:
                    bytecodeList.Add((byte)0x26); break;	// POPSPR[]
            }
        }
    }

    class ReadOnlyVariableExpr : Expr
    {
        // CHIPVER, CLKFREQ, CLKMODE, COGID
        public ReadOnlyVariableExpr(SimpleToken token)
            : base(token)
        {
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("ReadOnlyVariableExpr.MakeByteCode(false)", Token);

            switch (Token.Text.ToUpper())
            {
                case "CHIPVER":
                    bytecodeList.Add((byte)0x34);	//	PUSH#-1
                    bytecodeList.Add((byte)0x80);	//	PUSH.B	Mem[]
                    break;
                case "CLKFREQ":
                    bytecodeList.Add((byte)0x35);	//	PUSH#0
                    bytecodeList.Add((byte)0xc0);	//	PUSH.L	Mem[]
                    break;
                case "CLKMODE":
                    bytecodeList.Add((byte)0x38);	//	PUSH#k1
                    bytecodeList.Add((byte)0x04);	//			4
                    bytecodeList.Add((byte)0x80);	//	PUSH.B	Mem[]
                    break;
                case "COGID":
                    bytecodeList.Add((byte)0x3f);	//	REGPUSH
                    bytecodeList.Add((byte)0x89);	//			$89(?)
                    break;
            }
        }
    }

    class ConverterExpr : Expr
    {
        // FLOAT, ROUND, TRUNC
        Expr operand;
        public Expr Operand { get { return operand; } }

        public ConverterExpr(SimpleToken token, Expr operand)
            : base(token)
        {
            this.operand = operand;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("ReadOnlyVariableExpr.MakeByteCode(false)", Token);

            switch (Token.Text.ToUpper())
            {
                case "FLOAT":
                    MakePushInt(FloInt.FloatToIntBits((float)EvaluateIntConstant(Operand)), bytecodeList);
                    break;
                case "ROUND":
                    MakePushInt((int)(EvaluateFloatConstant(Operand) + 0.5), bytecodeList);
                    break;
                case "TRUNC":
                    MakePushInt((int)(EvaluateFloatConstant(Operand)), bytecodeList);
                    break;
            }
        }
    }

    class ConstantExpr : Expr
    {
        // CONSTANT( expr )
        Expr e;
        public ConstantExpr(SimpleToken token, Expr e)
            : base(token)
        {
            this.e = e;
        }

        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("ConstantExpr.MakeByteCode(false)", Token);
            FloInt x = Expr.EvaluateConstant(e);
            MakePushInt(x.AsIntBits(), bytecodeList);
        }
    }

    class StringExpr : Expr
    {
        // STRING( string )
        StringToken st;
        public StringExpr(SimpleToken token, StringToken st)
            : base(token)
        {
            this.st = st;
        }

        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("StringExpr.MakeByteCode(false)", Token);
            JmpTarget stringTarget = new JmpTarget();
            StringOffsetStart start = new StringOffsetStart(stringTarget);
            bytecodeList.Add(start);
            SymbolTable.StringList.Add(st.Text);
            SymbolTable.StringTargetList.Add(stringTarget);
        }
    }

    class StringOffsetStart
    {
        // This is similar to JmpStart but Fixup will generate PUSH#.B address-of-target.
        public int address;
        public byte[] bytecode;
        public JmpTarget target;
        public StringOffsetStart(JmpTarget target)
        {
            bytecode = new byte[3];	// string offsets always use 2-byte offset.
            bytecode[0] = 0x87;		// PUSH#.B
            this.target = target;
        }
    }

    class CoginewtExpr : Expr
    {
        Expr e0;
        Expr e1;
        Expr e2;
        public CoginewtExpr(SimpleToken token, Expr e0, Expr e1, Expr e2)
            : base(token)
        {
            this.e0 = e0;
            this.e1 = e1;
            this.e2 = e2;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            bool isInit = e0 != null;
            if (isInit && leaveOnStack)
                throw new ParseException("COGINIT does not return a value", Token);
            bool mark = false;
            int paramCount = 0;
            if (e1 is IdExpr)	// Gotta watch out for plain IDs that are actually method names...
            {
                SymbolInfo si = SymbolTable.LookupExisting((e1 as IdExpr).Token);
                if (si is MethodSymbolInfo)
                {
                    MethodSymbolInfo msi = si as MethodSymbolInfo;
                    paramCount = msi.ParamCount;
                    MakePushInt(msi.Index + (paramCount << 8), bytecodeList);
                    mark = true;
                }
                else
                {
                    if (isInit)
                    {
                        e0.MakeByteCode(true, bytecodeList);
                    }
                    else
                    {
                        bytecodeList.Add((byte)0x34);		// PUSH#-1
                    }
                    e1.MakeByteCode(true, bytecodeList);
                }
            }
            else if (e1 is CallExpr)
            {
                CallExpr callExpr = e1 as CallExpr;
                if (callExpr.ObjectToken == null)	// call to local method
                {
                    MethodSymbolInfo msi = SymbolTable.LookupExisting(callExpr.Token) as MethodSymbolInfo;
                    if (msi == null)
                        throw new ParseException("Expected method name", callExpr.Token);
                    foreach (Expr e in callExpr.ArgList)
                    {
                        e.MakeByteCode(true, bytecodeList);
                    }
                    paramCount = msi.ParamCount;
                    MakePushInt(msi.Index + (paramCount << 8), bytecodeList);
                    mark = true;
                }
                else	// calling method in another object
                {
                    this.Token.Tokenizer.PrintWarning("Calling a method in another object", this.Token);
                    if (isInit)
                    {
                        e0.MakeByteCode(true, bytecodeList);
                    }
                    else
                    {
                        bytecodeList.Add((byte)0x34);		// PUSH#-1
                    }
                    callExpr.MakeByteCode(true, bytecodeList);
                }
            }
            else	// e1 is a regular old expression
            {
                if (isInit)
                {
                    e0.MakeByteCode(true, bytecodeList);
                }
                else
                {
                    bytecodeList.Add((byte)0x34);		// PUSH#-1
                }
                e1.MakeByteCode(true, bytecodeList);
            }

            e2.MakeByteCode(true, bytecodeList);

            if (mark)
            {
                bytecodeList.Add((byte)0x15);	// MARK
                if (isInit)
                {
                    e0.MakeByteCode(true, bytecodeList);
                    bytecodeList.Add((byte)0x3f);	// REGPUSH
                    bytecodeList.Add((byte)0x8f);	//			$8f?
                    bytecodeList.Add((byte)0x37);	// PUSH#kp
                    bytecodeList.Add((byte)0x61);	//			-4
                    bytecodeList.Add((byte)0xd1);	// POP.L	Mem[][]
                }
            }
            if (leaveOnStack)
            {
                bytecodeList.Add((byte)0x28);		// COGIFUN
            }
            else
            {
                bytecodeList.Add((byte)0x2c);		// COGISUB
            }
        }
    }

    class StrFunctionExpr : Expr
    {
        // STRCOMP and STRSIZE
        bool isComp;
        ArrayList args;
        public StrFunctionExpr(SimpleToken token, bool isComp, ArrayList args)
            : base(token)
        {
            this.isComp = isComp;
            this.args = args;
        }

        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("StrFunctionExpr.MakeByteCode(false)", Token);
            foreach (Expr e in args)
            {
                e.MakeByteCode(true, bytecodeList);
            }
            bytecodeList.Add(isComp ? (byte)0x17 : (byte)0x16);	// STRCOMP | STRSIZE
        }
    }

    class LockExpr : Expr
    {
        // LOCKCLR/LOCKNEW/LOCKRET/LOCKSET
        ArrayList args;
        public LockExpr(SimpleToken token, ArrayList args)
            : base(token)
        {
            this.args = args;
        }

        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (leaveOnStack && Token.Text.ToUpper() == "LOCKRET")
                throw new ParseException("LOCKRET does not return a value", Token);
            if (args != null)
                foreach (Expr e in args)
                    e.MakeByteCode(true, bytecodeList);

            byte opcode = 0;
            switch (Token.Text.ToUpper())
            {
                case "LOCKCLR": opcode = leaveOnStack ? (byte)0x2b : (byte)0x2f; break;	// LCLRSUB/LCLRFUN
                case "LOCKNEW": opcode = leaveOnStack ? (byte)0x29 : (byte)0x2d; break;	// LNEWSUB/LNEWFUN
                case "LOCKRET": opcode = (byte)0x22; break;								// LRETSUB
                case "LOCKSET": opcode = leaveOnStack ? (byte)0x2a : (byte)0x2e; break;	// LSETSUB/LSETFUN
            }
            bytecodeList.Add(opcode);
        }
    }

    class DollarExpr : Expr
    {
        public DollarExpr(SimpleToken token)
            : base(token)
        {
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            throw new ParseException("$ valid only in DAT context", Token);
        }
    }

    class AtAtAtExpr : Expr
    {
        IdToken token;
        public AtAtAtExpr(IdToken token)
            : base(token)
        {
            this.token = token;
        }
        public override void Accept(IVisitor v)
        {
            v.Visit(this);
        }
        public override void MakeByteCode(bool leaveOnStack, ArrayList bytecodeList)
        {
            if (!leaveOnStack)
                throw new ParseException("AtAtAtExpr.MakeByteCode(false)", token);
            SymbolInfo si = SymbolTable.LookupExisting(token);
            if (!(si is DatSymbolInfo))
                throw new ParseException("Expected DAT symbol", token);
            MakePushInt((si as DatSymbolInfo).Dp + SymbolTable.HubAddress, bytecodeList);
        }
    }

    //======================================================================================================

    abstract class Stmt
    {
        SimpleToken token;
        public SimpleToken Token { get { return token; } }
        int endLineNumber;
        public int EndLineNumber { get { return endLineNumber; } }
        public Stmt(SimpleToken token, int endLineNumber)
        {
            this.token = token;
            this.endLineNumber = endLineNumber;
        }
        public abstract void MakeByteCode(ArrayList bytecodeList);
        public ObjectFileSymbolTable SymbolTable { get { return token.Tokenizer.SymbolTable; } }	// shortcut
    }

    class ExprStmt : Stmt
    {
        Expr e;
        public ExprStmt(SimpleToken token, int endLineNumber, Expr e)
            : base(token, endLineNumber)
        {
            this.e = e;
        }
        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));
            e.MakeByteCode(false, bytecodeList);
        }
    }

    class ReturnStmt : Stmt
    {
        Expr returnValueExpr;
        public ReturnStmt(SimpleToken token, int endLineNumber, Expr returnValueExpr)
            : base(token, endLineNumber)
        {
            this.returnValueExpr = returnValueExpr;
        }
        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));
            if (returnValueExpr == null)
            {
                bytecodeList.Add((byte)0x32);	// RETURN
            }
            else
            {
                returnValueExpr.MakeByteCode(true, bytecodeList);
                bytecodeList.Add((byte)0x33);	// RETVAL
            }
        }
    }

    class SourceReference
    {
        public SimpleToken token;
        public int endLineNumber;
        public SourceReference(SimpleToken token, int endLineNumber)
        {
            this.token = token;
            this.endLineNumber = endLineNumber;
        }
    }

    struct StringStart	// simple structs to deliminate string constants in bytecode
    {
    }
    struct StringEnd
    {
    }

    class JmpStart
    {
        public int address;
        public byte[] bytecode;
        public JmpTarget target;
        public JmpStart(byte opcode, JmpTarget target)
        {
            bytecode = new byte[2];	// assume it's a short branch (1 byte offset) to start with.
            bytecode[0] = opcode;
            this.target = target;
        }
    }

    class JmpTarget
    {
        public int address;
    }

    class IfStmt : Stmt
    {
        SimpleToken elseToken;
        int elseEndLineNumber;
        Expr condExpr;
        ArrayList ifStatementsList;
        ArrayList elseStatementsList;

        public IfStmt(SimpleToken token, int endLineNumber, SimpleToken elseToken, int elseEndLineNumber, Expr condExpr, ArrayList ifStatementsList, ArrayList elseStatementsList)
            : base(token, endLineNumber)
        {
            this.elseToken = elseToken;
            this.elseEndLineNumber = elseEndLineNumber;
            this.condExpr = condExpr;
            this.ifStatementsList = ifStatementsList;
            this.elseStatementsList = elseStatementsList;
        }
        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));
            condExpr.MakeByteCode(true, bytecodeList);

            byte jmpOpcode = Token.Text.ToUpper().EndsWith("NOT") ? (byte)0x0b : (byte)0x0a;	// JPT/JPF
            JmpTarget ifTarget = new JmpTarget();
            JmpStart ifJmp = new JmpStart(jmpOpcode, ifTarget);
            bytecodeList.Add(ifJmp);

            foreach (Stmt s in ifStatementsList)
            {
                s.MakeByteCode(bytecodeList);
            }
            if (elseStatementsList == null)
            {
                bytecodeList.Add(ifTarget);
            }
            else
            {
                if (this.elseToken != null)
                    bytecodeList.Add(new SourceReference(this.elseToken, this.elseEndLineNumber));
                JmpTarget elseTarget = new JmpTarget();
                JmpStart elseJmp = new JmpStart((byte)0x04, elseTarget);	// GOTO
                bytecodeList.Add(elseJmp);
                bytecodeList.Add(ifTarget);
                foreach (Stmt s in elseStatementsList)
                {
                    s.MakeByteCode(bytecodeList);
                }
                bytecodeList.Add(elseTarget);
            }
        }
    }

    enum RepeatType { Plain, NTimes, WhileLoop, LoopWhile, FromTo }

    class RepeatStmt : Stmt
    {
        Expr repeatExpr;
        ArrayList statementsList;
        RepeatType type;
        bool whileNotUntil;

        public RepeatStmt(SimpleToken token, int endLineNumber, RepeatType type, Expr repeatExpr, ArrayList statementsList, bool whileNotUntil)
            : base(token, endLineNumber)
        {
            this.type = type;
            this.repeatExpr = repeatExpr;
            this.statementsList = statementsList;
            this.whileNotUntil = whileNotUntil;
        }
        public override void MakeByteCode(ArrayList bytecodeList)
        {
            JmpTarget saveNextJmpTarget = SymbolTable.nextJmpTarget;
            JmpTarget saveQuitJmpTarget = SymbolTable.quitJmpTarget;
            JmpTarget nextJmpTarget = SymbolTable.nextJmpTarget = new JmpTarget();
            JmpTarget quitJmpTarget = SymbolTable.quitJmpTarget = new JmpTarget();
            int saveCaseNesting = SymbolTable.caseNesting;
            SymbolTable.caseNesting = 0;

            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            if (type == RepeatType.Plain)					// REPEAT
            {
                SymbolTable.insidePlainRepeat = true;

                bytecodeList.Add(nextJmpTarget);
                foreach (Stmt s in statementsList)
                {
                    s.MakeByteCode(bytecodeList);
                }
                JmpStart jmpUp = new JmpStart(0x04, nextJmpTarget);	// GOTO
                bytecodeList.Add(jmpUp);
                bytecodeList.Add(quitJmpTarget);
            }
            else if (type == RepeatType.NTimes)			// REPEAT n
            {
                SymbolTable.insidePlainRepeat = false;

                repeatExpr.MakeByteCode(true, bytecodeList);

                JmpStart jmpDown = new JmpStart(0x08, quitJmpTarget);	// LOOPJPF
                bytecodeList.Add(jmpDown);
                JmpTarget repeatJmpTarget = new JmpTarget();
                bytecodeList.Add(repeatJmpTarget);
                foreach (Stmt s in statementsList)
                {
                    s.MakeByteCode(bytecodeList);
                }
                bytecodeList.Add(nextJmpTarget);
                JmpStart jmpUp = new JmpStart(0x09, repeatJmpTarget);	// LOOPRPT
                bytecodeList.Add(jmpUp);
                bytecodeList.Add(quitJmpTarget);
            }
            else if (type == RepeatType.WhileLoop)			// REPEAT WHILE/UNTIL <cond> <stmts>
            {
                SymbolTable.insidePlainRepeat = true;

                bytecodeList.Add(nextJmpTarget);

                repeatExpr.MakeByteCode(true, bytecodeList);

                byte opcode = whileNotUntil ? (byte)0x0a : (byte)0x0b;	// JPF | JPT
                JmpStart jmpDown = new JmpStart(opcode, quitJmpTarget);
                bytecodeList.Add(jmpDown);

                foreach (Stmt s in statementsList)
                {
                    s.MakeByteCode(bytecodeList);
                }

                JmpStart jmpUp = new JmpStart(0x04, nextJmpTarget);	// GOTO
                bytecodeList.Add(jmpUp);
                bytecodeList.Add(quitJmpTarget);
            }
            else											// REPEAT <stmts> WHILE/UNTIL <cond>
            {
                JmpTarget topTarget = new JmpTarget();
                bytecodeList.Add(topTarget);

                foreach (Stmt s in statementsList)
                {
                    s.MakeByteCode(bytecodeList);
                }

                bytecodeList.Add(nextJmpTarget);

                repeatExpr.MakeByteCode(true, bytecodeList);

                byte opcode = whileNotUntil ? (byte)0x0b : (byte)0x0a;	// JPT | JPF
                JmpStart jmpUp = new JmpStart(opcode, topTarget);
                bytecodeList.Add(jmpUp);
                bytecodeList.Add(quitJmpTarget);
            }
            SymbolTable.caseNesting = saveCaseNesting;
            SymbolTable.nextJmpTarget = saveNextJmpTarget;
            SymbolTable.quitJmpTarget = saveQuitJmpTarget;
        }
    }
    class RepeatFromToStmt : Stmt
    {
        Expr varExpr;
        Expr fromExpr;
        Expr toExpr;
        Expr stepExpr;
        ArrayList statementsList;

        public RepeatFromToStmt(SimpleToken token, int endLineNumber, Expr varExpr, Expr fromExpr, Expr toExpr, Expr stepExpr, ArrayList statementsList)
            : base(token, endLineNumber)
        {
            this.varExpr = varExpr;
            this.fromExpr = fromExpr;
            this.toExpr = toExpr;
            this.stepExpr = stepExpr;
            this.statementsList = statementsList;
        }
        public override void MakeByteCode(ArrayList bytecodeList)
        {
            SymbolTable.insidePlainRepeat = true;

            JmpTarget saveNextJmpTarget = SymbolTable.nextJmpTarget;
            JmpTarget saveQuitJmpTarget = SymbolTable.quitJmpTarget;
            JmpTarget nextJmpTarget = SymbolTable.nextJmpTarget = new JmpTarget();
            JmpTarget quitJmpTarget = SymbolTable.quitJmpTarget = new JmpTarget();
            JmpTarget topJmpTarget = new JmpTarget();
            int saveCaseNesting = SymbolTable.caseNesting;
            SymbolTable.caseNesting = 0;

            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            fromExpr.MakeByteCode(true, bytecodeList);
            varExpr.MakeByteCode(Expr.StackOp.POP, bytecodeList);
            bytecodeList.Add(topJmpTarget);
            foreach (Stmt s in statementsList)
            {
                s.MakeByteCode(bytecodeList);
            }
            bytecodeList.Add(nextJmpTarget);
            byte opcode = 0x02;
            if (stepExpr != null)
            {
                stepExpr.MakeByteCode(true, bytecodeList);
                opcode = 0x06;
            }
            fromExpr.MakeByteCode(true, bytecodeList);
            toExpr.MakeByteCode(true, bytecodeList);
            varExpr.MakeByteCode(Expr.StackOp.USING, bytecodeList);
            bytecodeList.Add(new JmpStart(opcode, topJmpTarget));
            bytecodeList.Add(quitJmpTarget);

            SymbolTable.caseNesting = saveCaseNesting;
            SymbolTable.nextJmpTarget = saveNextJmpTarget;
            SymbolTable.quitJmpTarget = saveQuitJmpTarget;
        }
    }
    class NextQuitStmt : Stmt
    {
        public NextQuitStmt(SimpleToken token, int endLineNumber)
            : base(token, endLineNumber)
        {
        }
        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            JmpTarget target = Token.Text.ToUpper() == "NEXT" ? SymbolTable.nextJmpTarget : SymbolTable.quitJmpTarget;
            if (target == null)
                throw new ParseException("No enclosing REPEAT loop", Token);
            if (SymbolTable.caseNesting > 0)
            {
                Expr.MakePushInt(SymbolTable.caseNesting << 3, bytecodeList);
                bytecodeList.Add((byte)0x14);		// mystery op
            }
            byte opcode = 0x04;		// GOTO
            if (Token.Text.ToUpper() == "QUIT" && !SymbolTable.insidePlainRepeat)
                opcode = 0x0b;		// JPT
            bytecodeList.Add(new JmpStart(opcode, target));
        }
    }

    class CaseStmt : Stmt
    {
        Expr caseExpr;
        ArrayList matchExprListList;
        ArrayList matchStmtListList;
        ArrayList otherStmtList;
        ArrayList matchTokenList;
        ArrayList matchEndLineNumberList;

        public CaseStmt(SimpleToken token, int endLineNumber, Expr caseExpr, ArrayList matchExprListList, ArrayList matchStmtListList, ArrayList otherStmtList, ArrayList matchTokenList, ArrayList matchEndLineNumberList)
            : base(token, endLineNumber)
        {
            this.caseExpr = caseExpr;
            this.matchExprListList = matchExprListList;
            this.matchStmtListList = matchStmtListList;
            this.otherStmtList = otherStmtList;
            this.matchTokenList = matchTokenList;
            this.matchEndLineNumberList = matchEndLineNumberList;
        }

        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            ++SymbolTable.caseNesting;

            JmpTarget offsetTarget = new JmpTarget();
            bytecodeList.Add(new OffsetStart(offsetTarget));

            caseExpr.MakeByteCode(true, bytecodeList);

            JmpTarget[] targets = new JmpTarget[matchExprListList.Count];
            for (int i = 0; i < matchExprListList.Count; ++i)
            {
                targets[i] = new JmpTarget();
                // lay down all the match expressions for case i
                ArrayList matchExprList = matchExprListList[i] as ArrayList;
                foreach (object o in matchExprList)
                {
                    if (o is Expr)
                    {
                        (o as Expr).MakeByteCode(true, bytecodeList);
                        bytecodeList.Add(new JmpStart(0x0d, targets[i]));	// CASE
                    }
                    else
                    {
                        PairOfExpr p = (PairOfExpr)o;
                        p.left.MakeByteCode(true, bytecodeList);
                        p.right.MakeByteCode(true, bytecodeList);
                        bytecodeList.Add(new JmpStart(0x0e, targets[i]));	// CASER
                    }
                }
            }

            // Ensure that "OTHER:" gets listed in the case where there are no OTHER statements
            if (otherStmtList.Count == 0)
            {
                bytecodeList.Add(new SourceReference((SimpleToken)matchTokenList[matchTokenList.Count - 1],
                                                       (int)matchEndLineNumberList[matchEndLineNumberList.Count - 1]));
            }

            foreach (Stmt s in otherStmtList)
            {
                s.MakeByteCode(bytecodeList);
            }
            bytecodeList.Add((byte)0x0c);		// GOTO[]

            // Now some hackery to make sure that the "OTHER:" is
            // listed along with the first statement.
            if (otherStmtList.Count > 0)
            {
                Stmt firstStmt = (Stmt)otherStmtList[0];
                firstStmt.Token.LineNumber = ((SimpleToken)matchTokenList[matchTokenList.Count - 1]).LineNumber;
            }

            for (int i = 0; i < matchExprListList.Count; ++i)
            {
                bytecodeList.Add(targets[i]);
                ArrayList matchStmtList = matchStmtListList[i] as ArrayList;

                // Ensure that the match expression(s) gets listed in the case where there are
                // no match statements
                if (matchStmtList.Count == 0)
                {
                    bytecodeList.Add(new SourceReference((SimpleToken)matchTokenList[i],
                                                           (int)matchEndLineNumberList[i]));
                }

                foreach (Stmt s in matchStmtList)
                {
                    s.MakeByteCode(bytecodeList);
                }
                bytecodeList.Add((byte)0x0c);		// GOTO[]

                // Now some hackery to make sure that the match expression(s) is
                // listed along with the first statement.
                if (matchStmtList.Count > 0)
                {
                    Stmt firstStmt = (Stmt)matchStmtList[0];
                    firstStmt.Token.LineNumber = ((SimpleToken)matchTokenList[i]).LineNumber;
                }
            }
            bytecodeList.Add(offsetTarget);
            --SymbolTable.caseNesting;
        }
    }

    class AbortStmt : Stmt
    {
        Expr abortExpr;

        public AbortStmt(SimpleToken token, int endLineNumber, Expr abortExpr)
            : base(token, endLineNumber)
        {
            this.abortExpr = abortExpr;
        }

        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            if (abortExpr == null)
            {
                bytecodeList.Add((byte)0x30);		// ABORT
            }
            else
            {
                abortExpr.MakeByteCode(true, bytecodeList);
                bytecodeList.Add((byte)0x31);		// ABOVAL
            }
        }
    }

    class RebootStmt : Stmt
    {
        public RebootStmt(SimpleToken token, int endLineNumber)
            : base(token, endLineNumber)
        {
        }

        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            bytecodeList.Add((byte)0x37);		// PUSH#kp
            bytecodeList.Add((byte)0x06);		//         128
            bytecodeList.Add((byte)0x35);		// PUSH#0
            bytecodeList.Add((byte)0x20);		// CLKSET
        }
    }

    class FillMoveStmt : Stmt
    {
        ArrayList argList;

        public FillMoveStmt(SimpleToken token, int endLineNumber, ArrayList argList)
            : base(token, endLineNumber)
        {
            this.argList = argList;
        }

        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            foreach (Expr e in argList)
            {
                e.MakeByteCode(true, bytecodeList);
            }
            byte opcode = 0;
            switch (Token.Text.ToUpper())
            {
                case "BYTEFILL": opcode = 0x18; break;
                case "BYTEMOVE": opcode = 0x1c; break;
                case "WORDFILL": opcode = 0x19; break;
                case "WORDMOVE": opcode = 0x1d; break;
                case "LONGFILL": opcode = 0x1a; break;
                case "LONGMOVE": opcode = 0x1e; break;
            }
            bytecodeList.Add(opcode);
        }
    }

    class WaitStmt : Stmt
    {
        ArrayList argList;

        public WaitStmt(SimpleToken token, int endLineNumber, ArrayList argList)
            : base(token, endLineNumber)
        {
            this.argList = argList;
        }

        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            foreach (Expr e in argList)
            {
                e.MakeByteCode(true, bytecodeList);
            }
            byte opcode = 0;
            switch (Token.Text.ToUpper())
            {
                case "WAITCNT": opcode = 0x23; break;
                case "WAITPEQ": opcode = 0x1b; break;
                case "WAITPNE": opcode = 0x1f; break;
                case "WAITVID": opcode = 0x27; break;
            }
            bytecodeList.Add(opcode);
        }
    }

    class ClksetStmt : Stmt
    {
        ArrayList argList;

        public ClksetStmt(SimpleToken token, int endLineNumber, ArrayList argList)
            : base(token, endLineNumber)
        {
            this.argList = argList;
        }

        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            foreach (Expr e in argList)
            {
                e.MakeByteCode(true, bytecodeList);
            }
            bytecodeList.Add((byte)0x20);	// CLKSET
        }
    }

    class CogstopStmt : Stmt
    {
        ArrayList argList;

        public CogstopStmt(SimpleToken token, int endLineNumber, ArrayList argList)
            : base(token, endLineNumber)
        {
            this.argList = argList;
        }

        public override void MakeByteCode(ArrayList bytecodeList)
        {
            bytecodeList.Add(new SourceReference(this.Token, this.EndLineNumber));

            foreach (Expr e in argList)
            {
                e.MakeByteCode(true, bytecodeList);
            }
            bytecodeList.Add((byte)0x21);	// COGSTOP
        }
    }

    //================================================================================================================

    interface IVisitor
    {
        void Visit(UnaryExpr e);
        void Visit(BinaryExpr e);
        void Visit(BinaryAssignExpr e);
        void Visit(MemoryAccessExpr e);
        void Visit(IdExpr e);
        void Visit(VariableExpr e);
        void Visit(ConExpr e);
        void Visit(CallExpr e);
        void Visit(IntExpr e);
        void Visit(FloatExpr e);
        void Visit(LookExpr e);
        void Visit(RegisterExpr e);
        void Visit(SprExpr e);
        void Visit(ReadOnlyVariableExpr e);
        void Visit(ConverterExpr e);
        void Visit(ConstantExpr e);
        void Visit(StringExpr e);
        void Visit(CoginewtExpr e);
        void Visit(StrFunctionExpr e);
        void Visit(LockExpr e);
        void Visit(DollarExpr e);
        void Visit(AtAtAtExpr e);
    }

    interface IVisitable
    {
        void Accept(IVisitor v);
    }

    struct FloInt
    {
        int intValue;
        float floatValue;
        bool isInt;
        public int IntValue
        {
            get
            {
                if (!isInt)
                    throw new ParseException("FloInt: bad int access");
                return intValue;
            }
        }
        public float FloatValue
        {
            get
            {
                if (isInt)
                    throw new ParseException("FloInt: bad float access");
                return floatValue;
            }
        }
        public bool IsInt { get { return isInt; } }
        public FloInt(int i)
        {
            intValue = i;
            isInt = true;
            floatValue = 0.0f;	// to appease compiler
        }
        public FloInt(float f)
        {
            floatValue = f;
            isInt = false;
            intValue = 0;		// to appease compiler
        }
        public int AsIntBits()
        {
            if (isInt)
            {
                return intValue;
            }
            else
            {
                return FloatToIntBits(floatValue);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        struct BitMash
        {
            [FieldOffset(0)]
            public int intField;
            [FieldOffset(0)]
            public float floatField;
        }
        static public int FloatToIntBits(float f)
        {
            BitMash bitMash;
            bitMash.intField = 0;	// to appease compiler
            bitMash.floatField = f;
            return bitMash.intField;
        }
    }

    class EvaluateVisitor : IVisitor
    {
        FloInt result;
        public FloInt Result { get { return result; } }

        bool insideDat;
        public EvaluateVisitor(bool insideDat)
        {
            this.insideDat = insideDat;
        }
        public void Visit(UnaryExpr e)
        {
            if (e.Token.Text == "@")
            {
                result = new FloInt(EvaluateAt(e.Operand));
                return;
            }
            result = Expr.EvaluateConstant(e.Operand, insideDat);
            if (result.IsInt)
            {
                switch (e.Token.Text.ToUpper())
                {
                    case "^^": result = new FloInt((int)Math.Sqrt((uint)result.IntValue)); break;
                    case "||": result = new FloInt(Math.Abs(result.IntValue)); break;
                    case "|<": result = new FloInt(1 << result.IntValue); break;
                    case ">|": result = new FloInt(Encode(result.IntValue)); break;
                    case "!": result = new FloInt(~result.IntValue); break;
                    case "NOT": result = new FloInt(result.IntValue != 0 ? 0 : -1); break;
                    case "-": result = new FloInt(-result.IntValue); break;
                    default: throw new ParseException("Bad operator in constant expression: " + e.Token.Text, e.Token);
                }
            }
            else
            {
                switch (e.Token.Text.ToUpper())
                {
                    case "^^": result = new FloInt((float)Math.Sqrt(result.FloatValue)); break;
                    case "||": result = new FloInt(Math.Abs(result.FloatValue)); break;
                    case "NOT": result = new FloInt(result.FloatValue != 0.0f ? 0.0f : 1.0f); break;
                    case "-": result = new FloInt(-result.FloatValue); break;
                    default: throw new ParseException("Bad operator in constant expression: " + e.Token.Text, e.Token);
                }
            }
        }
        int EvaluateAt(Expr e)
        {
            SymbolInfo si = e.SymbolTable.LookupExisting(e.Token);
            if (!(si is DatSymbolInfo))
                throw new ParseException("Expected DAT symbol", e.Token);
            return (si as DatSymbolInfo).Dp;
        }
        int Encode(int a)
        {
            int i;
            for (i = 0; a != 0; ++i)
            {
                a = (a >> 1) & 0x7fffffff;
            }
            return i;
        }
        int Ror(int a, int n)
        {
            while (--n >= 0)
            {
                int b = a & 1;
                a >>= 1;
                if (b != 0)
                    a = (int)((uint)a | 0x80000000);
                else
                    a &= 0x7fffffff;
            }
            return a;
        }
        int Rol(int a, int n)
        {
            while (--n >= 0)
            {
                int b = (int)(a & 0x80000000);
                a <<= 1;
                if (b != 0)
                    a |= 0x00000001;
            }
            return a;
        }
        int Shr(int a, int n)
        {
            while (--n >= 0)
            {
                a >>= 1;
                a &= 0x7fffffff;
            }
            return a;
        }
        int Sar(int a, int n)
        {
            int b = (int)(a & 0x80000000);
            while (--n >= 0)
            {
                a >>= 1;
                a |= b;
            }
            return a;
        }
        int Rev(int a, int n)
        {
            int b = 0;
            while (--n >= 0)
            {
                b <<= 1;
                b |= a & 1;
                a >>= 1;
            }
            return b;
        }
        public void Visit(BinaryExpr e)
        {
            FloInt r1 = Expr.EvaluateConstant(e.Operand1, insideDat);
            FloInt r2 = Expr.EvaluateConstant(e.Operand2, insideDat);

            if (r1.IsInt)
            {
                if (!r2.IsInt)
                    throw new ParseException("Can't mix int and floating-point", e.Token);
                int i1 = r1.IntValue;
                int i2 = r2.IntValue;
                switch (e.Token.Text.ToUpper())
                {
                    case "->": result = new FloInt(Ror(i1, i2)); break;
                    case "<-": result = new FloInt(Rol(i1, i2)); break;
                    case ">>": result = new FloInt(Shr(i1, i2)); break;
                    case "<<": result = new FloInt(i1 << i2); break;
                    case "~>": result = new FloInt(Sar(i1, i2)); break;
                    case "><": result = new FloInt(Rev(i1, i2)); break;
                    case "&": result = new FloInt(i1 & i2); break;
                    case "|": result = new FloInt(i1 | i2); break;
                    case "^": result = new FloInt(i1 ^ i2); break;
                    case "*": result = new FloInt(i1 * i2); break;
                    case "**": result = new FloInt((int)(((long)i1 * (long)i2) >> 32)); break;
                    case "/": result = new FloInt(i1 / i2); break;
                    case "//": result = new FloInt(i1 % i2); break;
                    case "+": result = new FloInt(i1 + i2); break;
                    case "-": result = new FloInt(i1 - i2); break;
                    case "#>": result = new FloInt(i1 > i2 ? i1 : i2); break;
                    case "<#": result = new FloInt(i1 > i2 ? i2 : i1); break;
                    case "<": result = new FloInt(i1 < i2 ? -1 : 0); break;
                    case ">": result = new FloInt(i1 > i2 ? -1 : 0); break;
                    case "<>": result = new FloInt(i1 != i2 ? -1 : 0); break;
                    case "==": result = new FloInt(i1 == i2 ? -1 : 0); break;
                    case "=<": result = new FloInt(i1 <= i2 ? -1 : 0); break;
                    case "=>": result = new FloInt(i1 >= i2 ? -1 : 0); break;
                    case "AND": result = new FloInt((i1 != 0) && (i2 != 0) ? -1 : 0); break;
                    case "OR": result = new FloInt((i1 != 0) || (i2 != 0) ? -1 : 0); break;
                    default: throw new ParseException("Bad operator in constant expression: " + e.Token.Text, e.Token);
                }
            }
            else	// r1 is float
            {
                if (r2.IsInt)
                    throw new ParseException("Can't mix int and floating-point", e.Token);
                float f1 = r1.FloatValue;
                float f2 = r2.FloatValue;
                switch (e.Token.Text.ToUpper())
                {
                    case "*": result = new FloInt(f1 * f2); break;
                    case "/": result = new FloInt(f1 / f2); break;
                    case "+": result = new FloInt(f1 + f2); break;
                    case "-": result = new FloInt(f1 - f2); break;
                    case "#>": result = new FloInt(f1 > f2 ? f1 : f2); break;
                    case "<#": result = new FloInt(f1 > f2 ? f2 : f1); break;
                    case "<": result = new FloInt(f1 < f2 ? 1.0f : 0.0f); break;
                    case ">": result = new FloInt(f1 > f2 ? 1.0f : 0.0f); break;
                    case "<>": result = new FloInt(f1 != f2 ? 1.0f : 0.0f); break;
                    case "==": result = new FloInt(f1 == f2 ? 1.0f : 0.0f); break;
                    case "=<": result = new FloInt(f1 <= f2 ? 1.0f : 0.0f); break;
                    case "=>": result = new FloInt(f1 >= f2 ? 1.0f : 0.0f); break;
                    case "AND": result = new FloInt((f1 != 0.0f) && (f2 != 0.0f) ? 1.0f : 0.0f); break;
                    case "OR": result = new FloInt((f1 != 0.0f) || (f2 != 0.0f) ? 1.0f : 0.0f); break;
                    default: throw new ParseException("Bad operator in constant expression: " + e.Token.Text, e.Token);
                }
            }
        }
        public void Visit(BinaryAssignExpr e)
        {
            throw new ParseException("Bad operator in constant expression: " + e.Token.Text, e.Token);
        }
        public void Visit(MemoryAccessExpr e)
        {
            throw new ParseException("Memory access not allowed in constant expression", e.Token);
        }
        public void Visit(IdExpr e)
        {
            SymbolInfo symbolInfo = e.SymbolTable.LookupExisting(e.Token);
            if (symbolInfo is DatSymbolInfo)
            {
                if (insideDat)
                {
                    int x = (symbolInfo as DatSymbolInfo).CogAddressX4;
                    if ((x & 3) != 0)
                        throw new ParseException("Address is not long", e.Token);
                    result = new FloInt(x / 4);
                    return;
                }
                // else
                result = new FloInt((symbolInfo as DatSymbolInfo).Dp);
                return;
            }
            ConSymbolInfo conSymbolInfo = e.SymbolTable.LookupExisting(e.Token) as ConSymbolInfo;
            if (conSymbolInfo == null)
                throw new ParseException("Non-constant symbol", e.Token);
            result = conSymbolInfo.Value;
        }
        public void Visit(VariableExpr e)
        {
            throw new ParseException("Variable not allowed in constant expression", e.Token);
        }
        public void Visit(ConExpr e)
        {
            if (e.ObjectToken == null)
            {
                ConSymbolInfo csi = e.SymbolTable.LookupExisting(e.Token) as ConSymbolInfo;
                if (csi == null)
                    throw new ParseException("Expected " + e.Token.Text + " to be CON", e.Token);
                result = csi.Value;
            }
            else
            {
                ObjSymbolInfo osi = e.SymbolTable.LookupExisting(e.ObjectToken) as ObjSymbolInfo;
                if (osi == null)
                    throw new ParseException("Not an object", e.ObjectToken);
                GlobalSymbolInfo gsi = GlobalSymbolTable.LookupExisting(osi.FilenameToken);
                ConSymbolInfo csi = gsi.SymbolTable.LookupExisting(e.Token) as ConSymbolInfo;
                if (csi == null)
                    throw new ParseException("Expected " + e.Token.Text + " to be CON", e.Token);
                result = csi.Value;
            }
        }
        public void Visit(CallExpr e)
        {
            throw new ParseException("Call not allowed in constant expression", e.Token);
        }
        public void Visit(IntExpr e)
        {
            result = new FloInt(e.IntValue);
        }
        public void Visit(FloatExpr e)
        {
            result = new FloInt(e.FloatValue);
        }
        public void Visit(LookExpr e)
        {
            throw new ParseException("LOOKUP/DOWN not allowed in constant expression", e.Token);
        }
        public void Visit(RegisterExpr e)
        {
            if (insideDat)
                result = new FloInt(e.Reg + 0x1f0);
            else
                throw new ParseException("Registers not allowed in constant expression", e.Token);
        }
        public void Visit(SprExpr e)
        {
            throw new ParseException("SPR not allowed in constant expression", e.Token);
        }
        public void Visit(ReadOnlyVariableExpr e)
        {
            throw new ParseException("Read-only variable not allowed in constant expression", e.Token);
        }
        public void Visit(ConverterExpr e)
        {
            switch (e.Token.Text.ToUpper())
            {
                case "FLOAT":
                    result = new FloInt((float)Expr.EvaluateIntConstant(e.Operand, this.insideDat));
                    break;
                case "ROUND":
                    result = new FloInt((int)(Expr.EvaluateFloatConstant(e.Operand, this.insideDat) + 0.5));
                    break;
                case "TRUNC":
                    result = new FloInt((int)(Expr.EvaluateFloatConstant(e.Operand, this.insideDat)));
                    break;
            }
        }
        public void Visit(ConstantExpr e)
        {
            result = Expr.EvaluateConstant(e, this.insideDat);
        }
        public void Visit(StringExpr e)
        {
            throw new ParseException("STRING() not allowed in constant expression", e.Token);
        }
        public void Visit(CoginewtExpr e)
        {
            throw new ParseException("COGINIT/COGNEW not allowed in constant expression", e.Token);
        }
        public void Visit(StrFunctionExpr e)
        {
            throw new ParseException("STRCOMP/STRSIZE not allowed in constant expression", e.Token);
        }
        public void Visit(LockExpr e)
        {
            throw new ParseException("LOCKCLR/LOCKNEW/LOCKRET/LOCKSET not allowed in constant expression", e.Token);
        }
        public void Visit(DollarExpr e)
        {
            result = new FloInt(e.SymbolTable.Here);
        }
        public void Visit(AtAtAtExpr e)
        {
            SymbolInfo si = e.SymbolTable.LookupExisting(e.Token);
            if (!(si is DatSymbolInfo))
                throw new ParseException("Expected DAT symbol", e.Token);
            result = new FloInt((si as DatSymbolInfo).Dp + e.SymbolTable.HubAddress);
        }
    }
} // namespace Homespun
