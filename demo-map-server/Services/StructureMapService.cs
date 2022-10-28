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
    public class StructureMapService : Hl7.Fhir.DemoFileSystemFhirServer.DirectoryResourceService<IServiceProvider>, IFhirResourceServiceR4<IServiceProvider>
    {
        public StructureMapService(ModelBaseInputs<IServiceProvider> requestDetails, string resourceName, string directory, IResourceResolver Source, IAsyncResourceResolver AsyncSource)
            : base(requestDetails, resourceName, directory, Source, AsyncSource)
        {
        }

        new public async Task<Resource> Create(Resource resource, string ifMatch, string ifNoneExist, DateTimeOffset? ifModifiedSince)
        {
            var sm = resource as StructureMap;
            if (string.IsNullOrEmpty(sm?.Id))
            {
                // Check to see if this is a draft of the same canonical/version existing that we can update
                // otherwise this is a new resource create...
                var kvps = new List<KeyValuePair<string, string>>();
                kvps.Add(new KeyValuePair<string, string>("url", sm.Url));
                if (!string.IsNullOrEmpty(sm.Version))
                    kvps.Add(new KeyValuePair<string, string>("url", sm.Url));
                var content = await Search(kvps, null, SummaryType.False, null);
                var current = CurrentCanonical.Current(content.Entry.Where(e => e.Resource is StructureMap).Select(e => e.Resource as StructureMap)) as StructureMap;
                if (current != null)
                {
                    // copy some specific the values from the last version
                    sm.Version = current.Version;
                    sm.Id = current.Id;
                    if (current.Status.HasValue)
                        sm.Status = current.Status.Value;
                }
            }

            var result = await base.Create(resource, ifMatch, ifNoneExist, ifModifiedSince);
            return result;
        }

        new public async Task<Resource> PerformOperation(string operation, Parameters operationParameters, SummaryType summary)
        {
            switch (operation.ToLower())
            {
                case "transform":
                    return await PerformOperation_Transform(operationParameters, summary);
            }

            return await base.PerformOperation(operation, operationParameters, summary);
        }

        new public async Task<Resource> PerformOperation(string id, string operation, Parameters operationParameters, SummaryType summary)
        {
            switch (operation.ToLower())
            {
                case "transform":
                    // The provided resource is the input to the transformation process (will be in `resource` here)

                    // We need to inject the "map" into the parameters for the other operation
                    var resource = await Get(id, null, SummaryType.False);
                    operationParameters.Add("map", resource);
                    return await PerformOperation_Transform(operationParameters, summary);
            }
            return await base.PerformOperation(id, operation, operationParameters, summary);
        }

        private async Task<Resource> PerformOperation_Transform(Parameters operationParameters, SummaryType summary)
        {
            var outcome = new OperationOutcome();
            Resource resource = operationParameters["resource"]?.Resource ?? operationParameters["content"]?.Resource;
            StructureMap sm = operationParameters["map"]?.Resource as StructureMap;

            var resourceParams = operationParameters.Parameter.Where(p => p.Name == "resource" || p.Name == "content");
            if (resourceParams.Count() > 1)
            {
                outcome.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.Incomplete,
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Details = new CodeableConcept(null, null, "Multiple resources provided to transform")
                });
            }

            if (resource == null)
            {
                outcome.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.Incomplete,
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Details = new CodeableConcept(null, null, $"No content provided to transform with {sm?.Name}")
                });
            }

            if (sm == null)
            {
                // Check for the source parameter to resolve
                var sourceParams = operationParameters.Parameter.Where(p => p.Name == "source");
                if (!sourceParams.Any())
                {
                    outcome.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Code = OperationOutcome.IssueType.Incomplete,
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Details = new CodeableConcept(null, null, $"Missing the source paremeter")
                    });
                }
                else
                {
                    // TODO: This should really be splitting out the versioned cannonical...
                    var kvps = new List<KeyValuePair<string, string>>();
                    CanonicalUrl canonicalSource = new CanonicalUrl(sourceParams.First().Value?.ToString());
                    kvps.Add(new KeyValuePair<string, string>("url", canonicalSource.Url.Value));
                    if (canonicalSource.Version != null)
                        kvps.Add(new KeyValuePair<string, string>("url", canonicalSource.Version.Value));
                    var content = await Search(kvps, null, SummaryType.False, null);
                    if (!content.Entry.Any(e => e.Resource is IVersionableConformanceResource))
                    {
                        outcome.Issue.Add(new OperationOutcome.IssueComponent()
                        {
                            Code = OperationOutcome.IssueType.Incomplete,
                            Severity = OperationOutcome.IssueSeverity.Error,
                            Details = new CodeableConcept(null, null, $"Missing the source paremeter")
                        });
                    }
                    else
                    {
                        // Use the current version functionality to select the latest of them
                        var current = CurrentCanonical.Current(content.Entry.Where(e => e.Resource is IVersionableConformanceResource).Select(e => e.Resource as IVersionableConformanceResource));
                        if (current is StructureMap smt)
                            sm = smt;
                    }
                }

            }

            if (!outcome.Success)
            {
                outcome.SetAnnotation(HttpStatusCode.BadRequest);
                return outcome;
            }

            var worker = new MappingWorker(this, Source);

            // Scan the map for required structuredefinitions for target types
            var mapCanonicals = StructureMapUtilitiesExecute.getCanonicalTypeMapping(worker, sm);

            IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(
                Source,
                (string name, out string canonical) =>
                {
                    // first assume it's a FHIR resource type and use that core content
                    if (ModelInfo.FhirTypeNameToFhirType(name).HasValue)
                    {
                        canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
                        return true;
                    }

                    // non FHIR types
                    if (mapCanonicals.ContainsKey(name))
                    {
                        canonical = mapCanonicals[name];
                        return true;
                    }

                    canonical = null;
                    return false;
                });
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);

            var tmi = provider.Provide("http://hl7.org/fhir/StructureDefinition/Bundle");

            GroupComponent g = sm.Group.First();
            var gt = g.Input.FirstOrDefault(i => i.Mode == StructureMapInputMode.Target);
            var s = sm.Structure.FirstOrDefault(s => s.Mode == StructureMapModelMode.Target && s.Alias == gt.Type);
            if (s != null)
            {
                // narrow this list down to the type
                tmi = provider.Provide(s.Url);
            }

            var target = ElementNode.Root(provider, tmi.TypeName);
            try
            {
                engine.transform(null, resource.ToTypedElement(), sm, target);
                outcome.SetAnnotation(new StructureMapTransformOutput() { OutputContent = target });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
                outcome.Issue.Add(new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.Exception,
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Details = new CodeableConcept(null, null, $"Transform error: {ex.Message}")
                });
            }

            if (outcome.Success)
            {
                // resource validated fine, add an information message to report it
                string summaryMessage = $"Transformation of '{resource.TypeName}/{resource.Id}' was successful";
                if (outcome.Warnings > 0)
                    summaryMessage += $" (with {outcome.Warnings} warnings)";
                outcome.Issue.Insert(0, new OperationOutcome.IssueComponent
                {
                    Code = OperationOutcome.IssueType.Informational,
                    Severity = OperationOutcome.IssueSeverity.Information,
                    Details = new CodeableConcept(null, null, summaryMessage)
                });
            }
            return outcome;
        }
    }
}
