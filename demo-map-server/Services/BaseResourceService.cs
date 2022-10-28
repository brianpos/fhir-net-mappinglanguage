using demo_map_server.StructureMapTransform;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.StructuredDataCapture;
using Hl7.Fhir.Utility;
using Hl7.Fhir.WebApi;
using System.Net;
using static Hl7.Fhir.Model.StructureMap;

namespace demo_map_server.Services
{
    public class BaseResourceService : Hl7.Fhir.DemoFileSystemFhirServer.DirectoryResourceService<IServiceProvider>, IFhirResourceServiceR4<IServiceProvider>
    {
        public BaseResourceService(ModelBaseInputs<IServiceProvider> requestDetails, string resourceName, string directory, IResourceResolver Source, IAsyncResourceResolver AsyncSource)
            : base(requestDetails, resourceName, directory, Source, AsyncSource)
        {
        }

        new public async Task<Resource> Create(Resource resource, string ifMatch, string ifNoneExist, DateTimeOffset? ifModifiedSince)
        {
            var result = await base.Create(resource, ifMatch, ifNoneExist, ifModifiedSince);
            if (result is IVersionableConformanceResource sd)
            {
                // flush the cache for this canonical resource
                if (Source is CachedResolver cr && !string.IsNullOrWhiteSpace(sd.Url))
                {
                    // Rescan any directory resolvers underneath
                    if (cr.AsyncResolver is MultiResolver mr)
                    {
                        foreach (var item in mr.Sources)
                        {
                            if (item is DirectorySource dr)
                            {
                                dr.Refresh();
                            }
                        }
                    }

                    // Invalidate the specific item
                    cr.InvalidateByCanonicalUri(sd.Url);
                    cr.InvalidateByUri(sd.Url);

                    // Now check that searching for it will actually resolve it
                    var val = await cr.ResolveByCanonicalUriAsync(sd.Url);
                    val = await cr.ResolveByUriAsync(sd.Url);
                }
            }
            return result;
        }
        new public async Task<Resource> PerformOperation(string id, string operation, Parameters operationParameters, SummaryType summary)
        {
            //switch (operation.ToLower())
            //{
            //    case "transform":
            //        // The provided resource is the input to the transformation process (will be in `resource` here)

            //        // We need to inject the "map" into the parameters for the other operation
            //        var resource = await Get(id, null, SummaryType.False);
            //        operationParameters.Add("map", resource);
            //        return await PerformOperation_Transform(operationParameters, summary);
            //}
            return await base.PerformOperation(id, operation, operationParameters, summary);
        }
    }
}
