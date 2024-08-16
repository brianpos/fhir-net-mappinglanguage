using Hl7.Fhir.Model;
using Hl7.Fhir.Specification.Source;
using System.Resources;

namespace demo_map_server
{
	public class InMemoryResolver : IResourceResolver
	{
		Dictionary<string, StructureDefinition> _structureDefinitions = new();

		public void Add(StructureDefinition definition)
		{
			string canonicalUrl = definition.Url;
			if (!string.IsNullOrEmpty(canonicalUrl))
			{
				if (!_structureDefinitions.ContainsKey(canonicalUrl))
					_structureDefinitions.Add(canonicalUrl, definition);
				else
					_structureDefinitions[canonicalUrl] = definition;
			}
		}
		public Resource ResolveByCanonicalUri(string uri)
		{
            if (_structureDefinitions.ContainsKey(uri))
				return _structureDefinitions[uri];
            return null;
		}

		public Resource ResolveByUri(string uri)
		{
			return null;
		}
	}
}
