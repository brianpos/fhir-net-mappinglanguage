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

// Ported from https://github.com/hapifhir/org.hl7.fhir.core/blob/master/org.hl7.fhir.r4/src/main/java/org/hl7/fhir/r4/model/TypeDetails.java

using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using static Hl7.Fhir.MappingLanguage.ExpressionNode;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;
using static Hl7.Fhir.Model.ElementDefinition;

namespace Hl7.Fhir.MappingLanguage
{
    public class TypeDetails
    {
        public const string FHIR_NS = "http://hl7.org/fhir/StructureDefinition/";
        public const string FP_NS = "http://hl7.org/fhirpath/";
        public const string FP_String = "http://hl7.org/fhirpath/String";
        public const string FP_bool = "http://hl7.org/fhirpath/bool";
        public const string FP_Integer = "http://hl7.org/fhirpath/Integer";
        public const string FP_Decimal = "http://hl7.org/fhirpath/Decimal";
        public const string FP_Quantity = "http://hl7.org/fhirpath/Quantity";
        public const string FP_DateTime = "http://hl7.org/fhirpath/DateTime";
        public const string FP_Time = "http://hl7.org/fhirpath/Time";
        public const string FP_SimpleTypeInfo = "http://hl7.org/fhirpath/SimpleTypeInfo";
        public const string FP_ClassInfo = "http://hl7.org/fhirpath/ClassInfo";

        public class ProfiledType
        {
            internal string uri;
            internal List<string> profiles; // or, not and
            internal List<ElementDefinitionBindingComponent> bindings;

            public ProfiledType(String n)
            {
                uri = ns(n);
            }

            public String getUri()
            {
                return uri;
            }

            public bool hasProfiles()
            {
                return profiles != null && profiles.Count > 0;
            }
            public List<String> getProfiles()
            {
                return profiles;
            }

            public bool hasBindings()
            {
                return bindings != null && bindings.Count > 0;
            }
            public List<ElementDefinitionBindingComponent> getBindings()
            {
                return bindings;
            }

            public static String ns(String n)
            {
                return Utilities.isAbsoluteUrl(n) ? n : FHIR_NS + n;
            }

            public void addProfile(String profile)
            {
                if (profiles == null)
                    profiles = new List<String>();
                profiles.Add(profile);
            }

            public void addBinding(ElementDefinitionBindingComponent binding)
            {
                bindings = new List<ElementDefinitionBindingComponent>();
                bindings.Add(binding);
            }

            public bool hasBinding(ElementDefinitionBindingComponent b)
            {
                return false; // todo: do we need to do this?
            }

            public void addProfiles(List<Canonical> list)
            {
                if (profiles == null)
                    profiles = new List<String>();
                foreach (var u in list)
                    profiles.Add(u.Value);
            }
            public bool isSystemType()
            {
                return uri.StartsWith(FP_NS);
            }
        }

        private List<ProfiledType> types = new List<ProfiledType>();
        private CollectionStatus? collectionStatus;
        public TypeDetails(CollectionStatus? collectionStatus, params string[] names)
        {
            this.collectionStatus = collectionStatus;
            foreach (String n in names)
            {
                this.types.Add(new ProfiledType(n));
            }
        }
        public TypeDetails(CollectionStatus collectionStatus, ISet<String> names)
        {
            this.collectionStatus = collectionStatus;
            foreach (String n in names)
            {
                addType(new ProfiledType(n));
            }
        }
        public TypeDetails(CollectionStatus collectionStatus, ProfiledType pt)
        {
            this.collectionStatus = collectionStatus;
            this.types.Add(pt);
        }
        public String addType(String n)
        {
            ProfiledType pt = new ProfiledType(n);
            String res = pt.uri;
            addType(pt);
            return res;
        }
        public String addType(String n, String p)
        {
            ProfiledType pt = new ProfiledType(n);
            pt.addProfile(p);
            String res = pt.uri;
            addType(pt);
            return res;
        }

        public void addType(ProfiledType pt)
        {
            foreach (ProfiledType et in types)
            {
                if (et.uri.Equals(pt.uri))
                {
                    if (pt.profiles != null)
                    {
                        foreach (String p in pt.profiles)
                        {
                            if (et.profiles == null)
                                et.profiles = new List<String>();
                            if (!et.profiles.Contains(p))
                                et.profiles.Add(p);
                        }
                    }
                    if (pt.bindings != null)
                    {
                        foreach (ElementDefinitionBindingComponent b in pt.bindings)
                        {
                            if (et.bindings == null)
                                et.bindings = new List<ElementDefinitionBindingComponent>();
                            if (!et.hasBinding(b))
                                et.bindings.Add(b);
                        }
                    }
                    return;
                }
            }
            types.Add(pt);
        }

        public void addTypes(IEnumerable<String> names)
        {
            foreach (String n in names)
                addType(new ProfiledType(n));
        }

