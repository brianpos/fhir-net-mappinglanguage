using demo_map_server.StructureMapTransform;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.MappingLanguage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Specification.Snapshot;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.StructuredDataCapture;
using Hl7.Fhir.Support;
using Hl7.Fhir.Utility;
using Hl7.Fhir.WebApi;
using System.Data;
using System.Net;
using Test.Hl7.Fhir.MappingLanguage;
using static Hl7.Fhir.Model.StructureMap;

namespace demo_map_server.Services
{
	public class StructureMapService : Hl7.Fhir.DemoFileSystemFhirServer.DirectoryResourceService<IServiceProvider>, IFhirResourceServiceR4<IServiceProvider>
	{
		private const string OperationOutcomeFileExtension = "http://hl7.org/fhir/StructureDefinition/operationoutcome-file";

        CachedResolver _multiVersionResolver;

		public StructureMapService(ModelBaseInputs<IServiceProvider> requestDetails, string resourceName, string directory, IResourceResolver Source, IAsyncResourceResolver AsyncSource)
			: base(requestDetails, resourceName, directory, Source, AsyncSource)
		{
            _multiVersionResolver = new CachedResolver(new MultiResolver(Source,  new CrossVersionResolver()));
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

					// The instance map is primary; request maps remain available as supporting imports.
					var resource = await Get(id, null, SummaryType.False);
					operationParameters.Parameter.Insert(0, new Parameters.ParameterComponent
					{
						Name = "map",
						Resource = resource
					});
					return await PerformOperation_Transform(operationParameters, summary);
			}
			return await base.PerformOperation(id, operation, operationParameters, summary);
		}


        private void AnnotateStructureMapDebugAnnotations(StructureMap map)
        {
            SetChildLocationAnnotation(map.NamedChildren);
        }

        private void SetChildLocationAnnotation(IEnumerable<ElementValue> node, string? parentLocation = null)
        {
            if (node == null)
                return;
            string parentLocationPrefix = parentLocation != null ? parentLocation + "." : "";
            foreach (var n in node)
            {
                var targetLocation = $"{parentLocationPrefix}{n.ElementName}";
                SetLocationAnnotation(n.Value, targetLocation);
                SetChildLocationAnnotation(n.Value.NamedChildren, targetLocation);
            }
        }

        private void SetLocationAnnotation(IAnnotated a, string fhirpathLocation)
        {
            if (a?.HasAnnotation<DebugAnnotation>() == true)
            {
                a.Annotation<DebugAnnotation>().Expression = fhirpathLocation;
            }
        }

