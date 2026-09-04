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
// And also https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r5/src/main/java/org/hl7/fhir/r5/utils/structuremap/StructureMapUtilities.java

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
    public class StructureMapUtilitiesRender
    {
        private const bool RENDER_MULTIPLE_TARGETS_ONELINE = true;

        public StructureMapUtilitiesRender()
        {
        }

        public static string render(StructureMap map)
        {
            StringBuilder b = new StringBuilder();
#if FHIR_R5
            b.AppendLine($"/// url = \"{map.Url}\"");
            if (!string.IsNullOrEmpty(map.Name))
                b.AppendLine($"/// name = \"{Utilities.escapeJson(map.Name)}\"");
#else
            b.Append("map \"");
            b.Append(map.Url);
            b.Append("\" = \"");
            b.Append(Utilities.escapeJson(map.Name));
            b.Append("\"\r\n\r\n");
#endif
            if (!string.IsNullOrEmpty(map.Title))
                b.AppendLine($"/// title = \"{Utilities.escapeJson(map.Title)}\"");
            if (map.Status.HasValue)
                b.AppendLine($"/// status = \"{map.Status.GetLiteral()}\"");
            if (!string.IsNullOrEmpty(map.Description))
            {
                renderMultilineDoco(b, map.Description, 0);
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
#if FHIR_R5
                    var e = ce.getTargetFirstRep().Relationship;
#else
                    var e = ce.getTargetFirstRep().Equivalence;
#endif
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

#if FHIR_R5
        private static string getChar(ConceptMap.ConceptMapRelationship equivalence)
        {
            switch (equivalence)
            {
                case ConceptMap.ConceptMapRelationship.RelatedTo: return "-";
                case ConceptMap.ConceptMapRelationship.Equivalent: return "==";
                case ConceptMap.ConceptMapRelationship.NotRelatedTo: return "!=";
                case ConceptMap.ConceptMapRelationship.SourceIsNarrowerThanTarget: return "<=";
                case ConceptMap.ConceptMapRelationship.SourceIsBroaderThanTarget: return ">=";
                default: return "??";
            }
        }
#else
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
#endif

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
                    renderTarget(b, rt, canBeAbbreviated);
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
#if FHIR_R5
                        foreach (var rdp in rd.Parameter)
                        {
                            if (ifirst)
                                ifirst = false;
                            else
                                b.Append(", ");
                            switch (rdp.Value) 
                            {
                                case FhirString fs:
                                    b.Append(fs.Value);
                                    break;
                                case Id id:
                                    b.Append(id.Value);
                                    break;
                                case FhirDecimal fd:
                                    b.Append(fd.Value);
                                    break;
                                case Integer fi:
                                    b.Append(fi.Value);
                                    break;
                                default:
                                    b.Append("error!");
                                    break;
                            }
                        }
#else
                        foreach (string rdp in rd.Variable)
                        {
                            if (ifirst)
                                ifirst = false;
                            else
                                b.Append(", ");
                            b.Append(rdp);
                        }
#endif
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
            b.Append("//");
            if (!doco.StartsWith("/"))
                b.Append(" ");
            b.Append(doco.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
        }

        private static void renderMultilineDoco(StringBuilder b, String doco, int indent)
        {
            if (Utilities.noString(doco))
                return;
            String[] lines = doco.Replace("\r\n", "\n").Split(new[] { '\r', '\n' });
            foreach (String line in lines)
            {
                if (!line.StartsWith("/"))
                    for (int i = 0; i < indent; i++)
                        b.Append(' ');
                renderDoco(b, line);
                b.Append("\r\n");
            }
        }
    }
}