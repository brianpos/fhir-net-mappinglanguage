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

// Ported from https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.utilities/src/main/java/org/hl7/fhir/utilities/Utilities.java

using System;
using System.Linq;
using System.Text;

namespace Hl7.Fhir.MappingLanguage
{
    public class Utilities
    {
        internal static string escapeJava(string doco)
        {
            if (string.IsNullOrEmpty(doco))
                return "";

            StringBuilder b = new StringBuilder();
            foreach (char c in doco.ToCharArray())
            {
                if (c == '\r')
                    b.Append("\\r");
                else if (c == '\n')
                    b.Append("\\n");
                else if (c == '"')
                    b.Append("\\\"");
                else if (c == '\\')
                    b.Append("\\\\");
                else
                    b.Append(c);
            }
            return b.ToString();
        }

        public static string escapeJson(string value)
        {
            if (value == null)
                return "";

            StringBuilder b = new StringBuilder();
            foreach (char c in value.ToCharArray())
            {
                if (c == '\r')
                    b.Append("\\r");
                else if (c == '\n')
                    b.Append("\\n");
                else if (c == '\t')
                    b.Append("\\t");
                else if (c == '"')
                    b.Append("\\\"");
                else if (c == '\\')
                    b.Append("\\\\");
                else if (((int)c) < 32)
                    b.Append("\\u" + padLeft(((int)c).ToString(), '0', 4)); // TODO: BP - Check that this escaping is right
                else
                    b.Append(c);
            }
            return b.ToString();
        }

        public static string padLeft(string src, char c, int len)
        {
            StringBuilder s = new StringBuilder();
            for (int i = 0; i < len - src.Length; i++)
                s.Append(c);
            s.Append(src);
            return s.ToString();
        }


        internal static bool existsInList(string code, params string[] values)
        {
            return values.Contains(code);
        }

        internal static bool startsWithInList(string code, params string[] values)
        {
            return values.Any(v => v.StartsWith(code));
        }

        internal static bool noString(string value)
        {
            return string.IsNullOrEmpty(value);
        }

        internal static bool isToken(string tail)
        {
            if (string.IsNullOrEmpty(tail))
                return false;
            bool result = isAlphabetic(tail[0]);
            for (int i = 1; i < tail.Length; i++)
            {
                result = result && (isAlphabetic(tail[i]) || char.IsDigit(tail[i]) || (tail[i] == '_') || (tail[i] == '[') || (tail[i] == ']'));
            }
            return result;
        }

        public static bool isInteger(string v)
        {
            if (string.IsNullOrEmpty(v))
            {
                return false;
            }
            string value = v.StartsWith("-") ? v.Substring(1) : v;
            foreach (char next in value.ToCharArray())
            {
                if (!char.IsDigit(next))
                {
                    return false;
                }
            }
            // check bounds -2,147,483,648..2,147,483,647
            if (value.Length > 10)
                return false;
            if (v.StartsWith("-"))
            {
                if (value.Length == 10 && v.CompareTo("2147483648") > 0)
                    return false;
            }
            else
            {
                if (value.Length == 10 && v.CompareTo("2147483647") > 0)
                    return false;
            }
            return true;
        }

        public static bool isAlphabetic(char c)
        {
            return ((c >= 'a') && (c <= 'z')) || ((c >= 'A') && (c <= 'Z'));
        }

        internal static string capitalize(string s)
        {
            if (s == null) return null;
            if (s.Length == 0) return s;
            if (s.Length == 1) return s.ToLower();

            return s.Substring(0, 1).ToLower() + s.Substring(1);
        }

        public static string uncapitalize(string s)
        {
            if (s == null) return null;
            if (s.Length == 0) return s;
            if (s.Length == 1) return s.ToLower();

            return s.Substring(0, 1).ToLower() + s.Substring(1);
        }

        internal static bool isAbsoluteUrl(string n)
        {
            if (n != null && n.Contains(":"))
            {
                string scheme = n.Substring(0, n.IndexOf(":"));
                string details = n.Substring(n.IndexOf(":") + 1);
                return (existsInList(scheme, "http", "https", "urn") || (isToken(scheme) && scheme.Equals(scheme.ToLower())) || Utilities.startsWithInList(n, "urn:iso:", "urn:iso-iec:", "urn:iso-cie:", "urn:iso-astm:", "urn:iso-ieee:", "urn:iec:"))
                    && details != null && details.Length > 0 && !details.Contains(" "); // rfc5141
            }
            return false;
        }

        public static string pathURL(params string[] args)
        {
            StringBuilder s = new StringBuilder();
            bool d = false;
            foreach (string arg in args)
            {
                if (arg != null)
                {
                    if (!d)
                        d = !noString(arg);
                    else if (s.ToString() != null && !s.ToString().EndsWith("/") && !arg.StartsWith("/"))
                        s.Append("/");
                    s.Append(arg);
                }
            }
            return s.ToString();
        }

        public static int charCount(string s, char c)
        {
            int res = 0;
            foreach (char ch in s.ToCharArray())
                if (ch == c)
                    res++;
            return res;
        }
    }
}