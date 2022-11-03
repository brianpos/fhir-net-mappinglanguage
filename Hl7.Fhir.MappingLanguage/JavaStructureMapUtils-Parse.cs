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

// Port from https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/utils/StructureMapUtilities.java
// (the parse/serialize portions)

// remember group resolution
// trace - account for which wasn't transformed in the source

using Hl7.Fhir.Model;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Hl7.Fhir.MappingLanguage
{
    /**
     * Services in this class:
     *
     * string render(map) - take a structure and convert it to text
     * map parse(text) - take a text representation and parse it
     * getTargetType(map) - return the definition for the type to create to hand in
     * transform(appInfo, source, map, target) - transform from source to target following the map
     * analyse(appInfo, map) - generate profiles and other analysis artifacts for the targets of the transform
     * map generateMapFromMappings(StructureDefinition) - build a mapping from a structure definition with logical mappings
     *
     * @author Grahame Grieve
     *
     */
    public class StructureMapUtilitiesParse
    {
        public const string MAP_WHERE_CHECK = "map.where.check";
        public const string MAP_WHERE_LOG = "map.where.log";
        public const string MAP_WHERE_EXPRESSION = "map.where.expression";
        public const string MAP_SEARCH_EXPRESSION = "map.search.expression";
        public const string MAP_EXPRESSION = "map.transform.expression";
        private const bool RENDER_MULTIPLE_TARGETS_ONELINE = true;
        private const string AUTO_VAR_NAME = "vvv";

        private FHIRPathEngine fpe;

        public StructureMapUtilitiesParse()
        {
            fpe = new FHIRPathEngine();
        }

        public static string render(StructureMap map)
        {
            StringBuilder b = new StringBuilder();
            b.Append("map \"");
            b.Append(map.Url);
            b.Append("\" = \"");
            b.Append(Utilities.escapeJson(map.Name));
            b.Append("\"\r\n\r\n");
            if (!string.IsNullOrEmpty(map.Description?.Value))
            {
                renderMultilineDoco(b, map.Description.Value, 0);
                b.Append("\r\n");
            }
            renderConceptMaps(b, map);
            renderUses(b, map);
            renderImports(b, map);
            foreach (StructureMap.GroupComponent g in map.Group)
                renderGroup(b, g);
            return b.ToString();
        }

        private static void renderConceptMaps(StringBuilder b, StructureMap map)
        {
            foreach (Resource r in map.Contained)
            {
                if (r is ConceptMap)
                {
                    produceConceptMap(b, (ConceptMap)r);
                }
            }
        }

        private static void produceConceptMap(StringBuilder b, ConceptMap cm)
        {
            b.Append("conceptmap \"");
            b.Append(cm.Id);
            b.Append("\" {\r\n");
            Dictionary<string, string> prefixesSrc = new Dictionary<string, string>();
            Dictionary<string, string> prefixesTgt = new Dictionary<string, string>();
            char prefix = 's';
            foreach (ConceptMap.GroupComponent cg in cm.Group)
            {
                if (!prefixesSrc.ContainsKey(cg.Source))
                {
                    prefixesSrc.Add(cg.Source, prefix.ToString());
                    b.Append("  prefix ");
                    b.Append(prefix);
                    b.Append(" = \"");
                    b.Append(cg.Source);
                    b.Append("\"\r\n");
                    prefix++;
                }
                if (!prefixesTgt.ContainsKey(cg.Target))
                {
                    prefixesTgt.Add(cg.Target, prefix.ToString());
                    b.Append("  prefix ");
                    b.Append(prefix);
                    b.Append(" = \"");
                    b.Append(cg.Target);
                    b.Append("\"\r\n");
                    prefix++;
                }
            }
            b.AppendLine();
            foreach (ConceptMap.GroupComponent cg in cm.Group)
            {
                if (cg.Unmapped != null)
                {
                    b.Append("  unmapped for ");
                    b.Append(prefixesSrc[cg.Source]);
                    b.Append(" = ");
                    b.Append(cg.Unmapped.Mode.GetLiteral());
                    b.AppendLine();
                }
            }

            foreach (ConceptMap.GroupComponent cg in cm.Group)
            {
                foreach (var ce in cg.Element)
                {
                    b.Append("  ");
                    b.Append(prefixesSrc[cg.Source]);
                    b.Append(":");
                    if (Utilities.isToken(ce.Code))
                    {
                        b.Append(ce.Code);
                    }
                    else
                    {
                        b.Append("\"");
                        b.Append(ce.Code);
                        b.Append("\"");
                    }
                    b.Append(" ");
                    var e = ce.getTargetFirstRep().Equivalence;
                    b.Append(e.HasValue ? getChar(e.Value) : "??");
                    b.Append(" ");
                    b.Append(prefixesTgt[cg.Target]);
                    b.Append(":");
                    if (Utilities.isToken(ce.getTargetFirstRep().Code))
                    {
                        b.Append(ce.getTargetFirstRep().Code);
                    }
                    else
                    {
                        b.Append("\"");
                        b.Append(ce.getTargetFirstRep().Code);
                        b.Append("\"");
                    }
                    b.AppendLine();
                }
            }
            b.Append("}\r\n\r\n");
        }

        private static string getChar(ConceptMapEquivalence equivalence)
        {
            switch (equivalence)
            {
                case ConceptMapEquivalence.Relatedto: return "-";
                case ConceptMapEquivalence.Equal: return "=";
                case ConceptMapEquivalence.Equivalent: return "==";
                case ConceptMapEquivalence.Disjoint: return "!=";
                case ConceptMapEquivalence.Unmatched: return "--";
                case ConceptMapEquivalence.Wider: return "<=";
                case ConceptMapEquivalence.Subsumes: return "<-";
                case ConceptMapEquivalence.Narrower: return ">=";
                case ConceptMapEquivalence.Specializes: return ">-";
                case ConceptMapEquivalence.Inexact: return "~";
                default: return "??";
            }
        }

        private static void renderUses(StringBuilder b, StructureMap map)
        {
            foreach (StructureMap.StructureComponent s in map.Structure)
            {
                b.Append("uses \"");
                b.Append(s.Url);
                b.Append("\" ");
                if (!string.IsNullOrEmpty(s.Alias))
                {
                    b.Append("alias ");
                    b.Append(s.Alias);
                    b.Append(" ");
                }
                b.Append("as ");
                b.Append(s.Mode.GetLiteral());
                renderDoco(b, s.Documentation);
                b.AppendLine();
            }
            if (map.Structure.Any())
                b.AppendLine();
        }

        private static void renderImports(StringBuilder b, StructureMap map)
        {
            if (map.Import.Any())
            {
                foreach (var s in map.Import)
                {
                    b.AppendLine($"imports \"{s}\"");
                }
                b.AppendLine();
            }
        }

        private static void renderGroup(StringBuilder b, StructureMap.GroupComponent g)
        {
            if (!string.IsNullOrEmpty(g.Documentation))
            {
                renderMultilineDoco(b, g.Documentation, 0);
            }
            b.Append("group ");
            b.Append(g.Name);
            b.Append("(");
            bool first = true;
            foreach (StructureMap.InputComponent gi in g.Input)
            {
                if (first)
                    first = false;
                else
                    b.Append(", ");
                b.Append(gi.Mode.GetLiteral());
                b.Append(" ");
                b.Append(gi.Name);
                if (!string.IsNullOrEmpty(gi.Type))
                {
                    b.Append(" : ");
                    b.Append(gi.Type);
                }
            }
            b.Append(")");
            if (!string.IsNullOrEmpty(g.Extends))
            {
                b.Append(" extends ");
                b.Append(g.Extends);
            }

            if (g.TypeMode.HasValue)
            {
                switch (g.TypeMode)
                {
                    case StructureMap.StructureMapGroupTypeMode.Types:
                        b.Append(" <<types>>");
                        break;
                    case StructureMap.StructureMapGroupTypeMode.TypeAndTypes:
                        b.Append(" <<type+>>");
                        break;
                    default: // NONE, NULL
                        break;
                }
            }
            b.Append(" {\r\n");
            foreach (StructureMap.RuleComponent r in g.Rule)
            {
                renderRule(b, r, 2);
            }
            b.Append("}\r\n\r\n");
        }

        private static void renderRule(StringBuilder b, StructureMap.RuleComponent r, int indent)
        {
            if (!string.IsNullOrEmpty(r.Documentation))
            {
                renderMultilineDoco(b, r.Documentation, indent);
            }
            for (int i = 0; i < indent; i++)
                b.Append(' ');
            bool canBeAbbreviated = checkisSimple(r);

            bool first = true;
            foreach (StructureMap.SourceComponent rs in r.Source)
            {
                if (first)
                    first = false;
                else
                    b.Append(", ");
                renderSource(b, rs, canBeAbbreviated);
            }
            if (r.Target.Any())
            {
                b.Append(" -> ");
                first = true;
                foreach (StructureMap.TargetComponent rt in r.Target)
                {
                    if (first)
                        first = false;
                    else
                        b.Append(", ");
                    if (RENDER_MULTIPLE_TARGETS_ONELINE)
                        b.Append(' ');
                    else
                    {
                        b.AppendLine();
                        for (int i = 0; i < indent + 4; i++)
                            b.Append(' ');
                    }
                    renderTarget(b, rt, false);
                }
            }
            else if (r.Target.Any())
            {
                b.Append(" -> ");
                renderTarget(b, r.Target.First(), canBeAbbreviated);
            }
            if (r.Rule.Any())
            {
                b.Append(" then {\r\n");
                foreach (StructureMap.RuleComponent ir in r.Rule)
                {
                    renderRule(b, ir, indent + 2);
                }
                for (int i = 0; i < indent; i++)
                    b.Append(' ');
                b.Append("}");
            }
            else
            {
                if (r.Dependent.Any())
                {
                    b.Append(" then ");
                    first = true;
                    foreach (var rd in r.Dependent)
                    {
                        if (first)
                            first = false;
                        else
                            b.Append(", ");
                        b.Append(rd.Name);
                        b.Append("(");
                        bool ifirst = true;
                        foreach (string rdp in rd.Variable)
                        {
                            if (ifirst)
                                ifirst = false;
                            else
                                b.Append(", ");
                            b.Append(rdp);
                        }
                        b.Append(")");
                    }
                }
            }
            if (!string.IsNullOrEmpty(r.Name))
            {
                string n = ntail(r.Name);
                if (!n.StartsWith("\""))
                    n = "\"" + n + "\"";
                if (!matchesName(n, r.Source))
                {
                    b.Append(" ");
                    b.Append(n);
                }
            }
            b.Append(";");
            b.AppendLine();
        }

        private static bool matchesName(string n, List<StructureMap.SourceComponent> source)
        {
            if (source.Count != 1)
                return false;
            var src = source.First();
            string s = src.Element;
            if (string.IsNullOrEmpty(s))
                return false;
            if (n.Equals(s) || n.Equals("\"" + s + "\""))
                return true;
            if (!string.IsNullOrEmpty(src.Type))
            {
                s = s + "-" + src.Type;
                if (n.Equals(s) || n.Equals("\"" + s + "\""))
                    return true;
            }
            return false;
        }

        private static string ntail(string name)
        {
            if (name == null)
                return null;
            if (name.StartsWith("\""))
            {
                name = name.Substring(1);
                name = name.Substring(0, name.Length - 1);
            }
            return "\"" + (name.Contains(".") ? name.Substring(name.LastIndexOf(".") + 1) : name) + "\"";
        }

        private static bool checkisSimple(StructureMap.RuleComponent r)
        {
            var result =
                  (r.Source.Count() == 1 && r.getSourceFirstRep().Element != null && r.getSourceFirstRep().Variable != null) &&
                  (r.Target.Count() == 1 && r.getTargetFirstRep().Variable != null && (r.getTargetFirstRep().Transform == null || r.getTargetFirstRep().Transform == StructureMap.StructureMapTransform.Create) && r.getTargetFirstRep().Parameter.Count() == 0) &&
                  (r.Dependent.Count() == 0) && (r.Rule.Count() == 0);
            return result;
        }

        public static string sourceToString(StructureMap.SourceComponent r)
        {
            StringBuilder b = new StringBuilder();
            renderSource(b, r, false);
            return b.ToString();
        }

        private static void renderSource(StringBuilder b, StructureMap.SourceComponent rs, bool abbreviate)
        {
            b.Append(rs.Context);
            if (rs.Context.Equals("@search"))
            {
                b.Append('(');
                b.Append(rs.Element);
                b.Append(')');
            }
            else if (!string.IsNullOrEmpty(rs.Element))
            {
                b.Append('.');
                b.Append(rs.Element);
            }
            if (!string.IsNullOrEmpty(rs.Type))
            {
                b.Append(" : ");
                b.Append(rs.Type);
                if (rs.Min.HasValue)
                {
                    b.Append(" ");
                    b.Append(rs.Min);
                    b.Append("..");
                    b.Append(rs.Max);
                }
            }

            if (rs.ListMode.HasValue)
            {
                b.Append(" ");
                b.Append(rs.ListMode.GetLiteral());
            }
            if (rs.DefaultValue != null)
            {
                b.Append(" default ");
                // assert rs.getDefaultValue() is StringType;
                b.Append("\"" + Utilities.escapeJson(rs.DefaultValue.ToString()) + "\"");
            }
            if (!abbreviate && !string.IsNullOrEmpty(rs.Variable))
            {
                b.Append(" as ");
                b.Append(rs.Variable);
            }
            if (!string.IsNullOrEmpty(rs.Condition))
            {
                b.Append(" where ");
                b.Append(rs.Condition);
            }
            if (!string.IsNullOrEmpty(rs.Check))
            {
                b.Append(" check ");
                b.Append(rs.Check);
            }
            if (!string.IsNullOrEmpty(rs.LogMessage))
            {
                b.Append(" log ");
                b.Append(rs.LogMessage);
            }
        }

        public static string targetToString(StructureMap.TargetComponent rt)
        {
            StringBuilder b = new StringBuilder();
            renderTarget(b, rt, false);
            return b.ToString();
        }

        private static void renderTarget(StringBuilder b, StructureMap.TargetComponent rt, bool abbreviate)
        {
            if (!string.IsNullOrEmpty(rt.Context))
            {
                b.Append(rt.Context);
                if (!string.IsNullOrEmpty(rt.Element))
                {
                    b.Append('.');
                    b.Append(rt.Element);
                }
            }
            if (!abbreviate && rt.Transform.HasValue)
            {
                if (!string.IsNullOrEmpty(rt.Context))
                    b.Append(" = ");
                if (rt.Transform == StructureMap.StructureMapTransform.Copy && rt.Parameter.Count() == 1)
                {
                    renderTransformParam(b, rt.Parameter.First());
                }
                else if (rt.Transform == StructureMap.StructureMapTransform.Evaluate && rt.Parameter.Count() == 1)
                {
                    b.Append("(");
                    // TODO: BRIAN chasing up if this requires quotes or not
                    // b.Append("'" + ((FhirString)rt.Parameter.First().Value).ToString() + "'");
                    b.Append(((PrimitiveType)rt.Parameter.First().Value).ToString());
                    b.Append(")");
                }
                else if (rt.Transform == StructureMap.StructureMapTransform.Evaluate && rt.Parameter.Count() == 2)
                {
                    b.Append(rt.Transform.GetLiteral());
                    b.Append("(");
                    b.Append(((PrimitiveType)rt.Parameter.First().Value).ToString());
                    // TODO: BRIAN chasing up if this requires quotes or not
                    // b.Append("'" + ((FhirString)rt.Parameter[1].Value).ToString() + "'");
                    b.Append(((PrimitiveType)rt.Parameter[1].Value).ToString());
                    b.Append(")");
                }
                else
                {
                    b.Append(rt.Transform.GetLiteral());
                    b.Append("(");
                    bool first = true;
                    foreach (var rtp in rt.Parameter)
                    {
                        if (first)
                            first = false;
                        else
                            b.Append(", ");
                        renderTransformParam(b, rtp);
                    }
                    b.Append(")");
                }
            }
            if (!abbreviate && !string.IsNullOrEmpty(rt.Variable))
            {
                b.Append(" as ");
                b.Append(rt.Variable);
            }
            foreach (var lm in rt.ListMode)
            {
                b.Append(" ");
                b.Append(lm.GetLiteral());
                if (lm == StructureMap.StructureMapTargetListMode.Share)
                {
                    b.Append(" ");
                    b.Append(rt.ListRuleId);
                }
            }
        }

        public static string paramToString(StructureMap.ParameterComponent rtp)
        {
            StringBuilder b = new StringBuilder();
            renderTransformParam(b, rtp);
            return b.ToString();
        }

        private static void renderTransformParam(StringBuilder b, StructureMap.ParameterComponent rtp)
        {
            try
            {
                if (rtp.Value is FhirBoolean)
                    b.Append(rtp.Value.ToString());
                else if (rtp.Value is FhirDecimal)
                    b.Append(rtp.Value.ToString());
                else if (rtp.Value is Id)
                    b.Append(rtp.Value.ToString());
                //else if (rtp.hasValueDecimalType())
                //    b.Append(rtp.Value.ToString());
                else if (rtp.Value is Integer)
                    b.Append(rtp.Value.ToString());
                else
                    b.Append("'" + Utilities.escapeJava(rtp.Value.ToString()) + "'");
            }
            catch (FHIRException e)
            {
                System.Diagnostics.Trace.WriteLine(e.StackTrace);
                b.Append("error!");
            }
        }

        private static void renderDoco(StringBuilder b, string doco)
        {
            if (string.IsNullOrEmpty(doco))
                return;
            if (b != null && b.Length > 1 && b[b.Length - 1] != '\n' && b[b.Length - 1] != ' ')
            {
                b.Append(" ");
            }
            b.Append("// ");
            b.Append(doco.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
        }

        private static void renderMultilineDoco(StringBuilder b, String doco, int indent)
        {
            if (Utilities.noString(doco))
                return;
            String[] lines = doco.Replace("\r\n", "\n").Split(new[] { '\r', '\n' });
            foreach (String line in lines)
            {
                for (int i = 0; i < indent; i++)
                    b.Append(' ');
                renderDoco(b, line);
                b.Append("\r\n");
            }
        }

        public StructureMap parse(string text, string srcName)
        {
            FHIRLexer lexer = new FHIRLexer(text, srcName);
            if (lexer.done())
                throw lexer.error("Map Input cannot be empty");
            string comments = lexer.getAllComments();
            lexer.token("map");
            StructureMap result = new StructureMap();
            result.Url = lexer.readConstant("url");
            // result.Id = tail(result.Url); (not in java util code in R4b)
            lexer.token("=");
            result.Name = lexer.readConstant("name");
            if (!string.IsNullOrEmpty(comments))
            {
                // TODO: Parse these comments for code comments
                CommaSeparatedStringBuilder csb = new CommaSeparatedStringBuilder();
                foreach (var line in comments.Split('\n'))
                {
                    int index = line.IndexOf("=");
                    if (line.StartsWith("/ ") && index > 2)
                    {
                        // this is likely a metadata set item
                        string prop = line.Substring(2, index - 3).Trim();
                        string value = line.Substring(index + 1).Trim();
                        switch (prop.ToLower())
                        {
                            case "status":
                                result.Status = EnumUtility.ParseLiteral<PublicationStatus>(value);
                                break;

                            case "title":
                                result.Title = value;
                                break;

                            case "name":
                                // skip this one as it should be read from the name constant prop in the grammar
                                csb.append(line.TrimEnd());
                                break;
                            default:
                                csb.append(line.TrimEnd());
                                break;
                        }
                    }
                    else
                    {
                        csb.append(line.TrimEnd());
                    }
                }
                if (!string.IsNullOrEmpty(csb.ToString()))
                    result.Description = new Markdown(csb.ToString());
            }

            while (lexer.hasToken("conceptmap"))
                parseConceptMap(result, lexer);

            while (lexer.hasToken("uses"))
                parseUses(result, lexer);
            while (lexer.hasToken("imports"))
                parseImports(result, lexer);

            while (!lexer.done())
            {
                parseGroup(result, lexer);
            }

            if (!string.IsNullOrEmpty(text))
            {
                Narrative textNode = new Narrative();
                textNode.Status = Narrative.NarrativeStatus.Additional;
                // XhtmlNode node = new XhtmlNode(NodeType.Element, "div");
                // textNode.setDiv(node);
                // node.pre().tx(text);
                if (text.StartsWith("<div>"))
                    textNode.Div = text;
                else
                    textNode.Div = $"<div>{text}</div>";
            }

            return result;
        }

        private void parseConceptMap(StructureMap result, FHIRLexer lexer)
        {
            lexer.token("conceptmap");
            ConceptMap map = new ConceptMap();
            string id = lexer.readConstant("map id");
            if (id.StartsWith("#"))
                throw lexer.error("Concept Map identifier must start with #");
            map.Id = id;
            map.Status = PublicationStatus.Draft; // todo: how to add this to the text format
            result.Contained.Add(map);
            lexer.token("{");

            //	  lexer.token("source");
            //	  map.setSource(new UriType(lexer.readConstant("source")));
            //	  lexer.token("target");
            //	  map.setSource(new UriType(lexer.readConstant("target")));
            Dictionary<string, string> prefixes = new Dictionary<string, string>();
            while (lexer.hasToken("prefix"))
            {
                lexer.token("prefix");
                string n = lexer.take();
                lexer.token("=");
                string v = lexer.readConstant("prefix url");
                prefixes.Add(n, v);
            }
            while (lexer.hasToken("unmapped"))
            {
                lexer.token("unmapped");
                lexer.token("for");
                string n = readPrefix(prefixes, lexer);
                ConceptMap.GroupComponent g = getGroup(map, n, null);
                lexer.token("=");
                string v = lexer.take();
                if (v.Equals("provided"))
                {
                    if (g.Unmapped == null) g.Unmapped = new ConceptMap.UnmappedComponent();
                    g.Unmapped.Mode = ConceptMap.ConceptMapGroupUnmappedMode.Provided;
                }
                else
                    throw lexer.error("Only unmapped mode PROVIDED is supported at this time");
            }
            while (!lexer.hasToken("}"))
            {
                string srcs = readPrefix(prefixes, lexer);
                lexer.token(":");
                string sc = lexer.getCurrent().StartsWith("\"") ? lexer.readConstant("code") : lexer.take();
                ConceptMapEquivalence eq = readEquivalence(lexer);
                string tgts = (eq != ConceptMapEquivalence.Unmatched) ? readPrefix(prefixes, lexer) : "";
                ConceptMap.GroupComponent g = getGroup(map, srcs, tgts);
                var e = new ConceptMap.SourceElementComponent();
                g.Element.Add(e);
                e.Code = sc;
                if (e.Code.StartsWith("\""))
                    e.Code = lexer.processConstant(e.Code);
                var tgt = new ConceptMap.TargetElementComponent();
                e.Target.Add(tgt);
                tgt.Equivalence = eq;
                if (tgt.Equivalence != ConceptMapEquivalence.Unmatched)
                {
                    lexer.token(":");
                    tgt.Code = lexer.take();
                    if (tgt.Code.StartsWith("\""))
                        tgt.Code = lexer.processConstant(tgt.Code);
                }
                tgt.Comment = lexer.getFirstComment();
            }
            lexer.token("}");
        }

        private ConceptMap.GroupComponent getGroup(ConceptMap map, string srcs, string tgts)
        {
            foreach (ConceptMap.GroupComponent grp in map.Group)
            {
                if (grp.Source.Equals(srcs))
                    if (grp.Target == null || tgts == null || tgts.Equals(grp.Target))
                    {
                        if (grp.Target == null && tgts != null)
                            grp.Target = tgts;
                        return grp;
                    }
            }
            ConceptMap.GroupComponent group = new ConceptMap.GroupComponent()
            {
                Source = srcs,
                Target = tgts,
            };
            map.Group.Add(group);
            return group;
        }

        private string readPrefix(Dictionary<string, string> prefixes, FHIRLexer lexer)
        {
            string prefix = lexer.take();
            if (!prefixes.ContainsKey(prefix))
                throw lexer.error("Unknown prefix '" + prefix + "'");
            return prefixes[prefix];
        }

        private ConceptMapEquivalence readEquivalence(FHIRLexer lexer)
        {
            string token = lexer.take();
            if (token.Equals("-"))
                return ConceptMapEquivalence.Relatedto;
            if (token.Equals("="))
                return ConceptMapEquivalence.Equal;
            if (token.Equals("=="))
                return ConceptMapEquivalence.Equivalent;
            if (token.Equals("!="))
                return ConceptMapEquivalence.Disjoint;
            if (token.Equals("--"))
                return ConceptMapEquivalence.Unmatched;
            if (token.Equals("<="))
                return ConceptMapEquivalence.Wider;
            if (token.Equals("<-"))
                return ConceptMapEquivalence.Subsumes;
            if (token.Equals(">="))
                return ConceptMapEquivalence.Narrower;
            if (token.Equals(">-"))
                return ConceptMapEquivalence.Specializes;
            if (token.Equals("~"))
                return ConceptMapEquivalence.Inexact;
            throw lexer.error("Unknown equivalence token '" + token + "'");
        }

        private void parseUses(StructureMap result, FHIRLexer lexer)
        {
            lexer.token("uses");
            StructureMap.StructureComponent st = new StructureMap.StructureComponent();
            result.Structure.Add(st);
            st.Url = lexer.readConstant("url");
            if (lexer.hasToken("alias"))
            {
                lexer.token("alias");
                st.Alias = lexer.take();
            }
            lexer.token("as");
            st.Mode = EnumUtility.ParseLiteral<StructureMap.StructureMapModelMode>(lexer.take());
            lexer.skipToken(";");
            st.Documentation = lexer.getFirstComment();
        }

        private void parseImports(StructureMap result, FHIRLexer lexer)
        {
            lexer.token("imports");
            result.ImportElement.Add(new Canonical(lexer.readConstant("url")));
            lexer.skipToken(";");
            lexer.getFirstComment();
        }

        private void parseGroup(StructureMap result, FHIRLexer lexer)
        {
            String comment = lexer.getAllComments();
            lexer.token("group");
            StructureMap.GroupComponent group = new StructureMap.GroupComponent();
            if (!string.IsNullOrEmpty(comment))
                group.Documentation = comment;
            result.Group.Add(group);
            bool newFmt = false;
            if (lexer.hasToken("for"))
            {
                lexer.token("for");
                if ("type".Equals(lexer.getCurrent()))
                {
                    lexer.token("type");
                    lexer.token("+");
                    lexer.token("types");
                    group.TypeMode = StructureMap.StructureMapGroupTypeMode.TypeAndTypes;
                }
                else
                {
                    lexer.token("types");
                    group.TypeMode = StructureMap.StructureMapGroupTypeMode.Types;
                }
            }
            else
                group.TypeMode = StructureMap.StructureMapGroupTypeMode.None;
            group.Name = lexer.take();
            if (lexer.hasToken("("))
            {
                newFmt = true;
                lexer.take();
                while (!lexer.hasToken(")"))
                {
                    parseInput(group, lexer, true);
                    if (lexer.hasToken(","))
                        lexer.token(",");
                }
                lexer.take();
            }
            if (lexer.hasToken("extends"))
            {
                lexer.next();
                group.Extends = lexer.take();
            }
            if (newFmt)
            {
                group.TypeMode = StructureMap.StructureMapGroupTypeMode.None;
                if (lexer.hasToken("<"))
                {
                    lexer.token("<");
                    lexer.token("<");
                    if (lexer.hasToken("types"))
                    {
                        group.TypeMode = StructureMap.StructureMapGroupTypeMode.Types;
                        lexer.token("types");
                    }
                    else
                    {
                        lexer.token("type");
                        lexer.token("+");
                        group.TypeMode = StructureMap.StructureMapGroupTypeMode.TypeAndTypes;
                    }
                    lexer.token(">");
                    lexer.token(">");
                }
                lexer.token("{");
            }
            if (newFmt)
            {
                while (!lexer.hasToken("}"))
                {
                    if (lexer.done())
                        throw lexer.error("premature termination expecting 'endgroup'");
                    parseRule(result, group.Rule, lexer, true);
                }
            }
            else
            {
                while (lexer.hasToken("input"))
                    parseInput(group, lexer, false);
                while (!lexer.hasToken("endgroup"))
                {
                    if (lexer.done())
                        throw lexer.error("premature termination expecting 'endgroup'");
                    parseRule(result, group.Rule, lexer, false);
                }
            }
            lexer.next();
            if (newFmt && lexer.hasToken(";"))
                lexer.next();
        }

        private void parseInput(StructureMap.GroupComponent group, FHIRLexer lexer, bool newFmt)
        {
            var input = new StructureMap.InputComponent();
            group.Input.Add(input);
            if (newFmt)
            {
                input.Mode = EnumUtility.ParseLiteral<StructureMap.StructureMapInputMode>(lexer.take());
            }
            else
                lexer.token("input");
            input.Name = lexer.take();
            if (lexer.hasToken(":"))
            {
                lexer.token(":");
                input.Type = lexer.take();
            }
            if (!newFmt)
            {
                lexer.token("as");
                input.Mode = EnumUtility.ParseLiteral<StructureMap.StructureMapInputMode>(lexer.take());
                input.Documentation = lexer.getAllComments(); ;
                lexer.skipToken(";");
            }
        }

        private void parseRule(StructureMap map, List<StructureMap.RuleComponent> list, FHIRLexer lexer, bool newFmt)
        {
            StructureMap.RuleComponent rule = new StructureMap.RuleComponent();
            list.Add(rule);
            if (!newFmt)
            {
                rule.Name = lexer.takeDottedToken();
                lexer.token(":");
                lexer.token("for");
            }
            else
            {
                rule.Documentation = lexer.getFirstComment();
            }
            bool done = false;
            while (!done)
            {
                parseSource(rule, lexer);
                done = !lexer.hasToken(",");
                if (!done)
                    lexer.next();
            }
            if ((newFmt && lexer.hasToken("->")) || (!newFmt && lexer.hasToken("make")))
            {
                lexer.token(newFmt ? "->" : "make");
                done = false;
                while (!done)
                {
                    parseTarget(rule, lexer);
                    done = !lexer.hasToken(",");
                    if (!done)
                        lexer.next();
                }
            }
            if (lexer.hasToken("then"))
            {
                lexer.token("then");
                if (lexer.hasToken("{"))
                {
                    lexer.token("{");
                    while (!lexer.hasToken("}"))
                    {
                        if (lexer.done())
                            throw lexer.error("premature termination expecting '}' in nested group");
                        parseRule(map, rule.Rule, lexer, newFmt);
                    }
                    lexer.token("}");
                }
                else
                {
                    done = false;
                    while (!done)
                    {
                        parseRuleReference(rule, lexer);
                        done = !lexer.hasToken(",");
                        if (!done)
                            lexer.next();
                    }
                }
            }
            else if (string.IsNullOrEmpty(rule.Documentation) && lexer.hasComments())
            {
                rule.Documentation = lexer.getFirstComment();
            }
            if (isSimpleSyntax(rule))
            {
                rule.getSourceFirstRep().Variable = AUTO_VAR_NAME;
                rule.getTargetFirstRep().Variable = AUTO_VAR_NAME;
                rule.getTargetFirstRep().Transform = StructureMap.StructureMapTransform.Create; // with no parameter - e.g. imply what is to be created
                                                                                                // no dependencies - imply what is to be done based on types
            }
            if (newFmt)
            {
                if (lexer.isConstant())
                {
                    if (lexer.isStringConstant())
                    {
                        rule.Name = lexer.readConstant("ruleName");
                    }
                    else
                    {
                        rule.Name = lexer.take();
                    }
                }
                else
                {
                    //if (rule.Source.Count() != 1 || rule.getSourceFirstRep().Element == null)
                    //    throw lexer.error("Complex rules must have an explicit name");
                    if (rule.getSourceFirstRep().Type != null)
                        rule.Name = rule.getSourceFirstRep().Element + "-" + rule.getSourceFirstRep().Type;
                    else
                        rule.Name = rule.getSourceFirstRep().Element;
                }
                lexer.token(";");

                // only required for R4, R5 has removed this constraint
                if (string.IsNullOrEmpty(rule.Name) && ModelInfo.Version.StartsWith("4"))
                    rule.Name = Guid.NewGuid().ToFhirId();
            }
        }

        private bool isSimpleSyntax(StructureMap.RuleComponent rule)
        {
            if (rule.Source.Count() != 1 || rule.Target.Count() != 1)
                return false;
            var sourceFirstRep = rule.getSourceFirstRep();
            var targetFirstRep = rule.getTargetFirstRep();
            return
                (rule.Source.Count() == 1 && sourceFirstRep.Context != null && sourceFirstRep.Element != null && sourceFirstRep.Variable == null) &&
                (rule.Target.Count() == 1 && targetFirstRep.Context != null && targetFirstRep.Element != null && targetFirstRep.Variable == null && !targetFirstRep.Parameter.Any()) &&
                (rule.Dependent.Count() == 0 && rule.Rule.Count() == 0);
        }

        private void parseRuleReference(StructureMap.RuleComponent rule, FHIRLexer lexer)
        {
            var refD = new StructureMap.DependentComponent();
            rule.Dependent.Add(refD);
            refD.Name = lexer.take();
            lexer.token("(");
            bool done = false;
            while (!done)
            {
                refD.VariableElement.Add(new FhirString(lexer.take()));
                done = !lexer.hasToken(",");
                if (!done)
                    lexer.next();
            }
            lexer.token(")");
        }

        private void parseSource(StructureMap.RuleComponent rule, FHIRLexer lexer)
        {
            var source = new StructureMap.SourceComponent();
            rule.Source.Add(source);
            source.Context = lexer.take();
            if (source.Context.Equals("search") && lexer.hasToken("("))
            {
                source.Context = "@search";
                lexer.take();
                ExpressionNode node = fpe.parse(lexer);
                source.setUserData(MAP_SEARCH_EXPRESSION, node);
                source.Element = node.ToString();
                lexer.token(")");
            }
            else if (lexer.hasToken("."))
            {
                lexer.token(".");
                source.Element = lexer.take();
            }
            if (lexer.hasToken(":"))
            {
                // type and cardinality
                lexer.token(":");
                source.Type = lexer.takeDottedToken();
                if (!lexer.hasToken("as", "first", "last", "not_first", "not_last", "only_one", "default"))
                {
                    source.Min = lexer.takeInt();
                    lexer.token("..");
                    source.Max = lexer.take();
                }
            }
            if (lexer.hasToken("default"))
            {
                lexer.token("default");
                source.DefaultValue = new FhirString(lexer.readConstant("default value"));
            }
            if (Utilities.existsInList(lexer.getCurrent(), "first", "last", "not_first", "not_last", "only_one"))
                source.ListMode = EnumUtility.ParseLiteral<StructureMap.StructureMapSourceListMode>(lexer.take());

            if (lexer.hasToken("as"))
            {
                lexer.take();
                source.Variable = lexer.take();
            }
            if (lexer.hasToken("where"))
            {
                lexer.take();
                ExpressionNode node = fpe.parse(lexer);
                source.setUserData(MAP_WHERE_EXPRESSION, node);
                source.Condition = node.ToString();
            }
            if (lexer.hasToken("check"))
            {
                lexer.take();
                ExpressionNode node = fpe.parse(lexer);
                source.setUserData(MAP_WHERE_CHECK, node);
                source.Check = node.ToString();
            }
            if (lexer.hasToken("log"))
            {
                lexer.take();
                ExpressionNode node = fpe.parse(lexer);
                source.setUserData(MAP_WHERE_LOG, node);
                source.LogMessage = node.ToString();
            }
        }

        private void parseTarget(StructureMap.RuleComponent rule, FHIRLexer lexer)
        {
            var target = new StructureMap.TargetComponent();
            rule.Target.Add(target);
            string start = lexer.take();
            if (lexer.hasToken("."))
            {
                target.Context = start;
                target.ContextType = StructureMap.StructureMapContextType.Variable;
                start = null;
                lexer.token(".");
                target.Element = lexer.take();
            }
            string name;
            bool isConstant = false;
            if (lexer.hasToken("="))
            {
                if (start != null)
                    target.Context = start;
                lexer.token("=");
                isConstant = lexer.isConstant();
                name = lexer.take();
            }
            else
                name = start;

            if ("(".Equals(name))
            {
                // inline fluentpath expression
                target.Transform = StructureMap.StructureMapTransform.Evaluate;
                // consider if this *should* prefix the expression at this stage with the %
                ExpressionNode node = fpe.parse(lexer);
                target.addParameter().Value = new FhirString(node.ToString());
                if (!node.getName().StartsWith("%"))
                    node.setName("%" + node.getName());
                target.setUserData(MAP_EXPRESSION, node);
                lexer.token(")");
            }
            else if (lexer.hasToken("("))
            {
                target.Transform = EnumUtility.ParseLiteral<StructureMap.StructureMapTransform>(name);
                lexer.token("(");
                if (target.Transform == StructureMap.StructureMapTransform.Evaluate)
                {
                    parseParameter(target, lexer);
                    lexer.token(",");
                    ExpressionNode node = fpe.parse(lexer);
                    target.setUserData(MAP_EXPRESSION, node);
                    target.addParameter().Value = new FhirString(node.ToString());
                }
                else
                {
                    while (!lexer.hasToken(")"))
                    {
                        parseParameter(target, lexer);
                        if (!lexer.hasToken(")"))
                            lexer.token(",");
                    }
                }
                lexer.token(")");
            }
            else if (name != null)
            {
                target.Transform = StructureMap.StructureMapTransform.Copy;
                if (!isConstant)
                {
                    string id = name;
                    while (lexer.hasToken("."))
                    {
                        id = id + lexer.take() + lexer.take();
                    }
                    target.addParameter().Value = new Id(id);
                }
                else
                    target.addParameter().Value = readConstant(name, lexer);
            }
            if (lexer.hasToken("as"))
            {
                lexer.take();
                target.Variable = lexer.take();
            }
            ReadOnlyCollection<string> targetListModes = new ReadOnlyCollection<string>(new[] { "first", "last", "share", "collate" });
            while (targetListModes.Contains(lexer.getCurrent()))
            {
                if (lexer.getCurrent().Equals("share"))
                {
                    target.ListModeElement.Add(new Code<StructureMap.StructureMapTargetListMode>(StructureMap.StructureMapTargetListMode.Share));
                    lexer.next();
                    target.ListRuleId = lexer.take();
                }
                else
                {
                    if (lexer.getCurrent().Equals("first"))
                        target.ListModeElement.Add(new Code<StructureMap.StructureMapTargetListMode>(StructureMap.StructureMapTargetListMode.First));
                    else
                        target.ListModeElement.Add(new Code<StructureMap.StructureMapTargetListMode>(StructureMap.StructureMapTargetListMode.Last));
                    lexer.next();
                }
            }
        }

        private void parseParameter(StructureMap.TargetComponent target, FHIRLexer lexer)
        {
            if (!lexer.isConstant())
            {
                target.addParameter().Value = new Id(lexer.take());
            }
            else if (lexer.isStringConstant())
                target.addParameter().Value = new FhirString(lexer.readConstant("??"));
            else
            {
                target.addParameter().Value = readConstant(lexer.take(), lexer);
            }
        }

        private DataType readConstant(string s, FHIRLexer lexer)
        {
            if (s == "true")
                return new FhirBoolean(true);
            if (s == "false")
                return new FhirBoolean(false);
            if (int.TryParse(s, out var intVal))
                return new Integer(intVal);
            if (decimal.TryParse(s, out decimal decVal))
                return new FhirDecimal(decVal);

            return new FhirString(lexer.processConstant(s));
        }

        private string tail(string url)
        {
            return url.Substring(url.LastIndexOf("/") + 1);
        }

    }
}