using demo_map_server.Services;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.StructuredDataCapture;
using Hl7.Fhir.WebApi;
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

        /// <summary>
        /// Retrieve by the canonical URL
        /// </summary>
        /// <param name="canonicalUrl"></param>
        /// <returns></returns>
        public StructureMap getTransform(string canonicalUrl)
        {
            var kvps = new List<KeyValuePair<string, string>>();
            CanonicalUrl sm = new CanonicalUrl(canonicalUrl);
            kvps.Add(new KeyValuePair<string, string>("url", sm.Url.Value));
            if (sm.Version != null)
                kvps.Add(new KeyValuePair<string, string>("url", sm.Url.Value));
            var content = _service.Search(kvps, null, SummaryType.False, null).WaitResult();
            return CurrentCanonical.Current(content.Entry.Where(e => e.Resource is StructureMap).Select(e => e.Resource as StructureMap)) as StructureMap;
        }

        public IEnumerable<StructureMap> listTransforms(string canonicalUrlTemplate)
        {
            if (_service.Indexer.MemoryIndex.ContainsKey("StructureMap#url"))
            {
                var map = _service.Indexer.MemoryIndex["StructureMap#url"];
                List<StructureMap> maps = new List<StructureMap>();
                foreach (var kvp in map)
                { 
                    if (urlMatches(canonicalUrlTemplate, kvp.Key))
                    {
                        maps.Add(new FhirXmlParser().Parse<StructureMap>(File.ReadAllText(kvp.Value.First())));
                    }
                }
                return maps;

            }
            return null;
        }

        private bool urlMatches(string mask, string url)
        {
            return url.Length > mask.Length && url.StartsWith(mask.Substring(0, mask.IndexOf("*"))) && url.EndsWith(mask.Substring(mask.IndexOf("*") + 1));
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
