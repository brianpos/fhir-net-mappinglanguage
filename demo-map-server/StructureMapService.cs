using demo_map_server.StructureMapTransform;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Utility;
using Hl7.Fhir.WebApi;
using System.Net;
using static Hl7.Fhir.Model.StructureMap;

namespace demo_map_server
{
    public class StructureMapService : Hl7.Fhir.DemoFileSystemFhirServer.DirectoryResourceService<IServiceProvider>, Hl7.Fhir.WebApi.IFhirResourceServiceR4<IServiceProvider>
    {
        public StructureMapService(ModelBaseInputs<IServiceProvider> requestDetails, string resourceName, string directory, IResourceResolver Source, IAsyncResourceResolver AsyncSource)
            : base(requestDetails, resourceName, directory, Source, AsyncSource)
        {
        }

        public async Task<Resource> PerformOperation(string operation, Parameters operationParameters, SummaryType summary)
        {
            switch (operation.ToLower())
            {
                case "transform":
                    return await PerformOperation_Transform(operationParameters, summary);
            }

            return await base.PerformOperation(operation, operationParameters, summary);
        }

        public async Task<Resource> PerformOperation(string id, string operation, Parameters operationParameters, SummaryType summary)
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
                    var kvp = new KeyValuePair<string, string>("url", sourceParams.First().Value?.ToString());
                    var content = await this.Search(new[] { kvp }, 2, SummaryType.False, null);
                    if (!content.Entry.Any())
                    {
                        outcome.Issue.Add(new OperationOutcome.IssueComponent()
                        {
                            Code = OperationOutcome.IssueType.Incomplete,
                            Severity = OperationOutcome.IssueSeverity.Error,
                            Details = new CodeableConcept(null, null, $"Missing the source paremeter")
                        });
                    }
                    else if (content.Entry.Count() > 1)
                    {
                        // Use the current version functionality to select the latest of them
                        var current = CurrentCanonical.Current(content.Entry.Select(e => e.Resource as IVersionableConformanceResource));
                        if (current is StructureMap smt)
                            sm = smt;
                    }
                }

            }

            if (!outcome.Success)
            {
                outcome.SetAnnotation<HttpStatusCode>(HttpStatusCode.BadRequest);
                return outcome;
            }

            var worker = new MappingWorker(this, Source);

            IStructureDefinitionSummaryProvider provider = new StructureDefinitionSummaryProvider(
                Source,
                (string name, out string canonical) =>
                {
                    // first assume it's a FHIR resource type and use that core content
                    if (ModelInfo.IsKnownResource(name))
                    {
                        canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
                        return true;
                    }

                    switch (name)
                    {
                        case "TLeft":
                            canonical = "http://hl7.org/fhir/StructureDefinition/tutorial-left-5";
                            return true;
                        case "TRight":
                            canonical = "http://hl7.org/fhir/StructureDefinition/tutorial-right-5";
                            return true;
                    }
                    canonical = null;
                    return false;
                });
            var engine = new StructureMapUtilitiesExecute(worker, null, provider);

            var tmi = provider.Provide("http://hl7.org/fhir/StructureDefinition/Bundle");

            StructureMap.GroupComponent g = sm.Group.First();
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
            catch (System.Exception ex)
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
