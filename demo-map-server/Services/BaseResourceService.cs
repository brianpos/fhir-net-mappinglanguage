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
            switch (operation.ToLower())
            {
                case "transform":
                    // The provided resource is the input to the transformation process (will be in `resource` here)

                    OperationOutcome outcome = new OperationOutcome();
                    var resource = await Get(id, null, SummaryType.False);
                    // outcome.Contained.Add(resource);

                    StructureMap sm = null;
                    StructureMapService sms = new StructureMapService(RequestDetails, "StructureMap", this.ResourceDirectory, this.Source, this.AsyncSource)
                    {
                        Indexer = this.Indexer
                    };
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

                        var content = await sms.Search(kvps, null, SummaryType.False, null);
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

                    //if (sm != null)
                    //{
                    //    outcome.Contained.Add(sm);
                    //    outcome.Text = new Narrative() { Status = Narrative.NarrativeStatus.Additional };
                    //    outcome.Text.Div = $"<div>\r\n<pre>\r\n{StructureMapUtilitiesParse.render(sm)}\r\n</pre>\r\n</div>";
                    //}

                    if (!outcome.Success)
                    {
                        outcome.SetAnnotation(HttpStatusCode.BadRequest);
                        return outcome;
                    }

                    try
                    {
                        var worker = new MappingWorker(sms, Source);

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
                        var mapServices = new InlineServices(outcome, provider);
                        mapServices.DebugMode = operationParameters["debug"]?.Value != null;
                        var engine = new StructureMapUtilitiesExecute(worker, mapServices, provider);

                        var tmi = provider.Provide("http://hl7.org/fhir/StructureDefinition/Bundle");

                        GroupComponent g = sm.Group.First();
                        var gt = g.Input.FirstOrDefault(i => i.Mode == StructureMapInputMode.Target);
                        var targetType = sm.Structure.FirstOrDefault(s => s.Mode == StructureMapModelMode.Target && s.Alias == gt.Type);
                        if (targetType != null)
                        {
                            // narrow this list down to the type
                            tmi = provider.Provide(targetType.Url);
                        }

                        // Check that the source parameter is of the correct type too
                        var gs = g.Input.FirstOrDefault(i => i.Mode == StructureMapInputMode.Source);
                        var sourceType = sm.Structure.FirstOrDefault(s => s.Mode == StructureMapModelMode.Source && s.Alias == gs.Type);
                        if (sourceType != null)
                        {
                            // narrow this list down to the type
                            var source = Source.ResolveByCanonicalUri(sourceType.Url) as StructureDefinition;
                            // var source = provider.Provide(sourceType.Url);
                            if (source?.Type != this.ResourceName)
                            {
                                string canonicalUrl = sm.Url;
                                if (!string.IsNullOrEmpty(sm.Version)) canonicalUrl += $"|{sm.Version}";
                                outcome.Issue.Add(new OperationOutcome.IssueComponent()
                                {
                                    Code = OperationOutcome.IssueType.Exception,
                                    Severity = OperationOutcome.IssueSeverity.Error,
                                    Details = new CodeableConcept(null, null, $"Transform [{sm.Title ?? sm.Name ?? sm.Id}] on incompatible type {ResourceName} - map is designed for {source?.Type ?? "(not found)"}"),
                                    Diagnostics = canonicalUrl
                                });

                                outcome.SetAnnotation(HttpStatusCode.BadRequest);
                                return outcome;
                            }
                        }

                        var target = ElementNode.Root(provider, tmi.TypeName);
                        engine.transform(null, resource.ToTypedElement(), sm, target);
                        outcome.SetAnnotation(new StructureMapTransformOutput() { OutputContent = target, LogMessages = mapServices.FormatOutput() });
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
            return await base.PerformOperation(id, operation, operationParameters, summary);
        }
    }
}
