/*
  Copyright (c) 2011+, HL7, Inc.
  All rights reserved.

  Redistribution and use in source and binary forms, with or without modification,
  are permitted provided that the following conditions are met:

   * Redistributions of source code must retain the above copyright notice, this
     list of conditions and the following disclaimer.
   * Redistributions in binary form must reproduce the above copyright notice,
     this list of conditions and the following disclaimer in the documentation
     and/or other materials provided with the distribution.
   * Neither the name of HL7 nor the names of its contributors may be used to
     endorse or promote products derived from this software without specific
     prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
  ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
  WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
  IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT,
  INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
  NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
  PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY,
  WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
  ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.

*/

// Ported from https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/model/ExpressionNode.java

using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace Hl7.Fhir.MappingLanguage
{

    public class ExpressionNode
    {

        public enum Kind
        {
            Name, Function, Constant, Group, Unary
        }
        public enum Function
        {
            Custom,

            Empty, Not, Exists, SubsetOf, SupersetOf, IsDistinct, Distinct, Count, Where, Select, All, Repeat, Aggregate, Item /*implicit from name[]*/, As, Is, Single,
            First, Last, Tail, Skip, Take, Union, Combine, Intersect, Exclude, Iif, Upper, Lower, ToChars, IndexOf, Substring, StartsWith, EndsWith, Matches, ReplaceMatches, Contains, Replace, Length,
            Children, Descendants, MemberOf, Trace, Check, Today, Now, Resolve, Extension, AllFalse, AnyFalse, AllTrue, AnyTrue,
            HasValue, AliasAs, Alias, HtmlChecks, OfType, Type,
            ConvertsToBoolean, ConvertsToInteger, ConvertsToString, ConvertsToDecimal, ConvertsToQuantity, ConvertsToDateTime, ConvertsToTime, ToBoolean, ToInteger, ToString, ToDecimal, ToQuantity, ToDateTime, ToTime, ConformsTo
        }

        public static Function? fromFunctionCode(String name)
        {
            switch (name)
            {
                case "empty": return Function.Empty;
                case "not": return Function.Not;
                case "exists": return Function.Exists;
                case "subsetOf": return Function.SubsetOf;
                case "supersetOf": return Function.SupersetOf;
                case "isDistinct": return Function.IsDistinct;
                case "distinct": return Function.Distinct;
                case "count": return Function.Count;
                case "where": return Function.Where;
                case "select": return Function.Select;
                case "all": return Function.All;
                case "repeat": return Function.Repeat;
                case "aggregate": return Function.Aggregate;
                case "item": return Function.Item;
                case "as": return Function.As;
                case "is": return Function.Is;
                case "single": return Function.Single;
                case "first": return Function.First;
                case "last": return Function.Last;
                case "tail": return Function.Tail;
                case "skip": return Function.Skip;
                case "take": return Function.Take;
                case "union": return Function.Union;
                case "combine": return Function.Combine;
                case "intersect": return Function.Intersect;
                case "exclude": return Function.Exclude;
                case "iif": return Function.Iif;
                case "lower": return Function.Lower;
                case "upper": return Function.Upper;
                case "toChars": return Function.ToChars;
                case "indexOf": return Function.IndexOf;
                case "substring": return Function.Substring;
                case "startsWith": return Function.StartsWith;
                case "endsWith": return Function.EndsWith;
                case "matches": return Function.Matches;
                case "replaceMatches": return Function.ReplaceMatches;
                case "contains": return Function.Contains;
                case "replace": return Function.Replace;
                case "length": return Function.Length;
                case "children": return Function.Children;
                case "descendants": return Function.Descendants;
                case "memberOf": return Function.MemberOf;
                case "trace": return Function.Trace;
                case "check": return Function.Check;
                case "today": return Function.Today;
                case "now": return Function.Now;
                case "resolve": return Function.Resolve;
                case "extension": return Function.Extension;
                case "allFalse": return Function.AllFalse;
                case "anyFalse": return Function.AnyFalse;
                case "allTrue": return Function.AllTrue;
                case "anyTrue": return Function.AnyTrue;
                case "hasValue": return Function.HasValue;
                case "alias": return Function.Alias;
                case "aliasAs": return Function.AliasAs;
                case "htmlChecks": return Function.HtmlChecks;
                case "ofType": return Function.OfType;
                case "type": return Function.Type;
                case "toInteger": return Function.ToInteger;
                case "toDecimal": return Function.ToDecimal;
                case "toString": return Function.ToString;
                case "toQuantity": return Function.ToQuantity;
                case "toBoolean": return Function.ToBoolean;
                case "toDateTime": return Function.ToDateTime;
                case "toTime": return Function.ToTime;
                case "convertsToInteger": return Function.ConvertsToInteger;
                case "convertsToDecimal": return Function.ConvertsToDecimal;
                case "convertsToString": return Function.ConvertsToString;
                case "convertsToQuantity": return Function.ConvertsToQuantity;
                case "convertsToBoolean": return Function.ConvertsToBoolean;
                case "convertsToDateTime": return Function.ConvertsToDateTime;
                case "convertsToTime": return Function.ConvertsToTime;
                case "conformsTo": return Function.ConformsTo;
            }

            return null;
        }
        public static String toCode(Function value)
        {
            switch (value)
            {
                case Function.Empty: return "empty";
                case Function.Not: return "not";
                case Function.Exists: return "exists";
                case Function.SubsetOf: return "subsetOf";
                case Function.SupersetOf: return "supersetOf";
                case Function.IsDistinct: return "isDistinct";
                case Function.Distinct: return "distinct";
                case Function.Count: return "count";
                case Function.Where: return "where";
                case Function.Select: return "select";
                case Function.All: return "all";
                case Function.Repeat: return "repeat";
                case Function.Aggregate: return "aggregate";
                case Function.Item: return "item";
                case Function.As: return "as";
                case Function.Is: return "is";
                case Function.Single: return "single";
                case Function.First: return "first";
                case Function.Last: return "last";
                case Function.Tail: return "tail";
                case Function.Skip: return "skip";
                case Function.Take: return "take";
                case Function.Union: return "union";
                case Function.Combine: return "combine";
                case Function.Intersect: return "intersect";
                case Function.Exclude: return "exclude";
                case Function.Iif: return "iif";
                case Function.ToChars: return "toChars";
                case Function.Lower: return "lower";
                case Function.Upper: return "upper";
                case Function.IndexOf: return "indexOf";
                case Function.Substring: return "substring";
                case Function.StartsWith: return "startsWith";
                case Function.EndsWith: return "endsWith";
                case Function.Matches: return "matches";
                case Function.ReplaceMatches: return "replaceMatches";
                case Function.Contains: return "contains";
                case Function.Replace: return "replace";
                case Function.Length: return "length";
                case Function.Children: return "children";
                case Function.Descendants: return "descendants";
                case Function.MemberOf: return "memberOf";
                case Function.Trace: return "trace";
                case Function.Check: return "check";
                case Function.Today: return "today";
                case Function.Now: return "now";
                case Function.Resolve: return "resolve";
                case Function.Extension: return "extension";
                case Function.AllFalse: return "allFalse";
                case Function.AnyFalse: return "anyFalse";
                case Function.AllTrue: return "allTrue";
                case Function.AnyTrue: return "anyTrue";
                case Function.HasValue: return "hasValue";
                case Function.Alias: return "alias";
                case Function.AliasAs: return "aliasAs";
                case Function.HtmlChecks: return "htmlChecks";
                case Function.OfType: return "ofType";
                case Function.Type: return "type";
                case Function.ToInteger: return "toInteger";
                case Function.ToDecimal: return "toDecimal";
                case Function.ToString: return "toString";
                case Function.ToBoolean: return "toBoolean";
                case Function.ToQuantity: return "toQuantity";
                case Function.ToDateTime: return "toDateTime";
                case Function.ToTime: return "toTime";
                case Function.ConvertsToInteger: return "convertsToInteger";
                case Function.ConvertsToDecimal: return "convertsToDecimal";
                case Function.ConvertsToString: return "convertsToString";
                case Function.ConvertsToBoolean: return "convertsToBoolean";
                case Function.ConvertsToQuantity: return "convertsToQuantity";
                case Function.ConvertsToDateTime: return "convertsToDateTime";
                case Function.ConvertsToTime: return "isTime";
                case Function.ConformsTo: return "conformsTo";
                default: return "??";
            }
        }

        public enum Operation
        {
            Equals, Equivalent, NotEquals, NotEquivalent, LessThan, Greater, LessOrEqual, GreaterOrEqual, Is, As, Union, Or, And, Xor, Implies,
            Times, DivideBy, Plus, Minus, Concatenate, Div, Mod, In, Contains, MemberOf
        }

        public static Operation? fromOperationCode(String name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            switch (name)
            {
                case "=": return Operation.Equals;
                case "~": return Operation.Equivalent;
                case "!=": return Operation.NotEquals;
                case "!~": return Operation.NotEquivalent;
                case ">": return Operation.Greater;
                case "<": return Operation.LessThan;
                case ">=": return Operation.GreaterOrEqual;
                case "<=": return Operation.LessOrEqual;
                case "|": return Operation.Union;
                case "or": return Operation.Or;
                case "and": return Operation.And;
                case "xor": return Operation.Xor;
                case "is": return Operation.Is;
                case "as": return Operation.As;
                case "*": return Operation.Times;
                case "/": return Operation.DivideBy;
                case "+": return Operation.Plus;
                case "-": return Operation.Minus;
                case "&": return Operation.Concatenate;
                case "implies": return Operation.Implies;
                case "div": return Operation.Div;
                case "mod": return Operation.Mod;
                case "in": return Operation.In;
                case "contains": return Operation.Contains;
                case "memberOf": return Operation.MemberOf;
            }
            return null;

        }
        public static String toCode(Operation? value)
        {
            switch (value)
            {
                case Operation.Equals: return "=";
                case Operation.Equivalent: return "~";
                case Operation.NotEquals: return "!=";
                case Operation.NotEquivalent: return "!~";
                case Operation.Greater: return ">";
                case Operation.LessThan: return "<";
                case Operation.GreaterOrEqual: return ">=";
                case Operation.LessOrEqual: return "<=";
                case Operation.Union: return "|";
                case Operation.Or: return "or";
                case Operation.And: return "and";
                case Operation.Xor: return "xor";
                case Operation.Times: return "*";
                case Operation.DivideBy: return "/";
                case Operation.Plus: return "+";
                case Operation.Minus: return "-";
                case Operation.Concatenate: return "&";
                case Operation.Implies: return "implies";
                case Operation.Is: return "is";
                case Operation.As: return "as";
                case Operation.Div: return "div";
                case Operation.Mod: return "mod";
                case Operation.In: return "in";
                case Operation.Contains: return "contains";
                case Operation.MemberOf: return "memberOf";
                default: return "??";
            }
        }

        public enum CollectionStatus
        {
            SINGLETON, ORDERED, UNORDERED
        }

        //the expression will have one of either name or constant
        private String uniqueId;
        private Kind kind;
        private String name;
        private Base constant;
        private Function function;
        private List<ExpressionNode> parameters; // will be created if there is a function
        private ExpressionNode inner;
        private ExpressionNode group;
        private Operation? operation;
        private bool proximal; // a proximal operation is the first in the sequence of operations. This is significant when evaluating the outcomes
        private ExpressionNode opNext;
        private SourceLocation start;
        private SourceLocation end;
        private SourceLocation opStart;
        private SourceLocation opEnd;
        private TypeDetails types;
        private TypeDetails opTypes;


        public ExpressionNode(int uniqueId)
        {
            this.uniqueId = uniqueId.ToString();
        }

        public override String ToString()
        {
            StringBuilder b = new StringBuilder();
            switch (kind)
            {
                case Kind.Name:
                    b.Append(name);
                    break;
                case Kind.Function:
                    if (function == Function.Item)
                        b.Append("[");
                    else
                    {
                        b.Append(name);
                        b.Append("(");
                    }
                    bool first = true;
                    foreach (ExpressionNode n in parameters)
                    {
                        if (first)
                            first = false;
                        else
                            b.Append(", ");
                        b.Append(n.ToString());
                    }
                    if (function == Function.Item)
                    {
                        b.Append("]");
                    }
                    else
                    {
                        b.Append(")");
                    }
                    break;
                case Kind.Constant:
                    if (constant == null)
                    {
                        b.Append("{}");
                    }
                    else if (constant is FHIRPathEngineOriginal.FHIRConstant)
                    {
                        b.Append(Utilities.escapeJson(constant.ToString()));
                    }
                    else if (constant is FhirString)
                    {
                        b.Append("'" + Utilities.escapeJson(constant.ToString()) + "'");
                    }
                    else if (constant is Quantity q)
                    {
                        b.Append(Utilities.escapeJson(q.Value.ToString()));
                        b.Append(" '");
                        b.Append(Utilities.escapeJson(q.Unit));
                        b.Append("'");
                    }
                    else if (constant is PrimitiveType pt)
                    {
                        b.Append(Utilities.escapeJson(pt.ToString()));
                    }
                    else
                    {
                        b.Append(Utilities.escapeJson(constant.ToString()));
                    }
                    break;
                case Kind.Group:
                    b.Append("(");
                    b.Append(group.ToString());
                    b.Append(")");
                    break;
            }
            if (inner != null)
            {
                if (!((ExpressionNode.Kind.Function == inner.getKind()) && (ExpressionNode.Function.Item == inner.getFunction())))
                {
                    b.Append(".");
                }
                b.Append(inner.ToString());
            }
            if (operation != null)
            {
                b.Append(" ");
                b.Append(toCode(operation));
                b.Append(" ");
                b.Append(opNext.ToString());
            }

            return b.ToString();
        }

        public String getName()
        {
            return name;
        }
        public void setName(String name)
        {
            this.name = name;
        }
        public Base getConstant()
        {
            return constant;
        }
        public void setConstant(Base constant)
        {
            this.constant = constant;
        }

        public Function getFunction()
        {
            return function;
        }
        public void setFunction(Function function)
        {
            this.function = function;
            if (parameters == null)
                parameters = new List<ExpressionNode>();
        }

        public bool isProximal()
        {
            return proximal;
        }
        public void setProximal(bool proximal)
        {
            this.proximal = proximal;
        }
        public Operation? getOperation()
        {
            return operation;
        }
        public void setOperation(Operation? operation)
        {
            this.operation = operation;
        }
        public ExpressionNode getInner()
        {
            return inner;
        }
        public void setInner(ExpressionNode value)
        {
            this.inner = value;
        }
        public ExpressionNode getOpNext()
        {
            return opNext;
        }
        public void setOpNext(ExpressionNode value)
        {
            this.opNext = value;
        }
        public List<ExpressionNode> getParameters()
        {
            return parameters;
        }
        public bool checkName()
        {
            if (!name.StartsWith("$"))
                return true;
            else
                return name == "$this" || name == "$total";
        }

        public Kind getKind()
        {
            return kind;
        }

        public void setKind(Kind kind)
        {
            this.kind = kind;
        }

        public ExpressionNode getGroup()
        {
            return group;
        }

        public void setGroup(ExpressionNode group)
        {
            this.group = group;
        }

        public SourceLocation getStart()
        {
            return start;
        }

        public void setStart(SourceLocation start)
        {
            this.start = start;
        }

        public SourceLocation getEnd()
        {
            return end;
        }

        public void setEnd(SourceLocation end)
        {
            this.end = end;
        }

        public SourceLocation getOpStart()
        {
            return opStart;
        }

        public void setOpStart(SourceLocation opStart)
        {
            this.opStart = opStart;
        }
        public SourceLocation getOpEnd()
        {
            return opEnd;
        }
        public void setOpEnd(SourceLocation opEnd)
        {
            this.opEnd = opEnd;
        }
        public String getUniqueId()
        {
            return uniqueId;
        }


        public int parameterCount()
        {
            if (parameters == null)
                return 0;
            else
                return parameters.Count;
        }

        public String Canonical()
        {
            StringBuilder b = new StringBuilder();
            write(b);
            return b.ToString();
        }

        public String summary()
        {
            switch (kind)
            {
                case Kind.Name: return uniqueId + ": " + name;
                case Kind.Function: return uniqueId + ": " + function.ToString() + "()";
                case Kind.Constant: return uniqueId + ": " + constant;
                case Kind.Group: return uniqueId + ": (Group)";
            }
            return "??";
        }

        private void write(StringBuilder b)
        {

            switch (kind)
            {
                case Kind.Name:
                    b.Append(name);
                    break;
                case Kind.Constant:
                    b.Append(constant);
                    break;
                case Kind.Function:
                    b.Append(toCode(function));
                    b.Append('(');
                    bool f = true;
                    foreach (ExpressionNode n in parameters)
                    {
                        if (f)
                            f = false;
                        else
                            b.Append(", ");
                        n.write(b);
                    }
                    b.Append(')');

                    break;
                case Kind.Group:
                    b.Append('(');
                    group.write(b);
                    b.Append(')');
                    break;
            }

            if (inner != null)
            {
                b.Append('.');
                inner.write(b);
            }
            if (operation != null)
            {
                b.Append(' ');
                b.Append(toCode(operation));
                b.Append(' ');
                opNext.write(b);
            }
        }

        public String check()
        {

            switch (kind)
            {
                case Kind.Name:
                    if (Utilities.noString(name))
                        return "No Name provided @ " + location();
                    break;

                case Kind.Function:
                    if (function == null)
                        return "No Function id provided @ " + location();
                    foreach (ExpressionNode n in parameters)
                    {
                        String msg = n.check();
                        if (msg != null)
                            return msg;
                    }

                    break;

                case Kind.Unary:
                    break;
                case Kind.Constant:
                    if (constant == null)
                        return "No Constant provided @ " + location();
                    break;

                case Kind.Group:
                    if (group == null)
                        return "No Group provided @ " + location();
                    else
                    {
                        String msg = group.check();
                        if (msg != null)
                            return msg;
                    }
                    break;
            }
            if (inner != null)
            {
                String msg = inner.check();
                if (msg != null)
                    return msg;
            }
            if (operation == null)
            {

                if (opNext != null)
                    return "Next provided when it shouldn't be @ " + location();
            }
            else
            {
                if (opNext == null)
                    return "No Next provided @ " + location();
                else
                    opNext.check();
            }
            return null;

        }

        private String location()
        {
            return $"{start.getLine()}, {start.getColumn()}";
        }

        public TypeDetails getTypes()
        {
            return types;
        }

        public void setTypes(TypeDetails types)
        {
            this.types = types;
        }

        public TypeDetails getOpTypes()
        {
            return opTypes;
        }

        public void setOpTypes(TypeDetails opTypes)
        {
            this.opTypes = opTypes;
        }

    }
}