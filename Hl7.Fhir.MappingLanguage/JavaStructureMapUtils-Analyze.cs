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
// (The Analyze portions)

// remember group resolution
// trace - account for which wasn't transformed in the source

using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using static Hl7.Fhir.MappingLanguage.FHIRPathEngineOriginal; // for the IEvaluationContext
using static Hl7.Fhir.MappingLanguage.TypeDetails;

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
    public class StructureMapUtilitiesAnalyze
    {
        public class ResolvedGroup
        {
            public StructureMap.GroupComponent target;
            public StructureMap targetMap;
        }
        public const string MAP_WHERE_CHECK = "map.where.check";
        public const string MAP_WHERE_LOG = "map.where.log";
        public const string MAP_WHERE_EXPRESSION = "map.where.expression";
        public const string MAP_SEARCH_EXPRESSION = "map.search.expression";
        public const string MAP_EXPRESSION = "map.transform.expression";
        private const bool RENDER_MULTIPLE_TARGETS_ONELINE = true;
        private const string AUTO_VAR_NAME = "vvv";

        public interface ITransformerServices
        {
            //    public bool validateByValueSet(Coding code, string valuesetId);
            public void log(string message); // log internal progress
            public Base createType(Object appInfo, string name);
            public Base createResource(Object appInfo, Base res, bool atRootofTransform); // an already created resource is provided; this is to identify/store it
            public Coding translate(Object appInfo, Coding source, string conceptMapUrl);
            //    public Coding translate(Coding code)
            //    ValueSet validation operation
            //    Translation operation
            //    Lookup another tree of data
            //    Create an instance tree
            //    Return the correct string format to refer to a tree (input or output)
            public Base resolveReference(Object appContext, string url);
            public List<Base> performSearch(Object appContext, string url);
        }

        private class FFHIRPathHostServices : IEvaluationContext
        {
            public List<Base> resolveConstant(Object appContext, string name, bool beforeContext)
            {
                Variables vars = (Variables)appContext;
                Base res = vars.get(VariableMode.INPUT, name);
                if (res == null)
                    res = vars.get(VariableMode.OUTPUT, name);
                List<Base> result = new List<Base>();
                if (res != null)
                    result.Add(res);
                return result;
            }

            // @Override
            public TypeDetails resolveConstantType(Object appContext, string name)
            {
                if (!(appContext is VariablesForProfiling))
                    throw new Exception("Internal Logic Error (wrong type '" + appContext.GetType().Name + "' in resolveConstantType)");
                VariablesForProfiling vars = (VariablesForProfiling)appContext;
                VariableForProfiling v = vars.get(null, name);
                if (v == null)
                    throw new PathEngineException("Unknown variable '" + name + "' from variables " + vars.summary());
                return v.property.types;
            }

            // @Override
            public bool log(string argument, List<Base> focus)
            {
                throw new Exception("Not Implemented Yet");
            }

            // @Override
            public FHIRPathEngineOriginal.FunctionDetails resolveFunction(string functionName)
            {
                return null; // throw new Exception("Not Implemented Yet");
            }

            // @Override
            public TypeDetails checkFunction(Object appContext, string functionName, List<TypeDetails> parameters)
            {
                throw new Exception("Not Implemented Yet");
            }

            // @Override
            public List<Base> executeFunction(Object appContext, List<Base> focus, string functionName, List<List<Base>> parameters)
            {
                throw new Exception("Not Implemented Yet");
            }

            // @Override
            public Base resolveReference(Object appContext, string url)
            {
                // TODO: BRIAN wire in the Firely Resolver (is the a resource or canonical resolve?)
                throw new NotImplementedException("Not done yet (FFHIRPathHostServices.conformsToProfile), when item is element");
                //if (services == null)
                //    return null;
                //return services.resolveReference(appContext, url);
            }

            // @Override
            public bool conformsToProfile(Object appContext, Base item, string url)
            {
                // TODO: BRIAN wire in the Firely Validator
                //IResourceValidator val = worker.newValidator();
                //List<ValidationMessage> valerrors = new List<ValidationMessage>();
                //if (item is Resource)
                //{
                //    val.validate(appContext, valerrors, (Resource)item, url);
                //    bool ok = true;
                //    foreach (ValidationMessage v in valerrors)
                //        ok = ok && v.getLevel().isError();
                //    return ok;
                //}
                throw new NotImplementedException("Not done yet (FFHIRPathHostServices.conformsToProfile), when item is element");
            }

            // @Override
            public ValueSet resolveValueSet(Object appContext, string url)
            {
                throw new Exception("Not Implemented Yet");
            }

        }
        private IWorkerContext worker;
        private FHIRPathEngine fpe;
        private ITransformerServices services;
        private ProfileKnowledgeProvider pkp;
        private Dictionary<string, int> ids = new Dictionary<string, int>();
        private TerminologyServiceOptions terminologyServiceOptions = new TerminologyServiceOptions();

        public StructureMapUtilitiesAnalyze(IWorkerContext worker, ITransformerServices services, ProfileKnowledgeProvider pkp)
        {
            this.worker = worker;
            this.services = services;
            this.pkp = pkp;
            fpe = new FHIRPathEngine(worker);
            fpe.setHostServices(new FFHIRPathHostServices());
        }

        public StructureMapUtilitiesAnalyze(IWorkerContext worker, ITransformerServices services)
        {
            this.worker = worker;
            this.services = services;
            fpe = new FHIRPathEngine(worker);
            fpe.setHostServices(new FFHIRPathHostServices());
        }

        public StructureMapUtilitiesAnalyze(IWorkerContext worker)
        {
            this.worker = worker;
            fpe = new FHIRPathEngine(worker);
            fpe.setHostServices(new FFHIRPathHostServices());
        }

        #region << Render FML from StructureMap >>
        public static string render(StructureMap map)
        {
            StringBuilder b = new StringBuilder();
            b.Append("map \"");
            b.Append(map.Url);
            b.Append("\" = \"");
            b.Append(Utilities.escapeJson(map.Name));
            b.Append("\"\r\n\r\n");

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


        public interface IWorkerContext
        {
            T fetchResource<T>(string url) where T : Resource;
            ValueSet.ExpansionComponent expandVS(ValueSet vs, bool v1, bool v2);
            string getOverrideVersionNs();
            ValidationResult validateCode(TerminologyServiceOptions terminologyServiceOptions, string system, string code, object value);
            StructureDefinition fetchTypeDefinition(string code);
            ValueSet fetchResourceWithException(string v, string uri);
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
                    b.Append(ce.getTargetFirstRep().Equivalence.GetLiteral());
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
                b.AppendLine();
                renderDoco(b, s.Documentation);
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

        public static string groupToString(StructureMap.GroupComponent g)
        {
            StringBuilder b = new StringBuilder();
            renderGroup(b, g);
            return b.ToString();
        }

        private static void renderGroup(StringBuilder b, StructureMap.GroupComponent g)
        {
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

        public static string ruleToString(StructureMap.RuleComponent r)
        {
            StringBuilder b = new StringBuilder();
            renderRule(b, r, 0);
            return b.ToString();
        }

        private static void renderRule(StringBuilder b, StructureMap.RuleComponent r, int indent)
        {
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
                renderDoco(b, r.Documentation);
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
            renderDoco(b, r.Documentation);
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
            return
                  (r.Source.Count() == 1 && r.getSourceFirstRep().Element != null && r.getSourceFirstRep().Variable != null) &&
                  (r.Target.Count() == 1 && r.getTargetFirstRep().Variable != null && (r.getTargetFirstRep().Transform == null || r.getTargetFirstRep().Transform == StructureMap.StructureMapTransform.Create) && r.getTargetFirstRep().Parameter.Count() == 0) &&
                  (r.Dependent.Count() == 0) && (r.Rule.Count() == 0);
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
                if (rt.ContextType == StructureMap.StructureMapContextType.Type)
                    b.Append("@");
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
                    b.Append(((FhirString)rt.Parameter.First().Value).ToString());
                    b.Append(")");
                }
                else if (rt.Transform == StructureMap.StructureMapTransform.Evaluate && rt.Parameter.Count() == 2)
                {
                    b.Append(rt.Transform.GetLiteral());
                    b.Append("(");
                    b.Append(((Id)rt.Parameter.First().Value).ToString());
                    b.Append("'" + ((FhirString)rt.Parameter[1].Value).ToString() + "'");
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
            b.Append(" // ");
            b.Append(doco.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " "));
        }
        #endregion

        #region << Parse FML into StructureMap >>
        public StructureMap parse(string text, string srcName)
        {
            FHIRLexer lexer = new FHIRLexer(text, srcName);
            if (lexer.done())
                throw lexer.error("Map Input cannot be empty");
            lexer.skipComments();
            lexer.token("map");
            StructureMap result = new StructureMap();
            result.Url = lexer.readConstant("url");
            result.Id = tail(result.Url);
            lexer.token("=");
            result.Name = lexer.readConstant("name");
            lexer.skipComments();

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
            lexer.skipComments();
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
                if (lexer.hasComment())
                    tgt.Comment = lexer.take().Substring(2).Trim();
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
            if (lexer.hasComment())
            {
                st.Documentation = lexer.take().Substring(2).Trim();
            }
            lexer.skipComments();
        }

        private void parseImports(StructureMap result, FHIRLexer lexer)
        {
            lexer.token("imports");
            result.ImportElement.Add(new Canonical(lexer.readConstant("url")));
            lexer.skipToken(";");
            if (lexer.hasComment())
            {
                lexer.next();
            }
            lexer.skipComments();
        }

        private void parseGroup(StructureMap result, FHIRLexer lexer)
        {
            lexer.token("group");
            StructureMap.GroupComponent group = new StructureMap.GroupComponent();
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
            lexer.skipComments();
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
            lexer.skipComments();
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
                if (lexer.hasComment())
                {
                    input.Documentation = lexer.take().Substring(2).Trim();
                }
                lexer.skipToken(";");
                lexer.skipComments();
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
                    if (lexer.hasComment())
                    {
                        rule.Documentation = lexer.take().Substring(2).Trim();
                    }
                    lexer.skipComments();
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
            else if (lexer.hasComment())
            {
                rule.Documentation = lexer.take().Substring(2).Trim();
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
                    if (rule.Source.Count() != 1 || rule.getSourceFirstRep().Element == null)
                        throw lexer.error("Complex rules must have an explicit name");
                    if (rule.getSourceFirstRep().Type != null)
                        rule.Name = rule.getSourceFirstRep().Element + "-" + rule.getSourceFirstRep().Type;
                    else
                        rule.Name = rule.getSourceFirstRep().Element;
                }
                lexer.token(";");
            }
            lexer.skipComments();
        }

        private bool isSimpleSyntax(StructureMap.RuleComponent rule)
        {
            if (rule.Source.Count() != 1 || rule.Target.Count() != 1)
                return false;
            var sourceFirstRep = rule.getSourceFirstRep();
            var targetFirstRep = rule.getTargetFirstRep();
            return
                (rule.Source.Count() == 1 && sourceFirstRep.Context != null && sourceFirstRep.Element != null && sourceFirstRep.Variable == null) &&
                (rule.Target.Count() == 1 && targetFirstRep.Context != null && targetFirstRep.Element != null && targetFirstRep.Variable == null && targetFirstRep.Parameter == null) &&
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
                source.setUserData(MAP_WHERE_CHECK, node);
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
                ExpressionNode node = fpe.parse(lexer);
                target.setUserData(MAP_EXPRESSION, node);
                target.addParameter().Value = new FhirString(node.ToString());
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
        #endregion

        public enum VariableMode
        {
            INPUT, OUTPUT, SHARED
        }

        public class Variable
        {
            private VariableMode _mode;
            private string _name;
            private Base _object;
            public Variable(VariableMode mode, string name, Base obj)
            {

                this._mode = mode;
                this._name = name;
                this._object = obj;
            }
            public VariableMode Mode
            {
                get { return _mode; }
            }
            public string Name
            {
                get
                {
                    return _name;
                }
            }
            public Base getObject()
            {
                return _object;
            }
            public string summary()
            {
                if (_object == null)
                    return null;
                else if (_object is PrimitiveType)
                    return _name + ": \"" + ((PrimitiveType)_object).ToString() + '"';
                else
                    return _name + ": (" + _object.TypeName + ")";
            }
        }

        public class Variables
        {
            private List<Variable> list = new List<Variable>();

            public void add(VariableMode mode, string name, Base obj)
            {
                Variable vv = null;
                foreach (Variable v in list)
                    if ((v.Mode == mode) && v.Name.Equals(name))
                        vv = v;
                if (vv != null)
                    list.Remove(vv);
                list.Add(new Variable(mode, name, obj));
            }

            public Variables copy()
            {
                Variables result = new Variables();
                result.list.AddRange(list);
                return result;
            }

            public Base get(VariableMode mode, string name)
            {
                foreach (Variable v in list)
                    if ((v.Mode == mode) && v.Name.Equals(name))
                        return v.getObject();
                return null;
            }

            public string summary()
            {
                var s = new List<string>();
                var t = new List<string>();
                var sh = new List<string>();
                foreach (Variable v in list)
                    switch (v.Mode)
                    {
                        case VariableMode.INPUT:
                            s.Add(v.summary());
                            break;
                        case VariableMode.OUTPUT:
                            t.Add(v.summary());
                            break;
                        case VariableMode.SHARED:
                            sh.Add(v.summary());
                            break;
                    }
                return "source variables [" + string.Join(",", s) + "], target variables [" + string.Join(",", t) + "], shared variables [" + string.Join(",", sh) + "]";
            }

        }

        public class TransformContext
        {
            private Object appInfo;

            public TransformContext(Object appInfo)
            {

                this.appInfo = appInfo;
            }

            public Object getAppInfo()
            {
                return appInfo;
            }

        }

        private void log(string cnt)
        {
            if (services != null)
                services.log(cnt);
            else
                System.Diagnostics.Trace.WriteLine(cnt);
        }

        /**
         * Given an item, return all the children that conform to the pattern described in name
         *
         * Possible patterns:
         *  - a simple name (which may be the base of a name with [] e.g. value[x])
         *  - a name with a type replacement e.g. valueCodeableConcept
         *  - * which means all children
         *  - ** which means all descendents
         *
         * @param item
         * @param name
         * @param result
         * @throws FHIRException
         */


        private Coding buildCoding(string uri, string code)
        {
            // if we can get this as a valueSet, we will
            string system = null;
            string display = null;
            ValueSet vs = Utilities.noString(uri) ? null : worker.fetchResourceWithException("ValueSet", uri);
            if (vs != null)
            {
                var vse = worker.expandVS(vs, true, false);
                //if (vse.getError() != null)
                //    throw new FHIRException(vse.getError());
                CommaSeparatedStringBuilder b = new CommaSeparatedStringBuilder();
                foreach (var t in vse.Contains)
                {
                    if (!string.IsNullOrEmpty(t.Code))
                        b.append(t.Code);
                    if (code.Equals(t.Code) && !string.IsNullOrEmpty(t.System))
                    {
                        system = t.System;
                        display = t.Display;
                        break;
                    }
                    if (code.Equals(t.Display, StringComparison.InvariantCultureIgnoreCase) && !string.IsNullOrEmpty(t.System))
                    {
                        system = t.System;
                        display = t.Display;
                        break;
                    }
                }
                if (system == null)
                    throw new FHIRException("The code '" + code + "' is not in the value set '" + uri + "' (valid codes: " + b.ToString() + "; also checked displays)");
            }
            else
                system = uri;
            ValidationResult vr = worker.validateCode(terminologyServiceOptions, system, code, null);
            if (vr != null && vr.getDisplay() != null)
                display = vr.getDisplay();
            return new Coding(system, code, display);
        }


        private string getParamString(Variables vars, StructureMap.ParameterComponent parameter)
        {
            Base b = getParam(vars, parameter);
            if (b is PrimitiveType pt)
                return pt.ToString();
            return null;
        }

        private Base getParam(Variables vars, StructureMap.ParameterComponent parameter)
        {
            var p = parameter.Value as Id;
            if (p == null)
                return parameter.Value;

            string n = p.Value;
            Base b = vars.get(VariableMode.INPUT, n);
            if (b == null)
                b = vars.get(VariableMode.OUTPUT, n);
            if (b == null)
                throw new DefinitionException("Variable " + n + " not found (" + vars.summary() + ")");
            return b;
        }

        private class SourceElementComponentWrapper
        {
            internal ConceptMap.GroupComponent group;
            internal ConceptMap.SourceElementComponent comp;
            public SourceElementComponentWrapper(ConceptMap.GroupComponent group, ConceptMap.SourceElementComponent comp)
            {

                this.group = group;
                this.comp = comp;
            }
        }


        public class PropertyWithType
        {
            internal string path;
            internal Property baseProperty;
            internal Property profileProperty;
            internal TypeDetails types;
            public PropertyWithType(string path, Property baseProperty, Property profileProperty, TypeDetails types)
            {

                this.baseProperty = baseProperty;
                this.profileProperty = profileProperty;
                this.path = path;
                this.types = types;
            }

            public TypeDetails getTypes()
            {
                return types;
            }
            public string getPath()
            {
                return path;
            }

            public Property getBaseProperty()
            {
                return baseProperty;
            }

            public void setBaseProperty(Property baseProperty)
            {
                this.baseProperty = baseProperty;
            }

            public Property getProfileProperty()
            {
                return profileProperty;
            }

            public void setProfileProperty(Property profileProperty)
            {
                this.profileProperty = profileProperty;
            }

            public string summary()
            {
                return path;
            }

        }

        public class VariableForProfiling
        {
            internal VariableMode mode;
            private string name;
            internal PropertyWithType property;

            public VariableForProfiling(VariableMode mode, string name, PropertyWithType property)
            {

                this.mode = mode;
                this.name = name;
                this.property = property;
            }
            public VariableMode getMode()
            {
                return mode;
            }
            public string getName()
            {
                return name;
            }
            public PropertyWithType getProperty()
            {
                return property;
            }
            public string summary()
            {
                return name + ": " + property.summary();
            }
        }

        public class VariablesForProfiling
        {
            private List<VariableForProfiling> list = new List<VariableForProfiling>();
            private bool optional;
            private bool repeating;

            public VariablesForProfiling(bool optional, bool repeating)
            {
                this.optional = optional;
                this.repeating = repeating;
            }

            public void add(VariableMode mode, string name, string path, Property property, TypeDetails types)
            {
                add(mode, name, new PropertyWithType(path, property, null, types));
            }

            public void add(VariableMode mode, string name, string path, Property baseProperty, Property profileProperty, TypeDetails types)
            {
                add(mode, name, new PropertyWithType(path, baseProperty, profileProperty, types));
            }

            public void add(VariableMode mode, string name, PropertyWithType property)
            {
                VariableForProfiling vv = null;
                foreach (VariableForProfiling v in list)
                    if ((v.mode == mode) && v.getName().Equals(name))
                        vv = v;
                if (vv != null)
                    list.Remove(vv);
                list.Add(new VariableForProfiling(mode, name, property));
            }

            public VariablesForProfiling copy(bool optional, bool repeating)
            {
                VariablesForProfiling result = new VariablesForProfiling(optional, repeating);
                result.list.AddRange(list);
                return result;
            }

            public VariablesForProfiling copy()
            {
                VariablesForProfiling result = new VariablesForProfiling(optional, repeating);
                result.list.AddRange(list);
                return result;
            }

            public VariableForProfiling get(VariableMode? mode, string name)
            {
                if (mode == null)
                {
                    foreach (VariableForProfiling v in list)
                        if ((v.mode == VariableMode.OUTPUT) && v.getName().Equals(name))
                            return v;
                    foreach (VariableForProfiling v in list)
                        if ((v.mode == VariableMode.INPUT) && v.getName().Equals(name))
                            return v;
                }
                foreach (VariableForProfiling v in list)
                    if ((v.mode == mode) && v.getName().Equals(name))
                        return v;
                return null;
            }

            public string summary()
            {
                CommaSeparatedStringBuilder s = new CommaSeparatedStringBuilder();
                CommaSeparatedStringBuilder t = new CommaSeparatedStringBuilder();
                foreach (VariableForProfiling v in list)
                    if (v.mode == VariableMode.INPUT)
                        s.append(v.summary());
                    else
                        t.append(v.summary());
                return "source variables [" + s.ToString() + "], target variables [" + t.ToString() + "]";
            }
        }

        public class StructureMapAnalysis
        {
            internal List<StructureDefinition> profiles = new List<StructureDefinition>();
            internal XhtmlNode summary;
            public List<StructureDefinition> getProfiles()
            {
                return profiles;
            }
            public XhtmlNode getSummary()
            {
                return summary;
            }

        }

        /**
         * Given a structure map, return a set of analyses on it.
         *
         * Returned:
         *   - a list or profiles for what it will create. First profile is the target
         *   - a table with a summary (in xhtml) for easy human undertanding of the mapping
         *
         *
         * @param appInfo
         * @param map
         * @return
         * @throws Exception
         */
        public StructureMapAnalysis analyse(Object appInfo, StructureMap map)
        {
            ids.Clear();
            StructureMapAnalysis result = new StructureMapAnalysis();
            TransformContext context = new TransformContext(appInfo);
            VariablesForProfiling vars = new VariablesForProfiling(false, false);
            StructureMap.GroupComponent start = map.Group.First();
            foreach (var t in start.Input)
            {
                PropertyWithType ti = resolveType(map, t.Type, t.Mode);
                if (t.Mode == StructureMap.StructureMapInputMode.Source)
                    vars.add(VariableMode.INPUT, t.Name, ti);
                else
                    vars.add(VariableMode.OUTPUT, t.Name, createProfile(map, result.profiles, ti, start.Name, start));
            }

            result.summary = new XhtmlNode(NodeType.Element, "table").setAttribute("class", "grid");
            XhtmlNode tr = result.summary.addTag("tr");
            tr.addTag("td").addTag("b").addText("Source");
            tr.addTag("td").addTag("b").addText("Target");

            log("Start Profiling Transform " + map.Url);
            analyseGroup("", context, map, vars, start, result);
            ProfileUtilities pu = new ProfileUtilities(worker, null, pkp);
            foreach (StructureDefinition sd in result.getProfiles())
                pu.cleanUpDifferential(sd);
            return result;
        }


        private void analyseGroup(string indent, TransformContext context, StructureMap map, VariablesForProfiling vars, StructureMap.GroupComponent group, StructureMapAnalysis result)
        {
            log(indent + "Analyse Group : " + group.Name);
            // todo: extends
            // todo: check inputs
            XhtmlNode tr = result.summary.addTag("tr").setAttribute("class", "diff-title");
            XhtmlNode xs = tr.addTag("td");
            XhtmlNode xt = tr.addTag("td");
            foreach (var inp in group.Input)
            {
                if (inp.Mode == StructureMap.StructureMapInputMode.Source)
                    noteInput(vars, inp, VariableMode.INPUT, xs);
                if (inp.Mode == StructureMap.StructureMapInputMode.Target)
                    noteInput(vars, inp, VariableMode.OUTPUT, xt);
            }
            foreach (StructureMap.RuleComponent r in group.Rule)
            {
                analyseRule(indent + "  ", context, map, vars, group, r, result);
            }
        }


        private void noteInput(VariablesForProfiling vars, StructureMap.InputComponent inp, VariableMode mode, XhtmlNode xs)
        {
            VariableForProfiling v = vars.get(mode, inp.Name);
            if (v != null)
                xs.addText("Input: " + v.property.getPath());
        }

        private void analyseRule(string indent, TransformContext context, StructureMap map, VariablesForProfiling vars, StructureMap.GroupComponent group, StructureMap.RuleComponent rule, StructureMapAnalysis result)
        {
            log(indent + "Analyse rule : " + rule.Name);
            XhtmlNode tr = result.getSummary().addTag("tr");
            XhtmlNode xs = tr.addTag("td");
            XhtmlNode xt = tr.addTag("td");

            VariablesForProfiling srcVars = vars.copy();
            if (rule.Source.Count() != 1)
                throw new FHIRException("Rule \"" + rule.Name + "\": not handled yet");
            VariablesForProfiling source = analyseSource(rule.Name, context, srcVars, rule.getSourceFirstRep(), xs);

            TargetWriter tw = new TargetWriter();
            foreach (StructureMap.TargetComponent t in rule.Target)
            {
                analyseTarget(rule.Name, context, source, map, t, rule.getSourceFirstRep().Variable, tw, result.getProfiles(), rule.Name);
            }
            tw.commit(xt);

            foreach (StructureMap.RuleComponent childrule in rule.Rule)
            {
                analyseRule(indent + "  ", context, map, source, group, childrule, result);
            }
            //    foreach (StructureMapGroupRuleDependentComponent dependent in rule.Dependent) {
            //      executeDependency(indent+"  ", context, map, v, group, dependent); // do we need group here?
            //    }
        }

        public class StringPair
        {
            private string var;
            private string desc;
            public StringPair(string var, string desc)
            {

                this.var = var;
                this.desc = desc;
            }
            public string getVar()
            {
                return var;
            }
            public string getDesc()
            {
                return desc;
            }
        }

        public class TargetWriter
        {
            private Dictionary<string, string> newResources = new Dictionary<string, string>();
            private List<StringPair> assignments = new List<StringPair>();
            private List<StringPair> keyProps = new List<StringPair>();
            private CommaSeparatedStringBuilder txt = new CommaSeparatedStringBuilder();

            public void newResource(string var, string name)
            {
                newResources.Add(var, name);
                txt.append("new " + name);
            }

            public void valueAssignment(string context, string desc)
            {
                assignments.Add(new StringPair(context, desc));
                txt.append(desc);
            }

            public void keyAssignment(string context, string desc)
            {
                keyProps.Add(new StringPair(context, desc));
                txt.append(desc);
            }

            public void commit(XhtmlNode xt)
            {
                if (newResources.Count() == 1 && assignments.Count() == 1 && newResources.ContainsKey(assignments.First().getVar()) && keyProps.Count() == 1 && newResources.ContainsKey(keyProps.First().getVar()))
                {
                    xt.addText("new " + assignments.First().getDesc() + " (" + keyProps.First().getDesc().Substring(keyProps.First().getDesc().IndexOf(".") + 1) + ")");
                }
                else if (newResources.Count() == 1 && assignments.Count() == 1 && newResources.ContainsKey(assignments.First().getVar()) && keyProps.Count() == 0)
                {
                    xt.addText("new " + assignments.First().getDesc());
                }
                else
                {
                    xt.addText(txt.ToString());
                }
            }
        }

        private VariablesForProfiling analyseSource(string ruleId, TransformContext context, VariablesForProfiling vars, StructureMap.SourceComponent src, XhtmlNode td)
        {
            VariableForProfiling var = vars.get(VariableMode.INPUT, src.Context);
            if (var == null)
                throw new FHIRException("Rule \"" + ruleId + "\": Unknown input variable " + src.Context);
            PropertyWithType prop = var.getProperty();

            bool optional = false;
            bool repeating = false;

            if (src.Condition != null)
            {
                optional = true;
            }

            if (src.Element != null)
            {
                Property element = prop.getBaseProperty().getChild(prop.types.getType(), src.Element);
                if (element == null)
                    throw new FHIRException("Rule \"" + ruleId + "\": Unknown element name " + src.Element);
                if (element.getDefinition().Min == 0)
                    optional = true;
                if (element.getDefinition().Max.Equals("*"))
                    repeating = true;
                VariablesForProfiling result = vars.copy(optional, repeating);
                TypeDetails type = new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON);
                foreach (var tr in element.getDefinition().Type)
                {
                    if (string.IsNullOrEmpty(tr.Code))
                        throw new Exception("Rule \"" + ruleId + "\": Element has no type");
                    ProfiledType pt = new ProfiledType(tr.getWorkingCode());
                    if (tr.ProfileElement.Any())
                        pt.addProfiles(tr.ProfileElement);
                    if (element.getDefinition().Binding != null)
                        pt.addBinding(element.getDefinition().Binding);
                    type.addType(pt);
                }
                td.addText(prop.getPath() + "." + src.Element);
                if (src.Variable != null)
                    result.add(VariableMode.INPUT, src.Variable, new PropertyWithType(prop.getPath() + "." + src.Element, element, null, type));
                return result;
            }
            else
            {
                td.addText(prop.getPath()); // ditto!
                return vars.copy(optional, repeating);
            }
        }


        private void analyseTarget(string ruleId, TransformContext context, VariablesForProfiling vars, StructureMap map, StructureMap.TargetComponent tgt, string tv, TargetWriter tw, List<StructureDefinition> profiles, string sliceName)
        {
            VariableForProfiling var = null;
            if (tgt.Context != null)
            {
                var = vars.get(VariableMode.OUTPUT, tgt.Context);
                if (var == null)
                    throw new FHIRException("Rule \"" + ruleId + "\": target context not known: " + tgt.Context);
                if (tgt.Element == null)
                    throw new FHIRException("Rule \"" + ruleId + "\": Not supported yet");
            }


            TypeDetails type = null;
            if (tgt.Transform != null)
            {
                type = analyseTransform(context, map, tgt, var, vars);
                // profiling: dest.setProperty(tgt.Element.hashCode(), tgt.Element, v);
            }
            else
            {
                Property vp = var.property.baseProperty.getChild(tgt.Element, tgt.Element);
                if (vp == null)
                    throw new FHIRException("Unknown Property " + tgt.Element + " on " + var.property.path);

                type = new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, vp.getType(tgt.Element));
            }

            if (tgt.Transform == StructureMap.StructureMapTransform.Create)
            {
                string s = getParamString(vars, tgt.Parameter.First());
                if (ModelInfo.SupportedResources.Contains(s))
                    tw.newResource(tgt.Variable, s);
            }
            else
            {
                bool mapsSrc = false;
                foreach (var p in tgt.Parameter)
                {
                    DataType pr = p.Value;
                    if (pr is Id && ((Id)pr).ToString().Equals(tv))
                        mapsSrc = true;
                }
                if (mapsSrc)
                {
                    if (var == null)
                        throw new Exception("Rule \"" + ruleId + "\": Attempt to assign with no context");
                    tw.valueAssignment(tgt.Context, var.property.getPath() + "." + tgt.Element + getTransformSuffix(tgt.Transform));
                }
                else if (tgt.Context != null)
                {
                    if (isSignificantElement(var.property, tgt.Element))
                    {
                        string td = describeTransform(tgt);
                        if (td != null)
                            tw.keyAssignment(tgt.Context, var.property.getPath() + "." + tgt.Element + " = " + td);
                    }
                }
            }
            DataType fixedV = generateFixedValue(tgt);

            PropertyWithType prop = updateProfile(var, tgt.Element, type, map, profiles, sliceName, fixedV, tgt);
            if (tgt.Variable != null)
                if (tgt.Element != null)
                    vars.add(VariableMode.OUTPUT, tgt.Variable, prop);
                else
                    vars.add(VariableMode.OUTPUT, tgt.Variable, prop);
        }

        private DataType generateFixedValue(StructureMap.TargetComponent tgt)
        {
            if (!allParametersFixed(tgt))
                return null;
            if (!tgt.Transform.HasValue)
                return null;
            switch (tgt.Transform)
            {
                case StructureMap.StructureMapTransform.Copy: return tgt.Parameter.First().Value;
                case StructureMap.StructureMapTransform.Truncate: return null;
                //case ESCAPE:
                //case CAST:
                //case APPEND:
                case StructureMap.StructureMapTransform.Translate: return null;
                //case DATEOP,
                //case UUID,
                //case POINTER,
                //case EVALUATE,
                case StructureMap.StructureMapTransform.Cc:
                    CodeableConcept cc = new CodeableConcept(tgt.Parameter.First().Value.ToString(), tgt.Parameter[1].Value.ToString());
                    return cc;
                case StructureMap.StructureMapTransform.C:
                    return buildCoding(tgt.Parameter.First().Value.ToString(), tgt.Parameter[1].Value.ToString());
                case StructureMap.StructureMapTransform.Qty: return null;
                //case ID,
                //case CP,
                default:
                    return null;
            }
        }

        private bool allParametersFixed(StructureMap.TargetComponent tgt)
        {
            foreach (var p in tgt.Parameter)
            {
                DataType pr = p.Value;
                if (pr is Id)
                    return false;
            }
            return true;
        }

        private string describeTransform(StructureMap.TargetComponent tgt)
        {
            switch (tgt.Transform)
            {
                case StructureMap.StructureMapTransform.Copy: return null;
                case StructureMap.StructureMapTransform.Truncate: return null;
                //case ESCAPE:
                //case CAST:
                //case APPEND:
                case StructureMap.StructureMapTransform.Translate: return null;
                //case DATEOP,
                //case UUID,
                //case POINTER,
                //case EVALUATE,
                case StructureMap.StructureMapTransform.Cc: return describeTransformCCorC(tgt);
                case StructureMap.StructureMapTransform.C: return describeTransformCCorC(tgt);
                case StructureMap.StructureMapTransform.Qty: return null;
                //case ID,
                //case CP,
                default:
                    return null;
            }
        }

        // @SuppressWarnings("rawtypes")
        private string describeTransformCCorC(StructureMap.TargetComponent tgt)
        {
            if (tgt.Parameter.Count() < 2)
                return null;
            DataType p1 = tgt.Parameter.First().Value;
            DataType p2 = tgt.Parameter[1].Value;
            if (p1 is Id || p2 is Id)
                return null;
            if (!(p1 is PrimitiveType) || !(p2 is PrimitiveType))
                return null;
            string uri = ((PrimitiveType)p1).ToString();
            string code = ((PrimitiveType)p2).ToString();
            if (Utilities.noString(uri))
                throw new FHIRException("Describe Transform, but the uri is blank");
            if (Utilities.noString(code))
                throw new FHIRException("Describe Transform, but the code is blank");
            Coding c = buildCoding(uri, code);
            return NarrativeGenerator.describeSystem(c.System) + "#" + c.Code + (!string.IsNullOrEmpty(c.Display) ? "(" + c.Display + ")" : "");
        }


        private bool isSignificantElement(PropertyWithType property, string element)
        {
            if ("Observation".Equals(property.getPath()))
                return "code".Equals(element);
            else if ("Bundle".Equals(property.getPath()))
                return "type".Equals(element);
            else
                return false;
        }

        private string getTransformSuffix(StructureMap.StructureMapTransform? transform)
        {
            switch (transform)
            {
                case StructureMap.StructureMapTransform.Copy: return "";
                case StructureMap.StructureMapTransform.Truncate: return " (truncated)";
                //case ESCAPE:
                //case CAST:
                //case APPEND:
                case StructureMap.StructureMapTransform.Translate: return " (translated)";
                //case DATEOP,
                //case UUID,
                //case POINTER,
                //case EVALUATE,
                case StructureMap.StructureMapTransform.Cc: return " (--> CodeableConcept)";
                case StructureMap.StructureMapTransform.C: return " (--> Coding)";
                case StructureMap.StructureMapTransform.Qty: return " (--> Quantity)";
                //case ID,
                //case CP,
                default:
                    return " {??)";
            }
        }

        private PropertyWithType updateProfile(VariableForProfiling var, string element, TypeDetails type, StructureMap map, List<StructureDefinition> profiles, string sliceName, DataType fixedV, StructureMap.TargetComponent tgt)
        {
            if (var == null)
            {
                System.Diagnostics.Debug.Assert(Utilities.noString(element));
                // 1. start the new structure definition
                StructureDefinition sdn = worker.fetchResource<StructureDefinition>(type.getType());
                if (sdn == null)
                    throw new FHIRException("Unable to find definition for " + type.getType());
                ElementDefinition edn = sdn.Snapshot.getElementFirstRep();
                PropertyWithType pn = createProfile(map, profiles, new PropertyWithType(sdn.Id, new Property(worker, edn, sdn), null, type), sliceName, tgt);

                //      // 2. hook it into the base bundle
                //      if (type.Type.StartsWith("http://hl7.org/fhir/StructureDefinition/") && worker.getResourceNames().contains(type.Type.Substring(40))) {
                //        StructureDefinition sd = var.getProperty().profileProperty.Structure;
                //        ElementDefinition ed = sd.getDifferential().addElement();
                //        ed.setPath("Bundle.entry");
                //        ed.setName(sliceName);
                //        ed.setMax("1"); // well, it is for now...
                //        ed = sd.getDifferential().addElement();
                //        ed.setPath("Bundle.entry.fullUrl");
                //        ed.setMin(1);
                //        ed = sd.getDifferential().addElement();
                //        ed.setPath("Bundle.entry.resource");
                //        ed.setMin(1);
                //        ed.addType().setCode(pn.getProfileProperty().Structure.Type).setProfile(pn.getProfileProperty().Structure.Url);
                //      }
                return pn;
            }
            else
            {
                System.Diagnostics.Debug.Assert(!Utilities.noString(element));
                Property pvb = var.getProperty().getBaseProperty();
                Property pvd = var.getProperty().getProfileProperty();
                Property pc = pvb.getChild(element, var.property.types);
                if (pc == null)
                    throw new DefinitionException("Unable to find a definition for " + pvb.getDefinition().Path + "." + element);

                // the profile structure definition (derived)
                StructureDefinition sd = var.getProperty().profileProperty.getStructure();
                ElementDefinition ednew = new ElementDefinition();
                sd.Differential.Element.Add(ednew);
                ednew.Path = var.getProperty().profileProperty.getDefinition().Path + "." + pc.getName();
                ednew.setUserData("slice-name", sliceName);
                ednew.Fixed = fixedV;
                foreach (var pt in type.getProfiledTypes())
                {
                    if (pt.hasBindings())
                        ednew.Binding = pt.getBindings().First();
                    if (pt.getUri().StartsWith("http://hl7.org/fhir/StructureDefinition/"))
                    {
                        string t = pt.getUri().Substring(40);
                        t = checkType(t, pc, pt.getProfiles());
                        if (t != null)
                        {
                            if (pt.hasProfiles())
                            {
                                foreach (string p in pt.getProfiles())
                                    if (t.Equals("Reference"))
                                        ednew.getType(t).TargetProfileElement.Add(p);
                                    else
                                        ednew.getType(t).ProfileElement.Add(p);
                            }
                            else
                                ednew.getType(t);
                        }
                    }
                }

                return new PropertyWithType(var.property.path + "." + element, pc, new Property(worker, ednew, sd), type);
            }
        }

        private string checkType(string t, Property pvb, List<string> profiles)
        {
            if (pvb.getDefinition().Type.Count() == 1 && isCompatibleType(t, pvb.getDefinition().Type.First().getWorkingCode()) && profilesMatch(profiles, pvb.getDefinition().Type.First().ProfileElement))
                return null;
            foreach (var tr in pvb.getDefinition().Type)
            {
                if (isCompatibleType(t, tr.getWorkingCode()))
                    return tr.getWorkingCode(); // note what is returned - the base type, not the inferred mapping type
            }
            throw new FHIRException("The type " + t + " is not compatible with the allowed types for " + pvb.getDefinition().Path);
        }

        private bool profilesMatch(List<string> profiles, List<Canonical> profile)
        {
            return profiles == null || profiles.Count() == 0 || profile.Count() == 0 || (profiles.Count() == 1 && profiles.First().Equals(profile.First().Value));
        }

        private bool isCompatibleType(string t, string code)
        {
            if (t.Equals(code))
                return true;
            if (t.Equals("string"))
            {
                StructureDefinition sd = worker.fetchTypeDefinition(code);
                if (sd != null && sd.BaseDefinition.Equals("http://hl7.org/fhir/StructureDefinition/string"))
                    return true;
            }
            return false;
        }

        private TypeDetails analyseTransform(TransformContext context, StructureMap map, StructureMap.TargetComponent tgt, VariableForProfiling var, VariablesForProfiling vars)
        {
            switch (tgt.Transform)
            {
                case StructureMap.StructureMapTransform.Create:
                    string p = getParamString(vars, tgt.Parameter.First());
                    return new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, p);

                case StructureMap.StructureMapTransform.Copy:
                    return getParam(vars, tgt.Parameter.First());

                case StructureMap.StructureMapTransform.Evaluate:
                    ExpressionNode expr = (ExpressionNode)tgt.getUserData(MAP_EXPRESSION);
                    if (expr == null)
                    {
                        expr = fpe.parse(getParamString(vars, tgt.Parameter[tgt.Parameter.Count() - 1]));
                        tgt.setUserData(MAP_WHERE_EXPRESSION, expr);
                    }
                    return fpe.check(vars, null, expr);

                ////case TRUNCATE :
                ////  string src = getParamString(vars, tgt.Parameter.First());
                ////  string len = getParamString(vars, tgt.Parameter[1]);
                ////  if (Utilities.isInteger(len)) {
                ////    int l = Integer.parseInt(len);
                ////    if (src.length() > l)
                ////      src = src.Substring(0, l);
                ////  }
                ////  return new FhirString(src);
                ////case ESCAPE :
                ////  throw new Exception("Transform "+tgt.Transform.toCode()+" not supported yet");
                ////case CAST :
                ////  throw new Exception("Transform "+tgt.Transform.toCode()+" not supported yet");
                ////case APPEND :
                ////  throw new Exception("Transform "+tgt.Transform.toCode()+" not supported yet");
                case StructureMap.StructureMapTransform.Translate:
                    return new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, "CodeableConcept");
                case StructureMap.StructureMapTransform.Cc:

                    TypeDetails.ProfiledType res = new TypeDetails.ProfiledType("CodeableConcept");
                    if (tgt.Parameter.Count() >= 2 && isParamId(vars, tgt.Parameter[1]))
                    {
                        TypeDetails td = vars.get(null, getParamId(vars, tgt.Parameter[1])).property.types;
                        if (td != null && td.hasBinding())
                            // todo: do we need to check that there's no implicit translation her? I don't think we do...
                            res.addBinding(td.getBinding());
                    }
                    return new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, res);
                case StructureMap.StructureMapTransform.C:

                    return new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, "Coding");
                case StructureMap.StructureMapTransform.Qty:

                    return new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, "Quantity");

                case StructureMap.StructureMapTransform.Reference:
                    VariableForProfiling vrs = vars.get(VariableMode.OUTPUT, getParamId(vars, tgt.getParameterFirstRep()));
                    if (vrs == null)
                        throw new FHIRException("Unable to resolve variable \"" + getParamId(vars, tgt.getParameterFirstRep()) + "\"");
                    string profile = vrs.property.getProfileProperty().getStructure().Url;
                    TypeDetails tdr = new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON);
                    tdr.addType("Reference", profile);
                    return tdr;

                ////case DATEOP :
                ////  throw new Exception("Transform "+tgt.Transform.toCode()+" not supported yet");
                ////case UUID :
                ////  return new Id(UUID.randomUUID().ToString());
                ////case POINTER :
                ////  Base b = getParam(vars, tgt.Parameter.First());
                ////  if (b is Resource)
                ////    return new UriType("urn:uuid:"+((Resource) b).Id);
                ////  else
                ////    throw new FHIRException("Transform engine cannot point at an element of type "+b.TypeName);
                default:
                    throw new Exception("Transform Unknown or not handled yet: " + tgt.Transform.GetLiteral());
            }
        }

        private string getParamString(VariablesForProfiling vars, StructureMap.ParameterComponent parameter)
        {
            DataType p = parameter.Value;
            if (p == null || p is Id)
                return null;
            if (p is PrimitiveType pt)
                return pt.ToString();
            return null;
        }

        private string getParamId(VariablesForProfiling vars, StructureMap.ParameterComponent parameter)
        {
            DataType p = parameter.Value;
            if (p == null || !(p is Id))
                return null;
            return p.ToString();
        }

        private bool isParamId(VariablesForProfiling vars, StructureMap.ParameterComponent parameter)
        {
            DataType p = parameter.Value;
            if (p == null || !(p is Id))
                return false;
            return vars.get(null, p.ToString()) != null;
        }

        private TypeDetails getParam(VariablesForProfiling vars, StructureMap.ParameterComponent parameter)
        {
            DataType p = parameter.Value;
            if (!(p is Id))
                return new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, ProfileUtilities.sdNs(p.TypeName, worker.getOverrideVersionNs()));
            else
            {
                string n = ((Id)p).Value;
                VariableForProfiling b = vars.get(VariableMode.INPUT, n);
                if (b == null)
                    b = vars.get(VariableMode.OUTPUT, n);
                if (b == null)
                    throw new DefinitionException("Variable " + n + " not found (" + vars.summary() + ")");
                return b.getProperty().getTypes();
            }
        }

        private PropertyWithType createProfile(StructureMap map, List<StructureDefinition> profiles, PropertyWithType prop, string sliceName, Base ctxt)
        {
            if (prop.getBaseProperty().getDefinition().Path.Contains("."))
                throw new DefinitionException("Unable to process entry point");

            string type = prop.getBaseProperty().getDefinition().Path;
            string suffix = "";
            if (ids.ContainsKey(type))
            {
                int id = ids[type];
                id++;
                ids.Add(type, id);
                suffix = "-" + id.ToString();
            }
            else
                ids.Add(type, 0);

            StructureDefinition profile = new StructureDefinition();
            profiles.Add(profile);
            profile.Derivation = StructureDefinition.TypeDerivationRule.Constraint;
            profile.Type = type;
            profile.BaseDefinition = prop.getBaseProperty().getStructure().Url;
            profile.Name = "Profile for " + profile.Type + " for " + sliceName;
            profile.Url = map.Url.Replace("StructureMap", "StructureDefinition") + "-" + profile.Type + suffix;
            ctxt.setUserData("profile", profile.Url); // then we can easily assign this profile url for validation later when we actually transform
            profile.Id = map.Id + "-" + profile.Type + suffix;
            profile.Status = map.Status;
            profile.Experimental = map.Experimental;
            profile.Description = new Markdown("Generated automatically from the mapping by the Java Reference Implementation");
            profile.Contact = map.Contact.DeepCopy().ToList(); // contact property is the same datatype, so use the Firely SDK call
            profile.Date = map.Date;
            profile.Copyright = map.Copyright;
            profile.FhirVersion = EnumUtility.ParseLiteral<FHIRVersion>(ModelInfo.Version);
            profile.Kind = prop.getBaseProperty().getStructure().Kind;
            profile.Abstract = false;

            ElementDefinition ed = new ElementDefinition();
            profile.Differential.Element.Add(ed);
            ed.Path = profile.Type;

            prop.profileProperty = new Property(worker, ed, profile);
            return prop;
        }

        private PropertyWithType resolveType(StructureMap map, string type, StructureMap.StructureMapInputMode? mode)
        {
            foreach (StructureMap.StructureComponent imp in map.Structure)
            {
                if ((imp.Mode == StructureMap.StructureMapModelMode.Source && mode == StructureMap.StructureMapInputMode.Source) ||
                    (imp.Mode == StructureMap.StructureMapModelMode.Target && mode == StructureMap.StructureMapInputMode.Target))
                {
                    StructureDefinition sd = worker.fetchResource<StructureDefinition>(imp.Url);
                    if (sd == null)
                        throw new FHIRException("Import " + imp.Url + " cannot be resolved");
                    if (sd.Id.Equals(type))
                    {
                        return new PropertyWithType(sd.Type, new Property(worker, sd.Snapshot.Element.First(), sd), null, new TypeDetails(ExpressionNode.CollectionStatus.SINGLETON, sd.Url));
                    }
                }
            }
            throw new FHIRException("Unable to find structure definition for " + type + " in imports");
        }


        private string tail(string url)
        {
            return url.Substring(url.LastIndexOf("/") + 1);
        }


        private void addChildMappings(StringBuilder b, string id, string indent, StructureDefinition sd, ElementDefinition ed, bool inner)
        {
            bool first = true;
            List<ElementDefinition> children = ProfileUtilities.getChildMap(sd, ed);
            foreach (ElementDefinition child in children)
            {
                if (first && inner)
                {
                    b.Append(" then {\r\n");
                    first = false;
                }
                string map = getMapping(child, id);
                if (map != null)
                {
                    b.Append(indent + "  " + child.Path + ": " + map);
                    addChildMappings(b, id, indent + "  ", sd, child, true);
                    b.AppendLine();
                }
            }
            if (!first && inner)
                b.Append(indent + "}");

        }

        private string getMapping(ElementDefinition ed, string id)
        {
            foreach (var map in ed.Mapping)
                if (id.Equals(map.Identity))
                    return map.Map;
            return null;
        }
    }
}