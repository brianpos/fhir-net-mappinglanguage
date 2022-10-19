using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;

namespace Test.FhirMappingLanguage
{
    public class TestWorker : IWorkerContext
    {
        IResourceResolver _source;
        public TestWorker(IResourceResolver source)
        {
            _source = source;
        }

        public ValueSet.ExpansionComponent expandVS(ValueSet vs, bool v1, bool v2)
        {
            throw new NotImplementedException();
        }

        public T fetchResource<T>(string url) where T : Resource
        {
            var result = _source.ResolveByCanonicalUri(url);
            if (result is T value)
                return value;
            var result2 = _source.ResolveByUri(url);
            if (result2 is T value2)
                return value2;
            return null;
        }

        public T fetchResourceWithException<T>(string url) where T : Resource
        {
            var result = _source.ResolveByCanonicalUri(url);
            if (result is T value)
                return value;
            var result2 = _source.ResolveByUri(url);
            if (result2 is T value2)
                return value2;
            throw new FHIRException();
        }

        public StructureDefinition fetchTypeDefinition(string code)
        {
            var uri = ModelInfo.CanonicalUriForFhirCoreType(code);
            var result = _source.ResolveByCanonicalUri(uri);
            if (result is StructureDefinition value)
                return value;
            var result2 = _source.ResolveByUri(uri);
            if (result2 is StructureDefinition value2)
                return value2;
            // return null;
            throw new NotImplementedException();
        }

        public string getOverrideVersionNs()
        {
            return null;
            // throw new NotImplementedException();
        }

        public ValidationResult validateCode(TerminologyServiceOptions terminologyServiceOptions, string system, string code, object value)
        {
            throw new NotImplementedException();
        }
    }
}