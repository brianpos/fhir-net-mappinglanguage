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

using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Hl7.Fhir.MappingLanguage.FHIRPathEngineOriginal;

namespace Hl7.Fhir.MappingLanguage
{
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

        public List<Base> resolveConstant(object appContext, string name, bool beforeContext)
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

    internal static class StructureMapExtensions
    {
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
    }
}