        public bool hasType(IWorkerContext context, params string[] tn)
        {
            foreach (String n in tn)
            {
                String t = ProfiledType.ns(n);
                if (typesContains(t))
                    return true;
                if (Utilities.existsInList(n, "bool", "string", "integer", "decimal", "Quantity", "dateTime", "time", "ClassInfo", "SimpleTypeInfo"))
                {
                    t = FP_NS + Utilities.capitalize(n);
                    if (typesContains(t))
                        return true;
                }
            }
            foreach (String n in tn)
            {
                String id = n.Contains("#") ? n.Substring(0, n.IndexOf("#")) : n;
                String tail = null;
                if (n.Contains("#"))
                {
                    tail = n.Substring(n.IndexOf("#") + 1);
                    tail = tail.Substring(tail.IndexOf("."));
                }
                String t = ProfiledType.ns(n);
                StructureDefinition sd = context.fetchResource<StructureDefinition>(t);
                while (sd != null)
                {
                    if (tail == null && typesContains(sd.Url))
                        return true;
                    if (tail == null && getSystemType(sd.Url) != null && typesContains(getSystemType(sd.Url)))
                        return true;
                    if (tail != null && typesContains(sd.Url + "#" + sd.Type + tail))
                        return true;
                    if (!string.IsNullOrEmpty(sd.BaseDefinition))
                    {
                        if (sd.BaseDefinition.Equals("http://hl7.org/fhir/StructureDefinition/Element") && !sd.Type.Equals("string") && sd.Type.Equals("uri"))
                            sd = context.fetchResource<StructureDefinition>("http://hl7.org/fhir/StructureDefinition/string");
                        else
                            sd = context.fetchResource<StructureDefinition>(sd.BaseDefinition);
                    }
                    else
                        sd = null;
                }
            }
            return false;
        }

        private String getSystemType(String url)
        {
            if (url.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
            {
                String code = url.Substring(40);
                if (Utilities.existsInList(code, "string", "bool", "integer", "decimal", "dateTime", "time", "Quantity"))
                    return FP_NS + Utilities.capitalize(code);
            }
            return null;
        }

        private bool typesContains(String t)
        {
            foreach (ProfiledType pt in types)
                if (pt.uri.Equals(t))
                    return true;
            return false;
        }

        public void update(TypeDetails source)
        {
            foreach (ProfiledType pt in source.types)
                addType(pt);
            if (collectionStatus == null)
                collectionStatus = source.collectionStatus;
            else if (source.collectionStatus == CollectionStatus.UNORDERED)
                collectionStatus = source.collectionStatus;
            else
                collectionStatus = CollectionStatus.ORDERED;
        }
        public TypeDetails union(TypeDetails right)
        {
            TypeDetails result = new TypeDetails(null);
            if (right.collectionStatus == CollectionStatus.UNORDERED || collectionStatus == CollectionStatus.UNORDERED)
                result.collectionStatus = CollectionStatus.UNORDERED;
            else
                result.collectionStatus = CollectionStatus.ORDERED;
            foreach (ProfiledType pt in types)
                result.addType(pt);
            foreach (ProfiledType pt in right.types)
                result.addType(pt);
            return result;
        }

        public TypeDetails intersect(TypeDetails right)
        {
            TypeDetails result = new TypeDetails(null);
            if (right.collectionStatus == CollectionStatus.UNORDERED || collectionStatus == CollectionStatus.UNORDERED)
                result.collectionStatus = CollectionStatus.UNORDERED;
            else
                result.collectionStatus = CollectionStatus.ORDERED;
            foreach (ProfiledType pt in types)
            {
                bool found = false;
                foreach (ProfiledType r in right.types)
                    found = found || pt.uri.Equals(r.uri);
                if (found)
                    result.addType(pt);
            }
            foreach (ProfiledType pt in right.types)
                result.addType(pt);
            return result;
        }

        public bool hasNoTypes()
        {
            return !types.Any();
        }
        public ISet<String> getTypes()
        {
            ISet<String> res = new HashSet<String>();
            foreach (ProfiledType pt in types)
                res.Add(pt.uri);
            return res;
        }
        public TypeDetails toSingleton()
        {
            TypeDetails result = new TypeDetails(CollectionStatus.SINGLETON);
            result.types.AddRange(types);
            return result;
        }
        public CollectionStatus? getCollectionStatus()
        {
            return collectionStatus;
        }
        public bool hasType(String n)
        {
            String t = ProfiledType.ns(n);
            if (typesContains(t))
                return true;
            if (Utilities.existsInList(n, "bool", "string", "integer", "decimal", "Quantity", "dateTime", "time", "ClassInfo", "SimpleTypeInfo"))
            {
                t = FP_NS + Utilities.capitalize(n);
                if (typesContains(t))
                    return true;
            }
            return false;
        }

        public bool hasType(ISet<String> tn)
        {
            foreach (String n in tn)
            {
                String t = ProfiledType.ns(n);
                if (typesContains(t))
                    return true;
                if (Utilities.existsInList(n, "bool", "string", "integer", "decimal", "Quantity", "dateTime", "time", "ClassInfo", "SimpleTypeInfo"))
                {
                    t = FP_NS + Utilities.capitalize(n);
                    if (typesContains(t))
                        return true;
                }
            }
            return false;
        }
        public String describe()
        {
            return getTypes().ToString();
        }

        public String getType()
        {
            foreach (ProfiledType pt in types)
                return pt.uri;
            return null;
        }

        override public String ToString()
        {
            // TODO: BRIAN string join on the end here, what does the java code do with this?
            return (collectionStatus == null ? CollectionStatus.SINGLETON.ToString() : collectionStatus.ToString()) + string.Join(" ", getTypes());
        }

        public String getTypeCode()
        {
            if (types.Count() != 1)
                throw new DefinitionException("Multiple types? (" + types.ToString() + ")");
            foreach (ProfiledType pt in types)
                if (pt.uri.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
                    return pt.uri.Substring(40);
                else
                    return pt.uri;
            return null;
        }
        public List<ProfiledType> getProfiledTypes()
        {
            return types;
        }
        public bool hasBinding()
        {
            foreach (ProfiledType pt in types)
            {
                if (pt.hasBindings())
                    return true;
            }
            return false;
        }
        public ElementDefinitionBindingComponent getBinding()
        {
            foreach (ProfiledType pt in types)
            {
                foreach (ElementDefinitionBindingComponent b in pt.getBindings())
                    return b;
            }
            return null;
        }


    }

}