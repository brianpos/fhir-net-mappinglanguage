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

// Ported from https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/elementmodel/Property.java

using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;
using static Hl7.Fhir.Model.ElementDefinition;
using static Hl7.Fhir.Model.StructureDefinition;

namespace Hl7.Fhir.MappingLanguage
{
    public class Property
    {
        private IWorkerContext context;
        private ElementDefinition definition;
        private StructureDefinition structure;
        private bool canBePrimitive;

        public Property(IWorkerContext context, ElementDefinition definition, StructureDefinition structure)
        {
            this.context = context;
            this.definition = definition;
            this.structure = structure;
        }

        public String getName()
        {
            return definition.Path.Substring(definition.Path.LastIndexOf(".") + 1);
        }

        public ElementDefinition getDefinition()
        {
            return definition;
        }

        public String getType()
        {
            if (definition.Type.Count() == 0)
                return null;
            else if (definition.Type.Count() > 1)
            {
                String tn = definition.Type[0].getWorkingCode();
                for (int i = 1; i < definition.Type.Count(); i++)
                {
                    if (!tn.Equals(definition.Type[i].getWorkingCode()))
                        throw new FHIRException("logic error, gettype when types > 1");
                }
                return tn;
            }
            else
                return definition.Type[0].getWorkingCode();
        }

        public String getType(String elementName)
        {
            if (!definition.Path.Contains("."))
                return definition.Path;
            ElementDefinition ed = definition;
            if (!string.IsNullOrEmpty(definition.ContentReference))
            {
                if (!definition.ContentReference.StartsWith("#"))
                    throw new FHIRException("not handled yet");
                bool found = false;
                foreach (ElementDefinition d in structure.Snapshot.Element)
                {
                    if (!string.IsNullOrEmpty(d.ElementId) && d.ElementId.Equals(definition.ContentReference.Substring(1)))
                    {
                        found = true;
                        ed = d;
                    }
                }
                if (!found)
                    throw new FHIRException("Unable to resolve " + definition.ContentReference + " at " + definition.Path + " on " + structure.Url);
            }
            if (ed.Type.Count() == 0)
                return null;
            else if (ed.Type.Count() > 1)
            {
                String t = ed.Type[0].Code;
                bool all = true;
                foreach (TypeRefComponent tr in ed.Type)
                {
                    if (!t.Equals(tr.Code))
                        all = false;
                }
                if (all)
                    return t;
                String tail = ed.Path.Substring(ed.Path.LastIndexOf(".") + 1);
                if (tail.EndsWith("[x]") && elementName != null && elementName.StartsWith(tail.Substring(0, tail.Length - 3)))
                {
                    String name = elementName.Substring(tail.Length - 3);
                    return isPrimitive(lowFirst(name)) ? lowFirst(name) : name;
                }
                else
                    throw new FHIRException("logic error, gettype when types > 1, name mismatch for " + elementName + " on at " + ed.Path);
            }
            else if (ed.Type[0].Code == null)
            {
                if (Utilities.existsInList(ed.ElementId, "Element.id", "Extension.url"))
                    return "string";
                else
                    return structure.Id;
            }
            else
                return ed.Type[0].getWorkingCode();
        }

        public bool hasType(String elementName)
        {
            if (definition.Type.Count() == 0)
                return false;
            else if (definition.Type.Count() > 1)
            {
                String t = definition.Type[0].Code;
                bool all = true;
                foreach (TypeRefComponent tr in definition.Type)
                {
                    if (!t.Equals(tr.Code))
                        all = false;
                }
                if (all)
                    return true;
                String tail = definition.Path.Substring(definition.Path.LastIndexOf(".") + 1);
                if (tail.EndsWith("[x]") && elementName.StartsWith(tail.Substring(0, tail.Length - 3)))
                {
                    String name = elementName.Substring(tail.Length - 3);
                    return true;
                }
                else
                    return false;
            }
            else
                return true;
        }

        public StructureDefinition getStructure()
        {
            return structure;
        }

        /**
         * Is the given name a primitive
         *
         * @param E.g. "Observation.status"
         */
        public bool isPrimitiveName(String name)
        {
            String code = getType(name);
            return isPrimitive(code);
        }

        /**
         * Is the given type a primitive
         *
         * @param E.g. "integer"
         */
        public bool isPrimitive(String code)
        {
            return ModelInfo.IsPrimitive(code);
            // was this... but this can be very inefficient compared to hard coding the list
            //		StructureDefinition sd = context.fetchTypeDefinition(code);
            //      return sd != null && sd.getKind() == StructureDefinitionKind.PRIMITIVETYPE;
        }

        private String lowFirst(String t)
        {
            return t.Substring(0, 1).ToLower() + t.Substring(1);
        }

