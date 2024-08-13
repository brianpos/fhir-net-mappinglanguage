extern alias R5;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification;
using R5::Hl7.Fhir.Validation;
using System.Formats.Tar;
using System.IO.Compression;
using Task = System.Threading.Tasks.Task;
using Hl7.Fhir.MappingLanguage;
using Test.FhirMappingLanguage;
using Test.Hl7.Fhir.MappingLanguage;

namespace VersionConversionTester
{
	internal class Program
	{
		public static async Task Main(string[] args)
		{
			Console.WriteLine("Hello, FHIR Mappers!");
			Console.WriteLine(string.Join(" ", args));


			string sourceVersion = "R4B";
			string targetVersion = "R5";

			// From github https://github.com/FHIR/interversion.git
			string mappinginterversion_folder = @"c:\git\HL7\fhir-cross-version";
			string? outputFolder = null;

			// https://www.hl7.org/fhir/STU3/examples-json.zip
			string examplesFile = @"/mnt/c/temp/examples-json-r4b.zip";

			if (args.Length > 1)
			{
				mappinginterversion_folder = args[0];
				examplesFile = args[1];
				if (args.Length > 2)
					outputFolder = args[2];
				if (args.Length > 3)
					sourceVersion = args[3];
				if (args.Length > 4)
					targetVersion = args[4];
			}
			else
			{
				// check if there environment variables to fall back to
				string? val = Environment.GetEnvironmentVariable("FHIR_MAPPER_MAPFOLDER");
				if (val != null)
					mappinginterversion_folder = val;

				val = Environment.GetEnvironmentVariable("FHIR_MAPPER_INPUT");
				if (val != null)
					examplesFile = val;

				val = Environment.GetEnvironmentVariable("FHIR_MAPPER_OUTPUTFOLDER");
				if (val != null)
					outputFolder = val;
			}

			var program = new Program(sourceVersion, targetVersion);
			await program.ConvertExamples(sourceVersion, targetVersion, examplesFile, mappinginterversion_folder, outputFolder);
		}


		public Program(string sourceVersion, string targetVersion)
		{
			cvr = new CrossVersionResolver();
			CachedResolver source = new CachedResolver(cvr);
			source.Load += Source_Load;
			_source = source;

			switch (sourceVersion)
			{
				case "R4":
					_sourceResolver = new CachedResolver(cvr.OnlyR4);
					break;
				case "R4B":
					_sourceResolver = new CachedResolver(cvr.OnlyR4B);
					break;
				case "R5":
					_sourceResolver = new CachedResolver(cvr.OnlyR5);
					break;
			}
			// (_sourceResolver as CachedResolver).Load += Source_Load;

			switch (targetVersion)
			{
				case "R4":
					_targetResolver = new CachedResolver(cvr.OnlyR4);
					break;
				case "R4B":
					_targetResolver = new CachedResolver(cvr.OnlyR4B);
					break;
				case "R5":
					_targetResolver = new CachedResolver(cvr.OnlyR5);
					break;
			}
			// (_targetResolver as CachedResolver).Load += Source_Load;
		}

		private void Source_Load(object sender, CachedResolver.LoadResourceEventArgs e)
		{
			if (e.Resource is IConformanceResource cr)
			{
				// Console.WriteLine($"{e.Url} {cr.Name}");
			}
		}

		CrossVersionResolver cvr;
		IResourceResolver _source;
		IResourceResolver _sourceResolver;
		IResourceResolver _targetResolver;
		FhirXmlSerializationSettings _xmlSettings = new FhirXmlSerializationSettings() { Pretty = true };
		FhirJsonSerializationSettings _jsonSettings = new FhirJsonSerializationSettings() { Pretty = true };


		class ResourceTestResult
		{
			public int resourceErrors = 0;
			public int validationErrors = 0;
			public int resourceConverted = 0;
			public int resourceConvertedBack = 0;
			public int identicalXml = 0;
			public int identicalJson = 0;
		}

