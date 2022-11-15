using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Specification.Source;
using Hl7.Fhir.Specification;
using Hl7.Fhir.Validation;
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
            Console.WriteLine("Hello, World!");

            var program = new Program();
            await program.PrepareCrossVersionStructureDefinitionCache();
            await program.ConvertAllStu3ExamplesToR4FromZip();
        }

        // From github https://github.com/FHIR/interversion.git
        // const string mappinginterversion_folder = @"c:\git\HL7\interversion";
        const string mappinginterversion_folder = @"e:\git\HL7\interversion";

        public Program()
        {
            var cvr = new CrossVersionResolver();
            CachedResolver source = new CachedResolver(cvr);
            source.Load += Source_Load;
            _source = source;
            _sourceR3 = new CachedResolver(cvr.OnlyStu3);
            (_sourceR3 as CachedResolver).Load += Source_Load;
            _sourceR4 = new CachedResolver(cvr.OnlyR4);
            (_sourceR4 as CachedResolver).Load += Source_Load;
        }

        private void Source_Load(object sender, CachedResolver.LoadResourceEventArgs e)
        {
            if (e.Resource is IConformanceResource cr)
            {
                // Console.WriteLine($"{e.Url} {cr.Name}");
            }
        }

        IResourceResolver _source;
        IResourceResolver _sourceR3;
        IResourceResolver _sourceR4;
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

        public async System.Threading.Tasks.Task ConvertAllStu3ExamplesToR4FromZip()
        {
            // This test will end up producing an error report like the one at https://www.hl7.org/fhir/r3maps.html
            Dictionary<string, ResourceTestResult> results = new Dictionary<string, ResourceTestResult>();

            // Download the examples zip file
            // https://www.hl7.org/fhir/STU3/examples-json.zip
            string examplesFile = @"c:\temp\examples-json.zip";
            if (!File.Exists(examplesFile))
            {
                HttpClient server = new HttpClient();
                var stream = await server.GetStreamAsync("https://www.hl7.org/fhir/STU3/examples-json.zip");
                var outStream = File.OpenWrite(examplesFile);
                await stream.CopyToAsync(outStream);
                await outStream.FlushAsync();
            }

            // mapper engine parts
            var workerR3toR4 = new TestWorker(_source, @$"{mappinginterversion_folder}\r4\R3toR4");
            var workerR4toR3 = new TestWorker(_source, @$"{mappinginterversion_folder}\r4\R4toR3");
            var parser = new StructureMapUtilitiesParse();
            IStructureDefinitionSummaryProvider providerR4 = new StructureDefinitionSummaryProvider(_sourceR4);
            IStructureDefinitionSummaryProvider providerR3 = new StructureDefinitionSummaryProvider(_sourceR3);
            var debuggerConsole3to4 = new CommandLineServices(providerR4);
            var debuggerConsole4to3 = new CommandLineServices(providerR3);
            var engine3to4 = new StructureMapUtilitiesExecute(workerR3toR4, debuggerConsole3to4, providerR4);
            var engine4to3 = new StructureMapUtilitiesExecute(workerR4toR3, debuggerConsole4to3, providerR3);

            var validator = new Validator(new ValidationSettings() { ResourceResolver = _source });

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
                Console.WriteLine($"{DateTime.Now}: {file.Name}");
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
                            if (!File.Exists($@"{mappinginterversion_folder}\r4\R3toR4\{sourceNode.Name}.map"))
                            {
                                Console.WriteLine($"Skipping {file.Name} type ({sourceNode.Name}) that has no map");
                                continue;
                            }

                            var mapText = File.ReadAllText($@"{mappinginterversion_folder}\r4\R3toR4\{sourceNode.Name}.map");
                            var sm = parser.parse(mapText, null);
                            var source = engine3to4.GetSourceInput(sm, sourceNode, providerR3);
                            var target = engine3to4.GenerateEmptyTargetOutputStructure(sm);
                            engine3to4.transform(null, source, sm, target);

                            var xmlR4 = target.ToXml(_xmlSettings);
                            var jsonR4 = target.ToJson(_jsonSettings);

                            // Validate this content as R4
                            // var poco = target.ToPoco<Resource>(new PocoBuilderSettings() { AllowUnrecognizedEnums = true });
                            var output = validator.Validate(target);
                            if (!output.Success)
                            {
                                itemResult.validationErrors++;
                                Console.WriteLine(output.ToXml(_xmlSettings));
                            }
                            itemResult.resourceConverted++;

                            // Convert back down to STU3
                            if (!File.Exists($@"{mappinginterversion_folder}\r4\R4toR3\{target.Name}.map"))
                            {
                                Console.WriteLine($"Skipping {file.Name} type ({target.Name}) that has no backward map");
                                continue;
                            }
                            mapText = File.ReadAllText($@"{mappinginterversion_folder}\r4\R4toR3\{sourceNode.Name}.map");
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
    }
}