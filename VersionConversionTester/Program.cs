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

            var program = new Program();

			// From github https://github.com/FHIR/interversion.git
			string mappinginterversion_folder = @"c:\git\HL7\fhir-cross-version";
			// string mappinginterversion_folder = @"e:\git\HL7\fhir-cross-version";
			// string mappinginterversion_folder = @"/mnt/e/git/hl7/fhir-cross-version";
            string? outputFolder = null;

            // https://www.hl7.org/fhir/STU3/examples-json.zip
            string examplesFile = @"/mnt/c/temp/examples-json-r4b.zip";

            if (args.Length > 1)
            {
                mappinginterversion_folder = args[0];
                examplesFile = args[1];
                if (args.Length > 2)
                    outputFolder = args[2];
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

            // await program.PrepareCrossVersionStructureDefinitionCache();
            // await program.ConvertAllStu3ExamplesToR4FromZip(examplesFile, mappinginterversion_folder, outputFolder);
            await program.ConvertAllR4ExamplesToR5FromZip(examplesFile, mappinginterversion_folder, outputFolder);
        }


        public Program()
        {
            cvr = new CrossVersionResolver();
            CachedResolver source = new CachedResolver(cvr);
            source.Load += Source_Load;
            _source = source;

            //_sourceR3 = new CachedResolver(cvr.OnlyStu3);
            //(_sourceR3 as CachedResolver).Load += Source_Load;

            //_sourceR4 = new CachedResolver(cvr.OnlyR4);
            //(_sourceR4 as CachedResolver).Load += Source_Load;

			_sourceR4B = new CachedResolver(cvr.OnlyR4B);
			// (_sourceR4B as CachedResolver).Load += Source_Load;
			
            _sourceR5 = new CachedResolver(cvr.OnlyR5);
            // (_sourceR5 as CachedResolver).Load += Source_Load;
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
        IResourceResolver _sourceR3;
        IResourceResolver _sourceR4;
		IResourceResolver _sourceR4B;
		IResourceResolver _sourceR5;
        FhirXmlSerializationSettings _xmlSettings = new FhirXmlSerializationSettings() { Pretty = true };
        FhirJsonSerializationSettings _jsonSettings = new FhirJsonSerializationSettings() { Pretty = true };


        public async Task PrepareCrossVersionStructureDefinitionCache()
        {
            // Download the cross version packages zip file
            // http://fhir.org/packages/xver-packages.zip
            string crossVersionPackages = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FhirMapper");
            if (!Directory.Exists(crossVersionPackages))
                Directory.CreateDirectory(crossVersionPackages);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Cross version cache located in {crossVersionPackages}");
            Console.ResetColor();

            string crossVersionPackagesZipFile = Path.Combine(crossVersionPackages, "xver-packages.zip");

            if (!File.Exists(crossVersionPackagesZipFile))
            {
                Console.WriteLine($"Downloading http://fhir.org/packages/xver-packages.zip");
                HttpClient server = new HttpClient();
                var stream = await server.GetStreamAsync("http://fhir.org/packages/xver-packages.zip");
                using (var outStream = File.OpenWrite(crossVersionPackagesZipFile))
                {
                    await stream.CopyToAsync(outStream);
                    await outStream.FlushAsync();
                }
            }

            using (var zipStream = File.OpenRead(crossVersionPackagesZipFile))
            {
                ZipArchive archive = new ZipArchive(zipStream);
                foreach (var item in archive.Entries)
                {
                    if (item.Name.EndsWith(".as.r4b.tgz") && !item.Name.StartsWith("."))
                    {
                        Console.Write($"Verifying cache for {item.Name}");
                        var path = Path.Combine(
                            crossVersionPackages,
                            item.Name.Split('.').Skip(2).First());
                        if (!Directory.Exists(path))
                        {
                            Console.WriteLine($"\r\n    Extracting for {item.Name.Split('.').Skip(2).First()}");
                            Directory.CreateDirectory(path);

                            // Now extract this package into this folder
                            using (var tarStream = new GZipStream(item.Open(), CompressionMode.Decompress))
                            {
                                TarReader r = new TarReader(tarStream);
                                var a = await r.GetNextEntryAsync();
                                while (a != null)
                                {
                                    if (!a.Name.StartsWith("package/other/")
                                        && !a.Name.StartsWith("package/openapi/")
                                        && !a.Name.StartsWith("package/xml/")
                                        && a.Name != "package/.index.json")
                                    {
                                        // Console.WriteLine($"{a.Name}");
                                        await a.ExtractToFileAsync(Path.Combine(path, a.Name.Replace("package/", "")), true);
                                    }
                                    a = await r.GetNextEntryAsync();
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($" - current");
                        }
                    }
                }
            }
        }

        class ResourceTestResult
        {
            public int resourceErrors = 0;
            public int validationErrors = 0;
            public int resourceConverted = 0;
            public int resourceConvertedBack = 0;
            public int identicalXml = 0;
            public int identicalJson = 0;
        }

        public async System.Threading.Tasks.Task ConvertAllStu3ExamplesToR4FromZip(string examplesFile, string mappinginterversion_folder, string outputFolder)
        {
            // This test will end up producing an error report like the one at https://www.hl7.org/fhir/r3maps.html
            Dictionary<string, ResourceTestResult> results = new Dictionary<string, ResourceTestResult>();

            // Download the examples zip file
            if (!File.Exists(examplesFile))
            {
                HttpClient server = new HttpClient();
                using (var stream = await server.GetStreamAsync("https://www.hl7.org/fhir/STU3/examples-json.zip"))
                using (var outStream = File.OpenWrite(examplesFile))
                {
                    await stream.CopyToAsync(outStream);
                    await outStream.FlushAsync();
                }
            }

            // mapper engine parts
            var workerR3toR4 = new TestWorker(_source, Path.Combine(mappinginterversion_folder, "R3toR4"));
            var workerR4toR3 = new TestWorker(_source, Path.Combine(mappinginterversion_folder, "R4toR3"));
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider providerR4 = new StructureDefinitionSummaryProvider(_sourceR4);
            IStructureDefinitionSummaryProvider providerR3 = new StructureDefinitionSummaryProvider(_sourceR3);
            var debuggerConsole3to4 = new CommandLineServices(providerR4);
            var debuggerConsole4to3 = new CommandLineServices(providerR3);
            var engine3to4 = new StructureMapUtilitiesExecute(workerR3toR4, debuggerConsole3to4, providerR4);
            var engine4to3 = new StructureMapUtilitiesExecute(workerR4toR3, debuggerConsole4to3, providerR3);

            var validator = new Validator(new ValidationSettings() { ResourceResolver = new CachedResolver(cvr.r5) });

            bool createOutput = false;
            if (!string.IsNullOrEmpty(outputFolder))
            {
                createOutput = true;
                if (!Directory.Exists(outputFolder))
                    Directory.CreateDirectory(outputFolder);
            }

            // scan all the files in the zip
            var inputPath = ZipFile.OpenRead(examplesFile);
            var files = inputPath.Entries;
            //int resourceErrors = 0;
            //int validationErrors = 0;
            //int resourceConverted = 0;
            //int resourceConvertedBack = 0;
            //int identicalXml = 0;
            //int identicalJson = 0;
            foreach (var file in files)
            {
                // skip to the test file we want to check
                //if (file.Name != "capabilitystatement-capabilitystatement-base(base).json")
                //    continue;
                Console.WriteLine($"{DateTime.Now.ToString("u")}: {file.Name}");
                using (var stream = file.Open())
                {
                    using (var sr = new StreamReader(stream))
                    {
                        ISourceNode sourceNode;
                        var sourceText = sr.ReadToEnd();
                        if (file.Name.EndsWith(".json"))
                            sourceNode = FhirJsonNode.Parse(sourceText);
                        else
                            sourceNode = FhirXmlNode.Parse(sourceText);

                        ResourceTestResult itemResult;
                        if (!results.ContainsKey(sourceNode.Name))
                        {
                            itemResult = new ResourceTestResult();
                            results.Add(sourceNode.Name, itemResult);
                        }
                        itemResult = results[sourceNode.Name];

                        try
                        {
                            // Convert up to R4
                            string sourceMapFilename = Path.Combine(mappinginterversion_folder, "R3toR4", $"{sourceNode.Name}.fml");
                            if (!File.Exists(sourceMapFilename))
                            {
                                Console.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }

                            var mapText = File.ReadAllText(sourceMapFilename);
                            var sm = parser.parse(mapText, null);
                            var source = engine3to4.GetSourceInput(sm, sourceNode, providerR3);
                            var target = engine3to4.GenerateEmptyTargetOutputStructure(sm);
                            engine3to4.transform(null, source, sm, target);

                            var xmlR4 = target.ToXml(_xmlSettings);
                            var jsonR4 = target.ToJson(_jsonSettings);

                            itemResult.resourceConverted++;

                            if (createOutput)
                            {
                                if (file.Name.EndsWith(".json"))
                                    File.WriteAllText(Path.Combine(outputFolder, $"{file.Name}"), jsonR4);
                                else
                                    File.WriteAllText(Path.Combine(outputFolder, $"{file.Name}"), xmlR4);
                            }

                            // Validate this content as R4
                            // var poco = target.ToPoco<Resource>(new PocoBuilderSettings() { AllowUnrecognizedEnums = true });
                            var output = validator.Validate(target);
                            string validationResultFile = Path.Combine(outputFolder, $"{new FileInfo(file.Name).Name}.validation.xml");
                            if (!output.Success)
                            {
                                // strip out the warning/informational messages to reduce the amount of diagnostics coming out
                                output.Issue.RemoveAll(i => i.Severity == OperationOutcome.IssueSeverity.Warning || i.Severity == OperationOutcome.IssueSeverity.Information);
                                itemResult.validationErrors++;
                                if (createOutput)
                                {
                                    File.WriteAllText(validationResultFile, output.ToXml(_xmlSettings));
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
                                if (File.Exists(validationResultFile))
                                    File.Delete(validationResultFile);
                            }

                            // Convert back down to STU3
                            sourceMapFilename = Path.Combine(mappinginterversion_folder, "R4toR3", $"{target.Name}.fml");
                            if (!File.Exists(sourceMapFilename))
                            {
                                Console.WriteLine($"Skipping {file.Name} type ({target.Name}) that has no backward map");
                                continue;
                            }
                            mapText = File.ReadAllText(sourceMapFilename);
                            sm = parser.parse(mapText, null);
                            var targetR3 = engine4to3.GenerateEmptyTargetOutputStructure(sm);
                            engine4to3.transform(null, target, sm, targetR3);

                            itemResult.resourceConvertedBack++;

                            // and compare the results!
                            var sourceXmlR3 = source.ToXml(_xmlSettings);
                            var sourceJsonR3 = source.ToJson(_jsonSettings);
                            var targetXmlR3 = targetR3.ToXml(_xmlSettings);
                            var targetJsonR3 = targetR3.ToJson(_jsonSettings);

                            if (sourceXmlR3 == targetXmlR3)
                                itemResult.identicalXml++;
                            if (sourceJsonR3 == targetJsonR3)
                                itemResult.identicalJson++;
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
                Console.WriteLine($"{r.Key}\t{r.Value.resourceConverted}\t{r.Value.identicalXml},{r.Value.identicalJson}\t{(r.Value.resourceConverted - r.Value.validationErrors) / r.Value.resourceConverted}\t{r.Value.validationErrors}");
            }

            // Assert.AreEqual(0, results.Values.Sum(v => v.resourceErrors));
        }

        public async System.Threading.Tasks.Task ConvertAllR4ExamplesToR5FromZip(string examplesFile, string mappinginterversion_folder, string outputFolder)
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
			var workerR4toR5 = new TestWorker(_source, Path.Combine(mappinginterversion_folder, "R4BtoR5"));
            var workerR5toR4 = new TestWorker(_source, Path.Combine(mappinginterversion_folder, "R5toR4B"));
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider providerR4 = new StructureDefinitionSummaryProvider(_sourceR4B);
            IStructureDefinitionSummaryProvider providerR5 = new StructureDefinitionSummaryProvider(_sourceR5);
            var debuggerConsole4to5 = new CommandLineServices(providerR4);
            var debuggerConsole5to4 = new CommandLineServices(providerR5);
            var engine4to5 = new StructureMapUtilitiesExecute(workerR4toR5, debuggerConsole4to5, providerR5);
            var engine5to4 = new StructureMapUtilitiesExecute(workerR5toR4, debuggerConsole5to4, providerR4);

            var validator = new Validator(new ValidationSettings() { ResourceResolver = _sourceR5 });

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
            //int resourceErrors = 0;
            //int validationErrors = 0;
            //int resourceConverted = 0;
            //int resourceConvertedBack = 0;
            //int identicalXml = 0;
            //int identicalJson = 0;
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
                        catch(Exception ex) // this is a FormatException
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
                            // Convert up to R5
                            string sourceMapFilename = Path.Combine(mappinginterversion_folder, "R4BtoR5", $"{sourceNode.Name}4Bto5.fml");
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

							var source = engine4to5.GetSourceInput(sm, sourceNode, providerR4);
							if (source == null)
							{
								Console.WriteLine($"    Failed to handle {file.Name} type ({sourceNode.Name})");
								continue;
							}
							var target = engine4to5.GenerateEmptyTargetOutputStructure(sm);
                            engine4to5.transform(null, source, sm, target);

                            var xmlR5 = target.ToXml(_xmlSettings);
                            var jsonR5 = target.ToJson(_jsonSettings);

                            itemResult.resourceConverted++;

                            if (createOutput)
                            {
                                if (file.Name.EndsWith(".json"))
                                {
                                    File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".json", ".r4.json")}"), sourceText);
									File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".json", ".r5.json")}"), jsonR5);
								}
								else
                                {
									File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".xml", ".r4.xml")}"), sourceText);
									File.WriteAllText(Path.Combine(outputFolder, $"{file.Name.Replace(".xml", ".r5.xml")}"), xmlR5);
                                }
                            }

                            // Validate this content as R5
                            // var poco = target.ToPoco<Resource>(new PocoBuilderSettings() { AllowUnrecognizedEnums = true });
                            var output = validator.Validate(target);
                            output.Issue = output.Issue.Where(i => !i.Details.Text.Contains("Unable to resolve reference to profile")).ToList();
							
                            // strip out any errors relating to the htmlchecks() as nothing we can do if that was copied over from the old content
							output.Issue.RemoveAll(i => i.Diagnostics == "htmlChecks()");

							string validationResultFile = createOutput ? Path.Combine(outputFolder, $"{new FileInfo(file.Name).Name}.validation.xml") : null;
                            if (!output.Success)
                            {
                                // strip out the warning/informational messages to reduce the amount of diagnostics coming out
                                output.Issue.RemoveAll(i => i.Severity == OperationOutcome.IssueSeverity.Warning || i.Severity == OperationOutcome.IssueSeverity.Information);

								itemResult.validationErrors++;
                                if (createOutput)
                                {
                                    File.WriteAllText(validationResultFile, output.ToXml(_xmlSettings));
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
                            sourceMapFilename = Path.Combine(mappinginterversion_folder, "R5toR4B", $"{target.Name}5to4B.fml");
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
							var targetR4 = engine5to4.GenerateEmptyTargetOutputStructure(sm);
                            engine5to4.transform(null, target, sm, targetR4);

                            itemResult.resourceConvertedBack++;

                            // and compare the results!
                            var sourceXmlR3 = source.ToXml(_xmlSettings);
                            var sourceJsonR3 = source.ToJson(_jsonSettings);
                            var targetXmlR3 = targetR4.ToXml(_xmlSettings);
                            var targetJsonR3 = targetR4.ToJson(_jsonSettings);

                            if (sourceXmlR3 == targetXmlR3)
                                itemResult.identicalXml++;
                            if (sourceJsonR3 == targetJsonR3)
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
                    Console.WriteLine($"{r.Key}\t{r.Value.resourceConverted}\t{r.Value.identicalXml},{r.Value.identicalJson}\t{(r.Value.resourceConverted - r.Value.validationErrors) / r.Value.resourceConverted * 100 }%\t{r.Value.validationErrors}");
                else
					Console.WriteLine($"{r.Key}\t{r.Value.resourceConverted}\t{r.Value.identicalXml},{r.Value.identicalJson}\t0\t{r.Value.validationErrors}");
			}

			// Assert.AreEqual(0, results.Values.Sum(v => v.resourceErrors));
		}
    }
}