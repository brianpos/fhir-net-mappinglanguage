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

// Portions ported from/compatible with https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/model/StructureMap.java

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Introspection;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Utility;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using static Hl7.Fhir.MappingLanguage.FHIRPathEngineOriginal; // for the IEvaluationContext
using static Hl7.Fhir.Model.ElementDefinition;

namespace Hl7.Fhir.MappingLanguage
{
    public class CommaSeparatedStringBuilder
    {
        public CommaSeparatedStringBuilder()
        {
        }
        public CommaSeparatedStringBuilder(string separator)
        {
            _separator = separator;
        }

        private StringBuilder _sb = new StringBuilder();
        private string _separator = ",";

        public override string ToString()
        {
            return _sb.ToString();
        }

        internal void append(string value)
        {
            if (_sb.Length > 0)
                _sb.Append(_separator);
            _sb.Append(value);
        }

        internal void appendIfNotNull(string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                append(value);
            }
        }

        internal int Length()
        {
            return _sb.Length;
        }
    }

    public enum NodeType
    {
        Element
    };
    public class XhtmlNode
    {
        XmlDocument _document;
        XmlNode _node;
        public XhtmlNode(NodeType nodeType, string value)
        {
            this.nodeType = nodeType;
            this.value = value;
            if (nodeType == NodeType.Element)
            {
                _document = new XmlDocument();
                _node = _document.CreateElement(value);
            }
        }

        public NodeType nodeType { get; }
        public string value { get; }

        private XhtmlNode(XmlDocument parent, XmlNode node)
        {
            _document = parent;
            _node = node;
        }
        internal XhtmlNode addTag(string v)
        {
            return new XhtmlNode(_document, _node.AppendChild(_document.CreateElement(v)));
        }

        internal void addText(string v)
        {
            _node.InnerText += v;
        }

        internal XhtmlNode setAttribute(string v1, string v2)
        {
            var attr = _document.CreateAttribute(v1);
            attr.Value = v2;
            _node.Attributes.SetNamedItem(attr);
            return this;
        }
    }

    public class ProfileKnowledgeProvider
    {

    }

    public class TerminologyServiceOptions
    {

    }

    public class DotnetFhirPathEngineEnvironment : IEvaluationContext
    {
        public TypeDetails checkFunction(object appContext, string functionName, List<TypeDetails> parameters)
        {
            throw new NotImplementedException();
        }

        public bool conformsToProfile(object appContext, Base item, string url)
        {
            throw new NotImplementedException();
        }

        public List<Base> executeFunction(object appContext, List<Base> focus, string functionName, List<List<Base>> parameters)
        {
            throw new NotImplementedException();
        }

        public bool log(string argument, List<Base> focus)
        {
            throw new NotImplementedException();
        }

        public List<ITypedElement> resolveConstant(object appContext, string name, bool beforeContext)
        {
            throw new NotImplementedException();
        }

        public TypeDetails resolveConstantType(object appContext, string name)
        {
            throw new NotImplementedException();
        }

        public FunctionDetails resolveFunction(string functionName)
        {
            // need to do this extraction from the actual dotnet implementation
            if (functionName == "toDate")
                return new FunctionDetails("toDate", 0, 0);
            throw new NotImplementedException();
        }

        public Base resolveReference(object appContext, string url)
        {
            throw new NotImplementedException();
        }

        public ValueSet resolveValueSet(object appContext, string url)
        {
            throw new NotImplementedException();
        }
    }

    public class PathEngineException : FHIRException
    {
        public SourceLocation Location { get; private set; }
        public string Expression { get; private set; }

        public PathEngineException()
        {
        }

        public PathEngineException(string message, Exception cause)
            : base(message, cause)
        {
        }

        public PathEngineException(string message)
            : base(message)
        {
        }

        public PathEngineException(string message, SourceLocation location, string expression)
            : base(message)
        {
            Location = location;
            Expression = expression;
        }
    }

    public class DefinitionException : FHIRException
    {
        public DefinitionException()
        {
        }

        public DefinitionException(string message, Exception cause)
            : base(message, cause)
        {
        }

        public DefinitionException(string message)
            : base(message)
        {
        }
    }

    internal static class ToolingExtensions
    {
        // https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/utils/ToolingExtensions.java
        public const string EXT_FHIR_TYPE = "http://hl7.org/fhir/StructureDefinition/structuredefinition-fhir-type";
        public const string EXT_XML_TYPE = "http://hl7.org/fhir/StructureDefinition/structuredefinition-xml-type";
        public const string EXT_JSON_TYPE = "http://hl7.org/fhir/StructureDefinition/structuredefinition-json-type";
    }

    internal static class StructureMapExtensions
    {
        private class MapperUserData
        {
            public Dictionary<string, object> data = new Dictionary<string, object>();
        }

        public static void setUserData(this IAnnotated me, string key, object value)
        {
            if (me.TryGetAnnotation<MapperUserData>(out MapperUserData data))
            {
                data.data[key] = value;
                return;
            }
            if (me is IAnnotatable a)
            {
                var ud = new MapperUserData();
                ud.data[key] = value;
                a.SetAnnotation(ud);
            }
        }

        public static void setUserData(this ITypedElement me, string key, object value)
        {
            if (me is IAnnotatable a)
            {
                var ud = new MapperUserData();
                ud.data[key] = value;
                a.SetAnnotation(ud);
            }
        }

        public static object getUserData(this IAnnotated me, string key)
        {
            if (me.TryGetAnnotation<MapperUserData>(out MapperUserData data))
            {
                return data.data[key];
            }
            return null;
        }

        public static bool hasUserData(this IAnnotated me, string key)
        {
            if (me.TryGetAnnotation<MapperUserData>(out MapperUserData data))
            {
                return data.data[key] != null;
            }
            return false;
        }

        public static string getWorkingCode(this ElementDefinition.TypeRefComponent me)
        {
            var fhirTypeInExtension = me.GetStringExtension(ToolingExtensions.EXT_FHIR_TYPE);
            if (!string.IsNullOrEmpty(fhirTypeInExtension))
                return fhirTypeInExtension;
            if (me.CodeElement == null)
                return null;
            String s = me.CodeElement.GetStringExtension(ToolingExtensions.EXT_XML_TYPE);
            if (!string.IsNullOrEmpty(s))
            {
                if ("xsd:gYear OR xsd:gYearMonth OR xsd:date OR xsd:dateTime".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "dateTime";
                if ("xsd:gYear OR xsd:gYearMonth OR xsd:date".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "date";
                if ("xsd:dateTime".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "instant";
                if ("xsd:token".Equals(s))
                    return "code";
                if ("xsd:boolean".Equals(s))
                    return "boolean";
                if ("xsd:string".Equals(s))
                    return "string";
                if ("xsd:time".Equals(s))
                    return "time";
                if ("xsd:int".Equals(s))
                    return "integer";
                if ("xsd:decimal OR xsd:double".Equals(s))
                    return "decimal";
                if ("xsd:decimal".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "decimal";
                if ("xsd:base64Binary".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "base64Binary";
                if ("xsd:positiveInteger".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "positiveInt";
                if ("xsd:nonNegativeInteger".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "unsignedInt";
                if ("xsd:anyURI".Equals(s, StringComparison.InvariantCultureIgnoreCase))
                    return "uri";

                throw new FHIRException("Unknown xml type '" + s + "'");
            }
            return me.Code;
        }

        public static TypeRefComponent getType(this ElementDefinition me, String code)
        {
            foreach (var tr in me.Type)
                if (tr.Code.Equals(code))
                    return tr;
            TypeRefComponent newTref = new TypeRefComponent();
            newTref.Code = code;
            me.Type.Add(newTref);
            return newTref;
        }

        public static StructureMap.SourceComponent getSourceFirstRep(this StructureMap.RuleComponent r)
        {
            if (r.Source.Any())
                return r.Source.First();
            var newSource = new StructureMap.SourceComponent();
            r.Source.Add(newSource);
            return newSource;
        }

        public static StructureMap.TargetComponent getTargetFirstRep(this StructureMap.RuleComponent r)
        {
            if (r.Target.Any())
                return r.Target.First();
            var newTarget = new StructureMap.TargetComponent();
            r.Target.Add(newTarget);
            return newTarget;
        }

        public static ConceptMap.TargetElementComponent getTargetFirstRep(this ConceptMap.SourceElementComponent r)
        {
            if (r.Target.Any())
                return r.Target.First();
            var newTarget = new ConceptMap.TargetElementComponent();
            r.Target.Add(newTarget);
            return newTarget;
        }

        public static ElementDefinition getElementFirstRep(this StructureDefinition.SnapshotComponent me)
        {
            if (me.Element.Any())
                return me.Element.First();
            var newElement = new ElementDefinition();
            me.Element.Add(newElement);
            return newElement;
        }

        public static StructureMap.ParameterComponent addParameter(this StructureMap.TargetComponent target)
        {
            var newParameter = new StructureMap.ParameterComponent();
            target.Parameter.Add(newParameter);
            return newParameter;
        }

        public static StructureMap.ParameterComponent getParameterFirstRep(this StructureMap.TargetComponent me)
        {
            if (me.Parameter.Any())
                return me.Parameter.First();
            var newParameter = new StructureMap.ParameterComponent();
            me.Parameter.Add(newParameter);
            return newParameter;
        }

        public static bool hasRepresentation(this ElementDefinition me, PropertyRepresentation repType)
        {
            if (!me.Representation.Any())
                return false;
            if (me.Representation.Any(r => r.Value == repType))
                return true;
            return false;
        }

        public static ElementDefinition.SlicingComponent getSlicing(this ElementDefinition me)
        {
            // refer to notes here if there are issues with this auto-creation
            // https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/model/Configuration.java
            if (me.Slicing == null)
                me.Slicing = new ElementDefinition.SlicingComponent();
            return me.Slicing;
        }

        public static string[] getTypesForProperty(this ITypedElement me, IStructureDefinitionSummaryProvider pkp, string name)
        {
            if (me != null)
            {
                var ti = pkp.Provide(me.InstanceType);
                if (ti != null)
                {
                    var eds = ti.GetElements().Where(e => e.ElementName == name);
                    if (eds != null)
                    {
                        return eds.SelectMany(e => e.Type.Select(t => t.GetTypeName())).ToArray();
                    }
                }
            }
            return null;
        }

        // New version will map from ITypedElement to SourceNode
        //public static Base setProperty(this Base me, string name, Base value)
        //{
        //    if (me != null)
        //    {
        //        var cm = ModelInspector.GetClassMappingForType(me.GetType());
        //        if (cm != null)
        //        {
        //            var pm = cm.FindMappedElementByName(name);
        //            if (pm != null)
        //            {
        //                try
        //                {
        //                    if (pm.ImplementingType == typeof(string) && value is FhirString str)
        //                    {
        //                        pm.SetValue(me, str.Value);
        //                        return value;
        //                    }
        //                    pm.SetValue(me, value);
        //                    return value;
        //                }
        //                catch (Exception ex)
        //                {
        //                    Base value2 = Activator.CreateInstance(pm.ImplementingType) as Base;
        //                    if (value2 is PrimitiveType pt && value is PrimitiveType ps)
        //                    {
        //                        pt.ObjectValue = ps.ObjectValue;
        //                        pm.SetValue(me, value2);
        //                        return value2;
        //                    }
        //                    System.Diagnostics.Trace.WriteLine($"Recovered from {ex.Message}");
        //                }
        //            }
        //        }
        //    }
        //    return value;
        //}

        //public static Base makeProperty(this Base me, string name)
        //{
        //    if (me != null)
        //    {
        //        var cm = ModelInspector.GetClassMappingForType(me.GetType());
        //        if (cm != null)
        //        {
        //            var pm = cm.FindMappedElementByName(name);
        //            if (pm != null)
        //            {
        //                Base value = Activator.CreateInstance(pm.ImplementingType) as Base;
        //                if (pm.IsCollection)
        //                {
        //                    var list = pm.GetValue(me) as IList;
        //                    list.Add(value);
        //                }
        //                else
        //                {
        //                    pm.SetValue(me, value);
        //                }
        //                return value;
        //            }
        //        }
        //    }
        //    return null;
        //}

        public static ITypedElement setProperty(this ITypedElement me, IStructureDefinitionSummaryProvider pkp, string name, ITypedElement value)
        {
            if (me is ElementNode en)
            {
                return en.Add(pkp, name, value.Value, value.InstanceType);
            }
            return null;
        }

        public static ITypedElement makeProperty(this ITypedElement me, IStructureDefinitionSummaryProvider pkp, string name)
        {
            if (me is ElementNode en)
            {
                return en.Add(pkp, name);
            }
            return null;
        }
    }
}
