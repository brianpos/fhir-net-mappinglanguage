using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;

namespace demo_map_server
{
	public class InMemoryResolver : IResourceResolver
	{
		private readonly Dictionary<string, Resource> _resources = new();

		public void Add(IConformanceResource resource)
		{
			if (resource is not Resource fhirResource || string.IsNullOrEmpty(resource.Url))
				return;

			_resources[resource.Url] = fhirResource;
			if (resource is IVersionableConformanceResource versionable && !string.IsNullOrEmpty(versionable.Version))
				_resources[$"{resource.Url}|{versionable.Version}"] = fhirResource;
		}

		public Resource ResolveByCanonicalUri(string uri)
		{
			return _resources.TryGetValue(uri, out var resource) ? resource : null;
		}

		public Resource ResolveByUri(string uri)
		{
			return ResolveByCanonicalUri(uri);
		}
	}
}
