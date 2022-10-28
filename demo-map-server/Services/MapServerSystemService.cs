using Hl7.Fhir.DemoFileSystemFhirServer;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Support;
using Hl7.Fhir.WebApi;

namespace demo_map_server.Services
{
    public class MapServerSystemService : DirectorySystemService<IServiceProvider>, IFhirSystemServiceR4<IServiceProvider>
    {
        bool injected = false;
        public Task<Resource> PerformOperation(ModelBaseInputs<IServiceProvider> request, string operation, Parameters operationParameters, SummaryType summary)
        {
            if (operation == "cache-flush")
            {
                OperationOutcome outcome = new OperationOutcome();
                outcome.AddIssue(new OperationOutcome.IssueComponent()
                {
                    Severity = OperationOutcome.IssueSeverity.Information,
                    Code = OperationOutcome.IssueType.Informational,
                    Details = new CodeableConcept() { Text = "Caches flushed" }
                });
                var service = base.GetResourceService(request, "StructureDefinition") as DirectoryResourceService<IServiceProvider>;
                if (service?.Source is CachedResolver cr)
                {
                    // Refresh the directory resolver underneath
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
                    // then invalidate anything left in the cache
                    cr.Clear();
                }
                return System.Threading.Tasks.Task.FromResult(outcome as Resource);
            }

            // otherwise fallback to the ones in the demo app
            return base.PerformOperation(request, operation, operationParameters, summary);
        }

        new public async System.Threading.Tasks.Task<Bundle> ProcessBatch(ModelBaseInputs<IServiceProvider> request, Bundle batch)
        {
            BatchOperationProcessing<IServiceProvider> batchProcessor = new BatchOperationProcessing<IServiceProvider>();
            batchProcessor.DefaultPageSize = DefaultPageSize;
            batchProcessor.GetResourceService = GetResourceService;
            return await batchProcessor.ProcessBatch(request, batch);
        }

        new public IFhirResourceServiceR4<IServiceProvider> GetResourceService(ModelBaseInputs<IServiceProvider> request, string resourceName)
        {
            if (!ModelInfo.IsCoreModelType(resourceName))
                throw new NotImplementedException();

            var service = base.GetResourceService(request, resourceName) as DirectoryResourceService<IServiceProvider>;

            if (!injected)
            {
                injected = true;
                if (service?.Source is CachedResolver cr)
                {
                    cr.Load += Cr_Load;
                }
            }

            if (service != null && resourceName == "StructureMap")
                return new StructureMapService(request, service.ResourceName, service.ResourceDirectory, service.Source, service.AsyncSource)
                {
                    Indexer = service.Indexer
                };

            return new BaseResourceService(request, service.ResourceName, service.ResourceDirectory, service.Source, service.AsyncSource)
            {
                Indexer = service.Indexer
            };
        }

        private void Cr_Load(object sender, CachedResolver.LoadResourceEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine($"Loaded {e.Url}");
        }
    }
}