		private async Task<Resource> PerformOperation_Transform(Parameters operationParameters, SummaryType summary)
		{
			var outcome = new OperationOutcome();
			bool inputFormatIsJson = false;
			var ct = RequestDetails.Headers.FirstOrDefault(h => h.Key.Equals("content-type", StringComparison.OrdinalIgnoreCase));
			{
				if (ct.Value?.Any(v => v.Contains("json", StringComparison.OrdinalIgnoreCase)) == true)
					inputFormatIsJson = true;
			}
			var mapParameters = operationParameters.Parameter.Where(p => p.Name == "map").ToList();
			var suppliedMaps = new List<StructureMap>();
			string primaryMapFile = mapParameters.FirstOrDefault()?.GetStringExtension(OperationOutcomeFileExtension);
            // set the default issue file.
            if (!string.IsNullOrEmpty(primaryMapFile))
                outcome.SetStringExtension(OperationOutcomeFileExtension, primaryMapFile);
            StructureMap sm = null;

			foreach (var mapParameter in mapParameters)
			{
				StructureMap suppliedMap = mapParameter.Resource as StructureMap;
				string mapFile = mapParameter.GetStringExtension(OperationOutcomeFileExtension);

				if (mapParameter.Value is FhirString mapString)
				{
					// This is a workaround to support passing the raw map as a string as a resource parameter.
					try
					{
						var parser = new StructureMapUtilitiesParse();
						suppliedMap = parser.parse(mapString.Value, mapFile ?? "map");
                        AnnotateStructureMapDebugAnnotations(suppliedMap);
                        foreach (var group in suppliedMap.Group)
                        {
                            // give each group the file location
                            group.SetStringExtension(OperationOutcomeFileExtension, mapFile);
                        }
					}
					catch (Exception ex)
					{
						var issue = new OperationOutcome.IssueComponent()
						{
							Code = OperationOutcome.IssueType.Exception,
							Severity = OperationOutcome.IssueSeverity.Error,
							Details = new CodeableConcept(null, null, "Error parsing the map to transform with"),
							Diagnostics = ex.Message
						};
                        if (!string.IsNullOrEmpty(mapFile))
                            issue.SetStringExtension(OperationOutcomeFileExtension, mapFile);
						outcome.Issue.Add(issue);
					}
				}

				if (suppliedMap != null)
				{
					suppliedMaps.Add(suppliedMap);
					if (ReferenceEquals(mapParameter, mapParameters[0]))
						sm = suppliedMap;
				}
			}

			if (sm == null && mapParameters.Count > 0 && outcome.Success)
			{
				outcome.Issue.Add(new OperationOutcome.IssueComponent()
				{
					Code = OperationOutcome.IssueType.Incomplete,
					Severity = OperationOutcome.IssueSeverity.Error,
					Details = new CodeableConcept(null, null, "The first map parameter is not a valid StructureMap")
				});
			}

			if (sm == null && mapParameters.Count == 0)
			{
				// Check for the source parameter to resolve
				var sourceParams = operationParameters.Parameter.Where(p => p.Name == "source");
				if (!sourceParams.Any())
				{
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.Incomplete,
						Severity = OperationOutcome.IssueSeverity.Error,
						Details = new CodeableConcept(null, null, $"Missing the source parameter")
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

			try
			{
				var imr = new InMemoryResolver();
				foreach (var suppliedMap in suppliedMaps)
				{
					imr.Add(suppliedMap);
				}
				foreach (var sd in CustomStructureDefinitions(Source, operationParameters))
				{
					imr.Add(sd);
				}
                // scan for any other supporting resources (ConceptMaps)
                foreach (var sr in SupportingResources<ConceptMap>(operationParameters))
                {
                    imr.Add(sr);
                }

                var mr = new MultiResolver(imr, _multiVersionResolver); // use the multi-version resolver rather than the main embedded resolver
				var worker = new MappingWorker(this, mr, suppliedMaps);

				// Scan the map for required StructureDefinitions for source types
				// (source while parsing the source content, switches to target content once it's loaded)
				var mapCanonicalsSource = StructureMapUtilitiesExecute.getSourceCanonicalTypeMapping(worker, sm);

				IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(
					mr,
					(string name, out string canonical) =>
					{
						// first assume it's a FHIR resource type and use that core content
						if (ModelInfo.FhirTypeNameToFhirType(name).HasValue)
						{
							canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
							return true;
						}

						// non FHIR types
						if (mapCanonicalsSource.ContainsKey(name))
						{
							canonical = mapCanonicalsSource[name];
							return true;
						}

						canonical = null;
						return false;
					});

				// Scan the map for required structuredefinitions for target types
				var mapCanonicalsTarget = StructureMapUtilitiesExecute.getCanonicalTypeMapping(worker, sm);

				IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(
					mr,
					(string name, out string canonical) =>
					{
						// first assume it's a FHIR resource type and use that core content
						if (ModelInfo.FhirTypeNameToFhirType(name).HasValue)
						{
							canonical = ModelInfo.CanonicalUriForFhirCoreType(name)?.Value;
							return true;
						}

						// non FHIR types
						if (mapCanonicalsTarget.ContainsKey(name))
						{
							canonical = mapCanonicalsTarget[name];
							return true;
						}

						canonical = null;
						return false;
					});

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

				ITypedElement resource = GetInputResource(operationParameters, providerSource, ref inputFormatIsJson);
				if (resource == null)
				{
					outcome.Issue.Add(new OperationOutcome.IssueComponent()
					{
						Code = OperationOutcome.IssueType.Incomplete,
						Severity = OperationOutcome.IssueSeverity.Error,
						Details = new CodeableConcept(null, null, $"No content provided to transform with {sm?.Name}")
					});
				}

				var mapServices = new InlineServices(outcome, providerTarget, mr);
				mapServices.DebugMode = operationParameters["debug"]?.Value != null;
				var engine = new StructureMapUtilitiesExecute(worker, mapServices, providerTarget);
				var target = engine.GenerateEmptyTargetOutputStructure(sm);
                DebuggerTrace tracer = new DebuggerTrace();
				engine.transform(null, resource, sm, target, tracer);

				if (outcome.Success)
				{
					// resource validated fine, add an information message to report it
					string summaryMessage = $"Transformation was successful";
					if (outcome.Warnings > 0)
						summaryMessage += $" (with {outcome.Warnings} warnings)";
					outcome.Issue.Insert(0, new OperationOutcome.IssueComponent
					{
						Code = OperationOutcome.IssueType.Informational,
						Severity = OperationOutcome.IssueSeverity.Information,
						Details = new CodeableConcept(null, null, summaryMessage)
					});
				}

				if (operationParameters["debug"]?.Value != null)
				{
					// This is a debug mode request, so return in the parameters format!
					var result = new Parameters();

					// The outcome resource from the generation
					result.Parameter.Add(new Parameters.ParameterComponent()
					{
						Name = "outcome",
						Resource = outcome
					});

					// actual transformed object
					if (target != null)
					{
						result.Parameter.Add(new Parameters.ParameterComponent()
						{
							Name = "result",
							Value = new FhirString(inputFormatIsJson ? target.ToJson(new FhirJsonSerializationSettings() { Pretty = true })
																	: target.ToXml(new FhirXmlSerializationSettings() { Pretty = true }))
						});
					}

					// Any processing parameters
					// (including the map that was used to evaluate the request - in StructureMap format)
					var configParams = new Parameters.ParameterComponent() { Name = "parameters" };
					configParams.Part.Add(new Parameters.ParameterComponent() { Name = "evaluator", Value = new FhirString(".NET (brianpos) 5.13.4 beta-2") });
					configParams.Part.Add(new Parameters.ParameterComponent() { Name = "map", Resource = sm });
					result.Parameter.Add(configParams);

					// The Trace/debug processing messages
					var resultTrace = new Parameters.ParameterComponent()
					{
						Name = "trace",
					};
					result.Parameter.Add(resultTrace);

                    // new debug trace
                    if (true)
                    {
                        foreach (var log in tracer.Events)
                        {
                            var part = new Parameters.ParameterComponent()
                            {
                                Name = log.MessageType.ToString(),
                                Value = new FhirString(log.Message ?? $"{log.CallDepth} {log.GroupName} {log.Phase} {log.RuleName}")
                            };
                            part.SetStringExtension("http://fhirpath-lab.com/StructureDefinition/Cursor",
                                $"{log.CursorStartPosition} - {log.CursorEndPosition}");
                            if (log.MapFileName != primaryMapFile)
                                part.SetStringExtension(OperationOutcomeFileExtension, log.MapFileName);
                            part.SetStringExtension("http://fhirpath-lab.com/StructureDefinition/TracePhase", log.Phase.ToString());
                            part.SetIntegerExtension("http://fhirpath-lab.com/StructureDefinition/TraceDepth", log.CallDepth);
                            foreach (var variable in log.Variables)
                            {
                                LogVariable(part, variable);
                            }
                            resultTrace.Part.Add(part);
                        }
                        // Temp diagnostics
                        string json = resultTrace.ToJson(new FhirJsonSerializationSettings() { Pretty = true });
                        System.Diagnostics.Trace.WriteLine(json);
                    }
                    else
                    {
                        // Old debug trace
                        foreach (var log in mapServices.LogMessages)
                        {
                            var part = new Parameters.ParameterComponent()
                            {
                                Name = log.Key,
                                Value = new FhirString(log.Value.message)
                            };
                            if (log.Value.debugAnnotation != null)
                            {
                                var debugAnnot = log.Value.debugAnnotation;
                                //part.SetStringExtension("http://fhirpath-lab.com/StructureDefinition/Location",
                                //	$"L{debugAnnot.StartLoc?.getLine()} C{debugAnnot.StartLoc?.getColumn()} - L{debugAnnot.EndLoc.getLine()} C{debugAnnot.EndLoc.getColumn()}");
                                part.SetStringExtension("http://fhirpath-lab.com/StructureDefinition/Cursor",
                                    $"{debugAnnot.StartCursor} - {debugAnnot.EndCursor}");
                                foreach (var variable in log.Value.variables)
                                {
                                    LogVariable(part, variable);
                                }
                            }
                            resultTrace.Part.Add(part);
                        }
                    }
					return result;
				}
				outcome.SetAnnotation(new StructureMapTransformOutput() { OutputContent = target });
			}
			catch (Exception ex)
			{
				System.Diagnostics.Trace.WriteLine(ex.Message);
                var issue =
                new OperationOutcome.IssueComponent()
                {
                    Code = OperationOutcome.IssueType.Exception,
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Details = new CodeableConcept(null, null, $"Transform error: {ex.Message}")
                };
                if (ex is FHIRMapExecutionException mex)
                {
                    if (mex.Location != null)
                    {
                        SetSourceLocationExtensions(primaryMapFile, issue, mex);
                    }
                }
                if (ex.InnerException is FHIRMapExecutionException mexI)
                {
                    SetSourceLocationExtensions(primaryMapFile, issue, mexI);
                }
                outcome.Issue.Add(issue);
			}

			return outcome;
		}

        private static void SetSourceLocationExtensions(string primaryMapFile, OperationOutcome.IssueComponent issue, FHIRMapExecutionException mex)
        {
            if (mex.Location != null)
            {
                // The standard extensions for position information
                if (!string.IsNullOrEmpty(mex.Location.SourceFile) && mex.Location.SourceFile != primaryMapFile)
                    issue.SetStringExtension(OperationOutcomeFileExtension, mex.Location.SourceFile);
                issue.SetIntegerExtension("http://hl7.org/fhir/StructureDefinition/operationoutcome-issue-line", mex.Location.StartLoc.getLine());
                issue.SetIntegerExtension("http://hl7.org/fhir/StructureDefinition/operationoutcome-issue-col", mex.Location.StartLoc.getColumn());

                if (!string.IsNullOrEmpty(mex.Location.Expression))
                    issue.Expression = [mex.Location.Expression];

                // The FHIRPath Lab's custom extensions
                issue.SetStringExtension("http://fhirpath-lab.com/StructureDefinition/Cursor", $"{mex.Location.StartCursor} - {mex.Location.EndCursor}");
                issue.Details.Text += $" Line:{mex.Location.StartLoc.getLine()} Column:{mex.Location.StartLoc.getColumn()}";
            }
        }

        private static void LogVariable(Parameters.ParameterComponent part, TraceVariable variable)
		{
			var extValue = new Extension() { Url = "http://fhirpath-lab.com/StructureDefinition/Variable" };
			extValue.SetStringExtension("name-" + variable.Mode, variable.Name);
			if (!string.IsNullOrEmpty(variable.Path))
				extValue.SetStringExtension("path", variable.Path);
            if (!string.IsNullOrEmpty(variable.Type))
                extValue.SetStringExtension("type", variable.Type);
            if (variable.FhirValue != null)
                extValue.SetExtension("value", variable.FhirValue);
            else
                extValue.SetStringExtension("http://fhir.forms-lab.com/StructureDefinition/json-value", variable.JsonValue);
            part.Extension.Add(extValue);
		}

        /// <summary>
        /// Retrieve the Input Resource from the input parameters
        /// </summary>
        /// <remarks>
        /// If the resource is native to the bundle, and not parsed from a string, then the inputFormatIsJson
        /// value is not changed - that should be whatever the bundle coming in is.
        /// </remarks>
        /// <param name="operationParameters"></param>
        /// <param name="inputFormatIsJson"></param>
        /// <returns></returns>
        private static ITypedElement GetInputResource(Parameters operationParameters, IStructureDefinitionSummaryProvider provider, ref bool inputFormatIsJson)
		{
			if (operationParameters["resource"]?.Resource != null)
				return operationParameters["resource"]?.Resource.ToTypedElement();

			if (operationParameters["content"]?.Resource != null)
				return operationParameters["content"]?.Resource.ToTypedElement();

			var str = operationParameters.GetString("resource");
			try
			{

				var parserSettings = new ParserSettings() { PermissiveParsing = true, AllowUnrecognizedEnums = true };
				if (str.StartsWith('<'))
				{
					inputFormatIsJson = false;
					var xmlSettings = new FhirXmlParsingSettings() { PermissiveParsing = true };
					return FhirXmlNode.Parse(str, xmlSettings).ToTypedElement(provider);
				}
				else
				{
					inputFormatIsJson = true;
					var jsonSettings = new FhirJsonParsingSettings() { PermissiveParsing = true, AllowJsonComments = true };
					return FhirJsonNode.Parse(str, null, jsonSettings).ToTypedElement(provider);
				}
			}
			catch (FormatException ex)
			{
				// This likely isn't a FHIR resource and some form of logical model instance (random content)
				// so parse it using the raw XML/json node parsers
				if (str.StartsWith('<'))
				{
					inputFormatIsJson = false;
					return FhirXmlNode.Parse(str).ToTypedElement();
				}
				else
				{
					inputFormatIsJson = true;
					return FhirJsonNode.Parse(str).ToTypedElement();
				}
			}

			return null;
		}

		private IEnumerable<StructureDefinition> CustomStructureDefinitions(IResourceResolver resolver, Parameters operationParameters)
		{
			SnapshotGenerator sg = new SnapshotGenerator(resolver);
			var result = new List<StructureDefinition>();
			var models = operationParameters.Get("model");
			foreach (var model in models)
			{
				ScanResource(sg, result, model.Resource);

				// If there is no resource, but content in the string, assume it's the raw SD content
				if (model.Value is FhirString str)
				{
					var parserSettings = new ParserSettings() { PermissiveParsing = true, AllowUnrecognizedEnums = true };
					if (str.Value.StartsWith('<'))
					{
						var parser = new FhirXmlParser(parserSettings);
						var r = parser.Parse<Resource>(str.Value);
						ScanResource(sg, result, r);
					}
					else
					{
						var parser = new FhirJsonParser(parserSettings);
						var r = parser.Parse<Resource>(str.Value);
						ScanResource(sg, result, r);
					}
				}
			}
			return result;
		}

        private IEnumerable<T> SupportingResources<T>(Parameters operationParameters)
            where T : DomainResource, IVersionableConformanceResource
        {
            var result = new List<T>();
            var models = operationParameters.Get("model");
            foreach (var model in models)
            {
                ScanResource(result, model.Resource);

                // If there is no resource, but content in the string, assume it's the raw SD content
                if (model.Value is FhirString str)
                {
                    var parserSettings = new ParserSettings() { PermissiveParsing = true, AllowUnrecognizedEnums = true };
                    if (str.Value.StartsWith('<'))
                    {
                        var parser = new FhirXmlParser(parserSettings);
                        var r = parser.Parse<Resource>(str.Value);
                        ScanResource(result, r);
                    }
                    else
                    {
                        var parser = new FhirJsonParser(parserSettings);
                        var r = parser.Parse<Resource>(str.Value);
                        ScanResource(result, r);
                    }
                }
            }
            return result;
        }

        private static void ScanResource<T>(List<T> result, Resource? resource)
            where T : DomainResource, IVersionableConformanceResource
        {
            if (resource is T typedResource)
            {
                result.Add(typedResource);
            }
            if (resource is Bundle b)
            {
                foreach (var bsd in b.GetResources().OfType<T>())
                {
                    ScanResource(result, bsd);
                }
            }
        }

        private static void ScanResource(SnapshotGenerator sg, List<StructureDefinition> result, Resource? resource)
		{
			if (resource is StructureDefinition sd)
			{
				if (!sd.HasSnapshot)
				{
					sg.Update(sd);
				}
				if (sd.Abstract == true)
					sd.Abstract = false;
				result.Add(sd);
			}
			if (resource is Bundle b)
			{
				foreach (var bsd in b.GetResources().OfType<StructureDefinition>())
				{
					ScanResource(sg, result, bsd);
				}
			}
		}
	}
}
