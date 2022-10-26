using Hl7.Fhir.DemoFileSystemFhirServer;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.WebApi;

namespace demo_map_server
{
    public class MapServerSystemService : Hl7.Fhir.DemoFileSystemFhirServer.DirectorySystemService<IServiceProvider>, Hl7.Fhir.WebApi.IFhirSystemServiceR4<IServiceProvider>
    {
        public Task<Resource> PerformOperation(ModelBaseInputs<IServiceProvider> request, string operation, Parameters operationParameters, SummaryType summary)
        {
            if (operation == "convert")
            {
                Resource resource = operationParameters.GetResource("input");
                if (resource != null)
                    return Task<Resource>.FromResult(resource);
                OperationOutcome outcome = new OperationOutcome();
                return Task<Resource>.FromResult(outcome as Resource);
            }

            // otherwise fallback to the ones in the demo app
            return base.PerformOperation(request, operation, operationParameters, summary);
        }
        public IFhirResourceServiceR4<IServiceProvider> GetResourceService(ModelBaseInputs<IServiceProvider> request, string resourceName)
        {
            if (!Hl7.Fhir.Model.ModelInfo.IsCoreModelType(resourceName))
                throw new NotImplementedException();

            var service = base.GetResourceService(request, resourceName) as DirectoryResourceService<IServiceProvider>;

            if (service != null && resourceName == "StructureMap")
                return new StructureMapService(request, service.ResourceName, service.ResourceDirectory, service.Source, service.AsyncSource)
                {
                    Indexer = service.Indexer
                };

            return service;
        }
    }
}