		public async System.Threading.Tasks.Task ConvertExamples(string sourceVersion, string targetVersion, string examplesFile, string mappinginterversion_folder, string outputFolder)
		{
			// This test will end up producing an error report like the one at https://www.hl7.org/fhir/r3maps.html
			Dictionary<string, ResourceTestResult> results = new Dictionary<string, ResourceTestResult>();

			// Download the examples zip file
			if (!File.Exists(examplesFile))
			{
				HttpClient server = new HttpClient();
				using (var stream = await server.GetStreamAsync("https://www.hl7.org/fhir/R4/examples-json-r4b.zip"))
				using (var outStream = File.OpenWrite(examplesFile))
				{
					await stream.CopyToAsync(outStream);
					await outStream.FlushAsync();
				}
			}

			// mapper engine parts
			var settingsJson = new FhirJsonParsingSettings() { PermissiveParsing = true };
			var settingsXml = new FhirXmlParsingSettings() { PermissiveParsing = true };
			var settingsDir = new DirectorySourceSettings() { JsonParserSettings = settingsJson, XmlParserSettings = settingsXml, ParserSettings = { AcceptUnknownMembers = true, PermissiveParsing = true, AllowUnrecognizedEnums = true } };
			cvr.fallbackResolver = new CachedResolver(new R5::Hl7.Fhir.Specification.Source.DirectorySource(Path.Combine(mappinginterversion_folder, "codes"), settingsDir));

			var workerUp = new TestWorker(_source, Path.Combine(mappinginterversion_folder, $"{sourceVersion}to{targetVersion}"));
			var workerDown = new TestWorker(_source, Path.Combine(mappinginterversion_folder, $"{targetVersion}to{sourceVersion}"));

			var parser = new StructureMapUtilitiesParse();
			IStructureDefinitionSummaryProvider providerSource = new StructureDefinitionSummaryProvider(_sourceResolver);
			IStructureDefinitionSummaryProvider providerTarget = new StructureDefinitionSummaryProvider(_targetResolver);
			var transformServicesUp = new CommandLineServices(providerSource);
			var transformServicesDown = new CommandLineServices(providerTarget);
			var engineUp = new StructureMapUtilitiesExecute(workerUp, transformServicesUp, providerTarget);
			var engineDown = new StructureMapUtilitiesExecute(workerDown, transformServicesDown, providerSource);

			var validator = new Validator(new ValidationSettings() { ResourceResolver = _targetResolver });

			bool createOutput = false;
			if (!string.IsNullOrEmpty(outputFolder))
			{
				createOutput = true;
				if (!Directory.Exists(outputFolder))
					Directory.CreateDirectory(outputFolder);
			}

			// scan all the files in the zip
			var inputPath = ZipFile.OpenRead(examplesFile);
			Dictionary<string, StructureMap> mapCache = new Dictionary<string, StructureMap>();
			var files = inputPath.Entries;

			foreach (var file in files)
			{
				// Known bad R4b example(s)
				if (file.Name == "evidence-example.json")
					continue;

				// Skip the spec defined/generated extras (that aren't actually examples)
				if (file.Name.EndsWith("-questionnaire.json"))
					continue;
				if (file.Name.EndsWith(".profile.json"))
					continue;
				if (file.Name.StartsWith("extension-"))
					continue;
				if (file.Name == "package-min-ver.json")
					continue;
				if (file.Name == "profiles-others.json")
					continue;
				if (file.Name == "profiles-resources.json")
					continue;
				if (file.Name == "profiles-types.json")
					continue;
				if (file.Name == "dataelements.json")
					continue;

				// skip to the test file we want to check
				// if (file.Name != "account-questionnaire.json"
				//	&& file.Name != "codesystem-diagnosis-role.json"
				//                && file.Name != "sc-valueset-supplydelivery-status.json"
				//                && file.Name != "supplydelivery-example.json"
				//    && file.Name != "healthcareservice-example.json")
				//    continue;
				Console.WriteLine($"{DateTime.Now.ToString("u")}: {file.Name}");
				using (var stream = file.Open())
				{
					using (var sr = new StreamReader(stream))
					{
						ISourceNode sourceNode;
						var sourceText = sr.ReadToEnd();
						try
						{
							if (file.Name.EndsWith(".json"))
								sourceNode = FhirJsonNode.Parse(sourceText);
							else
								sourceNode = FhirXmlNode.Parse(sourceText);
						}
						catch (Exception ex) // this is a FormatException
						{
							Console.WriteLine($"    Failed to parse {file.Name}: {ex.Message}");
							continue;
						}

						ResourceTestResult itemResult;
						if (!results.ContainsKey(sourceNode.Name))
						{
							itemResult = new ResourceTestResult();
							results.Add(sourceNode.Name, itemResult);
						}
						itemResult = results[sourceNode.Name];

						try
						{
							// Convert up
							string sourceMapFilename = Path.Combine(mappinginterversion_folder, $"{sourceVersion}to{targetVersion}", $"{sourceNode.Name}{sourceVersion.Substring(1)}to{targetVersion.Substring(1)}.fml");
							if (!File.Exists(sourceMapFilename))
							{
								Console.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
								continue;
							}

							StructureMap sm;
							if (mapCache.ContainsKey(sourceMapFilename))
							{
								sm = mapCache[sourceMapFilename];
							}
							else
							{
								var mapText = File.ReadAllText(sourceMapFilename);
								sm = parser.parse(mapText, null);
								mapCache.Add(sourceMapFilename, sm);
							}

							var source = engineUp.GetSourceInput(sm, sourceNode, providerSource);
							if (source == null)
							{
								Console.WriteLine($"    Failed to handle {file.Name} type ({sourceNode.Name})");
								continue;
							}
							var target = engineUp.GenerateEmptyTargetOutputStructure(sm);
							engineUp.transform(null, source, sm, target);

							var xmlTarget = target.ToXml(_xmlSettings);
							var jsonTarget = target.ToJson(_jsonSettings);

							itemResult.resourceConverted++;

							if (createOutput)
							{
								if (file.Name.EndsWith(".json"))
								{
									File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".json", $".{sourceVersion}.json")}"), sourceText);
									File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".json", $".{targetVersion}.json")}"), jsonTarget);
								}
								else
								{
									File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".xml", $".{sourceVersion}.xml")}"), sourceText);
									File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".xml", $".{targetVersion}.xml")}"), xmlTarget);
								}
							}

							// Validate this content as the target version
							var output = validator.Validate(target);
							output.Issue = output.Issue.Where(i => !i.Details.Text.Contains("Unable to resolve reference to profile")).ToList();

							// strip out any errors relating to the htmlchecks() as nothing we can do if that was copied over from the old content (would have failed there too)
							output.Issue.RemoveAll(i => i.Diagnostics == "htmlChecks()");

							var fi = new FileInfo(file.Name);
							string? validationResultFile = createOutput ? Path.Combine(outputFolder, $"{fi.Name.Replace(fi.Extension,"")}.validation.xml") : null;
							if (!output.Success)
							{
								// strip out the warning/informational messages to reduce the amount of diagnostics coming out
								output.Issue.RemoveAll(i => i.Severity == OperationOutcome.IssueSeverity.Warning || i.Severity == OperationOutcome.IssueSeverity.Information);

								itemResult.validationErrors++;
								if (createOutput)
								{
									File.WriteAllText(validationResultFile!, output.ToXml(_xmlSettings));
									Console.WriteLine($"   - Validation failed, {validationResultFile}");
								}
								else
								{
									Console.WriteLine(output.ToXml(_xmlSettings));
								}
							}
							else
							{
								// delete a file from last time if it still exists
								if (createOutput && File.Exists(validationResultFile))
									File.Delete(validationResultFile);
							}

							// Convert back down to R4
							sourceMapFilename = Path.Combine(mappinginterversion_folder, $"{targetVersion}to{sourceVersion}", $"{target.Name}{targetVersion.Substring(1)}to{sourceVersion.Substring(1)}.fml");
							if (!File.Exists(sourceMapFilename))
							{
								Console.WriteLine($"Skipping {file.Name} type ({target.Name}) that has no backward map");
								continue;
							}
							if (mapCache.ContainsKey(sourceMapFilename))
							{
								sm = mapCache[sourceMapFilename];
							}
							else
							{
								var mapText = File.ReadAllText(sourceMapFilename);
								sm = parser.parse(mapText, null);
								mapCache.Add(sourceMapFilename, sm);
							}
							var targetR4 = engineDown.GenerateEmptyTargetOutputStructure(sm);
							engineDown.transform(null, target, sm, targetR4);

							itemResult.resourceConvertedBack++;

							// and compare the results!
							var sourceXml = source.ToXml(_xmlSettings);
							var sourceJson = source.ToJson(_jsonSettings);
							var targetXml = targetR4.ToXml(_xmlSettings);
							var targetJson = targetR4.ToJson(_jsonSettings);

							if (sourceXml == targetXml)
								itemResult.identicalXml++;
							if (sourceJson == targetJson)
								itemResult.identicalJson++;
						}
						catch (Hl7.Fhir.ElementModel.StructuralTypeException ex)
						{
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine("Known bad example: " + ex.Message);
							itemResult.resourceErrors++;
							Console.ResetColor();
						}
						catch (System.Exception ex)
						{
							Console.ForegroundColor = ConsoleColor.Red;
							Console.WriteLine(ex.Message);
							itemResult.resourceErrors++;
							Console.ResetColor();
						}
					}
				}
			}
			Console.WriteLine($"Processed: {files.Count}");
			Console.WriteLine($"Complete:  {results.Values.Sum(v => v.resourceConverted)}");
			Console.WriteLine($"And back:  {results.Values.Sum(v => v.resourceConvertedBack)}");
			Console.WriteLine($"Same xml:  {results.Values.Sum(v => v.identicalXml)}");
			Console.WriteLine($"Same json: {results.Values.Sum(v => v.identicalJson)}");
			Console.WriteLine($"Validation:{results.Values.Sum(v => v.validationErrors)}");
			Console.WriteLine($"Exceptions:{results.Values.Sum(v => v.resourceErrors)}");

			// Now output the table!
			foreach (var r in results)
			{
				if (r.Value.resourceConverted > 0)
					Console.WriteLine($"{r.Key}\t{r.Value.resourceConverted}\t{r.Value.identicalXml},{r.Value.identicalJson}\t{(r.Value.resourceConverted - r.Value.validationErrors) / r.Value.resourceConverted * 100}%\t{r.Value.validationErrors}");
				else
					Console.WriteLine($"{r.Key}\t{r.Value.resourceConverted}\t{r.Value.identicalXml},{r.Value.identicalJson}\t0\t{r.Value.validationErrors}");
			}

			// Assert.AreEqual(0, results.Values.Sum(v => v.resourceErrors));
		}
	}
}