        public bool isResource()
        {
            if (definition.Type.Count() > 0)
                return definition.Type.Count() == 1 && ("Resource".Equals(definition.Type[0].Code) || "DomainResource".Equals(definition.Type[0].Code));
            else
                return !definition.Path.Contains(".") && structure.Kind == StructureDefinitionKind.Resource;
        }

        public bool isList()
        {
            return !"1".Equals(definition.Max);
        }

        public String getScopedPropertyName()
        {
            return definition.Base.Path;
        }

        //public String getNamespace()
        //{
        //    if (ToolingExtensions.hasExtension(definition, "http://hl7.org/fhir/StructureDefinition/elementdefinition-namespace"))
        //        return ToolingExtensions.readStringExtension(definition, "http://hl7.org/fhir/StructureDefinition/elementdefinition-namespace");
        //    if (ToolingExtensions.hasExtension(structure, "http://hl7.org/fhir/StructureDefinition/elementdefinition-namespace"))
        //        return ToolingExtensions.readStringExtension(structure, "http://hl7.org/fhir/StructureDefinition/elementdefinition-namespace");
        //    return FormatUtilities.FHIR_NS;
        //}

        private bool isElementWithOnlyExtension(ElementDefinition ed, List<ElementDefinition> children)
        {
            bool result = false;
            if (!ed.Type.IsNullOrEmpty())
            {
                result = true;
                foreach (ElementDefinition ele in children)
                {
                    if (!ele.Path.Contains("extension"))
                    {
                        result = false;
                        break;
                    }
                }
            }
            return result;
        }

        //public bool IsLogicalAndHasPrimitiveValue(String name)
        //{
        //    //		if (canBePrimitive!= null)
        //    //			return canBePrimitive;

        //    canBePrimitive = false;
        //    if (structure.getKind() != StructureDefinitionKind.LOGICAL)
        //        return false;
        //    if (!hasType(name))
        //        return false;
        //    StructureDefinition sd = context.fetchResource<StructureDefinition>(structure.getUrl().substring(0, structure.getUrl().lastIndexOf("/") + 1) + getType(name));
        //    if (sd == null)
        //        sd = context.fetchResource<StructureDefinition>(ProfileUtilities.sdNs(getType(name), context.getOverrideVersionNs()));
        //    if (sd != null && sd.getKind() == StructureDefinitionKind.PRIMITIVETYPE)
        //        return true;
        //    if (sd == null || sd.getKind() != StructureDefinitionKind.LOGICAL)
        //        return false;
        //    foreach (ElementDefinition ed in sd.getSnapshot().getElement())
        //    {
        //        if (ed.Path.equals(sd.getId() + ".value") && ed.getType().size() == 1 && isPrimitive(ed.getType().get(0).getCode()))
        //        {
        //            canBePrimitive = true;
        //            return true;
        //        }
        //    }
        //    return false;
        //}

        //public bool isChoice()
        //{
        //    if (definition.getType().size() <= 1)
        //        return false;
        //    String tn = definition.getType().get(0).getCode();
        //    for (int i = 1; i < definition.getType().size(); i++)
        //        if (!definition.getType().get(i).getCode().equals(tn))
        //            return true;
        //    return false;
        //}


