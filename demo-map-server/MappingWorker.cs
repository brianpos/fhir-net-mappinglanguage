using demo_map_server.Services;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using static Hl7.Fhir.MappingLanguage.StructureMapUtilitiesAnalyze;

namespace demo_map_server
{
    public class MappingWorker : IWorkerContext
    {
        StructureMapService _service;
        IResourceResolver _source;

        public MappingWorker(StructureMapService service, IResourceResolver source)
        {
            _service = service;
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

        public StructureMap getTransform(string value)
        {
            // TODO: BRIAN unclear if this is a canonical or the actual ID of the resource
            return _service.Get(value, null, Hl7.Fhir.Rest.SummaryType.False).Result as StructureMap;
        }

        public IEnumerable<StructureMap> listTransforms()
        {
            throw new NotImplementedException();
        }

        public string oid2Uri(string code)
        {
            throw new NotImplementedException();
        }

        public ValidationResult validateCode(TerminologyServiceOptions terminologyServiceOptions, string system, string code, string display)
        {
            // TODO: BRIAN use the terminology service to handle this properly
            return new ValidationResult() { Display = display };
        }
    }

}
