﻿/*
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
// (The Execution portions)

// remember group resolution
// trace - account for which wasn't transformed in the source

using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;
using static Hl7.Fhir.Model.StructureMap;

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
    public partial class StructureMapUtilitiesExecute
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

        //public interface ITransformerServices
        //{
        //    //    public bool validateByValueSet(Coding code, string valuesetId);
        //    public void log(string message); // log internal progress
        //    public Base createType(Object appInfo, string name);
        //    public Base createResource(Object appInfo, Base res, bool atRootofTransform); // an already created resource is provided; this is to identify/store it
        //    public Coding translate(Object appInfo, Coding source, string conceptMapUrl);
        //    //    public Coding translate(Coding code)
        //    //    ValueSet validation operation
        //    //    Translation operation
        //    //    Lookup another tree of data
        //    //    Create an instance tree
        //    //    Return the correct string format to refer to a tree (input or output)
        //    public Base resolveReference(Object appContext, string url);
        //    public List<Base> performSearch(Object appContext, string url);
        //}

        //private class FFHIRPathHostServices : IEvaluationContext
        //{

        //    public List<Base> resolveConstant(Object appContext, string name, bool beforeContext)
        //    {
        //        Variables vars = (Variables)appContext;
        //        Base res = vars.get(VariableMode.INPUT, name);
        //        if (res == null)
        //            res = vars.get(VariableMode.OUTPUT, name);
        //        List<Base> result = new List<Base>();
        //        if (res != null)
        //            result.Add(res);
        //        return result;
        //    }

        //    // @Override
        //    public TypeDetails resolveConstantType(Object appContext, string name)
        //    {
        //        if (!(appContext is VariablesForProfiling))
        //            throw new Exception("Internal Logic Error (wrong type '" + appContext.GetType().Name + "' in resolveConstantType)");
        //        VariablesForProfiling vars = (VariablesForProfiling)appContext;
        //        VariableForProfiling v = vars.get(null, name);
        //        if (v == null)
        //            throw new PathEngineException("Unknown variable '" + name + "' from variables " + vars.summary());
        //        return v.property.types;
        //    }

        //    // @Override
        //    public bool log(string argument, List<Base> focus)
        //    {
        //        throw new Exception("Not Implemented Yet");
        //    }

        //    // @Override
        //    public FunctionDetails resolveFunction(string functionName)
        //    {
        //        return null; // throw new Exception("Not Implemented Yet");
        //    }

        //    // @Override
        //    public TypeDetails checkFunction(Object appContext, string functionName, List<TypeDetails> parameters)
        //    {
        //        throw new Exception("Not Implemented Yet");
        //    }

        //    // @Override
        //    public List<Base> executeFunction(Object appContext, List<Base> focus, string functionName, List<List<Base>> parameters)
        //    {
        //        throw new Exception("Not Implemented Yet");
        //    }

        //    // @Override
        //    public Base resolveReference(Object appContext, string url)
        //    {
        //        if (services == null)
        //            return null;
        //        return services.resolveReference(appContext, url);
        //    }

        //    // @Override
        //    public bool conformsToProfile(Object appContext, Base item, string url)
        //    {
        //        IResourceValidator val = worker.newValidator();
        //        List<ValidationMessage> valerrors = new List<ValidationMessage>();
        //        if (item is Resource)
        //        {
        //            val.validate(appContext, valerrors, (Resource)item, url);
        //            bool ok = true;
        //            foreach (ValidationMessage v in valerrors)
        //                ok = ok && v.getLevel().isError();
        //            return ok;
        //        }
        //        throw new NotImplementedException("Not done yet (FFHIRPathHostServices.conformsToProfile), when item is element");
        //    }

        //    // @Override
        //    public ValueSet resolveValueSet(Object appContext, string url)
        //    {
        //        throw new Exception("Not Implemented Yet");
        //    }

        //}
        private IWorkerContext worker;
        private FHIRPathEngine fpe;
        private ITransformerServices services;
        private IStructureDefinitionSummaryProvider pkp;
        private Dictionary<string, int> ids = new Dictionary<string, int>();
        private TerminologyServiceOptions terminologyServiceOptions = new TerminologyServiceOptions();
        private DefaultModelFactory _factory = new DefaultModelFactory();

        public StructureMapUtilitiesExecute(IWorkerContext worker, ITransformerServices services, IStructureDefinitionSummaryProvider pkp)
        {
            this.worker = worker;
            this.services = services;
            this.pkp = pkp;
            fpe = new FHIRPathEngine(worker);
            fpe.setHostServices(new FFHIRPathHostServices());
        }

        public StructureMapUtilitiesExecute(IWorkerContext worker, ITransformerServices services)
        {
            this.worker = worker;
            this.services = services;
            fpe = new FHIRPathEngine(worker);
            fpe.setHostServices(new FFHIRPathHostServices());
        }

        public StructureMapUtilitiesExecute(IWorkerContext worker)
        {
            this.worker = worker;
            fpe = new FHIRPathEngine(worker);
            fpe.setHostServices(new FFHIRPathHostServices());
        }

        public ElementNode GenerateEmptyTargetOutputStructure(StructureMap sm)
        {
            IStructureDefinitionSummary typeInfo = null;

            GroupComponent g = sm.Group.First();
            var gt = g.Input.FirstOrDefault(i => i.Mode == StructureMapInputMode.Target);
            var s = sm.Structure.FirstOrDefault(s => s.Mode == StructureMapModelMode.Target && s.Alias == gt.Type);
            if (s != null)
            {
                // narrow this list down to the type
                typeInfo = pkp.Provide(s.Url);
            }
            else
            {
                // Scan all the targets and resolve the types to get their specific typename
                foreach (StructureComponent tt in sm.Structure.Where(s => s.Mode == StructureMapModelMode.Target && !string.IsNullOrEmpty(s.Url)))
                {
                    var ti = pkp.Provide(tt.Url);
                    if (ti != null && ti.TypeName == gt.Type)
                    {
                        typeInfo = ti;
                        break;
                    }
                }
            }
            if (typeInfo == null)
            {
                log("warning", () => $"Unable to interpret output type [{gt.Type}], using Bundle");
                typeInfo = pkp.Provide("http://hl7.org/fhir/StructureDefinition/Bundle");
            }

            var target = ElementNode.Root(pkp, typeInfo.TypeName);
            return target;
        }

        public string GetSourceInputStructure(StructureMap sm)
        {
            GroupComponent g = sm.Group.First();
            var gt = g.Input.FirstOrDefault(i => i.Mode == StructureMapInputMode.Source);
            var s = sm.Structure.FirstOrDefault(s => s.Mode == StructureMapModelMode.Source && s.Alias == gt.Type);
            if (s != null)
            {
                // narrow this list down to the type
                return s.Url;
            }
            return null;
        }

        public ITypedElement GetSourceInput(StructureMap sm, ISourceNode sourceNode, IStructureDefinitionSummaryProvider sourceProvider)
        {
            var sourceUrl = GetSourceInputStructure(sm);
            var sd = sourceProvider.Provide(sourceUrl);
            return sourceNode.ToTypedElement(sourceProvider, sd.TypeName);
        }

        private void log(string category, Func<string> message)
        {
            if (services != null)
                services.log(category, message);
            else
                System.Diagnostics.Trace.WriteLine($"{category}: {message()}");
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
        protected void getChildrenByName(ITypedElement item, string name, List<ITypedElement> result)
        {
            if (Property.isPrimitive(item.InstanceType) && name == "value")
            {
                if (item.Value != null)
                    result.Add(ElementNode.ForPrimitive(item.Value));
                else
                    log("info", () => $"{item.Location}.{name} value was null - not setting");
                return;
            }
            foreach (ITypedElement v in item.Children(name))
            {
                if (v != null)
                {
                    result.Add(v);
                }
            }
        }


        public void transform(Object appInfo, ITypedElement source, StructureMap map, ElementNode target)
        {
            TransformContext context = new TransformContext(appInfo);
            log("debug", () => "Start Transform " + map.Url);
            StructureMap.GroupComponent g = map.Group.First();

            Variables vars = new Variables();
            vars.add(VariableMode.INPUT, getInputName(g, StructureMap.StructureMapInputMode.Source, "source"), source);
            if (target != null)
                vars.add(VariableMode.OUTPUT, getInputName(g, StructureMap.StructureMapInputMode.Target, "target"), target);

            executeGroup("", context, map, vars, g, true);
            // TODO: BRIAN what is this sort oder, the ordering of the elements in the property?
            //if (target is Element)
            //    ((Element)target).sort();
        }

        private string getInputName(StructureMap.GroupComponent g, StructureMap.StructureMapInputMode mode, string def)
        {
            string name = null;
            foreach (var inp in g.Input)
            {
                if (inp.Mode == mode)
                    if (name != null)
                        throw new DefinitionException("This engine does not support multiple source inputs");
                    else
                        name = inp.Name;
            }
            return name == null ? def : name;
        }

        private void executeGroup(string indent, TransformContext context, StructureMap map, Variables vars, StructureMap.GroupComponent group, bool atRoot)
        {
            log("debug", () => indent + "Group : " + group.Name + "; vars = " + vars.summary());
            // todo: check inputs
            if (!string.IsNullOrEmpty(group.Extends))
            {
                ResolvedGroup rg = resolveGroupReference(map, group, group.Extends);
                executeGroup(indent + " ", context, rg.targetMap, vars, rg.target, false);
            }

            foreach (StructureMap.RuleComponent r in group.Rule)
            {
                executeRule(indent + "  ", context, map, vars, group, r, atRoot);
            }
        }

        private void executeRule(string indent, TransformContext context, StructureMap map, Variables vars, StructureMap.GroupComponent group, StructureMap.RuleComponent rule, bool atRoot)
        {
            log("debug", () => indent + "rule : " + rule.Name + "; vars = " + vars.summary());
            Variables srcVars = vars.copy();
            if (rule.Source.Count() != 1)
                throw new FHIRException("Rule \"" + rule.Name + "\": not handled yet");
            List<Variables> source = processSource(rule.Name, context, srcVars, rule.Source.First(), map.Url, indent);
            if (source != null)
            {
                foreach (Variables v in source)
                {
                    foreach (StructureMap.TargetComponent t in rule.Target)
                    {
                        processTarget(rule.Name, context, v, map, group, t, rule.Source.Count() == 1 ? rule.getSourceFirstRep().Variable : null, atRoot, vars);
                    }
                    if (rule.Rule.Any())
                    {
                        foreach (StructureMap.RuleComponent childrule in rule.Rule)
                        {
                            executeRule(indent + "  ", context, map, v, group, childrule, false);
                        }
                    }
                    else if (rule.Dependent.Any())
                    {
                        foreach (var dependent in rule.Dependent)
                        {
                            executeDependency(indent + "  ", context, map, v, group, dependent);
                        }
                    }
                    else if (rule.Source.Count() == 1 && !string.IsNullOrEmpty(rule.getSourceFirstRep().Variable)
                          && rule.Target.Count() == 1 && !string.IsNullOrEmpty(rule.getTargetFirstRep().Variable)
                          && rule.getTargetFirstRep().Transform == StructureMapTransform.Create
                          && !rule.getTargetFirstRep().Parameter.Any())
                    {
                        // simple inferred, map by type
                        log("debug", () => v.summary());
                        ITypedElement src = v.getInputVar(rule.getSourceFirstRep().Variable);
                        ElementNode tgt = v.getOutputVar(rule.getTargetFirstRep().Variable);
                        string srcType = src.InstanceType;
                        string tgtType = tgt.InstanceType;
                        ResolvedGroup defGroup = resolveGroupByTypes(map, rule.Name, group, srcType, tgtType);
                        Variables vdef = new Variables();
                        vdef.add(VariableMode.INPUT, defGroup.target.Input.First().Name, src);
                        vdef.add(VariableMode.OUTPUT, defGroup.target.Input[1].Name, tgt);
                        executeGroup(indent + "  ", context, defGroup.targetMap, vdef, defGroup.target, false);
                    }
                }
            }
        }

        private void executeDependency(string indent, TransformContext context, StructureMap map, Variables vin, StructureMap.GroupComponent group, StructureMap.DependentComponent dependent)
        {
            ResolvedGroup rg = resolveGroupReference(map, group, dependent.Name);

            if (rg.target.Input.Count != dependent.Variable.Count())
            {
                throw new FHIRException($"Rule '{dependent.Name}' has {rg.target.Input.Count()} but the invocation has {dependent.Variable.Count()} variables");
            }
            Variables v = new Variables();
            for (int i = 0; i < rg.target.Input.Count(); i++)
            {
                var input = rg.target.Input[i];
                var rdp = dependent.VariableElement[i];
                string varVal = rdp.Value;
                VariableMode mode = input.Mode == StructureMap.StructureMapInputMode.Source ? VariableMode.INPUT : VariableMode.OUTPUT;
                ITypedElement vv = vin.get(mode, varVal);
                if (vv == null && mode == VariableMode.INPUT) // once source, always source. but target can be treated as source at user convenient
                    vv = vin.getOutputVar(varVal);
                if (vv == null)
                    throw new FHIRException("Rule '" + dependent.Name + "' " + mode.ToString() + " variable '" + input.Name + "' named as '" + varVal + "' has no value (vars = " + vin.summary() + ")");
                v.add(mode, input.Name, vv);
            }
            executeGroup(indent + "  ", context, rg.targetMap, v, rg.target, false);
        }

        private string determineTypeFromSourceType(StructureMap map, StructureMap.GroupComponent source, ITypedElement baseV, string[] types)
        {
            string type = baseV.InstanceType;
            string kn = "type^" + type;
            if (source.hasUserData(kn))
                return source.getUserData(kn) as string;

            ResolvedGroup res = new ResolvedGroup();
            res.targetMap = null;
            res.target = null;
            foreach (StructureMap.GroupComponent grp in map.Group)
            {
                if (matchesByType(map, grp, type))
                {
                    if (res.targetMap == null)
                    {
                        res.targetMap = map;
                        res.target = grp;
                    }
                    else
                        throw new FHIRException("Multiple possible matches looking for default rule for '" + type + "'");
                }
            }
            if (res.targetMap != null)
            {
                string resultT = getActualType(res.targetMap, res.target.Input[1].Type);
                source.setUserData(kn, resultT);
                return resultT;
            }

            foreach (var imp in map.ImportElement)
            {
                List<StructureMap> impMapList = findMatchingMaps(imp.Value);
                if (impMapList.Count() == 0)
                    throw new FHIRException("Unable to find map(s) for " + imp.Value);
                foreach (StructureMap impMap in impMapList)
                {
                    if (!impMap.Url.Equals(map.Url))
                    {
                        foreach (StructureMap.GroupComponent grp in impMap.Group)
                        {
                            if (matchesByType(impMap, grp, type))
                            {
                                if (res.targetMap == null)
                                {
                                    res.targetMap = impMap;
                                    res.target = grp;
                                }
                                else
                                    throw new FHIRException("Multiple possible matches for default rule for '" + type + "' in " + res.targetMap.Url + " (" + res.target.Name + ") and " + impMap.Url + " (" + grp.Name + ")");
                            }
                        }
                    }
                }
            }
            if (res.target == null)
                throw new FHIRException("No matches found for default rule for '" + type + "' from " + map.Url);
            string result = getActualType(res.targetMap, res.target.Input[1].Type); // should be .getType, but R2...
            source.setUserData(kn, result);
            return result;
        }

        /// <summary>
        /// Find any Maps that match the given template
        /// </summary>
        /// <param name="canonicalUrlTemplate">this could be a regular canonical URL, or could include the wildcard char *</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private List<StructureMap> findMatchingMaps(string canonicalUrlTemplate)
        {
            List<StructureMap> res = new List<StructureMap>();
            if (canonicalUrlTemplate.Contains("*"))
            {
                // TODO: BRIAN Note: this looks like a real runtime performance possible penalty
                foreach (StructureMap sm in worker.listTransforms(canonicalUrlTemplate))
                {
                    if (urlMatches(canonicalUrlTemplate, sm.Url))
                    {
                        res.Add(sm);
                    }
                }
            }
            else
            {
                StructureMap sm = worker.getTransform(canonicalUrlTemplate);
                if (sm != null)
                    res.Add(sm);
            }
            ISet<string> check = new HashSet<string>();
            foreach (StructureMap sm in res)
            {
                if (check.Contains(sm.Url))
                    throw new Exception("duplicate");
                else
                    check.Add(sm.Url);
            }
            return res;
        }

        private bool urlMatches(string mask, string url)
        {
            return url.Length > mask.Length && url.StartsWith(mask.Substring(0, mask.IndexOf("*"))) && url.EndsWith(mask.Substring(mask.IndexOf("*") + 1));
        }

        private ResolvedGroup resolveGroupByTypes(StructureMap map, string ruleid, StructureMap.GroupComponent source, string srcType, string tgtType)
        {
            string kn = "types^" + srcType + "in" + tgtType;
            if (source.hasUserData(kn))
                return (ResolvedGroup)source.getUserData(kn);

            ResolvedGroup res = new ResolvedGroup();
            res.targetMap = null;
            res.target = null;
            foreach (StructureMap.GroupComponent grp in map.Group)
            {
                if (matchesByType(map, grp, srcType, tgtType))
                {
                    if (res.targetMap == null)
                    {
                        res.targetMap = map;
                        res.target = grp;
                    }
                    else
                        throw new FHIRException("Multiple possible matches looking for rule for '" + srcType + "/" + tgtType + "', from rule '" + ruleid + "'");
                }
            }
            if (res.targetMap != null)
            {
                source.setUserData(kn, res);
                return res;
            }

            foreach (var imp in map.ImportElement)
            {
                List<StructureMap> impMapList = findMatchingMaps(imp.Value);
                if (impMapList.Count == 0)
                    throw new FHIRException("Unable to find map(s) for " + imp.Value);
                foreach (StructureMap impMap in impMapList)
                {
                    if (!impMap.Url.Equals(map.Url))
                    {
                        foreach (StructureMap.GroupComponent grp in impMap.Group)
                        {
                            if (matchesByType(impMap, grp, srcType, tgtType))
                            {
                                if (res.targetMap == null)
                                {
                                    res.targetMap = impMap;
                                    res.target = grp;
                                }
                                else
                                    throw new FHIRException("Multiple possible matches for rule for '" + srcType + "/" + tgtType + "' in " + res.targetMap.Url + " and " + impMap.Url + ", from rule '" + ruleid + "'");
                            }
                        }
                    }
                }
            }
            if (res.target == null)
                throw new FHIRException("No matches found for rule for '" + srcType + " to " + tgtType + "' from " + map.Url + ", from rule '" + ruleid + "'");
            source.setUserData(kn, res);
            return res;
        }


        private bool matchesByType(StructureMap map, StructureMap.GroupComponent grp, string type)
        {
            if (grp.TypeMode != StructureMapGroupTypeMode.TypeAndTypes)
                return false;
            if (grp.Input.Count() != 2 || grp.Input.First().Mode != StructureMapInputMode.Source || grp.Input[1].Mode != StructureMapInputMode.Target)
                return false;
            return matchesType(map, type, grp.Input.First().Type);
        }

        private bool matchesByType(StructureMap map, StructureMap.GroupComponent grp, string srcType, string tgtType)
        {
            if (grp.TypeMode == StructureMapGroupTypeMode.None)
                return false;
            if (grp.Input.Count() != 2 || grp.Input.First().Mode != StructureMapInputMode.Source || grp.Input[1].Mode != StructureMapInputMode.Target)
                return false;
            if (string.IsNullOrEmpty(grp.Input.First().Type) || string.IsNullOrEmpty(grp.Input[1].Type))
                return false;
            return matchesType(map, srcType, grp.Input.First().Type) && matchesType(map, tgtType, grp.Input[1].Type);
        }

        private bool matchesType(StructureMap map, string actualType, string statedType)
        {
            // check the aliases
            foreach (StructureMap.StructureComponent imp in map.Structure)
            {
                if (!string.IsNullOrEmpty(imp.Alias) && statedType.Equals(imp.Alias))
                {
                    StructureDefinition sd = worker.fetchResource<StructureDefinition>(imp.Url);
                    if (sd != null)
                        statedType = sd.Type;
                    else
                    {
                        // failed to find the type
                        log("error", () => $"Failed to find {imp.Url}");
                    }
                    break;
                }
            }

            if (Utilities.isAbsoluteUrl(actualType))
            {
                StructureDefinition sd = worker.fetchResource<StructureDefinition>(actualType);
                if (sd != null)
                    actualType = sd.Type;
            }
            if (Utilities.isAbsoluteUrl(statedType))
            {
                StructureDefinition sd = worker.fetchResource<StructureDefinition>(statedType);
                if (sd != null)
                    statedType = sd.Type;
            }
            return actualType.Equals(statedType);
        }

        public static Dictionary<string, string> getCanonicalTypeMapping(IWorkerContext worker, StructureMap map)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (StructureMap.StructureComponent structure in map.Structure)
            {
                if (structure.Mode == StructureMapModelMode.Target && !result.ContainsValue(structure.Url))
                {
                    StructureDefinition sd = worker.fetchResource<StructureDefinition>(structure.Url);
                    if (sd == null)
                        throw new FHIRException("Unable to resolve structure " + structure.Url);
                    var url = sd.Derivation == StructureDefinition.TypeDerivationRule.Constraint ? sd.BaseDefinition : sd.Url;
                    if (!result.ContainsValue(url))
                        result.Add(sd.Type, url);
                }
            }

            return result;

        }

        private string getActualType(StructureMap map, string statedType)
        {
            // check the aliases
            foreach (StructureMap.StructureComponent imp in map.Structure)
            {
                if (imp.Alias != null && statedType.Equals(imp.Alias))
                {
                    StructureDefinition sd = worker.fetchResource<StructureDefinition>(imp.Url);
                    if (sd == null)
                        throw new FHIRException("Unable to resolve structure " + imp.Url);
                    return sd.Type; // should be sd.Type, but R2...
                }
            }

            return statedType;
        }


        private ResolvedGroup resolveGroupReference(StructureMap map, StructureMap.GroupComponent source, string name)
        {
            string kn = "ref^" + name;
            if (source.hasUserData(kn))
                return (ResolvedGroup)source.getUserData(kn);

            ResolvedGroup res = new ResolvedGroup();
            res.targetMap = null;
            res.target = null;
            foreach (StructureMap.GroupComponent grp in map.Group)
            {
                if (grp.Name.Equals(name))
                {
                    if (res.targetMap == null)
                    {
                        res.targetMap = map;
                        res.target = grp;
                    }
                    else
                        throw new FHIRException("Multiple possible matches for rule '" + name + "'");
                }
            }
            if (res.targetMap != null)
            {
                source.setUserData(kn, res);
                return res;
            }

            foreach (var imp in map.ImportElement)
            {
                List<StructureMap> impMapList = findMatchingMaps(imp.Value);
                if (impMapList.Count() == 0)
                    throw new FHIRException("Unable to find map(s) for " + imp.Value);
                foreach (StructureMap impMap in impMapList)
                {
                    if (!impMap.Url.Equals(map.Url))
                    {
                        foreach (StructureMap.GroupComponent grp in impMap.Group)
                        {
                            if (grp.Name.Equals(name))
                            {
                                if (res.targetMap == null)
                                {
                                    res.targetMap = impMap;
                                    res.target = grp;
                                }
                                else
                                    throw new FHIRException("Multiple possible matches for rule group '" + name + "' in " +
                                     res.targetMap.Url + "#" + res.target.Name + " and " +
                                     impMap.Url + "#" + grp.Name);
                            }
                        }
                    }
                }
            }
            if (res.target == null)
                throw new FHIRException("No matches found for rule '" + name + "'. Reference found in " + map.Url);
            source.setUserData(kn, res);
            return res;
        }

        private List<Variables> processSource(string ruleId, TransformContext context, Variables vars, StructureMap.SourceComponent src, string pathForErrors, string indent)
        {
            List<ITypedElement> items;
            if (src.Context.Equals("@search"))
            {
                ExpressionNode expr = (ExpressionNode)src.getUserData(MAP_SEARCH_EXPRESSION);
                if (expr == null)
                {
                    expr = fpe.parse(src.Element);
                    src.setUserData(MAP_SEARCH_EXPRESSION, expr);
                }
                string search = fpe.evaluateToString(vars, null, null, ElementNode.ForPrimitive(""), expr); // string is a holder of nothing to ensure that variables are processed correctly
                items = services.performSearch(context.getAppInfo(), search);
            }
            else
            {
                items = new List<ITypedElement>();
                ITypedElement b = vars.getInputVar(src.Context);
                if (b == null)
                    throw new FHIRException("Unknown input variable " + src.Context + " in " + pathForErrors + " rule " + ruleId + " (vars = " + vars.summary() + ")");

                if (string.IsNullOrEmpty(src.Element))
                    items.Add(b);
                else
                {
                    getChildrenByName(b, src.Element, items);
                    if (items.Count() == 0 && src.DefaultValue != null)
                        items.Add(src.DefaultValue.ToTypedElement());
                }
            }

            if (!string.IsNullOrEmpty(src.Type))
            {
                List<ITypedElement> remove = new List<ITypedElement>();
                foreach (ITypedElement item in items)
                {
                    if (item != null && !isType(item, src.Type))
                    {
                        remove.Add(item);
                    }
                }
                items.RemoveAll(r => remove.Contains(r));
            }

            if (!string.IsNullOrEmpty(src.Condition))
            {
                ExpressionNode expr = (ExpressionNode)src.getUserData(MAP_WHERE_EXPRESSION);
                if (expr == null)
                {
                    expr = fpe.parse(src.Condition);
                    //        fpe.check(context.appInfo, ??, ??, expr)
                    src.setUserData(MAP_WHERE_EXPRESSION, expr);
                }
                List<ITypedElement> remove = new List<ITypedElement>();
                foreach (ITypedElement item in items)
                {
                    if (!fpe.evaluateToBoolean(vars, null, null, item, expr))
                    {
                        log("debug", () => indent + $"  condition [{src.Condition}] for {item.ToJson()} [{item.InstanceType}] : false");
                        remove.Add(item);
                    }
                    else
                        log("debug", () => indent + "  condition [" + src.Condition + "] for " + item.ToJson() + " : true");
                }
                items.RemoveAll(r => remove.Contains(r));
            }

            if (!string.IsNullOrEmpty(src.Check))
            {
                ExpressionNode expr = (ExpressionNode)src.getUserData(MAP_WHERE_CHECK);
                if (expr == null)
                {
                    expr = fpe.parse(src.Check);
                    //        fpe.check(context.appInfo, ??, ??, expr)
                    src.setUserData(MAP_WHERE_CHECK, expr);
                }
                List<ITypedElement> remove = new List<ITypedElement>();
                foreach (ITypedElement item in items)
                {
                    if (!fpe.evaluateToBoolean(vars, null, null, item, expr))
                        throw new FHIRException("Rule \"" + ruleId + "\": Check condition failed");
                }
            }

            if (!string.IsNullOrEmpty(src.LogMessage))
            {
                ExpressionNode expr = (ExpressionNode)src.getUserData(MAP_WHERE_LOG);
                if (expr == null)
                {
                    expr = fpe.parse(src.LogMessage);
                    //        fpe.check(context.appInfo, ??, ??, expr)
                    src.setUserData(MAP_WHERE_LOG, expr);
                }
                CommaSeparatedStringBuilder b = new CommaSeparatedStringBuilder();
                foreach (ITypedElement item in items)
                    b.appendIfNotNull(fpe.evaluateToString(vars, null, null, item, expr));
                if (b.Length() > 0)
                    log("info", () => b.ToString());
            }

            if (src.ListMode.HasValue && items.Any())
            {
                switch (src.ListMode)
                {
                    case StructureMapSourceListMode.First:
                        ITypedElement bt = items.First();
                        items.Clear();
                        items.Add(bt);
                        break;
                    case StructureMapSourceListMode.Not_first:
                        if (items.Count() > 0)
                            items.RemoveAt(0);
                        break;
                    case StructureMapSourceListMode.Last:
                        bt = items[items.Count() - 1];
                        items.Clear();
                        items.Add(bt);
                        break;
                    case StructureMapSourceListMode.Not_last:
                        if (items.Count() > 0)
                            items.RemoveAt(items.Count() - 1);
                        break;
                    case StructureMapSourceListMode.Only_one:
                        if (items.Count() > 1)
                            throw new FHIRException("Rule \"" + ruleId + "\": Check condition failed: the collection has more than one item");
                        break;
                }
            }
            List<Variables> result = new List<Variables>();
            foreach (ITypedElement r in items)
            {
                Variables v = vars.copy();
                if (!string.IsNullOrEmpty(src.Variable))
                    v.add(VariableMode.INPUT, src.Variable, r);
                result.Add(v);
            }
            return result;
        }


        private bool isType(ITypedElement item, string type)
        {
            if (type.Equals(item.InstanceType))
                return true;
            return false;
        }

        private void processTarget(string ruleId, TransformContext context, Variables vars, StructureMap map, StructureMap.GroupComponent group, StructureMap.TargetComponent tgt, string srcVar, bool atRoot, Variables sharedVars)
        {
            ITypedElement dest = null;
            if (!string.IsNullOrEmpty(tgt.Context))
            {
                dest = vars.getOutputVar(tgt.Context);
                if (dest == null)
                    throw new FHIRException("Rule \"" + ruleId + "\": target context not known: " + tgt.Context);
                if (string.IsNullOrEmpty(tgt.Element))
                    throw new FHIRException("Rule \"" + ruleId + "\": Not supported yet");
            }
            ITypedElement v = null;
            if (tgt.Transform.HasValue)
            {
                v = runTransform(ruleId, context, map, group, tgt, vars, dest, tgt.Element, srcVar, atRoot);
                if (v != null && dest != null)
                    v = dest.setProperty(log, pkp, tgt.Element, v); // reset v because some implementations may have to rewrite v when setting the value
            }
            else if (dest != null)
            {
                if (tgt.ListMode.Any(lm => lm == StructureMapTargetListMode.Share))
                {
                    v = sharedVars.get(VariableMode.SHARED, tgt.ListRuleId);
                    if (v == null)
                    {
                        v = dest.makeProperty(log, pkp, tgt.Element);
                        sharedVars.add(VariableMode.SHARED, tgt.ListRuleId, v);
                    }
                }
                else
                {
                    v = dest.makeProperty(log, pkp, tgt.Element);
                }
            }
            if (!string.IsNullOrEmpty(tgt.Variable) && v != null)
                vars.add(VariableMode.OUTPUT, tgt.Variable, v);
        }


        private ITypedElement runTransform(string ruleId, TransformContext context, StructureMap map, StructureMap.GroupComponent group, StructureMap.TargetComponent tgt, Variables vars, ITypedElement dest, string element, string srcVar, bool root)
        {
            try
            {
                switch (tgt.Transform)
                {
                    case StructureMap.StructureMapTransform.Create:
                        string tn;
                        if (!tgt.Parameter.Any())
                        {
                            // we have to work out the type. First, we see if there is a single type for the target. If there is, we use that
                            string[] types = dest.getTypesForProperty(pkp, element);
                            if (types.Count() == 1 && !"*".Equals(types[0]) && !types[0].Equals("Resource"))
                                tn = types[0];
                            else if (srcVar != null)
                            {
                                tn = determineTypeFromSourceType(map, group, vars.getInputVar(srcVar), types);
                            }
                            else
                                throw new Exception("Cannot determine type implicitly because there is no single input variable");
                        }
                        else
                        {
                            var reqType = getParamStringNoNull(vars, tgt.Parameter.First(), tgt.ToString());
                            tn = getActualType(map, reqType);
                        }
                        ITypedElement res = services != null ? services.createType(context.getAppInfo(), tn) : ElementNode.Root(pkp, tn) as ITypedElement;
                        if (!res.InstanceType.Equals("Parameters"))
                        {
                            //	        res.setIdBase(tgt.Parameter.Count() > 1 ? getParamString(vars, tgt.Parameter.First()) : UUID.randomUUID().ToString().toLowerCase());
                            if (services != null)
                                res = services.createResource(context.getAppInfo(), res, root);
                        }
                        if (tgt.hasUserData("profile"))
                            res.setUserData("profile", tgt.getUserData("profile"));
                        log("debug", () => $"Create {tn}");
                        return res;

                    case StructureMap.StructureMapTransform.Copy:
                        return getParam(vars, tgt.Parameter.First());

                    case StructureMap.StructureMapTransform.Evaluate:
                        ExpressionNode expr = (ExpressionNode)tgt.getUserData(MAP_EXPRESSION);
                        if (expr == null)
                        {
                            // This is a bug in the JAVA code too!
                            // Log it with Grahame to be fixed
                            if (tgt.Parameter.Count == 1)
                            {
                                // This is the "short circuit" format of the fhirpath expression
                                var expression = getParamStringNoNull(vars, tgt.Parameter[0], tgt.ToString());
                                expr = fpe.parse(expression);
                                if (!string.IsNullOrEmpty(expr.getName()) && !expr.getName().StartsWith("%"))
                                {
                                    // Check if this name is in the variables
                                    if (vars.All().Any(v => v.Name == expr.getName()))
                                        expr.setName("%" + expr.getName());
                                }
                            }
                            else if (tgt.Parameter.Count == 2)
                                expr = fpe.parse(getParamStringNoNull(vars, tgt.Parameter[1], tgt.ToString()));
                            tgt.setUserData(MAP_EXPRESSION, expr);
                        }
                        IEnumerable<ITypedElement> v = fpe.evaluate(vars, null, null, tgt.Parameter.Count() == 2 ? getParam(vars, tgt.Parameter.First()) : ElementNode.ForPrimitive(false), expr);
                        if (v.Count() == 0)
                            return null;
                        else if (v.Count() != 1)
                            throw new FHIRException($"Rule \"{ruleId}\": Evaluation of {expr.ToString()} returned {v.Count()} objects");
                        else
                            return v.First();

                    case StructureMap.StructureMapTransform.Truncate:
                        string src = getParamString(vars, tgt.Parameter.First());
                        string len = getParamStringNoNull(vars, tgt.Parameter[1], tgt.ToString());
                        if (Utilities.isInteger(len))
                        {
                            int l = int.Parse(len);
                            if (src.Length > l)
                                src = src.Substring(0, l);
                        }
                        return ElementNode.ForPrimitive(src);

                    case StructureMap.StructureMapTransform.Escape:
                        throw new Exception("Rule \"" + ruleId + "\": Transform " + tgt.Transform.GetLiteral() + " not supported yet");

                    case StructureMap.StructureMapTransform.Cast:
                        src = getParamString(vars, tgt.Parameter.First());
                        if (tgt.Parameter.Count() == 1)
                            throw new FHIRException("Implicit type parameters on cast not yet supported");
                        string t = getParamString(vars, tgt.Parameter[1]);
                        if (t.Equals("string"))
                            return ElementNode.ForPrimitive(src);
                        else
                            throw new FHIRException("cast to " + t + " not yet supported");

                    case StructureMap.StructureMapTransform.Append:
                        StringBuilder sb = new StringBuilder(getParamString(vars, tgt.Parameter.First()));
                        for (int i = 1; i < tgt.Parameter.Count(); i++)
                            sb.Append(getParamString(vars, tgt.Parameter[i]));
                        return ElementNode.ForPrimitive(sb.ToString());

                    case StructureMap.StructureMapTransform.Translate:
                        return translate(context, map, vars, tgt.Parameter);

                    case StructureMap.StructureMapTransform.Reference:
                        ITypedElement b = getParam(vars, tgt.Parameter.First());
                        if (b == null)
                            throw new FHIRException("Rule \"" + ruleId + "\": Unable to find parameter " + ((Id)tgt.Parameter.First().Value).ToString());
                        if (!ModelInfo.IsKnownResource(b.InstanceType) && !ModelInfo.IsCoreModelTypeUri(new Uri(b.InstanceType, UriKind.RelativeOrAbsolute)))
                            throw new FHIRException("Rule \"" + ruleId + "\": Transform engine cannot point at an element of type " + b.InstanceType);
                        else
                        {
                            string id = b.Children("id")?.FirstOrDefault()?.Value as string;
                            if (id == null)
                            {
                                id = Guid.NewGuid().ToFhirId();
                                b.setProperty(log, pkp, "id", ElementNode.ForPrimitive(id));
                            }
                            return ElementNode.ForPrimitive(b.InstanceType + "/" + id);
                        }
                    case StructureMap.StructureMapTransform.DateOp:
                        throw new Exception("Rule \"" + ruleId + "\": Transform " + tgt.Transform.GetLiteral() + " not supported yet");

                    case StructureMap.StructureMapTransform.Uuid:
                        return new Id(Guid.NewGuid().ToFhirId()).ToTypedElement();

                    case StructureMap.StructureMapTransform.Pointer:
                        b = getParam(vars, tgt.Parameter.First());
                        if (b is Resource)
                            return new FhirUri("urn:uuid:" + ((Resource)b).Id).ToTypedElement();

                        else
                            throw new FHIRException("Rule \"" + ruleId + "\": Transform engine cannot point at an element of type " + b.InstanceType);
                    case StructureMap.StructureMapTransform.Cc:
                        CodeableConcept cc = new CodeableConcept();
                        if (tgt.Parameter.Count == 0)
                            throw new Exception($"Rule \"{ruleId}\": cannot transform a codeableconcept with no parameters");
                        if (tgt.Parameter.Count == 1)
                            cc.Text = getParamStringNoNull(vars, tgt.Parameter.First(), tgt.ToString());
                        else
                            cc.Coding.Add(buildCoding(getParamStringNoNull(vars, tgt.Parameter.First(), tgt.ToString()), getParamStringNoNull(vars, tgt.Parameter[1], tgt.ToString()), tgt.Parameter.Count > 2 ? getParamStringNoNull(vars, tgt.Parameter[2], tgt.ToString()) : null));
                        return cc.ToTypedElement();

                    case StructureMap.StructureMapTransform.C:
                        if (tgt.Parameter.Count < 2)
                            throw new Exception($"Rule \"{ruleId}\": cannot transform a Coding with less than 2 parameters");
                        {
                            string system = getParamStringNoNull(vars, tgt.Parameter.First(), tgt.ToString());
                            string code = getParamStringNoNull(vars, tgt.Parameter[1], tgt.ToString());
                            string display = tgt.Parameter.Count > 2 ? getParamStringNoNull(vars, tgt.Parameter[2], tgt.ToString()) : null;
                            Coding c = buildCoding(system, code, display);
                        return c.ToTypedElement();
                        }

                    default:
                        throw new Exception("Rule \"" + ruleId + "\": Transform Unknown: " + tgt.Transform.GetLiteral());
                }
            }
            catch (Exception e)
            {
                throw new FHIRException("Exception executing transform " + tgt.ToString() + " on Rule \"" + ruleId + "\": " + e.Message, e);
            }
        }


        private Coding buildCoding(string system, string code, string display)
        {
            // if we can get this as a valueSet, we will
            //string system = null;
            //ValueSet vs = Utilities.noString(uri) ? null : worker.fetchResourceWithException<ValueSet>(uri);
            //if (vs != null)
            //{
            //    var vse = worker.expandVS(vs, true, false);
            //    //if (vse.getError() != null)
            //    //    throw new FHIRException(vse.getError());
            //    CommaSeparatedStringBuilder b = new CommaSeparatedStringBuilder();
            //    foreach (var t in vse.Contains)
            //    {
            //        if (!string.IsNullOrEmpty(t.Code))
            //            b.append(t.Code);
            //        if (code.Equals(t.Code) && !string.IsNullOrEmpty(t.System))
            //        {
            //            system = t.System;
            //            display = t.Display;
            //            break;
            //        }
            //        if (code.Equals(t.Display, StringComparison.InvariantCultureIgnoreCase) && !string.IsNullOrEmpty(t.System))
            //        {
            //            system = t.System;
            //            display = t.Display;
            //            break;
            //        }
            //    }
            //    if (system == null)
            //        throw new FHIRException("The code '" + code + "' is not in the value set '" + uri + "' (valid codes: " + b.ToString() + "; also checked displays)");
            //}
            //else
            //    system = uri;
            ValidationResult vr = worker.validateCode(terminologyServiceOptions, system, code, null);
            if (vr != null && vr.getDisplay() != null)
                display = vr.getDisplay();
            return new Coding(system, code, display);
        }


        private string getParamStringNoNull(Variables vars, StructureMap.ParameterComponent parameter, string message)
        {
            ITypedElement b = getParam(vars, parameter);
            if (b == null)
                throw new FHIRException("Unable to find a value for " + parameter.Value.ToString() + ". Context: " + message);
            if (Property.isPrimitive(b.InstanceType))
                return b.Value?.ToString();
            throw new FHIRException("Found a value for " + parameter.ToString() + ", but it has a type of " + b.InstanceType + " and cannot be treated as a string. Context: " + message);
        }

        private string getParamString(Variables vars, StructureMap.ParameterComponent parameter)
        {
            ITypedElement b = getParam(vars, parameter);
            if (Property.isPrimitive(b.InstanceType))
                return b.Value?.ToString();
            return null;
        }

        private ITypedElement getParam(Variables vars, StructureMap.ParameterComponent parameter)
        {
            var p = parameter.Value as Id;
            if (p == null)
                return parameter.Value.ToTypedElement();

            string n = p.Value;
            ITypedElement b = vars.getInputVar(n);
            if (b == null)
                b = vars.getOutputVar(n);
            if (b == null)
                throw new DefinitionException("Variable " + n + " not found (" + vars.summary() + ")");
            return b;
        }


        private ITypedElement translate(TransformContext context, StructureMap map, Variables vars, List<StructureMap.ParameterComponent> parameter)
        {
            ITypedElement src = getParam(vars, parameter.First());
            string id = getParamString(vars, parameter[1]);
            string fld = parameter.Count() > 2 ? getParamString(vars, parameter[2]) : null;
            return translate(context, map, src, id, fld);
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

        public ITypedElement translate(TransformContext context, StructureMap map, ITypedElement source, string conceptMapUrl, string fieldToReturn)
        {
            Coding src = new Coding();
            if (Property.isPrimitive(source.InstanceType))
            {
                src.Code = source.Value.ToString();
            }
            else if (source is Coding coding)
            {
                src.System = coding.System;
                src.Code = coding.Code;
                src.Display = coding.Display;
            }
            // TODO: BRIAN what is the typename "CE"
            //else if ("CE".Equals(source.TypeName))
            //{
            //    Base[] b = source.getProperty("codeSystem", true);
            //    if (b.Length == 1)
            //        src.System = b[0].primitiveValue();
            //    b = source.getProperty("code", true);
            //    if (b.Length == 1)
            //        src.Code = b[0].primitiveValue();
            //}
            else
                throw new FHIRException("Unable to translate source " + source.InstanceType);

            string su = conceptMapUrl;
            if (conceptMapUrl.Equals("http://hl7.org/fhir/ConceptMap/special-oid2uri"))
            {
                string uri = worker.oid2Uri(src.Code);
                if (uri == null)
                    uri = "urn:oid:" + src.Code;
                if ("uri".Equals(fieldToReturn))
                    return new FhirUri(uri).ToTypedElement();
                else
                    throw new FHIRException("Error in return code");
            }
            else
            {
                ConceptMap cmap = null;
                if (conceptMapUrl.StartsWith("#"))
                {
                    foreach (Resource r in map.Contained)
                    {
                        if (r is ConceptMap && ((ConceptMap)r).Id.Equals(conceptMapUrl.Substring(1)))
                        {
                            cmap = (ConceptMap)r;
                            su = map.Url + "#" + conceptMapUrl;
                        }
                    }
                    if (cmap == null)
                        throw new FHIRException("Unable to translate - cannot find map " + conceptMapUrl);
                }
                else
                {
                    if (conceptMapUrl.Contains("#"))
                    {
                        string[] p = conceptMapUrl.Split('#');
                        StructureMap mapU = worker.fetchResource<StructureMap>(p[0]);
                        foreach (Resource r in mapU.Contained)
                        {
                            if (r is ConceptMap && ((ConceptMap)r).Id.Equals(p[1]))
                            {
                                cmap = (ConceptMap)r;
                                su = conceptMapUrl;
                            }
                        }
                    }
                    if (cmap == null)
                        cmap = worker.fetchResource<ConceptMap>(conceptMapUrl);
                }
                Coding outcome = null;
                bool done = false;
                string message = null;
                if (cmap == null)
                {
                    if (services == null)
                        message = "No map found for " + conceptMapUrl;
                    else
                    {
                        outcome = services.translate(context.getAppInfo(), src, conceptMapUrl);
                        done = true;
                    }
                }
                else
                {
                    List<SourceElementComponentWrapper> list = new List<SourceElementComponentWrapper>();
                    foreach (ConceptMap.GroupComponent g in cmap.Group)
                    {
                        foreach (ConceptMap.SourceElementComponent e in g.Element)
                        {
                            if (string.IsNullOrEmpty(src.System) && src.Code.Equals(e.Code))
                                list.Add(new SourceElementComponentWrapper(g, e));
                            else if (!string.IsNullOrEmpty(src.System) && src.System.Equals(g.Source) && src.Code.Equals(e.Code))
                                list.Add(new SourceElementComponentWrapper(g, e));
                        }
                    }
                    if (list.Count() == 0)
                        done = true;
                    else if (list.First().comp.Target.Count() == 0)
                        message = "Concept map " + su + " found no translation for " + src.Code;
                    else
                    {
                        foreach (var tgt in list.First().comp.Target)
                        {
                            var equivalentTargets = new[] { ConceptMapEquivalence.Equal, ConceptMapEquivalence.Relatedto, ConceptMapEquivalence.Equivalent, ConceptMapEquivalence.Wider };
                            if (!tgt.Equivalence.HasValue || equivalentTargets.Contains(tgt.Equivalence.Value))
                            {
                                if (done)
                                {
                                    message = "Concept map " + su + " found multiple matches for " + src.Code;
                                    done = false;
                                }
                                else
                                {
                                    done = true;
                                    outcome = new Coding(list.First().group.Target, tgt.Code);
                                }
                            }
                            else if (tgt.Equivalence == ConceptMapEquivalence.Unmatched)
                            {
                                done = true;
                            }
                        }
                        if (!done)
                            message = "Concept map " + su + " found no usable translation for " + src.Code;
                    }
                }
                if (!done)
                    throw new FHIRException(message);
                if (outcome == null)
                    return null;
                if ("code".Equals(fieldToReturn))
                    return new Code(outcome.Code).ToTypedElement();
                else
                    return outcome.ToTypedElement();
            }
        }


        //public class PropertyWithType
        //{
        //    private string path;
        //    private Property baseProperty;
        //    private Property profileProperty;
        //    private TypeDetails types;
        //    public PropertyWithType(string path, Property baseProperty, Property profileProperty, TypeDetails types)
        //    {

        //        this.baseProperty = baseProperty;
        //        this.profileProperty = profileProperty;
        //        this.path = path;
        //        this.types = types;
        //    }

        //    public TypeDetails getTypes()
        //    {
        //        return types;
        //    }
        //    public string getPath()
        //    {
        //        return path;
        //    }

        //    public Property getBaseProperty()
        //    {
        //        return baseProperty;
        //    }

        //    public void setBaseProperty(Property baseProperty)
        //    {
        //        this.baseProperty = baseProperty;
        //    }

        //    public Property getProfileProperty()
        //    {
        //        return profileProperty;
        //    }

        //    public void setProfileProperty(Property profileProperty)
        //    {
        //        this.profileProperty = profileProperty;
        //    }

        //    public string summary()
        //    {
        //        return path;
        //    }

        //}

        //public class VariableForProfiling
        //{
        //    private VariableMode mode;
        //    private string name;
        //    internal PropertyWithType property;

        //    public VariableForProfiling(VariableMode mode, string name, PropertyWithType property)
        //    {

        //        this.mode = mode;
        //        this.name = name;
        //        this.property = property;
        //    }
        //    public VariableMode getMode()
        //    {
        //        return mode;
        //    }
        //    public string getName()
        //    {
        //        return name;
        //    }
        //    public PropertyWithType getProperty()
        //    {
        //        return property;
        //    }
        //    public string summary()
        //    {
        //        return name + ": " + property.summary();
        //    }
        //}

        //public class VariablesForProfiling
        //{
        //    private List<VariableForProfiling> list = new List<VariableForProfiling>();
        //    private bool optional;
        //    private bool repeating;

        //    public VariablesForProfiling(bool optional, bool repeating)
        //    {
        //        this.optional = optional;
        //        this.repeating = repeating;
        //    }

        //    public void add(VariableMode mode, string name, string path, Property property, TypeDetails types)
        //    {
        //        add(mode, name, new PropertyWithType(path, property, null, types));
        //    }

        //    public void add(VariableMode mode, string name, string path, Property baseProperty, Property profileProperty, TypeDetails types)
        //    {
        //        add(mode, name, new PropertyWithType(path, baseProperty, profileProperty, types));
        //    }

        //    public void add(VariableMode mode, string name, PropertyWithType property)
        //    {
        //        VariableForProfiling vv = null;
        //        foreach (VariableForProfiling v in list)
        //            if ((v.mode == mode) && v.Name.Equals(name))
        //                vv = v;
        //        if (vv != null)
        //            list.Remove(vv);
        //        list.Add(new VariableForProfiling(mode, name, property));
        //    }

        //    public VariablesForProfiling copy(bool optional, bool repeating)
        //    {
        //        VariablesForProfiling result = new VariablesForProfiling(optional, repeating);
        //        result.list.AddRange(list);
        //        return result;
        //    }

        //    public VariablesForProfiling copy()
        //    {
        //        VariablesForProfiling result = new VariablesForProfiling(optional, repeating);
        //        result.list.AddRange(list);
        //        return result;
        //    }

        //    public VariableForProfiling get(VariableMode? mode, string name)
        //    {
        //        if (mode == null)
        //        {
        //            foreach (VariableForProfiling v in list)
        //                if ((v.mode == VariableMode.OUTPUT) && v.Name.Equals(name))
        //                    return v;
        //            foreach (VariableForProfiling v in list)
        //                if ((v.mode == VariableMode.INPUT) && v.Name.Equals(name))
        //                    return v;
        //        }
        //        foreach (VariableForProfiling v in list)
        //            if ((v.mode == mode) && v.Name.Equals(name))
        //                return v;
        //        return null;
        //    }

        //    public string summary()
        //    {
        //        CommaSeparatedStringBuilder s = new CommaSeparatedStringBuilder();
        //        CommaSeparatedStringBuilder t = new CommaSeparatedStringBuilder();
        //        foreach (VariableForProfiling v in list)
        //            if (v.getMode() == VariableMode.INPUT)
        //                s.append(v.summary());
        //            else
        //                t.append(v.summary());
        //        return "source variables [" + s.ToString() + "], target variables [" + t.ToString() + "]";
        //    }
        //}

        public TerminologyServiceOptions getTerminologyServiceOptions()
        {
            return terminologyServiceOptions;
        }

        public void setTerminologyServiceOptions(TerminologyServiceOptions terminologyServiceOptions)
        {
            this.terminologyServiceOptions = terminologyServiceOptions;
        }

    }
}