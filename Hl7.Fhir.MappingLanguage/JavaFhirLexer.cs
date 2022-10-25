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

// Port of https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r5/src/main/java/org/hl7/fhir/r5/utils/FHIRLexer.java

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Hl7.Fhir.MappingLanguage
{
    public class FHIRException : Exception
    {
        public FHIRException()
        {
        }

        public FHIRException(string message, Exception cause)
            : base(message, cause)
        {
        }

        public FHIRException(string message)
            : base(message)
        {
        }
    }

    public class FHIRLexerException : FHIRException
    {
        public FHIRLexerException()
        {
        }

        public FHIRLexerException(string message, Exception cause)
            : base(message, cause)
        {
        }

        public FHIRLexerException(string message)
            : base(message)
        {
        }

        //public FHIRLexerException(Exception cause)
        //    : base(cause)
        //{
        //}
    }

    /// <summary>
    ///  shared lexer for concrete syntaxes
    /// - FluentPath
    /// - Mapping language
    /// </summary>
    /// <remarks>
    /// Direct port of
    /// https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/utils/FHIRLexer.java
    /// </remarks>
    public class FHIRLexer
    {
        private string source;
        private int cursor;
        private int currentStart;
        private string current;
        private List<string> comments = new List<string>();
        private SourceLocation currentLocation;
        private SourceLocation currentStartLocation;
        private int id;
        private string name;

        public FHIRLexer(string source, string name)
        {
            this.source = source;
            this.name = name == null ? "??" : name;
            currentLocation = new SourceLocation(1, 1);
            next();
        }
        public FHIRLexer(string source, int i)
        {
            this.source = source;
            this.cursor = i;
            currentLocation = new SourceLocation(1, 1);
            next();
        }
        public string getCurrent()
        {
            return current;
        }
        public SourceLocation getCurrentLocation()
        {
            return currentLocation;
        }

        public bool isConstant()
        {
            return current != null && (current[0] == '\'' || current[0] == '"') || current[0] == '@' || current[0] == '%' ||
                current[0] == '-' || current[0] == '+' || (current[0] >= '0' && current[0] <= '9') ||
                current.Equals("true") || current.Equals("false") || current.Equals("{}");
        }

        public bool isFixedName()
        {
            return current != null && (current[0] == '`');
        }

        public bool isStringConstant()
        {
            return current[0] == '\'' || current[0] == '"' || current[0] == '`';
        }

        public string take()
        {
            string s = current;
            next();
            return s;
        }

        public int takeInt()
        {
            string s = current;
            if (!int.TryParse(s, out int result))
                throw error("Found " + current + " expecting an integer");
            next();
            return result;
        }

        public bool isToken()
        {
            if (string.IsNullOrEmpty(current))
                return false;

            if (current.StartsWith("$"))
                return true;

            if (current.Equals("*") || current.Equals("**"))
                return true;

            if ((current[0] >= 'A' && current[0] <= 'Z') || (current[0] >= 'a' && current[0] <= 'z'))
            {
                for (int i = 1; i < current.Length; i++)
                    if (!((current[1] >= 'A' && current[1] <= 'Z') || (current[1] >= 'a' && current[1] <= 'z') ||
                        (current[1] >= '0' && current[1] <= '9')))
                        return false;
                return true;
            }
            return false;
        }

        public FHIRLexerException error(string msg)
        {
            return error(msg, currentLocation.ToString());
        }

        public FHIRLexerException error(string msg, string location)
        {
            if (!string.IsNullOrEmpty(name))
                return new FHIRLexerException("Error in " + name + " at " + location + ": " + msg);
            return new FHIRLexerException("Error @" + location + ": " + msg);
        }

        public void next()
        {
            skipWhitespaceAndComments();
            current = null;
            currentStart = cursor;
            currentStartLocation = currentLocation;
            if (cursor < source.Length)
            {
                char ch = source[cursor];
                if (ch == '!' || ch == '>' || ch == '<' || ch == ':' || ch == '-' || ch == '=')
                {
                    cursor++;
                    if (cursor < source.Length && (source[cursor] == '=' || source[cursor] == '~' || source[cursor] == '-') || (ch == '-' && source[cursor] == '>'))
                        cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if (ch == '.')
                {
                    cursor++;
                    if (cursor < source.Length && (source[cursor] == '.'))
                        cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if (ch >= '0' && ch <= '9')
                {
                    cursor++;
                    bool dotted = false;
                    while (cursor < source.Length && ((source[cursor] >= '0' && source[cursor] <= '9') || (source[cursor] == '.') && !dotted))
                    {
                        if (source[cursor] == '.')
                            dotted = true;
                        cursor++;
                    }
                    if (source[cursor - 1] == '.')
                        cursor--;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'))
                {
                    while (cursor < source.Length && ((source[cursor] >= 'A' && source[cursor] <= 'Z') || (source[cursor] >= 'a' && source[cursor] <= 'z') ||
                        (source[cursor] >= '0' && source[cursor] <= '9') || source[cursor] == '_'))
                        cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if (ch == '%')
                {
                    cursor++;
                    if (cursor < source.Length && (source[cursor] == '`'))
                    {
                        cursor++;
                        while (cursor < source.Length && (source[cursor] != '`'))
                            cursor++;
                        cursor++;
                    }
                    else
                        while (cursor < source.Length && ((source[cursor] >= 'A' && source[cursor] <= 'Z') || (source[cursor] >= 'a' && source[cursor] <= 'z') ||
                            (source[cursor] >= '0' && source[cursor] <= '9') || source[cursor] == ':' || source[cursor] == '-'))
                            cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if (ch == '/')
                {
                    cursor++;
                    if (cursor < source.Length && (source[cursor] == '/'))
                    {
                        // this is en error - should already have been skipped
                        error("This shouldn't happen?");
                    }
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if (ch == '$')
                {
                    cursor++;
                    while (cursor < source.Length && (source[cursor] >= 'a' && source[cursor] <= 'z'))
                        cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if (ch == '{')
                {
                    cursor++;
                    ch = source[cursor];
                    if (ch == '}')
                        cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else if (ch == '"')
                {
                    cursor++;
                    bool escape = false;
                    while (cursor < source.Length && (escape || source[cursor] != '"'))
                    {
                        if (escape)
                            escape = false;
                        else
                            escape = (source[cursor] == '\\');
                        cursor++;
                    }
                    if (cursor == source.Length)
                        throw error("Unterminated string");
                    cursor++;
                    current = "\"" + source.Substring(currentStart + 1, cursor - 2 - currentStart) + "\"";
                }
                else if (ch == '`')
                {
                    cursor++;
                    bool escape = false;
                    while (cursor < source.Length && (escape || source[cursor] != '`'))
                    {
                        if (escape)
                            escape = false;
                        else
                            escape = (source[cursor] == '\\');
                        cursor++;
                    }
                    if (cursor == source.Length)
                        throw error("Unterminated string");
                    cursor++;
                    current = "`" + source.Substring(currentStart + 1, cursor - 2 - currentStart) + "`";
                }
                else if (ch == '\'')
                {
                    cursor++;
                    char ech = ch;
                    bool escape = false;
                    while (cursor < source.Length && (escape || source[cursor] != ech))
                    {
                        if (escape)
                            escape = false;
                        else
                            escape = (source[cursor] == '\\');
                        cursor++;
                    }
                    if (cursor == source.Length)
                        throw error("Unterminated string");
                    cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                    if (ech == '\'')
                        current = "\'" + current.Substring(1, current.Length - 2) + "\'";
                }
                else if (ch == '`')
                {
                    cursor++;
                    bool escape = false;
                    while (cursor < source.Length && (escape || source[cursor] != '`'))
                    {
                        if (escape)
                            escape = false;
                        else
                            escape = (source[cursor] == '\\');
                        cursor++;
                    }
                    if (cursor == source.Length)
                        throw error("Unterminated string");
                    cursor++;
                    current = "`" + source.Substring(currentStart + 1, cursor - 2 - currentStart) + "`";
                }
                else if (ch == '@')
                {
                    int start = cursor;
                    cursor++;
                    while (cursor < source.Length && isDateChar(source[cursor], start))
                        cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
                else
                { // if CharInSet(ch, ['.', ',', '(', ')', '=', '$']) then
                    cursor++;
                    current = source.Substring(currentStart, cursor - currentStart);
                }
            }
        }


        private void skipWhitespaceAndComments()
        {
            comments.Clear();
            bool last13 = false;
            bool done = false;
            while (cursor < source.Length && !done)
            {
                if (cursor < source.Length - 1 && "//".Equals(source.Substring(cursor, 2)))
                {
                    int start = cursor + 2;
                    while (cursor < source.Length && !((source[cursor] == '\r') || source[cursor] == '\n'))
                    {
                        cursor++;
                    }
                    comments.Add(source.Substring(start, cursor - start).Trim());
                }
                else if (cursor < source.Length - 1 && "/*".Equals(source.Substring(cursor, 2)))
                {
                    int start = cursor + 2;
                    while (cursor < source.Length - 1 && !"*/".Equals(source.Substring(cursor, 2)))
                    {
                        last13 = currentLocation.checkChar(source[cursor], last13);
                        cursor++;
                    }
                    if (cursor >= source.Length - 1)
                    {
                        error("Unfinished comment");
                    }
                    else
                    {
                        comments.Add(source.Substring(start, cursor - start).Trim());
                        cursor = cursor + 2;
                    }
                }
                else if (char.IsWhiteSpace(source[cursor]))
                {
                    last13 = currentLocation.checkChar(source[cursor], last13);
                    cursor++;
                }
                else
                {
                    done = true;
                }
            }
        }

        private bool isDateChar(char ch, int start)
        {
            int eot = source[start + 1] == 'T' ? 10 : 20;

            return ch == '-' || ch == ':' || ch == 'T' || ch == '+' || ch == 'Z' || char.IsDigit(ch) || (cursor - start == eot && ch == '.' && cursor < source.Length - 1 && Char.IsDigit(source[cursor + 1]));
        }

        public bool isOp()
        {
            // This checks if the function is in the symbol table of the fhirpath engine
            // return ExpressionNode.Operation.fromCode(current) != null;
            return fromCode(current) != null;
        }

        public bool done()
        {
            return currentStart >= source.Length;
        }
        public int nextId()
        {
            id++;
            return id;
        }
        public SourceLocation getCurrentStartLocation()
        {
            return currentStartLocation;
        }

        // special case use
        public void setCurrent(string current)
        {
            this.current = current;
        }

        public bool hasComments()
        {
            return comments.Count > 0;
        }

        public List<string> getComments()
        {
            return comments;
        }

        public String getAllComments()
        {
            CommaSeparatedStringBuilder b = new CommaSeparatedStringBuilder("\r\n");
            foreach (var c in comments)
                b.append(c);
            comments.Clear();
            return b.ToString();
        }

        public String getFirstComment()
        {
            if (hasComments())
            {
                String s = comments.First();
                comments.RemoveAt(0);
                return s;
            }
            return null;
        }

        public bool hasToken(string kw)
        {
            return !done() && kw.Equals(current);
        }
        public bool hasToken(params string[] names)
        {
            if (done())
                return false;
            foreach (string s in names)
                if (s.Equals(current))
                    return true;
            return false;
        }

        public void token(string kw)
        {
            if (!kw.Equals(current))
                throw error("Found \"" + current + "\" expecting \"" + kw + "\"");
            next();
        }

        public string readConstant(string desc)
        {
            if (!isStringConstant())
                throw error("Found " + current + " expecting \"[" + desc + "]\"");

            return processConstant(take());
        }

        public string readFixedName(string desc)
        {
            if (!isFixedName())
                throw error("Found " + current + " expecting \"[" + desc + "]\"");

            return processFixedName(take());
        }

        public string processConstant(string s)
        {
            StringBuilder b = new StringBuilder();
            int i = 1;
            while (i < s.Length - 1)
            {
                char ch = s[i];
                if (ch == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 't':
                            b.Append('\t');
                            break;
                        case 'r':
                            b.Append('\r');
                            break;
                        case 'n':
                            b.Append('\n');
                            break;
                        case 'f':
                            b.Append('\f');
                            break;
                        case '\'':
                            b.Append('\'');
                            break;
                        case '"':
                            b.Append('"');
                            break;
                        case '`':
                            b.Append('`');
                            break;
                        case '\\':
                            b.Append('\\');
                            break;
                        case '/':
                            b.Append('/');
                            break;
                        case 'u':
                            i++;
                            int uc = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber);
                            b.Append((char)uc);
                            i = i + 4;
                            break;
                        default:
                            throw new FHIRLexerException("Unknown character escape \\" + s[i]);
                    }
                }
                else
                {
                    b.Append(ch);
                    i++;
                }
            }
            return b.ToString();
        }

        public string processFixedName(string s)
        {
            StringBuilder b = new StringBuilder();
            int i = 1;
            while (i < s.Length - 1)
            {
                char ch = s[i];
                if (ch == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case 't':
                            b.Append('\t');
                            break;
                        case 'r':
                            b.Append('\r');
                            break;
                        case 'n':
                            b.Append('\n');
                            break;
                        case 'f':
                            b.Append('\f');
                            break;
                        case '\'':
                            b.Append('\'');
                            break;
                        case '"':
                            b.Append('"');
                            break;
                        case '\\':
                            b.Append('\\');
                            break;
                        case '/':
                            b.Append('/');
                            break;
                        case 'u':
                            i++;
                            int uc = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber);
                            b.Append((char)uc);
                            i = i + 4;
                            break;
                        default:
                            throw new FHIRLexerException("Unknown character escape \\" + s[i]);
                    }
                }
                else
                {
                    b.Append(ch);
                    i++;
                }
            }
            return b.ToString();
        }

        public void skipToken(string token)
        {
            if (getCurrent().Equals(token))
                next();

        }
        public string takeDottedToken()
        {
            StringBuilder b = new StringBuilder();
            b.Append(take());
            while (!done() && getCurrent().Equals("."))
            {
                b.Append(take());
                b.Append(take());
            }
            return b.ToString();
        }

        public int getCurrentStart()
        {
            return currentStart;
        }

        public String getSource()
        {
            return source;
        }

        public enum Operation
        {
            Equals, Equivalent, NotEquals, NotEquivalent, LessThan, Greater, LessOrEqual, GreaterOrEqual, Is, As, Union, Or, And, Xor, Implies,
            Times, DivideBy, Plus, Minus, Concatenate, Div, Mod, In, Contains, MemberOf
        }

        public static Operation? fromCode(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            switch (name)
            {
                case "=":
                    return Operation.Equals;
                case "~":
                    return Operation.Equivalent;
                case "!=":
                    return Operation.NotEquals;
                case "!~":
                    return Operation.NotEquivalent;
                case ">":
                    return Operation.Greater;
                case "<":
                    return Operation.LessThan;
                case ">=":
                    return Operation.GreaterOrEqual;
                case "<=":
                    return Operation.LessOrEqual;
                case "|":
                    return Operation.Union;
                case "or":
                    return Operation.Or;
                case "and":
                    return Operation.And;
                case "xor":
                    return Operation.Xor;
                case "is":
                    return Operation.Is;
                case "as":
                    return Operation.As;
                case "*":
                    return Operation.Times;
                case "/":
                    return Operation.DivideBy;
                case "+":
                    return Operation.Plus;
                case "-":
                    return Operation.Minus;
                case "&":
                    return Operation.Concatenate;
                case "implies":
                    return Operation.Implies;
                case "div":
                    return Operation.Div;
                case "mod":
                    return Operation.Mod;
                case "in":
                    return Operation.In;
                case "contains":
                    return Operation.Contains;
                case "memberOf":
                    return Operation.MemberOf;
            }
            return null;

        }
        public string toCode(Operation me)
        {
            switch (me)
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
    }
}

public class SourceLocation
{
    private int line;
    private int column;
    public SourceLocation(int line, int column)
    {
        this.line = line;
        this.column = column;
    }

    public int getLine()
    {
        return line;
    }

    public int getColumn()
    {
        return column;
    }

    public void setLine(int line)
    {
        this.line = line;
    }

    public void setColumn(int column)
    {
        this.column = column;
    }

    public override string ToString()
    {
        return $"{line}, {column}";
    }

    public void newLine()
    {
        setLine(getLine() + 1);
        setColumn(1);
    }

    public bool checkChar(char ch, bool last13)
    {
        if (ch == '\r')
        {
            newLine();
            return true;
        }
        else if (ch == '\n')
        {
            if (!last13)
            {
                newLine();
            }
            return false;
        }
        else
        {
            setColumn(getColumn() + 1);
            return false;
        }
    }
}