        protected List<Property> getChildProperties(String elementName, String statedType)
        {
            ElementDefinition ed = definition;
            StructureDefinition sd = structure;
            List<ElementDefinition> children = ProfileUtilities.getChildMap(sd, ed);
            String url = null;
            if (children.IsNullOrEmpty() || isElementWithOnlyExtension(ed, children))
            {
                // ok, find the right definitions
                String t = null;
                if (ed.Type.Count() == 1)
                    t = ed.Type[0].getWorkingCode();
                else if (ed.Type.Count() == 0)
                    throw new FHIRException("types == 0, and no children found on " + getDefinition().Path);
                else
                {
                    t = ed.Type[0].getWorkingCode();
                    bool all = true;
                    foreach (TypeRefComponent tr in ed.Type)
                    {
                        if (!tr.getWorkingCode().Equals(t))
                        {
                            all = false;
                            break;
                        }
                    }
                    if (!all)
                    {
                        // ok, it's polymorphic
                        if (ed.hasRepresentation(PropertyRepresentation.TypeAttr))
                        {
                            t = statedType;
                            var defaultTypeValue = ed.GetStringExtension("http://hl7.org/fhir/StructureDefinition/elementdefinition-defaulttype");
                            if (t == null && !string.IsNullOrEmpty(defaultTypeValue))
                                t = defaultTypeValue;
                            bool ok = false;
                            foreach (TypeRefComponent tr in ed.Type)
                            {
                                if (tr.getWorkingCode().Equals(t))
                                    ok = true;
                                if (Utilities.isAbsoluteUrl(tr.getWorkingCode()))
                                {
                                    StructureDefinition sdt = context.fetchResource<StructureDefinition>(tr.getWorkingCode());
                                    if (sdt != null && sdt.Type.Equals(t))
                                    {
                                        url = tr.getWorkingCode();
                                        ok = true;
                                    }
                                }
                                if (ok)
                                    break;
                            }
                            if (!ok)
                                throw new DefinitionException("Type '" + t + "' is not an acceptable type for '" + elementName + "' on property " + definition.Path);

                        }
                        else
                        {
                            t = elementName.Substring(tail(ed.Path).Length - 3);
                            if (isPrimitive(lowFirst(t)))
                                t = lowFirst(t);
                        }
                    }
                }
                if (!"xhtml".Equals(t))
                {
                    foreach (TypeRefComponent aType in ed.Type)
                    {
                        if (aType.getWorkingCode().Equals(t))
                        {
                            if (aType.Profile.Any())
                            {
                                System.Diagnostics.Debug.Assert(aType.Profile.Count() == 1);
                                url = aType.ProfileElement[0].Value;
                            }
                            else
                            {
                                url = ProfileUtilities.sdNs(t, context.getOverrideVersionNs());
                            }
                            break;
                        }
                    }
                    if (url == null)
                        throw new FHIRException("Unable to find type " + t + " for element " + elementName + " with path " + ed.Path);
                    sd = context.fetchResource<StructureDefinition>(url);
                    if (sd == null)
                        throw new DefinitionException("Unable to find type '" + t + "' for name '" + elementName + "' on property " + definition.Path);
                    children = ProfileUtilities.getChildMap(sd, sd.Snapshot.Element[0]);
                }
            }
            List<Property> properties = new List<Property>();
            foreach (ElementDefinition child in children)
            {
                properties.Add(new Property(context, child, sd));
            }
            return properties;
        }

        protected List<Property> getChildProperties(TypeDetails type)
        {
            ElementDefinition ed = definition;
            StructureDefinition sd = structure;
            List<ElementDefinition> children = ProfileUtilities.getChildMap(sd, ed);
            if (!children.Any())
            {
                // ok, find the right definitions
                String t = null;
                if (ed.Type.Count == 1)
                    t = ed.Type[0].Code;
                else if (ed.Type.Count == 0)
                    throw new FHIRException("types == 0, and no children found");
                else
                {
                    t = ed.Type[0].Code;
                    bool all = true;
                    foreach (TypeRefComponent tr in ed.Type)
                    {
                        if (!tr.Code.Equals(t))
                        {
                            all = false;
                            break;
                        }
                    }
                    if (!all)
                    {
                        // ok, it's polymorphic
                        t = type.getType();
                    }
                }
                if (!"xhtml".Equals(t))
                {
                    sd = context.fetchResource<StructureDefinition>(t);
                    if (sd == null)
                        throw new DefinitionException("Unable to find class '" + t + "' for name '" + ed.Path + "' on property " + definition.Path);
                    children = ProfileUtilities.getChildMap(sd, sd.Snapshot.Element[0]);
                }
            }
            List<Property> properties = new List<Property>();
            foreach (ElementDefinition child in children)
            {
                properties.Add(new Property(context, child, sd));
            }
            return properties;
        }

        private String tail(String path)
        {
            return path.Contains(".") ? path.Substring(path.LastIndexOf(".") + 1) : path;
        }

        public Property getChild(String elementName, String childName)
        {
            List<Property> children = getChildProperties(elementName, null);
            foreach (Property p in children)
            {
                if (p.getName().Equals(childName))
                {
                    return p;
                }
            }
            return null;
        }

        public Property getChild(String name, TypeDetails type)
        {
            List<Property> children = getChildProperties(type);
            foreach (Property p in children)
            {
                if (p.getName().Equals(name) || p.getName().Equals(name + "[x]"))
                {
                    return p;
                }
            }
            return null;
        }

        public Property getChild(String name)
        {
            List<Property> children = getChildProperties(name, null);
            foreach (Property p in children)
            {
                if (p.getName().Equals(name))
                {
                    return p;
                }
            }
            return null;
        }

        public Property getChildSimpleName(String elementName, String name)
        {
            List<Property> children = getChildProperties(elementName, null);
            foreach (Property p in children)
            {
                if (p.getName().Equals(name) || p.getName().Equals(name + "[x]"))
                {
                    return p;
                }
            }
            return null;
        }

        public IWorkerContext getContext()
        {
            return context;
        }

        public override String ToString()
        {
            return definition.Path;
        }


    }
}